using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Base64Encoder
{
    class Program
    {
        static void Main(string[] args)
        {
            FileStream fs = new FileStream(@"E:\tomcat-8.0.22\webapps\KGame\res\title.png",FileMode.Open,FileAccess.Read);

            byte[] content = new byte[fs.Length];
            fs.Read(content, 0, (int)fs.Length);
            fs.Close();

            fs = new FileStream("C:\\aaa.txt", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);
            sw.Write(Convert.ToBase64String(content));
            sw.Close();
            fs.Close();

          

        }
    }
}
