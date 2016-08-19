using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;  

namespace KGameServer
{
    /// <summary>
    /// 用于比赛的配对
    /// </summary>
    public class MatchMaker : INotifyPropertyChanged  
    {
        private List<PlayerConnection> waitingPlayers;
        private Mutex mutex;        //控制waitingPlayers的访问

        private List<Match> runningMatch;
        private Mutex matchMutex;   //控制runningMatch的访问


        public event PropertyChangedEventHandler PropertyChanged;  

        /// <summary>
        /// 当前的对局数
        /// </summary>
        public int MacthCount
        {
            get
            {
                if (runningMatch == null) return 0;
                return runningMatch.Count;
            }
        }

        /// <summary>
        /// 在等待对局匹配的玩家数
        /// </summary>
        public int WaitingPlayersCount
        {
            get
            {
                if (waitingPlayers == null) return 0;
                return waitingPlayers.Count;
            }
        }

        public MatchMaker()
        {
            waitingPlayers = new List<PlayerConnection>();
            runningMatch = new List<Match>();

            mutex = new Mutex();
            matchMutex = new Mutex();
        }

        public void BeginNewMatchForSinglePlayer(PlayerConnection playerConnection)
        {
            if (playerConnection == null) return;
            Util.Log("准备开始一场单人游戏:"+playerConnection.ToString());
            Match m = GenerateSinglePlayerMatch(playerConnection);

            m.Start();

            matchMutex.WaitOne();
            runningMatch.Add(m);
            matchMutex.ReleaseMutex();
            PropertyChanged(this, new PropertyChangedEventArgs("MacthCount"));

        }

        public void AddWaitingPlayer(PlayerConnection playerConnection)
        {
            if (playerConnection == null) return;
            Util.Log("AddWaitingPlayer username=" + playerConnection.Nickname);
            List<PlayerConnection> matchPlayerList = null;
            mutex.WaitOne();
            try
            {
                if (waitingPlayers.Count == 0)
                {
                    waitingPlayers.Add(playerConnection);
                    PropertyChanged(this, new PropertyChangedEventArgs("WaitingPlayersCount"));
                }
                else
                {
                    matchPlayerList = new List<PlayerConnection>();
                    matchPlayerList.Add(playerConnection);
                    matchPlayerList.Add(waitingPlayers[0]);
                    waitingPlayers.Clear();
                    PropertyChanged(this, new PropertyChangedEventArgs("WaitingPlayersCount"));
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();

            if (matchPlayerList != null)
            {
                Match m = GenerateMultiplayerMatch(matchPlayerList);
                m.Start();

                matchMutex.WaitOne();
                runningMatch.Add(m);
                matchMutex.ReleaseMutex();
                PropertyChanged(this, new PropertyChangedEventArgs("MacthCount"));

            }
        }

        public Match GenerateMultiplayerMatch(List<PlayerConnection> matchPlayerList)
        {
            return new Match(matchPlayerList,this);
        }

        public Match GenerateSinglePlayerMatch(PlayerConnection playerConnection)
        {
            return new Match(playerConnection, this);
        }

        public void CloseConnection(PlayerConnection playerConnection)
        {
            mutex.WaitOne();
            try
            {
                for(int i=0;i<waitingPlayers.Count;i++)
                {
                    if(waitingPlayers[i]==playerConnection)
                    {
                        waitingPlayers.RemoveAt(i);
                        PropertyChanged(this, new PropertyChangedEventArgs("WaitingPlayersCount"));
                        break;
                    }
                }
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();

            Match match = playerConnection.MatchRunning;
            if(match!=null)
            {
                match.CloseConnection(playerConnection);
            }
        }

        public void CloseMatch(Match match)
        {
            matchMutex.WaitOne();
            try
            {
                runningMatch.Remove(match);
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
            }
            matchMutex.ReleaseMutex();
            PropertyChanged(this, new PropertyChangedEventArgs("MacthCount"));
        }
    }
}
