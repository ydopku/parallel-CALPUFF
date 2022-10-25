using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;

namespace dispersion
{
    class CreateUPDAT  //暂时不用
    {
        public string UPfilename;
        public DateTime CurrentUTCTime;
        public int period; //扩散时间，小时为单位
        public int StationNum; // 探空站的站数，决定了每单位时间数据的条数
        public double LowWindDirection; //低空的风向
        private double[] Pressure; //mb
        private double[] Height; //m
        private double[] Temperature; //K
        private double[] WindDirection;  //degrees
        private double[] WindSpeed;  //m/s

        public double ws;  //风速的临时变量

        public CreateUPDAT(DateTime youCurrentTime, int youPeriod, string youUP)
        {
            UPfilename = youUP;
            CurrentUTCTime = youCurrentTime.AddHours(-8);//当前时间转换为0时区的当前时间
            StationNum = 15;
            period = (youPeriod / 60 > 0) ? youPeriod / 60 : 1; //分钟转换为小时
            Pressure = new double[StationNum];
            Height = new double[StationNum];
            Temperature = new double[StationNum];
            WindDirection = new double[StationNum];
            WindSpeed = new double[StationNum];
            LowWindDirection = 270;
            ws = 1.5;
        }

        //获取探空数据（暂用某一往年数据）
        private int GetUpAirData()
        {
            Pressure = new double[15]{934.0, 926.0, 915.0, 896.0,
                                                 869.0, 837.0, 802.0, 763.0,
                                                 723.0, 684.0, 644.0, 604.0,
                                                 564.0, 524.0,  483.0};
            Height = new double[15]{703.0, 774.0, 882.0, 1064.0,
                                                 1325.0, 1632.0, 1990.0, 2404.0,
                                                 2838.0, 3292.0, 3770.0, 4274.0,
                                                 4808.0, 5376.0,  5982.0};
            Temperature = new double[15]{291.2, 291.3, 291.0, 290.0,
                                                 288.2, 285.6, 282.3, 278.7,
                                                 275.6, 272.9, 270.2, 267.1,
                                                 263.5, 260.2, 255.7};
            WindDirection = new double[15]{103, 106, 110, 121,
                                                 130, 134, 135, 136,
                                                 137, 144, 163, 193,
                                                 206, 220,  218};
            for (int i = 0; i < 15; i++)
            {
                WindDirection[i] = LowWindDirection;
            }
            /*WindSpeed = new double[15]{5, 6, 7, 8, 
                                                    9, 9, 9, 9,
                                                    10, 9, 7, 5,
                                                    4, 5,  7};
            */
            for (int i = 0; i < 15; i++)
            {
                WindSpeed[i] = ws;
            }
            return 0;
        }

        public int CreateUpAirFile()
        {
            if (GetUpAirData() == 0)//已读取探空数据，可以直接写文件
            {
                int n = period;//数据重复次数，重复4次
                DateTime st, et, ct;  //开始时间， 结束时间，当前时间，下一个时间  SURF文件头处的起止时间与数据部分的时间的关系？？？？？？？？？？？？？？？？？？？？？
                st = ct = CurrentUTCTime; //暂时设置为当前时间，通常情况起始时间段包含了计算所需时间即可，即st<CurrentTime
                et = CurrentUTCTime.AddHours(n);
                char s = ' ';
                string DataString = "";
                string headerString = "";
                string DataBlocks = "";
                headerString = "UP.DAT          2.1             Hour Start and End Times with Seconds\r\n" +
                                      "   1\r\n" +
                                      "Produced by READ62 Version: 5.661  Level: 110225\r\n" +
                                      "NONE\r\n" +
                                      "UTC+0000\r\n" +
                                      " " + st.Year.ToString().PadLeft(5, s) + st.DayOfYear.ToString().PadLeft(5, s) + st.Hour.ToString().PadLeft(5, s) + "    0" +
                                      et.Year.ToString().PadLeft(5, s) + et.DayOfYear.ToString().PadLeft(5, s) + et.Hour.ToString().PadLeft(5, s) + "    0" +
                                      " 500.    1    1\r\n" +
                                      "     F    F    F    F\r\n";
                for (int i = 0; i < StationNum; i++)
                {
                    DataBlocks += "   " + Pressure[i].ToString("f1").PadLeft(6, s) + "/" + Height[i].ToString("f0").PadLeft(5, s) +
                                       "/" + Temperature[i].ToString("f1").PadLeft(5, s) + "/" + WindDirection[i].ToString().PadLeft(3, s) +
                                       "/" + WindSpeed[i].ToString().PadLeft(3, s);
                    if ((i + 1) % 4 == 0)
                    {
                        DataBlocks += "\r\n";
                    }
                }
                DataBlocks += "\r\n";

                DataString += headerString;
                for (int i = 0; i < period + 1; i++)
                {
                    DataString += "   6201  00050000    " +
                    ct.Year.ToString().PadLeft(4, s) + ct.Month.ToString().PadLeft(4, s) + ct.Day.ToString().PadLeft(3, s) +
                    ct.Hour.ToString().PadLeft(3, s) + "    0" + "    " +
                    ct.Year.ToString().PadLeft(4, s) + ct.Month.ToString().PadLeft(4, s) + ct.Day.ToString().PadLeft(3, s) +
                    ct.Hour.ToString().PadLeft(3, s) + "    0" +
                    "   29   " + StationNum.ToString().PadLeft(5, s) + "\r\n";

                    DataString += DataBlocks;
                    ct = ct.AddHours(1);//加一小时并重复数据块
                }

                ASCIIEncoding ASCEncoding = new ASCIIEncoding();
                FileStream fs = new FileStream(UPfilename, FileMode.Create);
                fs.Write(ASCEncoding.GetBytes(DataString), 0, ASCEncoding.GetByteCount(DataString));
                fs.Flush();
                fs.Close();
                return 0;
            }
            else//未读取气象数据，提示
            {
                Console.WriteLine("Error in read UpAir data, please check if the met monitoring system runs normal!");
                return 1;
            }
        }
    }
}
