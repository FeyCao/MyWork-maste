using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Xml;
using System.Text.RegularExpressions;

namespace KGameServer
{
    public class Util
    {
        public static void PrintStackTrace()
        {
            StackTrace st = new StackTrace(true);
            for (int i = 1; i < st.FrameCount; i++)
            {
                StackFrame sf = st.GetFrame(i);
                System.Reflection.MethodBase method = sf.GetMethod();
                string lastcallmethod = method.DeclaringType.ToString() + "." + method.Name;
                Console.WriteLine("lastCallMethod:" + i + "=" + lastcallmethod);
            }
        }

        private static DateTime? lastDateTime;

        private static object changeFileLockObject = new object();

        private static void ChangeToNewLogFile()
        {
            lock (changeFileLockObject)
            {
                try
                {
                    if (lastDateTime != null)
                    {
                        //Util.LogInner("DateTime.Now.Date=" + DateTime.Now.Date.ToString());
                        //Util.LogInner("lastDateTime=" + lastDateTime.ToString());

                        if (DateTime.Now.Date != lastDateTime.Value.Date)
                        {
                            //需要更换一个新的日志
                            Util.LogInner("需要换一个新的日志");
                            string logPath = AppDomain.CurrentDomain.BaseDirectory + "log";
                            if (Directory.Exists(logPath) == false)
                            {
                                Directory.CreateDirectory(logPath);
                            }
                            string file = logPath + "\\log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                            if (File.Exists(file) == false)
                            {
                                Util.LogInner("重新建立日志文件:" + file);
                                FileStream fs = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite);
                                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Default);
                                sw.AutoFlush = true;
                                Console.SetOut(sw);
                            }
                            else
                            {
                                Util.LogInner("日志文件已经存在:" + file);
                            }
                        }
                    }
                    lastDateTime = DateTime.Now;
                    //Util.LogInner("设置后 lastDateTime=" + lastDateTime.ToString());
                }
                catch (Exception ex)
                {
                    Util.LogExceptionInner(ex);
                }
            }
        }


        private static void LogInner(String msg)
        {
            StackTrace st = new StackTrace(true);
            StackFrame sf = st.GetFrame(1);
            System.Reflection.MethodBase method = sf.GetMethod();
            string lastcallmethod = method.DeclaringType.ToString() + "." + method.Name;

            string dateTime = DateTime.Now.ToString("yyyyMMdd HH:mm:ss.fff");
            string currentThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
            try
            {
                Console.WriteLine("[" + dateTime + "]" + "[" + currentThreadID + "][" + System.Threading.Thread.CurrentThread.Priority.ToString() + "][" + lastcallmethod + "]\t" + msg);
                Console.WriteLine("");

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void LogExceptionInner(Exception ex)
        {
            if (ex != null)
            {
                Util.LogInner(ex.Message);
                Util.LogInner(ex.StackTrace);
            }
        }

        public static void Log(String msg)
        {
            ChangeToNewLogFile();

            StackTrace st = new StackTrace(true);
            StackFrame sf = st.GetFrame(1);
            System.Reflection.MethodBase method = sf.GetMethod();
            string lastcallmethod = method.DeclaringType.ToString() + "." + method.Name;

            string dateTime = DateTime.Now.ToString("yyyyMMdd HH:mm:ss.fff");
            string currentThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
            try
            {
                Console.WriteLine("[" + dateTime + "]" + "[" + currentThreadID + "][" + System.Threading.Thread.CurrentThread.Priority.ToString() + "][" + lastcallmethod + "]\t" + msg);
                Console.WriteLine("");

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void LogException(Exception ex)
        {
            if (ex != null)
            {
                Util.Log(ex.Message);
                Util.Log(ex.StackTrace);
            }
        }

        ///   <summary>
        ///   给一个字符串进行MD5加密
        ///   </summary>
        ///   <param   name="strText">待加密字符串</param>
        ///   <returns>加密后的字符串</returns>
        public static string MD5Encrypt(string strText)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] result = md5.ComputeHash(System.Text.Encoding.Default.GetBytes(strText));
            string ret = System.BitConverter.ToString(result);
            return ret.Replace("-","");
        }
    }
}
