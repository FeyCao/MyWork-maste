using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.IO;
using System.Xml;
using System.Threading;
using System.Data;

namespace KGameServer
{
    public class DBManager
    {
        private static string usrSql = "";
        private static string bizSql = "";

        private static Random matchFlagRandom;
        private static Mutex randomMutex;

        public static readonly string SOURCE_DHJK = "DHJK";     //东航金控APP
        public static readonly string SOURCE_ZSQQ = "ZSQQ";     //掌上全球APP
        public static readonly string SOURCE_ZZFW = "ZZFW";     //增值服务中心网站
        
        public static readonly string SOURCE_TEST = "TEST";     //东航金控APP
        public static int userIdForTest = 1;

        public static readonly char[] randomcharPrefix = new char[] { 'c', 'v', 'r', 's', 't' };
        public static readonly char[] randomcharPostfix =new char[]{ 'a', 'e', 'u' };


        /*private class DBManagerRequest
        {
            public OnReceiveBusinessDataCallBack onreceiveBusinessDataCallBack;
            public delegate void OnReceiveBusinessDataCallBack(List<int> businessDataList);
            public DBManagerRequest(OnReceiveBusinessDataCallBack aOnReceiveBusinessDataCallBack)
            {
                onreceiveBusinessDataCallBack = aOnReceiveBusinessDataCallBack;
            }

        }*/

        

        public static void Init()
        {
            matchFlagRandom = new Random((int)DateTime.Now.Ticks);
            randomMutex = new Mutex();
            GetConnectString();
        }

        private static int GetRandomForMatchFlag()
        {
            int ret;
            randomMutex.WaitOne();
            ret = matchFlagRandom.Next(10000000, 100000000);
            randomMutex.ReleaseMutex();
            return ret;
        }

        private static string GetConnectString()
        {
            string userFile = AppDomain.CurrentDomain.BaseDirectory + "setting//ServerInfo.xml";
            if (File.Exists(userFile) == true)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(userFile);
                XmlNodeList dbList = doc.SelectNodes("/config/usrdbconnect");
                if (dbList != null)
                {
                    foreach (XmlElement node in dbList)
                    {
                        if (node.HasAttribute("connectionstring"))
                        {
                            usrSql=node.Attributes["connectionstring"].Value;
                        }
                    }
                }


                dbList = doc.SelectNodes("/config/bizdbconnect");
                if (dbList != null)
                {
                    foreach (XmlElement node in dbList)
                    {
                        if (node.HasAttribute("connectionstring"))
                        {
                            bizSql = node.Attributes["connectionstring"].Value;
                        }
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// 验证用户
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static int ValidateUser(string username,string password,string source,out string errMsg)
        {
            Util.Log("ValidateUser username=" + username + " password=" + password + " source=" + source);
            if(source==SOURCE_DHJK)
            {
                //来自东航金控APP的登录
                return ValidateUserFromSource_DHJK(username, password, out errMsg);
            }
            else if (source == SOURCE_TEST)
            {
                errMsg = null;
                return userIdForTest++;
            }

            return ValidateUserFromSource_Self(username, password, out errMsg);

        }
        private static int ValidateUserFromSource_Self(string username, string password, out string errMsg)
        {
            errMsg = null;
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                errMsg = "请稍候尝试!";
                return -2;
            }

            try
            {
                MySqlCommand command = mySqlConnection.CreateCommand();
                command.CommandText = "SELECT id FROM kgame.tbl_user where UserName=?UserName and PWD=?Password and ISNULL(SOURCE)";
                command.Parameters.AddWithValue("UserName", username);
                command.Parameters.AddWithValue("Password", Util.MD5Encrypt(password));
                Util.Log("sql:" + command.CommandText);
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    
                    int userID = int.Parse(o.ToString());
                    Util.Log("userid=" + userID);
                    return userID;
                }
                else
                {
                    errMsg = "用户名或密码错误!";
                    return -1;
                }
            }
            catch (Exception ex)
            {
                errMsg = "请稍候尝试!";
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            }
            return -1;
        }

        /// <summary>
        /// databaseTag表示数据库的类型
        /// 0表示bizSql
        /// 1表示usrSql
        /// </summary>
        /// <param name="databaseTag"></param>
        /// <returns></returns>
        private static MySqlConnection OpenMySqlConnection(int databaseTag)
        {
            string sql = "";
            if(databaseTag==0)
            {
                sql = bizSql;
            }
            else if(databaseTag==1)
            {
                sql = usrSql;
            }

            MySqlConnection mySqlConnection = new MySqlConnection(sql);
            try
            {
                mySqlConnection.Open();
                return mySqlConnection;
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            return null;
        }


        private static int ValidateUserFromSource_DHJK(string username, string password, out string errMsg)
        {
            errMsg = null;
            MySqlConnection mySqlConnection = OpenMySqlConnection(1);
            if(mySqlConnection==null)
            {
                errMsg = "请稍候尝试!";
                return -2;
            }

            try
            {
                MySqlCommand command = mySqlConnection.CreateCommand();
                command.CommandText = "SELECT uid FROM service.t_member where UserName=?UserName and password=?Password";
                command.Parameters.AddWithValue("UserName", username);
                command.Parameters.AddWithValue("Password", password);
                Util.Log("sql:" + command.CommandText);
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    int userID = int.Parse(o.ToString());
                    Util.Log("userid=" + userID);
                    //需要将该用户从service.t_member插入到kgame.tbl_user中去
                    AddUserInfoFromOtherSourceToLocalSource(username, userID, SOURCE_DHJK);
                    return userID;
                }
                else
                {
                    errMsg = "用户名或密码错误!";
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
                errMsg = "请稍候尝试!";
            }
            finally
            {
                mySqlConnection.Close();
            }
            return -1;
        }

        /// <summary>
        /// 将其他数据库中登录的用户添加到本地的数据库中，例如将东航金控APP中的用户添加到本地的数据库中，以便以后统计时方便
        /// </summary>
        /// <param name="username"></param>
        /// <param name="fid"></param>
        /// <param name="source"></param>
        private static void AddUserInfoFromOtherSourceToLocalSource(string username,int fid,string source)
        {
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null) return;

            MySqlCommand command = null;
            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "SELECT id FROM kgame.tbl_user where username=?UserName and fid=?fid and source='"+SOURCE_DHJK+"'";
                command.Parameters.AddWithValue("UserName", username);
                command.Parameters.AddWithValue("fid", fid);
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    return;
                }
                command = mySqlConnection.CreateCommand();
                command.CommandText = "INSERT INTO kgame.tbl_user (username,FID,GUEST,SOURCE) values (?UserName,?fid,0,'" + SOURCE_DHJK + "')";
                command.Parameters.AddWithValue("UserName", username);
                command.Parameters.AddWithValue("fid", fid);
                o = command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            }
            return;
        }

        public static string  QucikLogin(ref string usernameOut, ref string passwordOut)
        {
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                return "请稍候尝试!";
            }

            MySqlCommand command = null;
            int totalCount=0;
            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "SELECT count(*) FROM kgame.tbl_user";
                object o = command.ExecuteScalar();
                totalCount=int.Parse(o.ToString());
            }
            catch (Exception ex)
            {
                mySqlConnection.Close();
                Util.LogException(ex);
                return "请稍候再尝试。";
            }

            Random r = new Random((int)DateTime.Now.Ticks);
            totalCount = totalCount + 100;
            usernameOut = randomcharPrefix[r.Next(5)] + totalCount.ToString() + randomcharPostfix[r.Next(3)];
            passwordOut = r.Next(100000, 1000000).ToString();

            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "INSERT INTO kgame.tbl_user (USERNAME,PWD,GUEST) values (?USERNAME,?PWD,false)";
                command.Parameters.AddWithValue("USERNAME", usernameOut);
                string md5Pwd = Util.MD5Encrypt(passwordOut);
                command.Parameters.AddWithValue("PWD", md5Pwd);
                object res = command.ExecuteScalar();
                if (res != null)
                {
                    return "请稍候再尝试。";
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
                return "请稍候再尝试。";
            }
            finally
            {
                mySqlConnection.Close();
            }
            return null;
        }


        /// <summary>
        /// 验证分享
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="matchID"></param>
        /// <returns></returns>
        public static int ValidateShare(string userID, string matchID, out string errMsg)
        {
            Util.Log("ValidateShare userID=" + userID + " matchID=" + matchID);
            errMsg = null;
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                errMsg = "请稍候尝试!";
                return -2;
            }
            MySqlCommand command = null;
            MySqlDataAdapter adapter = null;
            DataSet ds = null;
        
            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "select * from kgame.tbl_match where ID =" + userID;

                adapter = new MySqlDataAdapter(command);
                ds = new DataSet();
                ds.Tables.Clear();
                adapter.Fill(ds);
                if (ds == null)
                {

                    errMsg = "数据错误!";
                    return -1;

                }
                else
                {
                    return 1;
                } 
          
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
                errMsg = "数据错误!";
                return -1;
            }
            finally
            {
                mySqlConnection.Close();
            }
            //return -1;

        }
  

        /// <summary>
        /// 注册用户
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static String RegisterUser(string username,string password)
        {
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                return "请稍候尝试!";
            }

            MySqlCommand command = null;
            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "SELECT * FROM kgame.tbl_user where username=?username and ISNULL(SOURCE)";
                command.Parameters.AddWithValue("username", username);
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    mySqlConnection.Close();
                    return "用户名已经存在!";
                }
            }
            catch (Exception ex)
            {
                mySqlConnection.Close();
                Util.LogException(ex);
                return "请稍候再尝试。";
            }

            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "INSERT INTO kgame.tbl_user (USERNAME,PWD,GUEST) values (?USERNAME,?PWD,false)";
                command.Parameters.AddWithValue("USERNAME", username);
                string md5Pwd = Util.MD5Encrypt(password);
                command.Parameters.AddWithValue("PWD", md5Pwd);
                object res = command.ExecuteScalar();
                if (res != null)
                {
                    return "请稍候再尝试。";
                }
            }
            catch(Exception ex)
            {
                Util.LogException(ex);
                return "请稍候再尝试。";
            }
            finally
            {
                mySqlConnection.Close();
            }
            


            return null;
        }

        /// <summary>
        /// 获得某个用户的UID
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static int GetUserID(string username)
        {
            MySqlConnection mySqlConnection = OpenMySqlConnection(1);
            if (mySqlConnection == null)
            {
                return -2;
            }

            try
            {
                MySqlCommand command = mySqlConnection.CreateCommand();
                command.CommandText = "SELECT uid FROM service.t_member where nickname=?UserName";
                command.Parameters.AddWithValue("UserName", username);
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    int userID = int.Parse(o.ToString());

                    return userID;
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            }
            return -1;
        }
        /// <summary>
        /// 获得某次战斗的数据信息
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static matchInfo GetMatchID(int matchID)
        {
            matchInfo p = null;
            MySqlCommand command = null;
            MySqlDataAdapter adapter = null;
            DataSet ds = null;
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                return null;
            }

            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "select * from kgame.tbl_match where ID =" + matchID;
                
                adapter = new MySqlDataAdapter(command);
                ds = new DataSet();
                ds.Tables.Clear();
                adapter.Fill(ds);
                if (ds!= null)
                {
                    string aCode = ds.Tables[0].Rows[0]["CODE"].ToString();
                    string aExchange = ds.Tables[0].Rows[0]["EXCHANGE"].ToString();
                    DateTime aStartTime = (DateTime)ds.Tables[0].Rows[0]["STARTDATE"]; ;
                    int dayCount = (int)ds.Tables[0].Rows[0]["DAYCOUNT"];
                    p = new matchInfo(aCode, aExchange, aStartTime, dayCount);
                    return p;
                   
                }else
                {
                    Util.Log("数据未取到,需要再取一次数据");
                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            }
            return null;
        }


        public static int AddNewMatchInfo(int matchType,CodeInfo codeInfo,DateTime startDate,int dayCount)
        {
            int matchID = -1;
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                return matchID;
            }

            try
            {
                MySqlCommand command = mySqlConnection.CreateCommand();
                command.CommandText = "insert into kgame.tbl_match (DATETIME,TYPE,CODE,EXCHANGE,STARTDATE,DAYCOUNT,FLAG) values (";
                string dateTimeNow=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                command.CommandText += "'"+dateTimeNow+"',";
                command.CommandText += matchType+",";
                command.CommandText += "'" + codeInfo.code + "',";
                command.CommandText += "'" + codeInfo.exchange + "',";
                command.CommandText += "'" + startDate.ToString("yyyy-MM-dd") + "',";
                command.CommandText += dayCount+",";
                int matchRandomFlag=GetRandomForMatchFlag();
                command.CommandText += matchRandomFlag + ")";

                command.ExecuteScalar();
                command.CommandText = "select ID from kgame.tbl_match where flag=" + matchRandomFlag + " and DATETIME='" + dateTimeNow + "'";
                matchID = (int)command.ExecuteScalar();
                Util.Log("New Match added to database match id=" + matchID);
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            }
            return matchID;
        }
        /// <summary>
        /// 保存用户交易记录
        /// </summary>
        ///  <param name="ID"></param>'主键MATCHID*10000+USERID
        /// <param name="USER_ID"></param>'玩家的ID
        /// <param name="MATCH_ID"></param> '对决的ID
        /// <param name="TRADEDAYINDEX"></param>'交易日的信息，从起始日期开始的第几天，最小为0',
        /// <param name="BUYSELL"></param>'买卖方向，0为买入，1为卖出'
        /// <param name="PRICE"></param>'买卖价格
        /// <returns></returns>
        ///  public void ProcessUserOperation(PlayerConnection playerConnection, int index, bool isBuy)
        public static int AddNewTraderecordInfo(PlayerConnection playerConnection,int matchID, int index, bool isBuy, double price)
        {
            int traderID = -1;
            
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                return traderID;
            }

            try
            {
                MySqlCommand command = mySqlConnection.CreateCommand();
                command.CommandText = "insert into kgame.tbl_traderecord (USER_ID,MATCH_ID,TRADEDAYINDEX,BUYSELL,PRICE) values (";
                command.CommandText += playerConnection.UserId + ",";
                command.CommandText += matchID + ",";
                command.CommandText += index + ",";
                command.CommandText += isBuy + ",";
                command.CommandText += price + ")";

                command.ExecuteScalar();
                command.CommandText = "select ID from kgame.tbl_traderecrod where USER_ID=" + playerConnection.UserId + " and MATCH_ID='" + matchID + "'";
                traderID = (int)command.ExecuteScalar();
                Util.Log("New Match added to database match id=" + traderID);
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            };

            return traderID;
        }


        // /// <summary>
        /// 获取交易数据
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="matchID"></param>
        
        public static List<int> GetBusinessData(int userID,int matchID)
        {
            List<int> businessDataList = null;
            MySqlCommand command = null;
            MySqlDataAdapter adapter = null;
            DataSet ds = null;
            MySqlConnection mySqlConnection = OpenMySqlConnection(0);
            if (mySqlConnection == null)
            {
                return null;
            }

            try
            {
                command = mySqlConnection.CreateCommand();
                command.CommandText = "select * from kgame.tbl_traderecord where USER_ID =" + userID + " and MATCH_ID =" + matchID + " order by ID asc;";
                
                adapter = new MySqlDataAdapter(command);
                ds = new DataSet();
                ds.Tables.Clear();
                adapter.Fill(ds);

                businessDataList = new List<int>();//历史数据，按照时间倒序排列

                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    if ((int)row["BUYSELL"]==1)
                    {

                        businessDataList.Add((int)row["TRADEDAYINDEX"]);
                    }
                    else
                    {
                        businessDataList.Add(-(int)row["TRADEDAYINDEX"]);
                    }

                }
            }
            catch (Exception ex)
            {
                Util.LogException(ex);
            }
            finally
            {
                mySqlConnection.Close();
            }
            return businessDataList;
        }
    }

}
