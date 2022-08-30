using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HttpMultipartParser;
using System.Text.Json;
using System.Text;

namespace TestHttpServerNet
{
    public class HttpProcessor
    {
        private TcpClient   _tcpClient;
        private Stream      _inputStream;
        private Stream      _outputStream;
        private Dictionary<string, string> _dicHttpHeaders = new Dictionary<string, string>();
        private partial class JsonRequest
        {
            public string filename { get; set; }
        }
        public HttpProcessor(TcpClient s)
        {
            _tcpClient = s;
        }
        public void Process()
        {                       
            try
            {
                _inputStream = new BufferedStream(_tcpClient.GetStream());
                _outputStream = new BufferedStream(_tcpClient.GetStream());

                string strReqType = _GetRequestType();
                if (_ReadHeader())
                {
                    if (strReqType.Equals("GET"))
                        _OnGetRequest();
                    else if (strReqType.Equals("POST"))
                        _OnPostRequest();
                }
            }
            catch (Exception e)
            {
                GenLogic.WriteToConsole("Ошибка: " + e.ToString());
                _WriteError(404);
            }
            _outputStream.Flush();
            _inputStream = null; 
            _outputStream = null;
            _tcpClient.Close();
        }
        private string _ReadLineFromClient()
        {
            int nChar = -1;
            StringBuilder str = new StringBuilder("");
            
            while (true)
            {
                nChar = _inputStream.ReadByte();
                if (nChar == '\n') { break; }
                if (nChar == '\r') { continue; }
                if (nChar == -1) { Thread.Sleep(1); continue; };
                str.Append(Convert.ToChar(nChar));
            }
            return str.ToString();
        }

        private string _GetRequestType()
        {
            string request = _ReadLineFromClient();
            string[] items = request.Split(' ');
            if (items.Length != 3)
            {
                GenLogic.WriteToConsole("Ошибка в http запросе: " + request);
                return "";
            }
            return items[0].ToUpper();
        }

        private bool _ReadHeader()
        {
            string line;
            while ((line = _ReadLineFromClient()) != null)
            {
                if (line.Equals(""))
                    return true;

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    GenLogic.WriteToConsole("Ошибка в http заголовке: " + line);
                    return false;
                }
                string name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                    pos++;

                string value = line.Substring(pos, line.Length - pos);
                _dicHttpHeaders[name] = value;
            }
            return true;
        }

        private void _WriteSuccess()
        {
            StringBuilder str = new StringBuilder("");            
            str.AppendLine("HTTP/1.0 200 OK");
            str.AppendLine("Content-Type: text/html");
            str.AppendLine("Connection: close");
            str.AppendLine();

            Encoding encoding = Encoding.UTF8;
            byte[] bytesHeader = encoding.GetBytes(str.ToString());
            _outputStream.Write(bytesHeader, 0, bytesHeader.Length);
        }

        private void _WriteError(int nErr)
        {
            StringBuilder str = new StringBuilder("");
            if (404 == nErr)
            {
                str.AppendLine("HTTP/1.0 404 File not found");
                str.AppendLine("Connection: close");
                str.AppendLine();
            }
            else if (409 == nErr)
            {
                str.AppendLine("HTTP/1.0 409 File exist");
                str.AppendLine("Connection: close");
                str.AppendLine();
            }
            else if (413 == nErr)
            {
                str.AppendLine("HTTP/1.0 413 File to large");
                str.AppendLine("Connection: close");
                str.AppendLine();
            }
            if (str.Length > 0)
            {
                Encoding encoding = Encoding.UTF8;
                byte[] bytesHeader = encoding.GetBytes(str.ToString());
                _outputStream.Write(bytesHeader, 0, bytesHeader.Length);
            }
        }
        private bool _CheckInputParams() {
            try
            {
                Directory.CreateDirectory(YmlSetting.strWorkDir);
            }
            catch (Exception) { }

            if (!Directory.Exists(YmlSetting.strWorkDir))
            {
                GenLogic.WriteToConsole("Ошибка, отсутствует папка:" + YmlSetting.strWorkDir);
                _WriteError(404);
                return false;
            }

            int nContentLen = 0;
            if (_dicHttpHeaders.ContainsKey("Content-Length"))
                nContentLen = Convert.ToInt32(_dicHttpHeaders["Content-Length"]);

            if (nContentLen > YmlSetting.nMaxSize)
            {
                _WriteError(413);
                return false;
            }
            return true;
        }
        private async void _CompressAsync(MemoryStream memory, string strDest)
        {
            GZipStream gza = null;
            try
            {
                if (File.Exists(strDest))
                    File.Delete(strDest);

                FileStream destFile = File.Create(strDest);
                gza = new GZipStream(destFile, CompressionMode.Compress);
                await gza.WriteAsync(memory.ToArray());
                gza.Close();
            }
            catch (Exception)
            {
                if (null != gza)
                    gza.Close();
                //GenLogic.WriteToConsole("Не могу заархивировать файл");
                return;
            }
            return;
        }
     
        private void _WriteFileToClient(byte[] bytesData)
        {
            if (null == bytesData)
                return;
            try
            {
                string strBoundary = string.Format("----------{0:N}", Guid.NewGuid());

                StringBuilder str = new StringBuilder("");
                str.AppendLine("HTTP/1.1 200 OK");
                str.AppendLine("Content-Type: multipart/form-data; boundary=");
                str.Append(strBoundary);
                str.AppendLine("Content-Length: " + bytesData.Length);
                str.AppendLine("");
                Encoding encoding = Encoding.UTF8;
                byte[] bytesHeader = encoding.GetBytes(str.ToString());

                _outputStream.Write(bytesHeader, 0, bytesHeader.Length);
                _outputStream.Write(bytesData, 0, bytesData.Length);
                
            }
            catch (Exception ex) {
                GenLogic.WriteToConsole("Ошибка: " + ex.Message);
            }
        }
       
        private void _OnPostRequest()
        {
            if (!_dicHttpHeaders.ContainsKey("Content-Length"))
                return;
            int nContentLen = Convert.ToInt32(_dicHttpHeaders["Content-Length"]);
            MemoryStream ms = new MemoryStream();

            int BUF_SIZE = 4096;
            byte[] buf = new byte[BUF_SIZE];
            int nToRead = nContentLen;
            while (nToRead > 0)
            {
                int nReaded = _inputStream.Read(buf, 0, Math.Min(BUF_SIZE, nToRead));
                if (nReaded == 0)
                    break;
                nToRead -= nReaded;
                ms.Write(buf, 0, nReaded);
            }
            ms.Seek(0, SeekOrigin.Begin);

            if (!_CheckInputParams())
                return;

            MultipartFormDataParser multipartParser = MultipartFormDataParser.Parse(ms);
            foreach (FilePart fileInfo in multipartParser.Files)
            {
                if (File.Exists(YmlSetting.strWorkDir + @"\" + fileInfo.FileName + ".zip"))
                {
                    _WriteError(409);
                    return;
                }
                MemoryStream memory = new MemoryStream();                
                fileInfo.Data.CopyTo(memory);
                _CompressAsync(memory, YmlSetting.strWorkDir + @"\" + fileInfo.FileName + ".zip");
                _WriteSuccess();
                GenLogic.WriteToConsole("Сохранен файл:" + fileInfo.FileName + ".zip");
                break;
            }           
        }
        private void _OnGetRequest()
        {
            if (!_dicHttpHeaders.ContainsKey("Content-Length"))
                return;

            int nContentLen = Convert.ToInt32(_dicHttpHeaders["Content-Length"]);
            MemoryStream ms = new MemoryStream();

            int BUF_SIZE = 4096;
            byte[] buf = new byte[BUF_SIZE];
            int nToRead = nContentLen;
            while (nToRead > 0)
            {
                int nReaded = _inputStream.Read(buf, 0, Math.Min(BUF_SIZE, nToRead));
                if (nReaded == 0)
                    break;
                nToRead -= nReaded;
                ms.Write(buf, 0, nReaded);
            }
            ms.Seek(0, SeekOrigin.Begin);

            StreamReader sr = new StreamReader(ms);
            string myStr = sr.ReadToEnd();

            JsonRequest jr = null;
            try
            {
                jr = JsonSerializer.Deserialize<JsonRequest>(myStr);
            }
            catch (Exception ex) {
                GenLogic.WriteToConsole("Ошибка: в запросе, " + ex.Message);
                return;
            }
            string strFilePath = YmlSetting.strWorkDir + jr.filename + ".zip";
            string strFilePathUnZip = YmlSetting.strWorkDir + jr.filename;
            if (!File.Exists(strFilePath))
            {
                _WriteError(404);
                return;
            }

            byte[] btTo = null;
            lock (HttpServer.lockerCache)
            {
                    if (HttpServer.dicCachePathBody.ContainsKey(strFilePathUnZip))
                    {
                        byte[] btFrom = HttpServer.dicCachePathBody[strFilePathUnZip];
                        btTo = new byte[btFrom.Length];
                        Array.Copy(btFrom, btTo, btFrom.Length);
                    }
            }

            _WriteFileToClient(btTo);
        }
    }
}
