using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KGameServer
{
    /// <summary>
    /// 协议：类型(1个字节0-9,A-Z,a-z)|消息内容|
    /// 0:登录，内容:username#password#source
    /// A:快速登录，内容：无  快速登录即直接注册并且登录
    /// B:快速登录成功，内容：返回用户名#密码
    /// C:快速登录失败，内容：快速登录失败原因
    /// D:被踢下线，内容：被踢的原因
    /// E:客户端操作结束，内容：无
    /// F:服务器返回对局结束信息，内容：总用户个数(假设为2)#用户名A#收益率A#得分A#用户名B#收益率B#得分B#品种名字#起始日期#终止日期
    /// 1:登录成功，内容：返回用户名
    /// 2:登录失败，内容：失败原因
    /// 3:开始对局，等待，内容：游戏模式（无：单人，1：多人）
    /// 4:匹配成功，开始游戏，内容：对手信息
    /// 5:K线数据，内容：种子序号
    /// 6:收到买入，内容：序号
    /// 7:收到卖出，内容：序号
    /// 8:发送买入，内容：用户名#序号
    /// 9:发送卖出，内容：用户名#序号
    /// </summary>
    public class Packet
    {
        private string msgType;
        public string MsgType
        {
            get { return msgType; }
            set { msgType = value; }
        }

        private string content;
        public string Content
        {
            get { return content; }
            set { content = value; }
        }

        public Packet(string aMsgType,string acontent)
        {
            msgType = aMsgType;
            content = acontent;
        }

        public override string ToString()
        {
            return msgType + "|"  + content + "|";
        }

        public static List<Packet> Parse(string fullContent,out string remainingContent)
        {
            List<Packet> ret = null;
            remainingContent = "";
            try
            {
                if (fullContent == null) return ret;
                int index = fullContent.IndexOf('|');
                int count = 1;
                while(index!=-1)
                {
                    if(count==2)
                    {
                        if(ret==null)
                        {
                            ret = new List<Packet>();
                        }
                        Packet p = ParseSingleRow(fullContent.Substring(0, index + 1));
                        if(p!=null)
                        {
                            ret.Add(p);
                        }
                        fullContent = fullContent.Substring(index + 1);
                        index = -1;
                        count = 0;
                    }
                    index = fullContent.IndexOf('|',index+1);
                    count++;
                }
                remainingContent = fullContent;
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            return ret;
        }

        private static Packet ParseSingleRow(string fullContent)
        {
            if (fullContent == null || fullContent.Trim() == "") return null;
            try
            {
                string[] fs = fullContent.Split('|');
                string msgType = fs[0];
                string content = fs[1];
                return new Packet(msgType, content);
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
            }
            return null;
        }
    }
}
