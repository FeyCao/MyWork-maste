using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using MySql.Data.MySqlClient;
using System.Threading;
using System.Data;

namespace KGameServer
{
    /// <summary>
    /// 从历史数据库中获取历史数据，返回给Match
    /// </summary>
    public class HisDataMng
    {
        /// <summary>
        /// 表示一次历史数据请求的类
        /// </summary>
        private class HisDataRequest
        {
            public int preDayCount;
            public int mainDayCount;
            public OnReceiveHistoryDataCallBack onreceiveHistoryDataCallBack;

            public HisDataRequest(OnReceiveHistoryDataCallBack aOnreceiveHistoryDataCallBack)
            {
                preDayCount = 240;
                mainDayCount = 60;
                onreceiveHistoryDataCallBack = aOnreceiveHistoryDataCallBack;
            }

            public HisDataRequest(OnReceiveHistoryDataCallBack aOnreceiveHistoryDataCallBack, int aPreDayCount, int aMainDayCount)
            {
                preDayCount = aPreDayCount;
                mainDayCount = aMainDayCount;
                onreceiveHistoryDataCallBack = aOnreceiveHistoryDataCallBack;
            }
        }



        /// <summary>
        /// 除权数据
        /// </summary>
        private class PWRData
        {
            public string code { get; set; }
            public string exchange { get; set; }
            public DateTime date { get; set; }
            public double instock { get; set; } //每股送股数加每股转增股数
            public double rightstockNum { get; set; } //每股配股数
            public double rightstockPrice { get; set; } //每股配股价
            public double bonus { get; set; }//每股分红

            public PWRData()
            {
                code = "";
                exchange = "";
                instock = 0;
                rightstockNum = 0;
                rightstockPrice = 0;
                bonus = 0;
            }
        }


        private static Thread processThread;

        public delegate void OnReceiveHistoryDataCallBack(List<DayData> historyDataList, CodeInfo codeInfo);

        private static SynQueue<HisDataRequest> hisDataRequestQueue;

        private static Mutex mutexForCodeInfoDict;
        private static DateTime lastUpdateTime;
        private static Dictionary<CodeInfo, int> codeInfoDictGP;
        private static Dictionary<CodeInfo, int> codeInfoDictQH;


        private static string hisdataSql = "";
        public static void Init()
        {
            hisDataRequestQueue = new SynQueue<HisDataRequest>();

            codeInfoDictGP = new Dictionary<CodeInfo, int>();
            codeInfoDictQH = new Dictionary<CodeInfo, int>();
            mutexForCodeInfoDict = new Mutex();

            lastUpdateTime = new DateTime(1999, 1, 1);

            processThread = new Thread(MainProcessThread);
            processThread.IsBackground = true;
            processThread.Start();

            GetConnectString();
        }

        private static void RefreshCodeInfoList()
        {
            if (DateTime.Now.Subtract(lastUpdateTime).Days < 1)
            {
                Util.Log("RefreshCodeInfoList 返回 lastUpdateTime=" + lastUpdateTime.ToString()+" Now="+DateTime.Now.ToString());
                return;
            }


            string codeInfoFile = System.AppDomain.CurrentDomain.BaseDirectory + "setting\\codeinfo.txt";
            FileStream fs = null;
            StreamReader sr = null;

            mutexForCodeInfoDict.WaitOne();
            Util.Log("mutexForCodeInfoDict Passed");
            try
            {
                fs = new FileStream(codeInfoFile, FileMode.Open, FileAccess.Read);
                sr = new StreamReader(fs,ASCIIEncoding.Default);
                codeInfoDictGP.Clear();
                codeInfoDictQH.Clear();
                string line = sr.ReadLine();
                while (line != null)
                {
                    string[] fields = line.Split('\t');
                    line = sr.ReadLine();
                    if ((fields[1] == "XSHG" && (fields[0].StartsWith("6"))) ||
                        (fields[1] == "XSHE" && (fields[0].StartsWith("00") || fields[0].StartsWith("30"))))
                    {
                        CodeInfo codeInfo = new CodeInfo(fields[0], fields[1]);
                        int count = int.Parse(fields[2]);
                        if (codeInfoDictGP.ContainsKey(codeInfo) == false)
                        {
                            codeInfoDictGP[codeInfo] = count;
                        }
                    }
                    else if(fields[1]=="CCFX" || fields[1]=="XZCE" || fields[1]=="XDCE"|| fields[1]=="XSGE")
                    {
                        CodeInfo codeInfo = new CodeInfo(fields[0], fields[1]);
                        int count = int.Parse(fields[2]);
                        if (codeInfoDictQH.ContainsKey(codeInfo) == false)
                        {
                            codeInfoDictQH[codeInfo] = count;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForCodeInfoDict.ReleaseMutex();
            Util.Log("mutexForCodeInfoDict Released");

            if (sr != null)
            {
                sr.Close();
            }
            if (fs != null)
            {
                fs.Close();
            }
        }

        private static void MainProcessThread()
        {
            while (true)
            {
                List<HisDataRequest> hisDataRequestList = hisDataRequestQueue.DequeueAll();
                Util.Log("hisDataRequestList 出队，hisDataRequestList中个数为:" + hisDataRequestList.Count);
                MySqlConnection mySqlConnection = new MySqlConnection(hisdataSql);
                try
                {
                    mySqlConnection.Open();
                    Util.Log("数据库已经打开");
                }
                catch (Exception ex)
                {
                    Util.LogException(ex);
                    try
                    {
                        if (mySqlConnection != null)
                        {
                            mySqlConnection.Close();
                        }
                    }
                    catch (Exception ex2)
                    {
                        Util.LogException(ex2);
                    }
                    return;
                }
                ///先刷新一遍所有的CodeInfo，如果需要的话
                RefreshCodeInfoList();
                Util.Log("RefreshCodeInfoList 已经完成");

                foreach (HisDataRequest hisDataRequest in hisDataRequestList)
                {
                    ProcessHisDataRequest(hisDataRequest, mySqlConnection);
                }
                Util.Log("ProcessHisDataRequest 已经结束");

                try
                {
                    if (mySqlConnection != null)
                    {
                        mySqlConnection.Close();
                        Util.Log("数据库已经关闭");
                    }
                }
                catch (Exception ex2)
                {
                    Util.LogException(ex2);
                }
            }
        }

        private static void ProcessHisDataRequest(HisDataRequest hisDataRequest, MySqlConnection mySqlConnection)
        {
            Util.Log("ProcessHisDataRequest hisDataRequest");
            List<DayData> dayDataList = null;
            CodeInfo codeInfo = null;
            mutexForCodeInfoDict.WaitOne();
            try
            {
                int tryCount = 0;
                string sql = null;
                MySqlCommand command = null;
                MySqlDataAdapter adapter = null;
                DataSet ds = null;
                //根据随机数获得Seed,0到200
                Random r = new Random((int)(DateTime.Now.Ticks));
                bool isQH = false;
                if (r.Next(10) <= 7)
                {
                    isQH = true;
                }
                Dictionary<CodeInfo, int> codeInfoDict = codeInfoDictGP;
                if (isQH)
                {
                    codeInfoDict = codeInfoDictQH;
                }
                while (tryCount < 10)
                {

                    Util.Log("codeInfoDict.Keys.Count=" + codeInfoDict.Keys.Count);
                    int codeInfoIndex = r.Next(codeInfoDict.Keys.Count);
                    Util.Log("codeInfoIndex=" + codeInfoIndex);

                    int totalCandleCount = hisDataRequest.preDayCount + hisDataRequest.mainDayCount;
                    Util.Log("totalCandleCount=" + totalCandleCount);

                    codeInfo = codeInfoDict.Keys.ElementAt(codeInfoIndex);
                    Util.Log("codeInfo=" + codeInfo.ToString());

                    int count = codeInfoDict[codeInfo];
                    if (count <= totalCandleCount)
                    {
                        tryCount++;
                        Util.Log("总个数不够count=" + count + "，重新取");
                        continue;
                    }
                    int startIndex = r.Next(count - totalCandleCount);

                    Util.Log("随机数据，codeInfo=" + codeInfo.ToString() + " ,startIndex=" + startIndex);

                    //测试用
                   // codeInfo = new CodeInfo("600459", "XSHG");
                   // startIndex = 1105;

                    string tbl = "tbl_dayhistorydata";
                    if (codeInfo.exchange == "XSHG")
                    {
                        tbl = "tbl_gp_sh_dayhistorydata";
                    }
                    else if(codeInfo.exchange == "XSHE")
                    {
                        tbl = "tbl_gp_sz_dayhistorydata";
                    }
                    sql = "select * from " + tbl + " where CODE='" + codeInfo.code + "' order by date desc LIMIT " + startIndex + "," + totalCandleCount;

                    command = mySqlConnection.CreateCommand();
                    command.CommandText = sql;
                    adapter = new MySqlDataAdapter(command);
                    ds = new DataSet();
                    ds.Tables.Clear();
                    adapter.Fill(ds);
                    dayDataList = new List<DayData>();//历史数据，按照时间倒序排列
                    if (ds.Tables[0].Rows.Count == 0)
                    {

                        tryCount++;
                        Util.Log("数据取到了全是0，需要再取一次数据，已经尝试了" + tryCount + " 次");
                        continue;
                    }
                    foreach (DataRow row in ds.Tables[0].Rows)
                    {
                        DayData dayData = new DayData();
                        dayData.dateTime = (DateTime)row["DATE"];
                        dayData.open = (double)row["OPEN"];
                        dayData.max = (double)row["MAXPRICE"];
                        dayData.min = (double)row["MINPRICE"];
                        dayData.close = (double)row["CLOSE"];
                        dayData.volumn = (long)row["VOLUMN"];
                        dayDataList.Add(dayData);
                    }
                    break;
                }


                if (isQH == false)
                {
                    //取时间区间内的除权
                    DateTime startDate = dayDataList[dayDataList.Count - 1].dateTime;
                    DateTime endDate = dayDataList[0].dateTime;

                    sql = "select * from historydata.tbl_gp_xrinformation where CODE='" + codeInfo.code + "' and exchange='" + codeInfo.exchange + "' and date>'" + startDate.ToString("yyyy-MM-dd 00:00:00") + "' and date<='" + endDate.ToString("yyyy-MM-dd 00:00:00") + "' order by date desc";
                    command.CommandText = sql;
                    adapter = new MySqlDataAdapter(command);
                    ds.Tables.Clear();
                    adapter.Fill(ds);
                    List<PWRData> pwrDataList = new List<PWRData>();

                    foreach (DataRow row in ds.Tables[0].Rows)
                    {
                        PWRData pwrData = new PWRData();
                        pwrData.date = (DateTime)row["DATE"];
                        pwrData.instock = (double)row["instock"];
                        pwrData.rightstockNum = (double)row["rightstockNum"];
                        pwrData.rightstockPrice = (double)row["rightstockPrice"];
                        pwrData.bonus = (double)row["bonus"];
                        pwrDataList.Add(pwrData);
                    }
                    RestoreExright(dayDataList, pwrDataList);
                }
                
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForCodeInfoDict.ReleaseMutex();
            Util.Log("获得到了历史数据，开始调用回调函数");
            hisDataRequest.onreceiveHistoryDataCallBack(dayDataList, codeInfo);
        }


        /// <summary>
        /// 复权
        /// </summary>
        /// <param name="dayDayaList"></param>
        /// <param name="pwrDataList"></param>
        private static void RestoreExright(List<DayData> dayDayaList, List<PWRData> pwrDataList)
        {
            if (dayDayaList == null || pwrDataList == null) return;
            if (dayDayaList.Count == 0 || pwrDataList.Count == 0) return;

            for (int i = 0; i < dayDayaList.Count; i++)
            {
                for (int j = 0; j < pwrDataList.Count; j++)
                {
                    if (dayDayaList[i].dateTime.Subtract(pwrDataList[j].date).TotalDays < 0)
                    {
                        //计算公式 价格=(价格-每10股红利(元)/10+配股价*每10股配股数/10)/(1+每10股配股数/10+每10股送股数/10);
                        dayDayaList[i].open = (dayDayaList[i].open - pwrDataList[j].bonus + pwrDataList[j].rightstockPrice * pwrDataList[j].rightstockNum) / (1 + pwrDataList[j].rightstockNum + pwrDataList[j].instock);
                        dayDayaList[i].max = (dayDayaList[i].max - pwrDataList[j].bonus + pwrDataList[j].rightstockPrice * pwrDataList[j].rightstockNum) / (1 + pwrDataList[j].rightstockNum + pwrDataList[j].instock);
                        dayDayaList[i].min = (dayDayaList[i].min - pwrDataList[j].bonus + pwrDataList[j].rightstockPrice * pwrDataList[j].rightstockNum) / (1 + pwrDataList[j].rightstockNum + pwrDataList[j].instock);
                        dayDayaList[i].close = (dayDayaList[i].close - pwrDataList[j].bonus + pwrDataList[j].rightstockPrice * pwrDataList[j].rightstockNum) / (1 + pwrDataList[j].rightstockNum + pwrDataList[j].instock);
                        dayDayaList[i].volumn = (long)(dayDayaList[i].volumn * (1 + pwrDataList[j].rightstockNum + pwrDataList[j].instock));
                    }
                }
            }
        }


        /// <summary>
        /// 请求历史数据
        /// </summary>
        /// <param name="onreceiveHistoryDataCallBack"></param>
        /// <param name="aPreDayCount"></param>
        /// <param name="aMainDayCount"></param>
        public static void RequestHistoryData(OnReceiveHistoryDataCallBack onreceiveHistoryDataCallBack,
            int aPreDayCount = 240, int aMainDayCount = 120)
        {
            Util.Log("RequestHistoryData 请求");
            hisDataRequestQueue.Enqueue(new HisDataRequest(onreceiveHistoryDataCallBack, aPreDayCount, aMainDayCount),
                false);
            Util.Log("入队结束");
        }

        private static string GetConnectString()
        {
            string userFile = AppDomain.CurrentDomain.BaseDirectory + "setting//ServerInfo.xml";
            if (File.Exists(userFile) == true)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(userFile);
                XmlNodeList dbList = doc.SelectNodes("/config/hisdatadbconnect");
                if (dbList != null)
                {
                    foreach (XmlElement node in dbList)
                    {
                        if (node.HasAttribute("connectionstring"))
                        {
                            hisdataSql = node.Attributes["connectionstring"].Value;
                        }
                    }
                }
            }
            return null;
        }
    }
}
