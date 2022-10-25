using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;

namespace dispersion
{
    class CreateSURFDAT
    {
        public string SURFfilename;
        public DateTime CurrentTime;
        public int period; //扩散时间，小时为单位
        public double[] WindSpeed;//风速 9999.0  m/s
        public double[] WindDirection;//风向 9999.0  degree
        public int[] CeilingHeight;//云底高度 9999  hundreds of feet
        public int[] OpaqueCover;//云量 9999  tenths
        public double[] Temperature;//温度  9999.0  K
        public int[] RelativeHumidity;//相对湿度  9999  %
        public double[] Pressure;//气压  9999.0  mbar
        public int[] PrecipitationCode;//降水 9999  0=no precipitation, 1-18=liquid precipitation, 19-45=frozen precipitation

        //临时变量
        public double ws, wd, temp, press, preci;//按照读取的值定义，读取的单位
        public int cehei, Opa, hum;

        public CreateSURFDAT(DateTime youCurrentTime, int youPeriod, string youSURF)
        {
            SURFfilename = youSURF;
            CurrentTime = youCurrentTime;
            period = (youPeriod / 60 > 0) ? youPeriod / 60 : 1; //分钟转换为小时
            WindSpeed = new double[period + 1];
            WindDirection = new double[period + 1];
            CeilingHeight = new int[period + 1];
            OpaqueCover = new int[period + 1];
            Temperature = new double[period + 1];
            RelativeHumidity = new int[period + 1];
            Pressure = new double[period + 1];
            PrecipitationCode = new int[period + 1];

            ws = 1.5; //m/s  
            wd = 270; //degree
            cehei = 100; //云底高度 9999  hundreds of feet
            Opa = 5; //云量 9999  tenths
            press = 1000; //hpa百帕
            hum = 257;  //%
            temp = 7;  //℃ 
            preci = 0; //mm
        }

        //WebService获取实时监测数据，获取period的各项数据？？？？？？？？？？？？？？？？？？？？？？？？？mm值转代码？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
        private int GetRealTimeMetData()
        {
            //读取实时数据并赋值
            //             ServiceReference1.xmlSoapClient client = new ServiceReference1.xmlSoapClient("xmlSoap");
            //             DataTable mytable = client.GetCurrentData("气象站");

            //             for (int i = 0; i < mytable.Rows.Count; i++)
            //             {
            //                 if (mytable.Rows[i]["description"].ToString().Contains("风速"))
            //                 {
            //                     double.TryParse(mytable.Rows[i]["value"].ToString(), out ws);
            //                     continue;
            //                 }
            //                 if (mytable.Rows[i]["description"].ToString().Contains("风向"))
            //                 {
            //                     double.TryParse(mytable.Rows[i]["value"].ToString(), out wd);
            //                     continue;
            //                 }
            //                 if (mytable.Rows[i]["description"].ToString().Contains("风压"))
            //                 {
            //                     double.TryParse(mytable.Rows[i]["value"].ToString(), out press);
            //                     continue;
            //                 }
            //                 if (mytable.Rows[i]["description"].ToString().Contains("湿度"))
            //                 {
            //                     int.TryParse(mytable.Rows[i]["value"].ToString(), out hum);
            //                     continue;
            //                 }
            //                 if (mytable.Rows[i]["description"].ToString().Contains("气温"))
            //                 {
            //                     double.TryParse(mytable.Rows[i]["value"].ToString(), out temp);
            //                     continue;
            //                 }
            //                 if (mytable.Rows[i]["description"].ToString().Contains("粉尘"))
            //                 {
            //                     double.TryParse(mytable.Rows[i]["value"].ToString(), out preci);
            //                     continue;
            //                 }
            //             }

            for (int i = 0; i < period + 1; i++)
            {
                WindSpeed[i] = ws;
                WindDirection[i] = wd;
                CeilingHeight[i] = cehei;
                OpaqueCover[i] = Opa;
                Temperature[i] = temp + 273.1; //摄氏度转热力学温度
                RelativeHumidity[i] = hum;
                Pressure[i] = press;  //hpa = mbar
                PrecipitationCode[i] = 0;  //mm值转代码？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
            }
            return 0;
        }

        //SURF文件头处的起止时间与数据部分的时间的关系？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
        public int CreateSURFFile()
        {
            if (GetRealTimeMetData() == 0)//已读取气象数据，可以直接写文件
            {
                DateTime st, et, ct, nt; //开始时间， 结束时间，当前时间，下一个时间  SURF文件头处的起止时间与数据部分的时间的关系？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
                st = ct = nt = CurrentTime; //暂时设置为当前时间，通常情况起始时间段包含了计算所需时间即可，即st<CurrentTime
                et = CurrentTime.AddHours(period + 1);
                char s = ' ';
                string DataString = "";
                string headerString = "";
                headerString = "SURF.DAT        2.1             Hour Start and End Times with Seconds\r\n" +
                                      "   1\r\n" +
                                      "Produced by SMERGE Version: 5.7.0  Level: 121203\r\n" +
                                      "NONE\r\n" +
                                      "UTC+0800\r\n" +
                                      "  " + st.Year.ToString().PadLeft(4, s) + " " + st.DayOfYear.ToString().PadLeft(3, s) + "  " + st.Hour.ToString().PadLeft(2, s) + "  " + (st.Minute * 60 + st.Second).ToString().PadLeft(4, s) +
                                      "  " + et.Year.ToString().PadLeft(4, s) + " " + et.DayOfYear.ToString().PadLeft(3, s) + "  " + et.Hour.ToString().PadLeft(2, s) + "  " + (et.Minute * 60 + et.Second).ToString().PadLeft(4, s) +
                                      "    1\r\n" +
                                      "   50000\r\n";
                for (int i = 0; i < period + 1; i++)
                {
                    ct = nt;
                    nt = nt.AddHours(1);//加一分钟，每分钟一条数据
                    DataString += ct.Year.ToString().PadLeft(4, s) + "  " + ct.DayOfYear.ToString().PadLeft(3, s) + "   " + ct.Hour.ToString().PadLeft(2, '0') + "   " + (ct.Minute * 60 + ct.Second).ToString().PadLeft(4, '0') + "   " +
                                       nt.Year.ToString().PadLeft(4, s) + "  " + nt.DayOfYear.ToString().PadLeft(3, s) + "   " + nt.Hour.ToString().PadLeft(2, '0') + "   " + (nt.Minute * 60 + nt.Second).ToString().PadLeft(4, '0') + "\r\n" +
                                       "   " + WindSpeed[i].ToString("f3").PadLeft(6, s) + "  " + WindDirection[i].ToString("f3").PadLeft(7, s) +
                                       "  " + CeilingHeight[i].ToString().PadLeft(3, s) + "  " + OpaqueCover[i].ToString().PadLeft(3, s) +
                                       "  " + Temperature[i].ToString("f3").PadLeft(7, s) + "     " + RelativeHumidity[i].ToString().PadLeft(2, s) +
                                       "  " + Pressure[i].ToString("f3").PadLeft(7, s) + "   " + PrecipitationCode[i].ToString().PadLeft(2, s) + "\r\n";
                }
                ASCIIEncoding ASCEncoding = new ASCIIEncoding();
                FileStream fs = new FileStream(SURFfilename, FileMode.Create);
                fs.Write(ASCEncoding.GetBytes(headerString + DataString), 0, ASCEncoding.GetByteCount(headerString + DataString));
                fs.Flush();
                fs.Close();
                return 0;
            }
            else//未读取气象数据，提示
            {
                Console.WriteLine("Error in read Surf data, please check if the met monitoring system runs normal!");
                return 1;
            }
        }
    }
}
