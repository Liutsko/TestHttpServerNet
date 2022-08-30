using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace TestHttpServerNet
{
    public class GenLogic
    {
        public static object lockerConsole = new object();
        public static bool GetFromYml(string strPathYml)
        {
            try
            {
                if (!File.Exists(strPathYml))
                {
                    WriteToConsole("Нет файла:" + strPathYml);
                    return false;
                }
                string[] lines = File.ReadAllLines(strPathYml);
                lock (lockerConsole)
                {
                    foreach (string line in lines)
                    {
                        int pos = line.LastIndexOf("files_dir:");
                        if (-1 != pos)
                        {
                            string strWorkDir = line.Substring(pos + "files_dir:".Length).Trim();
                            if ('\"' == strWorkDir[0] && '\"' == strWorkDir[strWorkDir.Length - 1])
                                strWorkDir = strWorkDir.Substring(1, strWorkDir.Length - 2);
                            YmlSetting.strWorkDir = strWorkDir;
                            Directory.CreateDirectory(YmlSetting.strWorkDir);
                        }
                        pos = line.LastIndexOf("max_size:");
                        if (-1 != pos)
                            YmlSetting.nMaxSize = Convert.ToInt64(line.Substring(pos + "max_size:".Length).Trim());
                    }
                }
                GenLogic.WriteToConsole("Используем настройки files_dir: " + YmlSetting.strWorkDir + ", max_size: " + YmlSetting.nMaxSize.ToString());
                return true;
            }
            catch (Exception ex)
            {
                WriteToConsole("Ошибка:" + ex.Message);
                return false;
            }
        }
        public static void WriteToConsole(string strVal)
        {
            lock (lockerConsole)
            {
                Console.WriteLine(strVal);
            }
        }
    }
    static public class YmlSetting
    {
        static public string strWorkDir = "";
        static public long nMaxSize = -1;
    }
}
