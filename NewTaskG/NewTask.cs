using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dispersion
{
    public class RpcClient
    {
        private const string QUEUE_NAME = "rpc_queue";

        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string replyQueueName;
        private readonly EventingBasicConsumer consumer;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper =
                    new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        public RpcClient()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };

            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            /*var args = new Dictionary<string, object>
            {
                { "x-message-ttl", 10000 },
                { "x-expires", 20000 }
            };*/
            replyQueueName = channel.QueueDeclare(arguments: null).QueueName;
            consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out TaskCompletionSource<string> tcs))
                    return;
                var body = ea.Body;
                var response = Encoding.UTF8.GetString(body);
                tcs.TrySetResult(response);
            };
        }

        public Task<string> CallAsync(int p, int parts, string s, int division, CancellationToken cancellationToken = default(CancellationToken))
        {
            IBasicProperties props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;
            //props.Expiration = "10000";
            var messageBytes = Encoding.UTF8.GetBytes(p.ToString() + "," + parts.ToString() + "," + s + "," + division.ToString());
            var tcs = new TaskCompletionSource<string>();
            callbackMapper.TryAdd(correlationId, tcs);
            channel.ExchangeDeclare(exchange: "model_request", type: "direct");

            channel.BasicPublish(
                exchange: "model_request",
                routingKey: "parallel",
                basicProperties: props,
                body: messageBytes);

            channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);

            cancellationToken.Register(() => callbackMapper.TryRemove(correlationId, out var tmp));
            return tcs.Task;
        }

        public Task<string> CallAsync2(string s, int layer, int layerpart, int layerparts, string timestep, CancellationToken cancellationToken = default(CancellationToken))
        {
            IBasicProperties props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;
            //props.Expiration = "10000";
            var messageBytes = Encoding.UTF8.GetBytes("C" + "," + s + "," + layer.ToString() + "," + layerpart.ToString() + "," + layerparts.ToString() + "," + timestep);
            var tcs = new TaskCompletionSource<string>();
            callbackMapper.TryAdd(correlationId, tcs);
            channel.ExchangeDeclare(exchange: "model_request", type: "direct");

            channel.BasicPublish(
                exchange: "model_request",
                routingKey: "parallel",
                basicProperties: props,
                body: messageBytes);

            channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);

            cancellationToken.Register(() => callbackMapper.TryRemove(correlationId, out var tmp));
            return tcs.Task;
        }

        public void Close()
        {
            connection.Close();
        }
    }

    public class Rpc
    {
        private static int results = 0;  //结果计数
        private static int part = -1;  //calmet的第几部分
        private static int parts = 10;  //总份数
        private static int division = 1;  //分割方式,1为一维y轴分割，2为二维分割
        private static int layer = 0;  //第几层
        private static int layercount = 4;  //总层数(不含地面)，默认5层，每层2m
        private static int layerparts = 10;  //每层按受体点数量划分为若干部分
        private static int layerpart = -1;  //每层的第几部分
        private static string timestep = "m";  //默认为分钟级计算

        private static bool calmet = false;
        private static bool calpuff = true;  //是否计算该模块，测试用

        //method for fib
        /*
        public static void Main(string[] args)
        {
            Console.WriteLine("RPC Client");
            string n = args.Length > 0 ? args[0] : "30";
            Task t = InvokeAsync(n);
            t.Wait();

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private static async Task InvokeAsync(string n)
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            var rpcClient = new RpcClient();

            Console.WriteLine(" [x] Requesting fib({0})", n);
            var response = await rpcClient.CallAsync(n.ToString());
            Console.WriteLine(" [.] Got '{0}'", response);

            rpcClient.Close();
        }
        */

        //method for model library
        public static void Main(string[] args)
        {
            Console.WriteLine("Model Client");
            Console.WriteLine("Usage:dotnet run num_of_parts");
            List<EventWaitHandle> waits = new List<EventWaitHandle>();
            if (calmet)
            {
                int num1 = args.Length > 0 ? int.Parse(args[0]) : 10;
                parts = num1;
                int num0 = args.Length > 1 ? int.Parse(args[1]) : 1;
                division = num0;
                if (division == 2)
                {
                    parts = 4;
                }

                DateTime t1 = DateTime.Now;
                for (int i = 0; i < parts; i++)
                {
                    ManualResetEvent handler = new ManualResetEvent(false);
                    waits.Add(handler);
                    new Thread(new ParameterizedThreadStart(Go))
                    {
                        Name = "thread" + i.ToString()
                    }.Start(new Tuple<string, EventWaitHandle>("test print:" + i, handler));
                }
                WaitHandle.WaitAll(waits.ToArray());
                DateTime t2 = DateTime.Now;
                TimeSpan ts = t2.Subtract(t1);
                Console.WriteLine("Calmet tasks all complete in {0} seconds", ts.TotalSeconds);
                if (division == 1)
                {
                    UnZip0(parts);
                    Merge();
                }
                else
                {
                    //Todo:二维分割的拼接方法
                }
                Zip();
            }
            if (calpuff)
            {
                DateTime t3 = DateTime.Now;
                int num2 = args.Length > 2 ? int.Parse(args[2]) : 4;
                layercount = num2;
                int num3 = args.Length > 3 ? int.Parse(args[3]) : 10;
                layerparts = num3;
                timestep = args.Length > 4 ? args[4] : "m";
                for (int i = 0; i <= layercount; i++)
                {
                    for (int j = 1; j <= layerparts; j++)
                    {
                        ManualResetEvent handler = new ManualResetEvent(false);
                        waits.Add(handler);
                        new Thread(new ParameterizedThreadStart(Go2))
                        {
                            Name = "thread" + j.ToString()
                        }.Start(new Tuple<string, EventWaitHandle>("test print:" + j, handler));
                    }
                }
                WaitHandle.WaitAll(waits.ToArray());
                UnZip(layercount, layerparts);
                DateTime t4 = DateTime.Now;
                TimeSpan ts2 = t4.Subtract(t3);
                Console.WriteLine("Calpuff tasks all complete in {0} seconds", ts2.TotalSeconds);
            }
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private static void Go(object param)
        {
            part += 1;
            Tuple<string, EventWaitHandle> p = (Tuple<string, EventWaitHandle>)param;
            Task t = InvokeAsync(part, division);
            t.Wait();
            results += 1;
            p.Item2.Set();
        }

        private static void Go2(object param)
        {
            if (layerpart == layerparts - 1)
            {
                layer++;
                layerpart = -1;
            }
            layerpart += 1;
            Tuple<string, EventWaitHandle> p = (Tuple<string, EventWaitHandle>)param;
            Task t = InvokeAsync2(layer, layerpart);
            t.Wait();
            p.Item2.Set();
        }

        private static async Task InvokeAsync(int p, int d)
        {
            var rpcClient = new RpcClient();
            Console.WriteLine(" [x] Requesting calmet result part {0}", p);
            string s = GetIpAddress();
            var response = await rpcClient.CallAsync(p, parts, s, d);
            string url = "http://" + response + "/calpuffWorkerP/" + "CALMET0" + p.ToString() + ".zip";
            string result = "CALMET0" + p.ToString() + ".zip";
            HttpDownloadFile(url, result);
            Console.WriteLine(" [.] Got '{0}' from {1}", result, response);
            rpcClient.Close();
        }

        private static async Task InvokeAsync2(int l, int lp)
        {
            var rpcClient = new RpcClient();
            Console.WriteLine(" [x] Requesting calpuff result layer {0}-{1}", l, lp);
            string s = GetIpAddress();
            var response2 = await rpcClient.CallAsync2(s, l, lp, layerparts, timestep);
            string url2 = "http://" + response2 + "/calpuffWorkerP/" + "result" + l.ToString() + "-" + lp.ToString() + ".zip";
            string result2 = "result" + l.ToString() + "-" + lp.ToString() + ".zip";
            HttpDownloadFile(url2, result2);
            Console.WriteLine(" [.] Got {0} from {1}", result2, response2);
            rpcClient.Close();
        }

        private static int Merge()
        {
            RunCALPUFF newCompute;
            DateTime CurrentTime = new DateTime(2018, 3, 1, 4, 0, 0);
            double SourceX, SourceY, Elavation;
            SourceX = 231.7887;
            SourceY = 3473.362;
            Elavation = 844;
            newCompute = new RunCALPUFF(SourceX, SourceY, Elavation, 49, CurrentTime, 0, parts);
            newCompute.MergeCalmet();
            return 0;
        }

        public static void HttpDownloadFile(string url, string path)
        {
            DateTime t1 = DateTime.Now;
            // 设置参数
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            //发送请求并获取相应回应数据
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            //直到request.GetResponse()程序才开始向目标网页发送Post请求
            Stream responseStream = response.GetResponseStream();
            //创建本地文件写入流
            Stream stream = new FileStream(path, FileMode.Create);
            byte[] bArr = new byte[1024];
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
            Console.WriteLine("{0} downloaded in {1} seconds", path, ts.TotalSeconds);
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

        //Generate zip
        private static void Zip0(int c, int p)
        {
            for(int i = 0; i <= c; i++)
            {
                string zipFilePath = "result" + i.ToString() + ".zip";
                Console.WriteLine(zipFilePath);
                using (FileStream zipFileToOpen = new FileStream(zipFilePath, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(zipFileToOpen, ZipArchiveMode.Create))
                {
                    for(int j = 0; j < p; j++)
                    {
                        string path = "result" + i.ToString() + "-" + j.ToString() + ".zip";
                        ZipArchiveEntry readMeEntry = archive.CreateEntry(path);
                        using (Stream stream = readMeEntry.Open())
                        {
                            byte[] bytes = File.ReadAllBytes(path);
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }
        }

        private static void Zip()
        {
            string zipFilePath = "CALMET.zip";
            Console.WriteLine(zipFilePath);
            string path = "CALMET.DAT";
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

        private static void UnZip0(int p)
        {
            for (int j = 0; j < p; j++)
            {
                string path = "CALMET0" + j.ToString() + ".zip";
                ZipFile.ExtractToDirectory(path, "../NewTaskP/", true);
            }
        }

        private static void UnZip(int c, int p)
        {
            for (int i = 0; i <= c; i++)
            {
                {
                    for (int j = 0; j < p; j++)
                    {
                        string path = "result" + i.ToString() + "-" + j.ToString() + ".zip";
                        ZipFile.ExtractToDirectory(path, "../NewTaskP/", true);
                    }
                }
            }
        }
    }
}