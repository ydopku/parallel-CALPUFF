using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace dispersion
{
    class CreateEmissionFile  //暂时不用
    {
        public string EmissionFile;
        public DateTime CurrentTime;
        public int period; //扩散时间，分钟为单位，如扩散15分钟，值为15  写文件时再转化成秒 0000-3599

        //面源污染源的四个角点坐标
        public int UTMzone;
        public double[] X, Y;  //X,Y-coordinate (km) of each of the four vertices defining the perimeter of the area source 
        public double EmissionHeight; //Effective height (m) of the emissions above the ground
        public double GroundElevation; //Elevation of ground (m MSL)

        //随时间变化的量，排放数据
        private double[] Temp; //Temperature (deg. K)
        private double[] RiseVelocity;  //Effective rise velocity (m/s) 
        private double[] RadiusForRise;  //Effective radius(m) for rise calculation 
        private double SigmaZ;  //Initial vertical spread (m) 
        private double[] EmissionRate; //Emission rates (g/s or g/m2/s) for each species in the order specified
        private int[] TimeInterval;//排放的时间间隔;

        public CreateEmissionFile(DateTime youCurrentTime, int youPeriod, string youEmissionFile, double uSourceX, double uSourceY, int zone, double uGroundElevation, 
            double heightAboveGround, double radius)
        {
            EmissionFile = youEmissionFile;
            CurrentTime = youCurrentTime;
            period = youPeriod;
            UTMzone = zone;
            X = new double[4];
            Y = new double[4];
            X[0] = X[3] = uSourceX - radius;
            X[1] = X[2] = uSourceX + radius;
            Y[0] = Y[1] = uSourceY - radius;
            Y[2] = Y[3] = uSourceY + radius;
            GroundElevation = uGroundElevation;
            EmissionHeight = heightAboveGround;
            SigmaZ = EmissionHeight / 2.15;
        }

        private int GetEmissionData()
        {
            //EmissionHeight = 0.00000;
            //GroundElevation = 0844.00;
            Temp = new double[31] { 227.53, 234.86, 237.48, 273.10, 240.91, 242.024, 
                                               243.505, 244.829, 246.007, 246.556, 247.058, 247.482, 
                                               247.814, 248.103, 249.2, 249.82, 250.33, 250.62,
                                               254.11, 258.251, 257.617, 256.874, 255.926, 258.438,
                                               269.399, 282.950, 282.950, 282.950, 282.950, 282.950,
                                               282.95 };
            RiseVelocity = new double[31] { 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000,
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000};
            RadiusForRise = new double[31] { 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000,
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 0.00000, 
                                                        0.00000};
//             EmissionRate = new double[18] { 23548000, 16840000, 10132000, 9640000, 8556000, 5332000,
//                                                          4512000, 2984000, 2024000, 1376000, 912000, 656000,
//                                                          432000, 344000, 156000, 152000, 38400, 00000 };
            EmissionRate = new double[31] { 6856780, 3045150, 2627520, 2386050, 2072240, 1890990, 
                                                        1648171, 1423841, 1209828, 1102331, 995265, 896002, 
                                                        810091, 729806, 623740, 516470, 430020, 383150,
                                                        301440, 234478, 181022, 140095, 108759, 94700, 
                                                        71852, 48569, 40265, 31710, 22623, 11836, 
                                                        0.00000 };
            //TimeInterval = new int[18] { 1, 1, 3, 5, 5, 15, 20, 5, 5, 5, 5, 10, 10, 10, 10, 10, 30, 7050 };
            TimeInterval = new int[31] {1, 1, 1, 1, 2, 2, 
                                                        4, 4, 6, 4, 4, 5, 
                                                        6, 7, 3, 2, 3, 2,
                                                        4, 4, 4, 4, 4, 2, 
                                                        4, 8, 3, 4, 5, 8, 
                                                        7088 };
            return 0;
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
                DataString = "BAEMARB.DAT     2.1             Comments, times with seconds, time zone, coord info\r\n" +
                                      "   2\r\n" +
                                      "Prepared by user\r\n" +
                                      "CDB\r\n" +
                                      "UTM\r\n" +
                                      "  "+UTMzone.ToString()+"N\r\n" +
                                      "WGS-84  02-21-2003\r\n" +
                                      "  KM\r\n" +
                                      "UTC+0800\r\n" +
                                      st.Year.ToString().PadLeft(4, s) + "  " + st.DayOfYear.ToString().PadLeft(3, s) + "  " + st.Hour.ToString().PadLeft(2, s) + "  " + (st.Minute * 60 + st.Second).ToString().PadLeft(4, s) +
                                      "  " + et.Year.ToString().PadLeft(4, s) + "  " + et.DayOfYear.ToString().PadLeft(3, s) + "  " + et.Hour.ToString().PadLeft(2, s) + "  " + (et.Minute * 60 + et.Second).ToString().PadLeft(4, s) + "\r\n" +
                                      "   1   1\r\n" +
                                      "'CH4'\r\n" + 
                                      "    16.0000\r\n" +
                                      "'SRC_1'   'g/m2/s'       0.0        0.0\r\n";
                DataBlock = "'SRC_1'   " + X[0].ToString() + "       " + X[1].ToString() + "       " + X[2].ToString() + "       " + X[3].ToString() + "       \r\n" +
                                 "   " + Y[0].ToString() + "       " + Y[1].ToString() + "       " + Y[2].ToString() + "       " + Y[3].ToString() + "       " + EmissionHeight.ToString() + "       \r\n" +
                                 "    " + GroundElevation.ToString() + "       ";
                int n = EmissionRate.Length;
                for (int i = 0; i < n; i++)
                {
                    ct = nt;
                    nt = nt.AddSeconds(Convert.ToDouble(TimeInterval[i]));//？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？？
                    DataString += "       " + ct.Year.ToString().PadLeft(4, s) + "  " + ct.DayOfYear.ToString().PadLeft(3, s) + "   " + ct.Hour.ToString().PadLeft(2, '0') + "   " + (ct.Minute * 60 + ct.Second).ToString().PadLeft(4, '0') + "        " +
                                       nt.Year.ToString().PadLeft(4, s) + "  " + nt.DayOfYear.ToString().PadLeft(3, s) + "   " + nt.Hour.ToString().PadLeft(2, '0') + "   " + (nt.Minute * 60 + nt.Second).ToString().PadLeft(4, '0') + "\r\n";
                    DataString += DataBlock;
                    DataString += Temp[i].ToString("f3").PadLeft(6, s) + "        " + RiseVelocity[i].ToString("f3").PadLeft(7, s) +
                                       "        " + RadiusForRise[i].ToString().PadLeft(3, s) + "        " + SigmaZ.ToString().PadLeft(3, s) +
                                       "        " + EmissionRate[i].ToString("f3").PadLeft(10, s) + "\r\n";
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
