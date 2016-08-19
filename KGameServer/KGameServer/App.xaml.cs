using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace KGameServer
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            string logPath = AppDomain.CurrentDomain.BaseDirectory + "log";
            if (Directory.Exists(logPath) == false)
            {
                Directory.CreateDirectory(logPath);
            }
            FileStream fs = new FileStream(logPath + "\\log_" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".log", FileMode.CreateNew, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Default);
            sw.AutoFlush = true;
            Console.SetOut(sw);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Util.Log(e.ExceptionObject.ToString());
        }
    }
}
