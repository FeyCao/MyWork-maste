using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fleck;

namespace KGameServer
{
    /// <summary>
    /// 没有来源的消息，发生该问题的可能是OnOpen的调用在OnMessage之后
    /// </summary>
    public class NoSourceMessage
    {
        private DateTime dateTime;
        public DateTime DateTime
        {
            get { return dateTime; }
            set { dateTime = value; }
        }


        private string message;
        public string Message
        {
            get { return message; }
            set { message = value; }
        }

        private IWebSocketConnection connection;
        public IWebSocketConnection Connection
        {
            get { return connection; }
            set { connection = value; }
        }

        public NoSourceMessage(string aMessage,IWebSocketConnection aConnection)
        {
            message = aMessage;
            connection = aConnection;
        }
    }
}
