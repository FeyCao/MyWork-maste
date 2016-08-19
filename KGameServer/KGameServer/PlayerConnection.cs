using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;
using System.Threading;

namespace KGameServer
{
    public class PlayerConnection
    {
        private string nickname;
        public string Nickname
        {
            get { return nickname; }
            set { nickname = value; }
        }

        private string userName;
        public string UserName
        {
            get { return userName; }
            set { userName = value; }
        }
       

        /// <summary>
        /// 用户的ID，指该用户在kgame.tbl_user中的记录的ID
        /// </summary>
        private int userId;
        public int UserId
        {
            get { return userId; }
            set { userId = value; }
        }
        
        /// <summary>
        /// 用户的来源，可能是来自东航金融，也可能是来自于网站
        /// </summary>
        private string source;
        public string Source
        {
            get { return source; }
            set { source = value; }
        }

        private bool islogined;
        public bool Islogined
        {
            get { return islogined; }
            set { islogined = value; }
        }

        private IWebSocketConnection clientConnection;
        public IWebSocketConnection ClientConnection
        {
            get { return clientConnection; }
            set { clientConnection = value; }
        }

        private ServerInst serverInst;

        private string remainingContent;

        private Match matchRunning;
        public Match MatchRunning
        {
            get 
            {
                Match ret = null;
                mutexFotMatch.WaitOne();
                ret = matchRunning;
                mutexFotMatch.ReleaseMutex();
                return ret; 
            }
            set
            {
                mutexFotMatch.WaitOne();
                matchRunning = value;
                mutexFotMatch.ReleaseMutex();
            }
        }
        private Mutex mutexFotMatch;

        /// <summary>
        /// 是否该连接已经断开了，可能用户在对局中，但是用户把连接断了，这个时候对局数据还未记录，需要等对决结束后再记录
        /// </summary>
        private bool isClosed;
        public bool IsClosed
        {
            get { return isClosed; }
            set { isClosed = value; }
        }

        public PlayerConnection(IWebSocketConnection aClientConnection,ServerInst aServerInst)
        {
            clientConnection = aClientConnection;
            nickname = "";
            userName = "";
            remainingContent = "";
            islogined = false;
            serverInst = aServerInst;
            isClosed = false;
            mutexFotMatch = new Mutex();
            matchRunning = null;
            this.userId = 0;
        }

        public void ProcessMessage(string msg)
        {
            msg=remainingContent+msg;
            List<Packet> packetList = Packet.Parse(msg, out remainingContent);
            foreach(Packet packet in packetList)
            {
                ProcessPacket(packet);
            }
        }

        public void ProcessPacket(Packet p)
        {
            if (p == null) return;
            switch(p.MsgType)
            {
                case "0":
                    //用户登入
                    ProcessUserLogin(p.Content);
                    break;
                case "A":
                    //快速登录
                    ProcessUserQuickLogin(p.Content);
                    break;
                case "3":
                    //用户请求开始对局
                    ProcessBeginMatch(p.Content);
                    break;

                case "6":
                    //该用户买入
                    ProcessUserBuyOrSell(p.Content, true);
                    break;

                case "7":
                    //该用户卖出
                    ProcessUserBuyOrSell(p.Content, false);
                    break;
                case "E":
                    //客户端操作内容结束
                    ProcessUserOperationEnd();
                    break;
                case "S":
                    //客户端操作分享请求
                    ProcessUserOperationShare(p.Content);
                    break;

                default:
                    Util.Log("unknown msg type");
                    break;
            }
        }

        /// <summary>
        /// 该玩家的对局结束
        /// </summary>
        private void ProcessUserOperationEnd()
        {
            MatchRunning.PlayerEnd(this);
        }


        /// <summary>
        /// 该玩家的对局分享吧
        /// </summary>
        private void ProcessUserOperationShare(string content)
        {
            MatchRunning.PlayerShare(this);
        }

        public void ProcessUserQuickLogin(string content)
        {
            if (content == null) return;
            string usernameOut="",passwordOut="";
            string result = QucikLogin(ref usernameOut, ref passwordOut);
            if (result == null)
            {
                //快速登录成功
                string contentToSend = "B|" + usernameOut + "#" + passwordOut + "|";
                Util.Log("快速登录成功:" + contentToSend);
                clientConnection.Send(contentToSend);
            }
            else
            {
                //快速登录失败
                clientConnection.Send("C|" + result + "|");
            }
        }

        public string QucikLogin(ref string usernameOut, ref string passwordOut)
        {
            return DBManager.QucikLogin(ref usernameOut, ref passwordOut);
        }



        public string Register(string username, string password)
        {
            return DBManager.RegisterUser(username, password);
        }

        public void ProcessUserBuyOrSell(string content,bool isBuy)
        {
            int index = -1;
            if(false==int.TryParse(content,out index))return;
            if(MatchRunning!=null)
            {
                MatchRunning.ProcessUserOperation(this, index, isBuy);
            }
        }


        public void ProcessUserLogin(string content)
        {
            if (content == null) return;
            string[] fs = content.Split('#');
            Util.Log("用户登录,用户名:" + fs[0] + " 密码:" + fs[1]+" 来源:"+fs[2]);

            string errMsg = "";
            int useridRet=DBManager.ValidateUser(fs[0], fs[1], fs[2],out errMsg);
            Util.Log("useridRet=" + useridRet);
            if (useridRet > 0)
            {
                //现判断该用户是否已经登录了
                PlayerConnection anotherLogin = serverInst.GetPlayerLoginedByUserID(useridRet);
                if (anotherLogin!=null)
                {
                    //该用户已经登录了
                    string kickoutMsg = "您在其他地方登录，登录地址为：" + this.ClientConnection.ConnectionInfo.ClientIpAddress;
                    anotherLogin.SendKickOutMsg(kickoutMsg);
                    //将anotherLogin踢掉
                    serverInst.ClosePlayerConnection(anotherLogin);
                }
                islogined = true;
                userName = fs[0];
                this.userId = useridRet;
                //记录登录上的用户数
                serverInst.AddLoginedPlayer(this);
                string toSentMsg = "1|" + userName + "|";
                Util.Log("发送信息:" + toSentMsg);
                clientConnection.Send(toSentMsg);
            }
            else
            {
                islogined = false;
                string toSentMsg = "2|" + errMsg + "|";
                Util.Log("发送信息:" + toSentMsg);
                clientConnection.Send(toSentMsg);
            }
        }

        public void ProcessBeginMatch(string content)
        {
            if (content == null) return;
            serverInst.AddBeginMatchPlayer(this, content);
        }

        public void BeginNewMatch(Match match)
        {
            clientConnection.Send("4|"+match.GetOtherPlayerInfoContent(this)+"|");
            mutexFotMatch.WaitOne();
            matchRunning = match;
            mutexFotMatch.ReleaseMutex();
        }

        public void SendMatchHistoryData(string jsonText)
        {
            clientConnection.Send("5|" + jsonText + "|");
        }

        public void SendKickOutMsg(string reason)
        {
            Util.Log("发送Kick out message:reason=" + reason);
            clientConnection.Send("D|" + reason + "|");
        }

        public void SendOtherPlayerOperation(PlayerConnection otherPlayerConnection,int index, bool isBuy)
        {
            if (otherPlayerConnection == null) return;
            string content = "";
            if(isBuy)
            {
                content = "8|";
            }
            else
            {
                content = "9|";
            }
            content += otherPlayerConnection.userName + "#" + index+"|";
            clientConnection.Send(content);
        }

        public void SendMatchEndString(string endMatchInfo)
        {
            string content = "F|" + endMatchInfo+"|";
            Util.Log("发送结束对局信息到:" + userName + "，信息内容为:" + content);
            clientConnection.Send(content);
        }

        public override string ToString()
        {
            string ret = "";
            try{
                ret = "[" + this.clientConnection.ConnectionInfo.ClientIpAddress + ":" + this.clientConnection.ConnectionInfo.ClientPort + "]";
                ret = ret + "[" + userId + ":" + userName + "]";
            }
            catch( Exception ex)
            {
                Util.LogException(ex);
            }
            return ret;
        }
    }
}
