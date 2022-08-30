using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO.Compression;

namespace TestHttpServerNet
{
    public class HttpServer
    {
        private int _nPort;
        private TcpListener _tcpListener; //можно также использовать HttpListener
        public static Dictionary<string, byte[]> dicCachePathBody = new Dictionary<string, byte[]>();
        public static object lockerCache = new object();
        FileSystemWatcher _folderWatcher = new FileSystemWatcher();
        FileSystemWatcher _ymlWatcher = new FileSystemWatcher();
        public string _strPathToYml;

        public HttpServer(int port, string strPathYml) {
            _nPort = port;
            _strPathToYml = strPathYml;
        }

        public void Listen()
        {
            _StartFolderWatcher();
            _StartYmlWatcher();
            _FillCache();

            _tcpListener = new TcpListener(IPAddress.Any, _nPort);
            _tcpListener.Start();
            while (true)
            {
                TcpClient obj = _tcpListener.AcceptTcpClient();
                HttpProcessor httpProc = new HttpProcessor(obj);
                new Thread(new ThreadStart(httpProc.Process)).Start();
                Thread.Sleep(1);
            }
        }
        
        private void _StartYmlWatcher()
        {
            _ymlWatcher.Path = Path.GetDirectoryName(_strPathToYml);
            _ymlWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            _ymlWatcher.Filter = Path.GetFileName(_strPathToYml);
            _ymlWatcher.Changed += new FileSystemEventHandler(OnChangedYml);
            _ymlWatcher.EnableRaisingEvents = true;
        }
        private void _StartFolderWatcher() {
            _folderWatcher.Path = YmlSetting.strWorkDir;
            _folderWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            _folderWatcher.Filter = "*.zip";
            _folderWatcher.Changed += new FileSystemEventHandler(OnChangedInFolder);
            _folderWatcher.Created += new FileSystemEventHandler(OnChangedInFolder);
            _folderWatcher.Deleted += new FileSystemEventHandler(OnChangedInFolder);
            _folderWatcher.Renamed += new RenamedEventHandler(OnRenamedInFolder);
            _folderWatcher.EnableRaisingEvents = true;
        }        
        private void OnChangedYml(object source, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            if (!GenLogic.GetFromYml(_strPathToYml))
                return;
            _FillCache();
        }

        private void OnChangedInFolder(object source, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            string strPathUnzip = path.Substring(0, path.Length - 4);
            lock (lockerCache) {
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    dicCachePathBody.Remove(strPathUnzip);
                    return;
                }
                if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    dicCachePathBody[strPathUnzip] = _Decompress(File.ReadAllBytes(e.FullPath));
                    return;
                }
            }
        }
        private void OnRenamedInFolder(object source, RenamedEventArgs e)
        {
            string path = e.OldFullPath;
            string strPathUnzip = path.Substring(0, path.Length - 4);
            lock (lockerCache)
            {
                dicCachePathBody.Remove(strPathUnzip);
                path = e.FullPath;
                strPathUnzip = path.Substring(0, path.Length - 4);
                dicCachePathBody[strPathUnzip] = _Decompress(File.ReadAllBytes(e.FullPath));
            }
        }
        private void _FillCache() {
            try
            {
                lock (lockerCache)
                {
                    dicCachePathBody.Clear();
                    string[] paths = Directory.GetFiles(YmlSetting.strWorkDir, "*.zip");
                    foreach (string path in paths)
                    {
                        string strPathUnzip = path.Substring(0, path.Length - 4);
                        dicCachePathBody[strPathUnzip] = _Decompress(File.ReadAllBytes(path));
                    }
                }
            }
            catch (Exception ex) {
                GenLogic.WriteToConsole("Ошибка:" + ex.Message);
            }
        }
        private byte[] _Decompress(byte[] compressed)
        {
            try
            {
                using var from = new MemoryStream(compressed);
                using var to = new MemoryStream();
                using var gZipStream = new GZipStream(from, CompressionMode.Decompress);
                gZipStream.CopyTo(to);
                return to.ToArray();                
            }
            catch (Exception ex)
            {
                GenLogic.WriteToConsole("Ошибка:" + ex.Message);
                return null;
            }
        }
    }
}
