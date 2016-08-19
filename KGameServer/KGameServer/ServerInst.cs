using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using Fleck;


namespace KGameServer
{
    public class WebSocketMsg
    {
        public IWebSocketConnection socket;
        public string message;
        public int msgType;     //0表示OnOpen，1表示OnClose，2表示OnMessage

        public WebSocketMsg(IWebSocketConnection aSocket, string aMessage, int aMsgType)
        {
            this.socket = aSocket;
            this.message = aMessage;
            this.msgType = aMsgType;
        }
    }

    public class ServerInst
    {
        private PlayerConnectionMap playerConnectionMap;
        public PlayerConnectionMap PlayerConnectionMap
        {
            get { return playerConnectionMap; }
        }


        /// <summary>
        /// 没有来源的消息的列表
        /// </summary>
        private Dictionary<IWebSocketConnection, List<NoSourceMessage>> noResourceMessageDict;
        /// <summary>
        /// 控制noResourceMessageDict的访问
        /// </summary>
        private Mutex mutexForDict;

       

        private string url;
        private WebSocketServer webSocketServer;
        private MatchMaker matchMaker;
        public MatchMaker MatchMaker
        {
            get { return matchMaker; }
        }

        private SynQueue<WebSocketMsg> messageQueue;
        private Thread messageQueueThread;


        public ServerInst()
        {
            playerConnectionMap = new PlayerConnectionMap(this);
            matchMaker = new MatchMaker();
            noResourceMessageDict = new Dictionary<IWebSocketConnection, List<NoSourceMessage>>();
            mutexForDict = new Mutex();
            messageQueue = new SynQueue<WebSocketMsg>();
            messageQueueThread = new Thread(ThreadProc);
            messageQueueThread.IsBackground = true;
        }


        public void Init()
        {
            if (ReadServerInfoXML() == false)
            {
                Util.Log("读取配置文件错误");
                throw new Exception("读取配置文件错误");
            }
            //先开启消息处理的线程
            messageQueueThread.Start();
            //然后启动websocket;
            webSocketServer = new WebSocketServer(url);
            webSocketServer.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Util.Log("OnOpen");
                    messageQueue.Enqueue(new WebSocketMsg(socket, null, 0), false);
                    
                };
                socket.OnClose = () =>
                {
                    messageQueue.Enqueue(new WebSocketMsg(socket, null, 1), false);
                };
                socket.OnMessage = message =>
                {
                    Util.Log("OnMessage:message="+message);
                    messageQueue.Enqueue(new WebSocketMsg(socket, message, 2), false);
                };
            });
        }

        private void ThreadProc()
        {
            while (true)
            {
                List<WebSocketMsg> webSocketMsgList= messageQueue.DequeueAll();
                foreach (WebSocketMsg m in webSocketMsgList)
                {
                    try
                    {
                        if (m.msgType == 0)
                        {
                            //表示Open
                            SocketConnected(m.socket);
                        }
                        else if (m.msgType == 1)
                        {
                            //表示Close
                            SocketClosed(m.socket);
                        }
                        else if (m.msgType == 2)
                        {
                            //表示接收到的消息
                            SocketReceievedMsg(m.socket,m.message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Util.Log("消息处理时发生错误...");
                        Util.LogException(ex);
                    }
                }
            }
        }

        private void SocketConnected(IWebSocketConnection clientConnection)
        {
            Util.Log("Entered SocketConnected");
            PlayerConnection playerConnection = playerConnectionMap.AddConnection(clientConnection);
            Util.Log("Connected");
            //还需要判断该连接是否有预先接收到的数据 
            ProcessPreReceievedMessage(playerConnection);
        }

        private void ProcessPreReceievedMessage(PlayerConnection playerConnection)
        {
            List<NoSourceMessage> l = null;
            if (playerConnection == null) return;
            IWebSocketConnection clientConnection = playerConnection.ClientConnection;
            mutexForDict.WaitOne();
            try
            {
                Util.Log("为连接:" + playerConnection.ToString() + " 处理没有来源的消息");
                Util.Log("noResourceMessageDict中，无来源的消息共有" + noResourceMessageDict.Keys.Count + " 个来源");
                if (noResourceMessageDict.ContainsKey(clientConnection) == true)
                {
                    Util.Log("开始处理没有来源的消息，该消息的来源已经被发现了");
                    l = noResourceMessageDict[clientConnection];
                    noResourceMessageDict.Remove(clientConnection);
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForDict.ReleaseMutex();
            if(l!=null)
            {
                Util.Log("共有" + l.Count + "条没有来源的消息可以处理");
                foreach(NoSourceMessage m in l)
                {
                    Util.Log("内容为:" + m.Message);
                    playerConnection.ProcessMessage(m.Message);
                }
            }
        }

        /// <summary>
        /// 用户连接断开
        /// </summary>
        /// <param name="clientConnection"></param>
        private void SocketClosed(IWebSocketConnection clientConnection)
        {
            ClosePlayerConnection(playerConnectionMap.GetPlayerConnection(clientConnection));
        }

        /// <summary>
        /// 删除某个用户的链接
        /// </summary>
        /// <param name="playerConnection"></param>
        public void ClosePlayerConnection( PlayerConnection playerConnection)
        {
            Util.Log("ClosePlayerConnection Call");
            if (playerConnection == null) return;
            playerConnection.IsClosed = true;
            //先从对局中或者等待中删除该用户
            matchMaker.CloseConnection(playerConnection);
            //再从用户表中删除该用户
            playerConnectionMap.CloseConnection(playerConnection);
            Util.Log("Closed");
        }

        /// <summary>
        /// 处理没有来源的消息
        /// </summary>
        /// <param name="clientConnection"></param>
        /// <param name="msg"></param>
        private void ProcessNoSourceMessages(IWebSocketConnection clientConnection, string msg)
        {
            Util.Log("开始处理未知来源的消息");
            mutexForDict.WaitOne();
            try
            {
                List<NoSourceMessage> l = null;
                Util.Log("共有 " + noResourceMessageDict.Keys.Count + " 个未知的来源");
                if(noResourceMessageDict.ContainsKey(clientConnection)==true)
                {
                    l = noResourceMessageDict[clientConnection];
                }
                else
                {
                    l = new List<NoSourceMessage>();
                    noResourceMessageDict.Add(clientConnection, l);
                }
                l.Add(new NoSourceMessage(msg, clientConnection));
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForDict.ReleaseMutex();
        }

        private void SocketReceievedMsg(IWebSocketConnection clientConnection, string msg)
        {
            PlayerConnection playerConnection = playerConnectionMap.GetPlayerConnection(clientConnection);
            if (playerConnection != null)
            {
                playerConnection.ProcessMessage(msg);
            }
            else
            {
                Util.Log("未知来源的消息:" + msg);
                ProcessNoSourceMessages(clientConnection, msg);
            }
        }

        /// <summary>
        /// 记录已经成功登录的用户
        /// </summary>
        /// <param name="playerConnection"></param>
        public void AddLoginedPlayer(PlayerConnection playerConnection)
        {
            playerConnectionMap.AddLoginedPlayer(playerConnection);
        }

        /// <summary>
        /// 根据UserID获得登录的PlayerConnection
        /// </summary>
        /// <param name="userID"></param>
        public PlayerConnection GetPlayerLoginedByUserID(int userID)
        {
            return playerConnectionMap.GetPlayerLoginedByUserID(userID);
        }



        /// <summary>
        /// 读取每日预测数据服务器端口的配置文件
        /// </summary>
        private Boolean ReadServerInfoXML()
        {
            string serverInfoFile = AppDomain.CurrentDomain.BaseDirectory + "setting\\serverInfo.xml";
            if (File.Exists(serverInfoFile) == false)
            {
                Util.Log("不存在文件:" + serverInfoFile);
                return false;
            }
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(serverInfoFile);
                XmlNode node = doc.SelectSingleNode("config/server");
                if (node == null || node.Attributes["url"] == null)
                {
                    throw new Exception("配置文件内容错误，未发现URL设置");
                }
                url = node.Attributes["url"].Value;
            }
            catch (Exception ex)
            {
                Util.Log("exception:" + ex.Message);
                Util.Log(ex.StackTrace);
                return false;
            }
            return true;
        }


        public void AddBeginMatchPlayer(PlayerConnection playerConnection,string mode)
        {
            //////////TODO
            if (mode == "0")
            {
                //开始的是单人模式
                matchMaker.BeginNewMatchForSinglePlayer(playerConnection);

            }
            else if (mode == "1")
            {
                //开始的是多人模式
                matchMaker.AddWaitingPlayer(playerConnection);
            }
        }
    }
}
