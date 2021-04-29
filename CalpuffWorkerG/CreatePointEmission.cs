using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace dispersion
{
    class CreatePointEmission  //暂时不用
    {
        public string EmissionFile;
        public DateTime CurrentTime;
        public int period; //扩散时间，分钟为单位，如扩散15分钟，值为15  写文件时再转化成秒 0000-3599

        //面源污染源的四个角点坐标
        public int UTMzone;
        public double X, Y;  //X,Y-coordinate (km) of each of the point source 
        public double EmissionHeight; //Effective height (m) of the emissions above the ground
        public double GroundElevation; //Elevation of ground (m MSL)
        public double diameter;//泄露孔径直径 m

        //随时间变化的量，排放数据
        private double[] Temp; //Temperature (deg. K)
        private double[] ExitVelocity;  //Effective rise velocity (m/s) 
        private double SigmaY;  //Effective radius(m) for rise calculation 
        private double SigmaZ;  //Initial vertical spread (m) 
        private double[] EmissionRate; //Emission rates (g/s or g/m2/s) for each species in the order specified
        private int[] TimeInterval;//排放的时间间隔;

        public CreatePointEmission(DateTime youCurrentTime, int youPeriod, string youEmissionFile, double uSourceX, double uSourceY, int zone, double uGroundElevation, 
            double heightAboveGround, double udiameter)
        {
            EmissionFile = youEmissionFile;
            CurrentTime = youCurrentTime;
            period = youPeriod;
            UTMzone = zone;
            X = uSourceX;
            Y = uSourceY;
            diameter = udiameter;
            GroundElevation = uGroundElevation;
            EmissionHeight = heightAboveGround;
            SigmaZ = EmissionHeight / 2.15;  //??????????????????????????????????
            SigmaY = EmissionHeight / 4.3;  //??????????????????????????????????
        }

        private int GetEmissionData()
        {
            if ( diameter == 0.01 )//10mm,恒定速率
            {
                Temp = new double[2] { 0294.25, 0294.25 };
                ExitVelocity = new double[2] { 500, 0.0 };
                EmissionRate = new double[2] { 1250, 0.0 };
                TimeInterval = new int[2] { 600, 2700 };//4500
                return 0;
            } 
            else if ( diameter == 0.05 )//50mm,变速率
            {
                Temp = new double[35] { 0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 
                                               0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 
                                               0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 0294.25,
                                               0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 
                                               0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 0294.25, 
                                               0294.25, 0294.25, 0294.25, 0294.25, 0294.25 };
                ExitVelocity = new double[35] { 500, 5000, 500, 500, 500, 500,
                                                         500, 5000, 500, 500, 500, 500,
                                                         500, 5000, 500, 500, 500, 500,
                                                         500, 5000, 500, 500, 500, 500,
                                                         490, 460, 440, 420, 400, 360, 
                                                         320, 250, 180, 70, 0 };
                EmissionRate = new double[35] { 26000, 250000, 24000, 21900, 20000, 18750,
                                                         17500, 16250, 15000, 13300, 11800,11100,
                                                         10000, 8100, 6660, 5500, 5000, 4800,
                                                         4200, 3500, 3100, 2750, 2400, 2000, 
                                                         1700, 1400, 1200, 1100, 900, 650, 
                                                         480, 400, 210, 130, 0 };
                TimeInterval = new int[35] { 60, 20, 20, 40, 40, 20, 
                                                        33, 34, 33, 50, 50, 40, 
                                                        60, 100, 100, 100, 80, 20,
                                                        100, 100, 100, 100, 100, 100, 
                                                        100, 100, 100, 100, 100, 200,
                                                        200, 100, 100, 100, 4500, };
                return 0;
            }
            else
                return 1;
        }

        public int CreateEmission()
        {
            if (GetEmissionData() == 0)//已读取气象数据，可以直接写文件
            {
                DateTime st, et, ct, nt; //开始时间， 结束时间，当前时间，下一个时间  SURF文件头处的起止时间与数据部分的时间的关系？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
                st = ct = nt = CurrentTime; //暂时设置为当前时间，通常情况起始时间段包含了计算所需时间即可，即st<CurrentTime
                et = CurrentTime.AddHours(2);
                char s = '0';
                string DataString = "";
                string DataBlock = "";
                DataString = "PTEMARB.DAT     2.1             Comments, times with seconds, time zone, coord info\r\n" +
                                      "   2\r\n" +
                                      "Prepared by user\r\n" +
                                      "CDB\r\n" +
                                      "UTM\r\n" +
                                      "  " + UTMzone.ToString() + "N\r\n" +
                                      "WGS-84  02-21-2003\r\n" +
                                      "  KM\r\n" +
                                      "UTC+0800\r\n" +
                                      st.Year.ToString().PadLeft(4, s) + "  " + st.DayOfYear.ToString().PadLeft(3, s) + "  " + st.Hour.ToString().PadLeft(2, s) + "  " + (st.Minute * 60 + st.Second).ToString().PadLeft(4, s) +
                                      "  " + et.Year.ToString().PadLeft(4, s) + "  " + et.DayOfYear.ToString().PadLeft(3, s) + "  " + et.Hour.ToString().PadLeft(2, s) + "  " + (et.Minute * 60 + et.Second).ToString().PadLeft(4, s) + "\r\n" +
                                      "   1   1\r\n" +
                                      "'CH4'\r\n" +
                                      "    16.0000\r\n"; 
                DataBlock = "'SRC_1'   " + X.ToString() + "       "  + Y.ToString() + "       "  + EmissionHeight.ToString() + "       " + diameter.ToString() +
                                 "    " + GroundElevation.ToString() + "           0.00  1.00    0.00\r\n";
                DataString += DataBlock;
                int n = EmissionRate.Length;
                for (int i = 0; i < n; i++)
                {
                    ct = nt;
                    nt = nt.AddSeconds(Convert.ToDouble(TimeInterval[i]));//？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
                    DataString += "       " + ct.Year.ToString().PadLeft(4, s) + "  " + ct.DayOfYear.ToString().PadLeft(3, s) + "   " + ct.Hour.ToString().PadLeft(2, '0') + "   " + (ct.Minute * 60 + ct.Second).ToString().PadLeft(4, '0') + "        " +
                                       nt.Year.ToString().PadLeft(4, s) + "  " + nt.DayOfYear.ToString().PadLeft(3, s) + "   " + nt.Hour.ToString().PadLeft(2, '0') + "   " + (nt.Minute * 60 + nt.Second).ToString().PadLeft(4, '0') + "\r\n";
                    DataString += "'SRC_1'   " + Temp[i].ToString("f3").PadLeft(6, s) + "        " + ExitVelocity[i].ToString("f3").PadLeft(7, s) +
                                       "        " + SigmaY.ToString().PadLeft(3, s) + "        " + SigmaZ.ToString().PadLeft(3, s) +
                                       "        " + EmissionRate[i].ToString("f3").PadLeft(7, s) + "\r\n";
                }
                ASCIIEncoding ASCEncoding = new ASCIIEncoding();
                FileStream fs = new FileStream(EmissionFile, FileMode.Create);
                fs.Write(ASCEncoding.GetBytes(DataString), 0, ASCEncoding.GetByteCount(DataString));
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
