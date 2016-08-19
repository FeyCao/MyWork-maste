using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Xml;
using System.Threading;

namespace MySqlRelay
{
    /// <summary>
    /// 接收网络数据后操作数据库，并且返回结果的类
    /// </summary>
    public class NetManager
    {
        private class ClientSentText
        {
            public string text;
            public IPEndPoint endpoint;

            public ClientSentText(string aText,IPEndPoint aIpEndPoint)
            {
                text = aText;
                endpoint = aIpEndPoint;
            }
        }

        private static Socket udpSocket=null;
        private static int recvBufferSize = 1024 * 1024;
        private static byte[] receiveBuffer = new byte[recvBufferSize];
        private static string address = "";
        private static int port = 0;
        private static Thread receiveThread;
        private static Thread processTextThread;

        private static SynQueue<ClientSentText> receievedClientTextQueue;

        /// <summary>
        /// 初始化
        /// </summary>
        public static void Init()
        {
            receievedClientTextQueue = new SynQueue<ClientSentText>();
            ReadPortInfoFromXml();
            CreateSocketAndReceive();
        }

        private static void CreateSocketAndReceive()
        {
            if(udpSocket==null)
            {
                IPAddress ipAddress = IPAddress.Parse(address);
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, recvBufferSize);
                udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpSocket.Bind(new IPEndPoint(ipAddress, port));
            }
            receiveThread = new Thread(ReceivingThread);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            processTextThread = new Thread(ProcessTextThreadProc);
            processTextThread.IsBackground = true;
            processTextThread.Start();

        }

        private static void ProcessTextThreadProc()
        {
            while(true)
            {
                ClientSentText clientSentText = receievedClientTextQueue.Dequeue();
                ProcessText(clientSentText);
            }
        }

        private static void ProcessText(ClientSentText clientSentText)
        {

        }

        private static void ReceivingThread()
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remote = (EndPoint)sender;
            while(true)
            {
                int receivedLenth = udpSocket.ReceiveFrom(receiveBuffer, ref remote);
                string text = ASCIIEncoding.Default.GetString(receiveBuffer, 0, receivedLenth);
                receievedClientTextQueue.Enqueue(new ClientSentText(text,remote as IPEndPoint), false);
            }
        }


        private static void ReadPortInfoFromXml()
        {
            string userFile = AppDomain.CurrentDomain.BaseDirectory + "setting//ServerInfo.xml";

            if (File.Exists(userFile) == true)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(userFile);
                XmlNodeList dbList = doc.SelectNodes("/config/udp");
                if (dbList != null)
                {
                    foreach (XmlElement node in dbList)
                    {
                        if (node.HasAttribute("address"))
                        {
                            address = node.Attributes["address"].Value;
                        }

                        if (node.HasAttribute("port"))
                        {
                            port = int.Parse(node.Attributes["port"].Value);
                        }
                    }
                }
            }
        }
    }
}
