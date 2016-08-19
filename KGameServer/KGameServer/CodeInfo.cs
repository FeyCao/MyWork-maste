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
}
