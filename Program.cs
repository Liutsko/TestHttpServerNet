using System;
using System.Threading;
using System.IO;

namespace TestHttpServerNet
{
    class Program
    {
        static void Main(string[] args)
        {
            string strPathYml = "";
            if (args.GetLength(0) > 0)
            {
                if(File.Exists(args[0]))
                    strPathYml = args[0];
            }
            if ("" == strPathYml)
            {
                strPathYml = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + @"\config.yml";
                GenLogic.WriteToConsole(@"не передан путь к yml файлу, используем по умолчанию .\config.yml");
            }
            if (!GenLogic.GetFromYml(strPathYml))
                return; 

            HttpServer httpServer = new HttpServer(8081, strPathYml);
            new Thread(new ThreadStart(httpServer.Listen)).Start();
            return;
        }
    }
}
