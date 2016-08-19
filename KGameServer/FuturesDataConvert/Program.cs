using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace FuturesDataConvert
{
    class Program
    {
        static string[] zj = {"IC","IF","IH"};
        static string[] sq = { "沪胶", "沪金", "沪铝", "沪镍", "沪铅", "沪铜", "沪锡", "沪锌", "沪银", "沥青", "螺纹", "燃油", "热卷", "线材" };
        static string[] ds = { "PP", "PVC", "豆一", "豆二", "淀粉", "豆粕", "豆油", "鸡蛋", "胶板", "焦煤", "焦炭", "塑料", "铁矿", "纤板", "玉米", "棕榈" };
        static string[] zs = { "PTA", "白糖", "玻璃", "菜粕", "菜籽", "硅铁", "粳稻", "锰硅", "晚稻", "早稻", "郑醇", "郑麦", "郑煤", "郑棉", "郑油", "普麦" };

        static string content = "";
        static FileStream fs2;
        static FileStream fs3;
        static StreamWriter sw;
        static void Main(string[] args)
        {
            fs2 = new FileStream("E:\\QHData.sql", FileMode.Create, FileAccess.Write);
            fs3 = new FileStream("E:\\coedeinfo.txt", FileMode.Create, FileAccess.Write);
            sw = new StreamWriter(fs3);

            foreach (string file in Directory.GetFiles(@"C:\Users\dh\Desktop\指数\"))
            {
                ParseFile(file.Trim());
            }

            foreach (string file in Directory.GetFiles(@"C:\Users\dh\Desktop\主连\"))
            {
                ParseFile(file.Trim());
            }

            fs2.Close();
            sw.Close();
            fs3.Close();
        }

        static void ParseFile(string file)
        {
            string rawFilename = file.Substring(file.LastIndexOf('\\')+1);
            rawFilename = rawFilename.Replace(".csv", "").Trim();
            string codename = rawFilename.Remove(rawFilename.Length - 2);
            string exchange = "";
            if (zj.Contains<string>(codename))
            {
                exchange = "CCFX";
            }
            else if (sq.Contains<string>(codename))
            {
                exchange = "XSGE";
            }
            else if (ds.Contains<string>(codename))
            {
                exchange = "XDCE";
            }
            else if (zs.Contains<string>(codename))
            {
                exchange = "XZCE";
            }
            if (exchange == "")
            {
                Console.Write("a");
            }
            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            string line = sr.ReadLine();
            int count = 0;
            while (line != null)
            {
                string[] fields = line.Split(',');
                string[] time = fields[0].Split(' ');
                if (time.Length != 2)
                {
                    line = sr.ReadLine();
                    continue;
                }
                content = rawFilename + "|";
                content += exchange + "|";
                content += time[0].Split('/')[0] + time[0].Split('/')[1] + time[0].Split('/')[2] + "|";
                content += fields[1] + "|";
                content += fields[2] + "|";
                content += fields[3] + "|";
                content += fields[4] + "|";
                content += fields[5] + "|";
                content += fields[6] + "\n";
                line = sr.ReadLine();
                count++;

                byte[] contentBytes = Encoding.UTF8.GetBytes(content);
                fs2.Write(contentBytes, 0, contentBytes.Length);

            }

            sr.Close();
            fs.Close();

           

            string countContent = rawFilename + "\t\t" + exchange + "\t" + count.ToString();
            sw.WriteLine(countContent);

            Console.WriteLine(file + "处理结束");
            
        }
    }
}
