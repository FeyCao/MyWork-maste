using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KGameServer
{
    /// <summary>
    /// 一天的开高低收
    /// </summary>
    public class DayData
    {
        public double open;
        public double max;
        public double min;
        public double close;
        public long volumn;
        public DateTime dateTime;

        public override string ToString()
        {
            return dateTime.ToString("yyyy-MM-dd") + " O:" + open.ToString("0.000") + " X:" + max.ToString("0.000") + " I:" + min.ToString("0.000") + " C:" + close.ToString("0.000") + " V:" + volumn;
        }
    }

}
