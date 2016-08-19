using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KGameServer
{
    public class CodeInfo
    {
        public string code;
        public string exchange;

        public override bool Equals(object obj)
        {
            if (obj as CodeInfo == null) return false;
            return (obj as CodeInfo).code == code && (obj as CodeInfo).exchange == exchange;
        }

        public CodeInfo(string aCode,string aExchange)
        {
            code = aCode;
            exchange = aExchange;
        }

        public override string ToString()
        {
            return code.ToString() + " " + exchange.ToString();
        }

        public override int GetHashCode()
        {
            return (this.ToString()).GetHashCode();
        }
    }
    public class matchInfo
    {
        public string code;
        public string exchange;
        public DateTime startTime;
        public int daycount;

        public override bool Equals(object obj)
        {
            if (obj as matchInfo == null) return false;
            return (obj as matchInfo).code == code && (obj as matchInfo).exchange == exchange && (obj as matchInfo).daycount == daycount && (obj as matchInfo).startTime == startTime;
        }

        public matchInfo(string aCode, string aExchange, DateTime aStartTime, int aDaycount)
        {
            code = aCode;
            exchange = aExchange;
            startTime = aStartTime;
            daycount = aDaycount;
        }
    }
}
