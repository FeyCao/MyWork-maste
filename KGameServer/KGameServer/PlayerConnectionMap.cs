using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Fleck;
using System.ComponentModel;  

namespace KGameServer
{
    public class PlayerConnectionMap : INotifyPropertyChanged  
    {
        private Dictionary<IWebSocketConnection, PlayerConnection> playerConnectionDict;
        private Mutex mutex;

        private Dictionary<int, PlayerConnection> playerConnectionDictByUserID;
        private Mutex mutexForUserID;
        
        public event PropertyChangedEventHandler PropertyChanged;
        private ServerInst serverInst;

        public PlayerConnectionMap(ServerInst aServerInst)
        {
            serverInst = aServerInst;

            playerConnectionDict = new Dictionary<IWebSocketConnection, PlayerConnection>();
            mutex = new Mutex();

            playerConnectionDictByUserID = new Dictionary<int, PlayerConnection>();
            mutexForUserID = new Mutex();

        }

        /// <summary>
        /// 在线用户数，包括有登录的和无登录的
        /// </summary>
        public int OnlineCount
        {
            get
            {
                int count = 0;
                mutex.WaitOne(1000);
                count = playerConnectionDict.Values.Count;
                mutex.ReleaseMutex();
                return count;
            }
        }

        /// <summary>
        /// 在线用户数，有UserID的
        /// </summary>
        public int OnlineUserCount
        {
            get
            {
                int count = 0;
                mutex.WaitOne(1000);
                count = playerConnectionDictByUserID.Values.Count;
                mutex.ReleaseMutex();
                return count;
            }
        }

        public PlayerConnection AddConnection(IWebSocketConnection clientConnection)
        {
            PlayerConnection ret = null;
            mutex.WaitOne(1000);
            try
            {
                ret=new PlayerConnection(clientConnection, serverInst);
                playerConnectionDict.Add(clientConnection, ret);
                PropertyChanged(this, new PropertyChangedEventArgs("OnlineCount")); //在线人数变化
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
            return ret;
        }

        public void CloseConnection(PlayerConnection playerConnection)
        {
            if (playerConnection == null) return;
            //先删除在线数
            mutex.WaitOne(1000);
            try
            {
                if (playerConnectionDict.ContainsKey(playerConnection.ClientConnection))
                {
                    playerConnectionDict.Remove(playerConnection.ClientConnection);
                    PropertyChanged(this, new PropertyChangedEventArgs("OnlineCount")); //在线人数变化
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();


            //再删除登录用户数（如果有的话）
            mutexForUserID.WaitOne(1000);
            try
            {
                if (playerConnection != null)
                {
                    if (playerConnectionDictByUserID.ContainsKey(playerConnection.UserId))
                    {
                        PlayerConnection toDelete = playerConnectionDictByUserID[playerConnection.UserId];
                        if (toDelete == playerConnection)
                        {
                            playerConnectionDictByUserID.Remove(playerConnection.UserId);
                            PropertyChanged(this, new PropertyChangedEventArgs("OnlineUserCount")); //登录人数变化
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForUserID.ReleaseMutex();
        }

        public PlayerConnection GetPlayerConnection(IWebSocketConnection clientConnection)
        {
            PlayerConnection ret = null;
            mutex.WaitOne(1000);
            try
            {
                Util.Log("playerConnectionDict key count=" + playerConnectionDict.Keys.Count);
                if (playerConnectionDict.ContainsKey(clientConnection))
                {
                    ret = playerConnectionDict[clientConnection];
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutex.ReleaseMutex();
            return ret;
        }

        /// <summary>
        /// 增加已经登录了的用户
        /// </summary>
        /// <param name="playerConnection"></param>
        public void AddLoginedPlayer(PlayerConnection playerConnection)
        {
            if (playerConnection == null) return;
            if (playerConnection.UserId == 0)
            {
                Util.Log("错误:用户ID为空");
                return;
            }

            mutexForUserID.WaitOne(1000);
            try
            {
                if (playerConnectionDictByUserID.ContainsKey(playerConnection.UserId))
                {
                    Util.Log("用户名:" + playerConnection.Nickname + "已经登录上了");
                }
                else
                {
                    playerConnectionDictByUserID.Add(playerConnection.UserId, playerConnection);
                    PropertyChanged(this, new PropertyChangedEventArgs("OnlineUserCount")); //登录人数变化
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForUserID.ReleaseMutex();
        }

        /// <summary>
        /// 根据UserID获得登录的PlayerConnection
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public PlayerConnection GetPlayerLoginedByUserID(int userID)
        {
            PlayerConnection ret = null;
            mutexForUserID.WaitOne(1000);
            try
            {
                if (playerConnectionDictByUserID.ContainsKey(userID))
                {
                    ret = playerConnectionDictByUserID[userID];
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            mutexForUserID.ReleaseMutex();
            return ret;
        }
    }
}
