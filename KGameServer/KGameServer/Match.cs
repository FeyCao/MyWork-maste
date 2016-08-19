using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace KGameServer
{
    /// <summary>
    /// 表示多个玩家之间的一局对战
    /// </summary>
    public class Match
    {
        private List<PlayerConnection> playerConnectionList;         //所有的玩家
        private List<PlayerConnection> endPlayerConnectionList;      //对局已经结束的连接
        private Dictionary<PlayerConnection, List<UserOperation>> userOperationDict;     //玩家的买卖操作
        private Mutex mutex;     //处理playerConnectionList和endPlayerConnectionList的同步
        private string mode;

        private CodeInfo thisTurnCodeInfo;
        /// <summary>
        /// 该次比赛用到的历史数据，按照时间顺序排列
        /// </summary>
        private List<DayData> dayDataList = new List<DayData>();
        private int preDayCount = 240;            //游戏之前的天数
        private int mainDayCount = 120;           //游戏时的天数
        private double standard = 0;              //需要归一化的价格

        private MatchMaker matchMaker;
        private int matchID;                      //对局的唯一ID

        //多人游戏
        public Match(List<PlayerConnection> aPlayerConnectionList, MatchMaker aMatchMaker)
        {
            playerConnectionList = new List<PlayerConnection>();
            endPlayerConnectionList = new List<PlayerConnection>();
            userOperationDict = new Dictionary<PlayerConnection, List<UserOperation>>();
            matchMaker = aMatchMaker;
            mutex = new Mutex();
            playerConnectionList.AddRange(aPlayerConnectionList);
            foreach(PlayerConnection pc in playerConnectionList)
            {
                userOperationDict[pc] = new List<UserOperation>();
            }
        }

        //单人游戏
        public Match(PlayerConnection playerConnection, MatchMaker aMatchMaker)
        {
            playerConnectionList = new List<PlayerConnection>();
            endPlayerConnectionList = new List<PlayerConnection>();
            userOperationDict = new Dictionary<PlayerConnection, List<UserOperation>>();
            matchMaker = aMatchMaker;
            mutex = new Mutex();
            playerConnectionList.Add(playerConnection);
            foreach (PlayerConnection pc in playerConnectionList)
            {
                userOperationDict[pc] = new List<UserOperation>();
            }
        }



        public void Start()
        {
            string userInfos = "";
            int playersCount = 0;
            mutex.WaitOne();
            try
            {
                playersCount = playerConnectionList.Count;
                foreach (PlayerConnection pc in playerConnectionList)
                {
                    userInfos += "[" + pc.UserId + ":" + pc.Nickname + "] ";
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();

            Util.Log(playersCount.ToString() + "人局开始: " + userInfos);
            HisDataMng.RequestHistoryData(OnReceiveHistoryDataCallBack);

        }

        /// <summary>
        /// 在启动对决时，获得除self之外的其他的玩家的信息
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public string GetOtherPlayerInfoContent(PlayerConnection self)
        {
            string content = "";
            mutex.WaitOne();
            try
            {
                for (int i = 0; i < playerConnectionList.Count; i++)
                {
                    if (playerConnectionList[i] != self)
                    {
                        content += playerConnectionList[i].Nickname;
                    }
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
            return content;
        }

        /// <summary>
        /// 是否玩家在此对局中
        /// </summary>
        /// <param name="playerConnection"></param>
        /// <returns></returns>
        public bool IsPlayerPlaying(PlayerConnection playerConnection)
        {
            bool ret = false;
            mutex.WaitOne();
            try
            {
                for (int i = 0; i < playerConnectionList.Count; i++)
                {
                    if (playerConnectionList[i] != playerConnection)
                    {
                        ret = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
            return ret;
        }

        public void CloseConnection(PlayerConnection playerConnection)
        {
            playerConnection.MatchRunning = null;

            mutex.WaitOne();
            bool allClosed = true;
            try
            {
                for (int i = 0; i < playerConnectionList.Count; i++)
                {
                    if (playerConnectionList[i].IsClosed == false)
                    {
                        allClosed = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
            if (allClosed)
            {
                CloseMatch();
            }
        }

        private void CloseMatch()
        {
            //关闭Match时先计算收益率
            string content=this.CalculateRatio();
            
            mutex.WaitOne();
            try
            {
                for (int i = 0; i < playerConnectionList.Count; i++)
                {
                    playerConnectionList[i].SendMatchEndString(content);
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();

            //给每个玩家传送信息
            matchMaker.CloseMatch(this);
        }

        public string CalculateRatio()
        {
            string content = "";
            mutex.WaitOne();
            try
            {
                content = userOperationDict.Keys.Count + "#";
                foreach (PlayerConnection p in userOperationDict.Keys)
                {
                    double totalProfit = CalculateSingleRation(userOperationDict[p]);
                    content += p.UserName + "#" + totalProfit.ToString("0.00") +"#" + "0#";
                }
                string exchange = "";
                switch (thisTurnCodeInfo.exchange)
                {
                    case "XSHG":
                        exchange="上证";
                        break;
                    case "XSHE":
                        exchange="深证";
                        break;
                    case "CCFX":
                        exchange = "中金所";
                        break;
                    case "XSGE":
                        exchange = "上期所";
                        break;
                    case "XDCE":
                        exchange = "大商所";
                        break;
                    case "XZCE":
                        exchange = "郑商所";
                        break;

                }
                content += thisTurnCodeInfo.code + "(" + exchange + ")#";
                content += dayDataList[0].dateTime.ToShortDateString() + "#" + dayDataList[dayDataList.Count - 1].dateTime.ToShortDateString();
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
            return content;
        }

        //计算收益率
        private double CalculateSingleRation(List<UserOperation> userOperationList)
        {
            if (userOperationList == null) return 0;
            double totalProfit = 0;
            if (userOperationList.Count % 2 == 1)
            {
                if (userOperationList[userOperationList.Count - 1].isBuy)
                {
                    //最后一笔是卖出
                    totalProfit += dayDataList[dayDataList.Count - 1].close * 100 / standard;
                }
                else
                {
                    //最后一笔是买入
                    totalProfit -= dayDataList[dayDataList.Count - 1].close * 100 / standard;
                }
            }
            for (int i = 0; i < userOperationList.Count; i++)
            {
                if (userOperationList[i].isBuy)
                {
                    totalProfit -= userOperationList[i].price * 100 / standard;
                }
                else
                {
                    totalProfit += userOperationList[i].price * 100 / standard;
                }
            }
            return totalProfit;
        }

        /// <summary>
        /// 记录用户买卖的信息
        /// </summary>
        /// <param name="playerConnection"></param>
        /// <param name="index"></param>
        /// <param name="isBuy"></param>
        public void ProcessUserOperation(PlayerConnection playerConnection, int index, bool isBuy)
        {
            mutex.WaitOne();
            try
            {
                for (int i = 0; i < playerConnectionList.Count; i++)
                {
                    if (playerConnectionList[i] != playerConnection)
                    {
                        playerConnectionList[i].SendOtherPlayerOperation(playerConnection, index, isBuy);
                    }
                }

                List<UserOperation> userOperationList = null;
                if (userOperationDict.ContainsKey(playerConnection) == false)
                {
                    userOperationList = new List<UserOperation>();
                    userOperationDict.Add(playerConnection, userOperationList);
                }
                else
                {
                    userOperationList = userOperationDict[playerConnection];
                }
                if(userOperationList!=null)
                {
                    userOperationList.Add(new UserOperation(isBuy, index, dayDataList[index].close));
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
        }

        /// <summary>
        /// 当某个玩家结束时，记录结束时的数据
        /// </summary>
        /// <param name="playerConnection"></param>
        public void PlayerEnd(PlayerConnection playerConnection)
        {
            bool bCanCloseMatch = false;
            mutex.WaitOne();
            try
            {
                if (playerConnectionList.Contains(playerConnection) == true)
                {
                    if (endPlayerConnectionList.Contains(playerConnection) == false)
                    {
                        endPlayerConnectionList.Add(playerConnection);
                        if (endPlayerConnectionList.Count == playerConnectionList.Count)
                        {
                            //收到全部玩家的结束消息
                            bCanCloseMatch = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();

            if (bCanCloseMatch)
            {
                CloseMatch();
            }
        }


        /// <summary>
        /// 历史数据按照时间倒序排列
        /// </summary>
        /// <param name="historyDataList"></param>
        /// <param name="codeInfo"></param>
        private void OnReceiveHistoryDataCallBack(List<DayData> historyDataList, CodeInfo codeInfo)
        {
            dayDataList.Clear();

            thisTurnCodeInfo = codeInfo;
            standard = historyDataList[mainDayCount].close;

            string jsonText = "{\"data\":[";

            for (int i = 0; i < mainDayCount + preDayCount; i++)
            {
                if (i < mainDayCount)
                {
                    dayDataList.Insert(0, historyDataList[i]);
                }
                int index = mainDayCount + preDayCount - 1 - i;
                jsonText += (100 * (historyDataList[index].open / standard)).ToString("0.000") + ",";
                jsonText += (100 * (historyDataList[index].max / standard)).ToString("0.000") + ",";
                jsonText += (100 * (historyDataList[index].min / standard)).ToString("0.000") + ",";
                jsonText += (100 * (historyDataList[index].close / standard)).ToString("0.000") + ",";
                jsonText += historyDataList[index].volumn.ToString();

                if (index != 0)
                {
                    jsonText += ",";
                }
            }
            jsonText += "],\"count\":[";
            jsonText += mainDayCount.ToString() + "," + preDayCount.ToString() + "]}";

            ///将该对局信息插入数据库
            matchID = DBManager.AddNewMatchInfo(0, codeInfo, historyDataList[mainDayCount - 1].dateTime, mainDayCount);
            if (matchID == -1)
            {
                Util.Log("由于未能添加对局信息到数据库中，此次对局无法成功启动，返回。。。。。。");
                this.CloseMatch();
                return;
            }

            mutex.WaitOne();
            try
            {
                //通知双方对局开始
                foreach (PlayerConnection pc in playerConnectionList)
                {
                    pc.BeginNewMatch(this);
                }

                foreach (PlayerConnection pc in playerConnectionList)
                {
                    pc.SendMatchHistoryData(jsonText);
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
        }
    }
}
