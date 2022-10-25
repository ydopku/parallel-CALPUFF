using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Configuration;

namespace dispersion
{
    class RPCServer
    {
        public static void Main()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.ExchangeDeclare(exchange: "model_request", type: "direct");
                channel.QueueDeclare(queue: "parallel_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);
                channel.QueueBind(queue: "parallel_queue", exchange: "model_request", routingKey: "parallel");
                var consumer = new EventingBasicConsumer(channel);
                channel.BasicConsume(queue: "parallel_queue", autoAck: false, consumer: consumer);
                Console.WriteLine(" [x] Awaiting parallel calpuff requests");

                //method for fib
                /*
                consumer.Received += (model, ea) =>
                {
                    string response = null;

                    var body = ea.Body;
                    var props = ea.BasicProperties;
                    var replyProps = channel.CreateBasicProperties();
                    replyProps.CorrelationId = props.CorrelationId;

                    try
                    {
                        var message = Encoding.UTF8.GetString(body);
                        int n = int.Parse(message);
                        Console.WriteLine(" [.] fib({0})", message);
                        response = fib(n).ToString();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(" [.] " + e.Message);
                        response = "";
                    }
                    finally
                    {
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, basicProperties: replyProps, body: responseBytes);
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                };
                */

                //method for calpuff
                consumer.Received += (model, ea) =>
                {
                    string response = null;

                    var body = ea.Body;
                    var props = ea.BasicProperties;
                    var replyProps = channel.CreateBasicProperties();
                    replyProps.CorrelationId = props.CorrelationId;

                    try
                    {
                        var message = Encoding.UTF8.GetString(body);
                        if (message[0] != 'C')
                        {
                            string[] strs = message.Split(',');
                            Console.WriteLine(" [.] Calmet Parts:{0}", strs[0]);
                            response = GetIpAddress();
                            DateTime t1 = DateTime.Now;
                            int p = int.Parse(strs[0]);
                            int parts = int.Parse(strs[1]);
                            int division = int.Parse(strs[3]);
                            int buffer = int.Parse(strs[4]);
                            RunCalmetP(p, parts, division, buffer);
                            string path1 = "CALMET0" + p.ToString() + ".DAT";
                            string path2 = "CALMET0" + p.ToString() + ".zip";
                            ZipC(p, parts, division);
                            DateTime t2 = DateTime.Now;
                            TimeSpan ts = t2.Subtract(t1);
                            Console.WriteLine("Calmet Task {0} complete in {1} seconds", strs[0], ts.TotalSeconds);
                        }
                        else
                        {
                            string[] strs = message.Split(',');
                            Console.WriteLine(" [.] Calpuff Layer:{0}-{1}", strs[2], strs[3]);
                            response = GetIpAddress();
                            DateTime t1 = DateTime.Now;
                            string httpurl = "http://" + strs[1] + "/NewTaskP/" + "CALMET.zip";
                            string ftpurl = "ftp://" + strs[1] + "/NewTaskP/" + "CALMET.zip";
                            string result = "CALMET.zip";
                            //HttpDownloadFile(httpurl, result);
                            //FtpDownloadFile(ftpurl, result);
                            MultiThreadDownload(httpurl, result);
                            Console.WriteLine(" [.] Got '{0}' from {1}", result, response);
                            ZipFile.ExtractToDirectory("CALMET.zip", "../calpuffWorkerP", true);
                            //RunCalpuff();
                            int l = int.Parse(strs[2]);
                            int l2 = int.Parse(strs[3]);
                            int l3 = int.Parse(strs[4]);
                            RunCalpuffP(l, l2, l3, strs[5]);
                            Zip(strs[2], strs[3], strs[5]);
                            DateTime t2 = DateTime.Now;
                            TimeSpan ts = t2.Subtract(t1);
                            Console.WriteLine("Calpuff Task {0} complete in {1} seconds", strs[2], ts.TotalSeconds);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(" [.] " + e.Message);
                        response = "";
                    }
                    finally
                    {
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, basicProperties: replyProps, body: responseBytes);
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                };

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }

        /*
        /// <summary>
        /// Assumes only valid positive integer input.
        /// Don't expect this one to work for big numbers, and it's probably the slowest recursive implementation possible.
        /// </summary>
        private static int fib(int n)
        {
            if (n == 0 || n == 1)
            {
                return n;
            }

            if (n > 30)
            {
                Console.WriteLine("N must be less than 30. Overflow error.");
                return -1;
            }

            return fib(n - 1) + fib(n - 2);
        }
        */

        //run parallel calmet
        private static void RunCalmetP(int p, int parts, int division, int buffer)
        {
            RunCALPUFF newCompute;
            DateTime CurrentTime = new DateTime(2003, 12, 23, 0, 0, 0);
            double SourceX, SourceY, Elavation;
            //重庆高桥
            SourceX = 237.321;
            SourceY = 3474.793;
            Elavation = 606.4;
            //四川宣汉（川东北环评报告）
            /*SourceX = 779.834;
            SourceY = 3474.128;
            Elavation = 384.08;*/
            //重庆高桥
            newCompute = new dispersion.RunCALPUFF(SourceX, SourceY, Elavation, 49, CurrentTime, p, parts);
            //四川宣汉（川东北环评报告）
            //newCompute = new dispersion.RunCALPUFF(SourceX, SourceY, Elavation, 48, CurrentTime, p, parts);

            newCompute.DivideTask(division, buffer);
            if (division == 2)
            {
                newCompute.ParallelRunCALMETExe3(p, buffer);
            }
            else
            {
                newCompute.ParallelRunCALMETExe2(p);
            }
        }

        //run calpuff
        private static void RunCalpuff()
        {
            RunCALPUFF newCompute;
            DateTime CurrentTime = new DateTime(2003, 12, 23, 0, 0, 0);
            double SourceX, SourceY, Elavation;
            //重庆高桥
            SourceX = 237.321;
            SourceY = 3474.793;
            Elavation = 606.4;
            //四川宣汉（川东北环评报告）
            /*SourceX = 779.834;
            SourceY = 3474.128;
            Elavation = 384.08;*/
            //重庆高桥
            newCompute = new dispersion.RunCALPUFF(SourceX, SourceY, Elavation, 49, CurrentTime, 1, 1);
            //四川宣汉（川东北环评报告）
            //newCompute = new dispersion.RunCALPUFF(SourceX, SourceY, Elavation, 48, CurrentTime, 1, 1);
            newCompute.DivideTask(1, 0);
            newCompute.RunCALPUFFExe();
            newCompute.RunCALPOSTExe();
        }

        //run parallel calpuff
        private static void RunCalpuffP(int layer, int layerpart, int layerparts, string timestep)
        {
            RunCALPUFF newCompute;
            DateTime CurrentTime = new DateTime(2003, 12, 23, 0, 0, 0);
            double SourceX, SourceY, Elavation;
            //重庆高桥
            SourceX = 237.321;
            SourceY = 3474.793;
            Elavation = 606.4;
            //四川宣汉（川东北环评报告）
            /*SourceX = 779.834;
            SourceY = 3474.128;
            Elavation = 384.08;*/
            //重庆高桥
            newCompute = new dispersion.RunCALPUFF(SourceX, SourceY, Elavation, 49, CurrentTime, 1, 1);
            //四川宣汉（川东北环评报告）
            //newCompute = new dispersion.RunCALPUFF(SourceX, SourceY, Elavation, 48, CurrentTime, 1, 1);
            newCompute.LayerPart = layerpart;
            newCompute.LayerParts = layerparts;
            if (timestep == "s")
            {
                newCompute.PUFFTimeStep = 1;
            }
            else
            {
                newCompute.PUFFTimeStep = 60;
            }
            newCompute.DivideTask(1, 0);
            newCompute.ReadElevations();
            newCompute.UpdateElevation();
            newCompute.RunCALPUFF3D(layer * newCompute.LayerHeight);
            newCompute.RunCALPOST3D(layer);
        }

        //Generate zip
        private static void Zip0(string s)
        {
            string zipFilePath = "result" + s + ".zip";
            Console.WriteLine(zipFilePath);
            string path1 = "Layer" + s + "\\2003_M012_D23_0";
            string path2 = "(UTC+0800)_L00_H2S_1MIN_CONC.DAT";
            using (FileStream zipFileToOpen = new FileStream(zipFilePath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipFileToOpen, ZipArchiveMode.Create))
            {
                for (int i = 0; i < 60; i++)
                {
                    int n = 000 + i;
                    string path = path1 + n.ToString() + path2;
                    ZipArchiveEntry readMeEntry = archive.CreateEntry(path);
                    using (Stream stream = readMeEntry.Open())
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                for (int i = 0; i < 60; i++)
                {
                    int n = 100 + i;
                    string path = path1 + n.ToString() + path2;
                    ZipArchiveEntry readMeEntry = archive.CreateEntry(path);
                    using (Stream stream = readMeEntry.Open())
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                for (int i = 0; i < 60; i++)
                {
                    int n = 200 + i;
                    string path = path1 + n.ToString() + path2;
                    ZipArchiveEntry readMeEntry = archive.CreateEntry(path);
                    using (Stream stream = readMeEntry.Open())
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
            }
        }

        //Generate zip
        private static void Zip(string s2, string s3, string s5)
        {
            string zipFilePath = "result" + s2 + "-" + s3 + ".zip";
            Console.WriteLine(zipFilePath);
            string path1 = "Layer" + s2 + "\\TSERIES_H2S_1MIN_CONC_" + s2 + "-" + s3 + ".DAT";
            if (s5 == "s")
            {
                path1 = "Layer" + s2 + "\\TSERIES_H2S_1SEC_CONC_" + s2 + "-" + s3 + ".DAT";
            }
            string path2 = "Layer" + s2 + "\\RANK(ALL)_H2S_1MIN_CONC_" + s2 + "-" + s3 + ".DAT";
            if (s5 == "s")
            {
                path2 = "Layer" + s2 + "\\RANK(ALL)_H2S_1SEC_CONC_" + s2 + "-" + s3 + ".DAT";
            }
            using (FileStream zipFileToOpen = new FileStream(zipFilePath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipFileToOpen, ZipArchiveMode.Create))
            {
                ZipArchiveEntry readMeEntry = archive.CreateEntry(path1);
                using (Stream stream = readMeEntry.Open())
                {
                    byte[] bytes = File.ReadAllBytes(path1);
                    stream.Write(bytes, 0, bytes.Length);
                }
                readMeEntry = archive.CreateEntry(path2);
                using (Stream stream = readMeEntry.Open())
                {
                    byte[] bytes = File.ReadAllBytes(path2);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
        }
        private static void ZipC(int p, int parts, int division)
        {
            string zipFilePath = "CALMET0" + p.ToString() + ".zip";
            Console.WriteLine(zipFilePath);
            string path;
            if (division == 1)
            {
                path = "CALMET0" + p.ToString() + ".DAT";
            }
            else
            {
                int subx = (int)Math.Sqrt(parts);
                int i = p / subx;
                int j = p % subx;
                path = "CALMET" + i.ToString() + j.ToString() + ".DAT";
            }
            using (FileStream zipFileToOpen = new FileStream(zipFilePath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipFileToOpen, ZipArchiveMode.Create))
            {
                ZipArchiveEntry readMeEntry = archive.CreateEntry(path);
                using (Stream stream = readMeEntry.Open())
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        public static void HttpDownloadFile(string url, string path)
        {
            DateTime t1 = DateTime.Now;
            // 设置参数
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Proxy = null;
            //发送请求并获取相应回应数据
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            //直到request.GetResponse()程序才开始向目标网页发送Post请求
            Stream responseStream = response.GetResponseStream();
            //创建本地文件写入流
            Stream stream = new FileStream(path, FileMode.Create);
            byte[] bArr = new byte[1048576];
            int size = responseStream.Read(bArr, 0, (int)bArr.Length);
            while (size > 0)
            {
                stream.Write(bArr, 0, size);
                size = responseStream.Read(bArr, 0, (int)bArr.Length);
            }
            stream.Close();
            responseStream.Close();
            DateTime t2 = DateTime.Now;
            TimeSpan ts = t2.Subtract(t1);
            Console.WriteLine("{0} http downloaded in {1} seconds", path, ts.TotalSeconds);
        }

        public static void FtpDownloadFile(string url, string path)
        {
            DateTime t1 = DateTime.Now;
            // 设置参数
            FtpWebRequest request = WebRequest.Create(url) as FtpWebRequest;
            request.Proxy = null;
            request.UseBinary = true;
            request.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["ReportUserName"], ConfigurationManager.AppSettings["ReportPassWord"]);
            //发送请求并获取相应回应数据
            FtpWebResponse response = request.GetResponse() as FtpWebResponse;
            //直到request.GetResponse()程序才开始向目标网页发送Post请求
            Stream responseStream = response.GetResponseStream();
            //创建本地文件写入流
            Stream stream = new FileStream(path, FileMode.Create);
            byte[] bArr = new byte[1048576];
            int size = responseStream.Read(bArr, 0, (int)bArr.Length);
            while (size > 0)
            {
                stream.Write(bArr, 0, size);
                size = responseStream.Read(bArr, 0, (int)bArr.Length);
            }
            stream.Close();
            responseStream.Close();
            DateTime t2 = DateTime.Now;
            TimeSpan ts = t2.Subtract(t1);
            Console.WriteLine("{0} ftp downloaded in {1} seconds", path, ts.TotalSeconds);
        }

        //Get ip address
        private static string GetIpAddress()
        {
            string hostName = Dns.GetHostName();   //获取本机名
            //IPHostEntry localhost = Dns.GetHostByName(hostName);    //方法已过期，可以获取IPv4的地址
            //IPAddress localaddr = localhost.AddressList[0];
            IPHostEntry IpEntry = Dns.GetHostEntry(hostName);   //获取IPv6地址
            for (int i = 0; i < IpEntry.AddressList.Length; i++)
            {
                //从IP地址列表中筛选出IPv4类型的IP地址
                //AddressFamily.InterNetwork表示此IP为IPv4,
                //AddressFamily.InterNetworkV6表示此地址为IPv6类型
                if (IpEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return IpEntry.AddressList[i].ToString();
                }
            }
            return "";
        }

        //多线程下载
        private static void MultiThreadDownload(string url, string path)
        {
            DateTime t1 = DateTime.Now;
            MultiDownload md = new MultiDownload(10, url, path);
            md.Start();
            while (true)
            {
                if (md._threadCompleteNum == md._threadNum) break;
                else
                {
                    Thread.Sleep(100);
                }
            }
            md.Complete();
            DateTime t2 = DateTime.Now;
            TimeSpan ts = t2.Subtract(t1);
            Console.WriteLine("{0} multithread downloaded in {1} seconds", path, ts.TotalSeconds);
        }
    }
    public class MultiDownload
    {
        #region 变量
        public int _threadNum;    //线程数量
        private long _fileSize;    //文件大小
        private string _fileUrl;   //文件地址
        private string _fileName;   //文件名
        private string _savePath;   //保存路径
        public short _threadCompleteNum; //线程完成数量
        private bool _isComplete;   //是否完成
        private volatile int _downloadSize; //当前下载大小(实时的)
        private Thread[] _thread;   //线程数组
        private List<string> _tempFiles = new List<string>();
        private object locker = new object();
        #endregion
        #region 属性
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName
        {
            get
            {
                return _fileName;
            }
            set
            {
                _fileName = value;
            }
        }
        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize
        {
            get
            {
                return _fileSize;
            }
        }
        /// <summary>
        /// 当前下载大小(实时的)
        /// </summary>
        public int DownloadSize
        {
            get
            {
                return _downloadSize;
            }
        }
        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsComplete
        {
            get
            {
                return _isComplete;
            }
        }
        /// <summary>
        /// 线程数量
        /// </summary>
        public int ThreadNum
        {
            get
            {
                return _threadNum;
            }
        }
        /// <summary>
        /// 保存路径
        /// </summary>
        public string SavePath
        {
            get
            {
                return _savePath;
            }
            set
            {
                _savePath = value;
            }
        }
        #endregion
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="threadNum">线程数量</param>
        /// <param name="fileUrl">文件Url路径</param>
        /// <param name="savePath">本地保存路径</param>
        public MultiDownload(int threadNum, string fileUrl, string savePath)
        {
            this._threadNum = threadNum;
            this._thread = new Thread[threadNum];
            this._fileUrl = fileUrl;
            this._savePath = savePath;
        }
        public void Start()
        {
            for(int j = 0; j < _threadNum; j++)
            {
                _tempFiles.Add(null);
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_fileUrl);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            _fileSize = response.ContentLength;
            int singleNum = (int)(_fileSize / _threadNum);  //平均分配
            int remainder = (int)(_fileSize % _threadNum);  //获取剩余的
            request.Abort();
            response.Close();
            for (int i = 0; i < _threadNum; i++)
            {
                List<int> range = new List<int>();
                range.Add(i * singleNum);
                if (remainder != 0 && (_threadNum - 1) == i) //剩余的交给最后一个线程
                    range.Add(i * singleNum + singleNum + remainder - 1);
                else
                    range.Add(i * singleNum + singleNum - 1);
                //下载指定位置的数据
                int[] ran = new int[] { range[0], range[1], i };
                _thread[i] = new Thread(new ParameterizedThreadStart(Download));
                _thread[i].Name = System.IO.Path.GetFileNameWithoutExtension(_fileUrl) + "_{0}".Replace("{0}", Convert.ToString(i + 1));
                _thread[i].Start(ran);
            }
        }
        private void Download(object obj)
        {
            Stream httpFileStream = null, localFileStream = null;
            try
            {
                int[] ran = obj as int[];
                string tmpFileBlock = System.IO.Path.GetTempPath() + Thread.CurrentThread.Name + ".tmp";
                _tempFiles[ran[2]] = tmpFileBlock;
                HttpWebRequest httprequest = (HttpWebRequest)WebRequest.Create(_fileUrl);
                httprequest.AddRange(ran[0], ran[1]);
                httprequest.Proxy = null;
                HttpWebResponse httpresponse = (HttpWebResponse)httprequest.GetResponse();
                httpFileStream = httpresponse.GetResponseStream();
                localFileStream = new FileStream(tmpFileBlock, FileMode.Create);
                byte[] by = new byte[1048576];
                int getByteSize = httpFileStream.Read(by, 0, (int)by.Length); //Read方法将返回读入by变量中的总字节数
                while (getByteSize > 0)
                {
                    Thread.Sleep(2);
                    lock (locker) _downloadSize += getByteSize;
                    localFileStream.Write(by, 0, getByteSize);
                    getByteSize = httpFileStream.Read(by, 0, (int)by.Length);
                }
                lock (locker) _threadCompleteNum++;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message.ToString());
            }
            finally
            {
                if (httpFileStream != null) httpFileStream.Dispose();
                if (localFileStream != null) localFileStream.Dispose();
            }
        }
        /// <summary>
        /// 下载完成后合并文件块
        /// </summary>
        public void Complete()
        {
            Stream mergeFile = new FileStream(@_savePath, FileMode.Create);
            BinaryWriter AddWriter = new BinaryWriter(mergeFile);
            foreach (string file in _tempFiles)
            {
                using (FileStream fs = new FileStream(file, FileMode.Open))
                {
                    BinaryReader TempReader = new BinaryReader(fs);
                    AddWriter.Write(TempReader.ReadBytes((int)fs.Length));
                    TempReader.Close();
                }
                File.Delete(file);
            }
            AddWriter.Close();
            _isComplete = true;
        }
    }

}