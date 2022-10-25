using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;

namespace dispersion
{
    class RunCALPUFF
    {
        //CALMET输入输出数据路径、文件名
        public string GeoPath; // \geo
        public string GEOFileName; //GEO.DAT
        public string SURFFileName; //SURF.DAT
        public string UPFileName; //UP.DAT
        public string METDATAName; //输出CALMET.DAT
        public string METListName;  //输出CALMET.LST

        //CALPUFF输入输出文件名
        public string CONDAT;  //CONC.DAT
        public string EmissionFile; //baemarb.DAT

        //计算时间
        public DateTime StartTime;
        public int TimeSpan; // 需要计算的扩散时间，（0，60），单位min，一般选5，10，15，30
        public int METTimeStep; //计算的时间步长，单位秒，一般选60，30，15
        public int PUFFTimeStep; //计算的时间步长，单位秒，一般选60，30，15

        //地形处理相关参数，与GEO.DAT一致
        public int UTMZone;  //UTM投影带号，48/49
        public double Xsw, Ysw;  // 风场格网左下角（西南角）坐标，km, (230.642, 3471.161)
        public int GridCellsX, GridCellsY;  //格网数 (50,50)
        public double GridSpace; //格网间隔 km  0.1
        //三个网格场的竖直方向的相关参数
        public int GridCellsZ; // No. of vertical layers (NZ)    default 10
        public double[] Zface; // Cell face heights(m) in arbitrary,  vertical grid (ZFACE(NZ+1))  ZFACE = 0.,20.,40.,80.,160.,320.,640.,1200.,2000.,3000.,4000.
        public int ReNest; //受体格网的细分因子：nest=1,间隔是100m; nest=2,间隔是50m; nest=5,间隔是20m;

        //气象站相关参数
        public double SurfX, SurfY, SurfZ;  //Unit: km, km, m     233.147, 3473.666    10
        public double UpX, UpY; //Unit: km, km    233.152, 3473.671

        //污染源点的坐标
        public double SourceX, SourceY, Elevation;
        public double EmissionHeight; 
        public double radius; //面源是以污染源点为中心，以0.25米为半径的正方形
        public int RType; //泄露类型 0=10mm，1=50mm，2=全断裂
        public double EmissionRate;

        //气象数据
        public double WindSpeed;//风速 9999.0  m/s
        public double WindDirection;//风向 9999.0  degree
        public int CeilingHeight;//云底高度 9999  hundreds of feet
        public int OpaqueCover;//云量 9999  tenths
        public double Temperature;//温度  9999.0  ℃
        public int RelativeHumidity;//相对湿度  9999  %
        public double Pressure;//气压  9999.0  mbar
        public int Precipitation;//降水 9999  0=no precipitation, 1-18=liquid precipitation, 19-45=frozen precipitation

        //并行计算参数
        public int SubX, SubY;  //X,Y方向划分数，目前为了结果文件拼接，应只在Y方向划分，SubX必须等于1
        public int SubGridX, SubGridY;  //X,Y方向每一划分的子网格数
        public int LastX, LastY;  //用于处理带余数的最后一行(列)
        public int SubGridXb, SubGridYb;  //加上缓冲区时需要计算的实际大小
        public int LastXb, LastYb;  //同上
        public List<double> SubXsw = new List<double>();
        public List<double> SubYsw = new List<double>();  //用于储存每一文件的西南角坐标

        //输入污染源坐标（km）及带号
        public RunCALPUFF(double uSourceX, double uSourceY, double uElevation, int Zone, DateTime youStartTime, int p, int parts)
        {
            GEOFileName = "GEO.DAT";
            SURFFileName = "SURF.DAT";
            UPFileName = "UP.DAT";
            METDATAName = "CALMET.DAT";
            METListName = "CALMET.LST";
            CONDAT = "CONC.DAT";
            EmissionFile = "baemarb.DAT"; // 面源污染秒级文件

            StartTime = youStartTime;
            TimeSpan = 240;//min
            METTimeStep = 3600;//s
            PUFFTimeStep = 60;//s

            UTMZone = Zone;
            SourceX = uSourceX;
            SourceY = uSourceY;
            Elevation = uElevation;
            EmissionHeight = 1;
            EmissionRate = 4000.0;
            radius = 2.5E-04;  //不用
            RType = 2;  //不用
            ReNest = 1;
            GridCellsX = 50;
            GridCellsY = 50;
            GridSpace = 1;
            Xsw = SourceX - GridCellsX * GridSpace * 0.5;
            Ysw = SourceY - GridCellsY * GridSpace * 0.5;
            GridCellsZ = 10;
            Zface = new double[11] { 0.0, 20.0, 40.0, 80.0, 160.0, 320.0, 640.0, 1200.0, 2000.0, 3000.0, 4000.0 };

            SurfX = SourceX; //假设在污染源（233.142, 3473.661）5米半径的圆上
            SurfY = SourceY;
            SurfZ = 10.0;
            UpX = SourceX;
            UpY = SourceY;
            GeoPath = "";

            //高桥
            WindSpeed = 1.5; //m/s  
            WindDirection = 270; //degree
            CeilingHeight = 100; //云底高度 9999  hundreds of feet
            OpaqueCover = 5; //云量 9999  tenths
            Temperature = 7; //℃
            RelativeHumidity = 25;  //%
            Pressure = 1000;  // hpa百帕
            Precipitation = 0; //mm

            //宣汉
            /*WindSpeed = 1.5; //m/s  
            WindDirection = 45; //degree
            CeilingHeight = 100; //云底高度 9999  hundreds of feet
            OpaqueCover = 5; //云量 9999  tenths
            Temperature = 16.8; //℃
            RelativeHumidity = 25;  //%
            Pressure = 1000;  // hpa百帕
            Precipitation = 0; //mm*/

            SubX = 1;
            SubY = parts;  //同上，SubX必须等于1
            //DivideTask(1, 0);  //这里为了方便，预先执行划分任务
        }
        
        //重新计算格网左下角坐标
        public void ComputeSWCoor()
        {
            //Xsw = SourceX - GridCellsX * GridSpace * 0.5-0.05;
            //Ysw = SourceY - GridCellsY * GridSpace * 0.5-0.05;
            //这里以及上面初始化时去掉了刘茜师姐的-0.05，更好地跟calpuff view程序对应
            Xsw = SourceX - GridCellsX * GridSpace * 0.5;
            Ysw = SourceY - GridCellsY * GridSpace * 0.5;
        }

        /*
        //创建SURF.DAT
        private int ComputeSURFFile()
        {
            CreateSURFDAT newSurf = new CreateSURFDAT(StartTime, TimeSpan, SURFFileName);
            newSurf.ws = WindSpeed; //m/s  
            newSurf.wd = WindDirection; //degree
            newSurf.cehei = CeilingHeight; //云底高度 9999  hundreds of feet
            newSurf.Opa = OpaqueCover; //云量 9999  tenths
            newSurf.press = Pressure; //hpa百帕
            newSurf.hum = RelativeHumidity;  //%
            newSurf.temp = Temperature;  //℃ 
            newSurf.preci = Precipitation; //mm
            newSurf.CreateSURFFile();
            return 0;
        }

        //创建UP.DAT  //暂时不用
        private int ComputeUPFile()
        {
            CreateUPDAT newUp = new CreateUPDAT(StartTime, UPFileName);
            newUp.LowWindDirection = WindDirection;
            newUp.ws = WindSpeed;
            newUp.CreateUpAirFile();
            return 0;
        }

        //创建GEO.DAT
        private int ComputeGEOFile(double xsw, double ysw, int gridx, int gridy)
        {
            MakeGeoDAT newGeo = new MakeGeoDAT(UTMZone, xsw, ysw, gridx, gridy, GridSpace, GeoPath);
            newGeo.MakeGeo();
            GEOFileName = newGeo.GEODAT;
            return 0;
        }

        //创建/选择秒级面源文件  //暂时不用
        private int ComputeSourceFile()
        {
            double radius;
            switch (RType)
            {
                case 0: //10mm
                    radius = 0.01;
                    EmissionFile = "ptemarb.DAT"; // 点源污染秒级文件
                    CreatePointEmission newP1Emis = new CreatePointEmission(StartTime, TimeSpan, EmissionFile, SourceX, SourceY, UTMZone, Elevation, EmissionHeight, radius);
                    newP1Emis.CreateEmission();
                    break;
                case 1:  //50mm
                    radius = 0.05;
                    EmissionFile = "ptemarb.DAT"; // 点源污染秒级文件
                    CreatePointEmission newP2Emis = new CreatePointEmission(StartTime, TimeSpan, EmissionFile, SourceX, SourceY, UTMZone, Elevation, EmissionHeight, radius);
                    newP2Emis.CreateEmission();
                    break;
                case 2:  //0.5m
                    radius = 2.5E-04;
                    EmissionFile = "baemarb.DAT"; // 面源污染秒级文件
                    CreateEmissionFile newEmis = new CreateEmissionFile(StartTime, TimeSpan, EmissionFile, SourceX, SourceY, UTMZone, Elevation, EmissionHeight, radius);
                    newEmis.CreateEmission();
                    break;
                default:
                    break;
            }
            return 0;
        }
        

        //运行CALMET.EXE
        public int RunCALMETExe()
        {
            ComputeSURFFile();
            ComputeUPFile();
            ComputeGEOFile(Xsw, Ysw, GridCellsX, GridCellsY);
            //修改inp文件
            string path = "CALMET.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
            string strcontent = sr.ReadToEnd();
            string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
            MatchCollection mc = Regex.Matches(strcontent, regex);
            foreach (Match item in mc)
            {
                UpdateFileNameParameters(item, ref strcontent, "GEODAT", GeoPath + GEOFileName);
                UpdateFileNameParameters(item, ref strcontent, "SRFDAT", SURFFileName);
                UpdateFileNameParameters(item, ref strcontent, "METDAT", METDATAName);
                UpdateFileNameParameters(item, ref strcontent, "METLST", METListName);
                UpdateFileNameParameters(item, ref strcontent, "UPDAT", UPFileName);
                UpdateMETGridParameters(item, ref strcontent, Xsw, Ysw, GridCellsX, GridCellsY);
                UpdateTimeParameters(item, ref strcontent);
                UpdateMetStationParameters(item, ref strcontent);
            }
            sr.Close();
            StreamWriter sw1 = new StreamWriter(path, false);
            sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
            sw1.Close();
            fs.Close();

            Process CALMET = new Process();
            CALMET.StartInfo.FileName = "calmet_v6.5.0.exe";
            CALMET.Start();
            CALMET.WaitForExit();
            CALMET.Close();
            CALMET.Dispose();
            return 0;
        }
        */

        //任务划分
        public void DivideTask(int division, int buffer)
        {
            //先清空已有列表避免冲突
            SubXsw.Clear();
            SubYsw.Clear();
            //二维分割测试
            if (division == 2)
            {
                SubX = (int)Math.Sqrt(SubY);
                SubY = SubX;
            }
            //暂时采用向下取整，因为方便分割，避免16列分10块若每块2列则后面几块不够
            //实际效率最优时应向上取整，因为14列分10块若每块1列则最后一块有5列，拖慢速度，显然每块2列只用前7块更优
            SubGridX = GridCellsX / SubX;
            SubGridY = GridCellsY / SubY;
            ComputeSWCoor();
            double xsw = Xsw;
            double ysw = Ysw;
            //有缓冲区时西南角坐标需进行移动
            if (division == 2)
            {
                xsw = Xsw - buffer * GridSpace;
                ysw = Ysw - buffer * GridSpace;
            }
            //西南角坐标依次增加并记录
            for (int i = 0; i < SubX; i++)
            {
                SubXsw.Add(xsw);
                xsw += GridSpace * SubGridX;
            }
            for (int j = 0; j < SubY; j++)
            {
                SubYsw.Add(ysw);
                ysw += GridSpace * SubGridY;
            }
            LastX = GridCellsX - (SubX - 1) * SubGridX;
            LastY = GridCellsY - (SubY - 1) * SubGridY;
            //设置缓冲区
            if (division == 2)
            {
                SubGridXb = SubGridX + 2 * buffer;
                SubGridYb = SubGridY + 2 * buffer;
                LastXb = LastX + 2 * buffer;
                LastYb = LastY + 2 * buffer;
            }
        }

        /*
        //并行运行CALMET.EXE(单机版)
        public int ParallelRunCALMETExe1()
        {
            ComputeSURFFile();
            ComputeUPFile();
            for (int i = 0; i < SubX; i++)
            {
                for (int j = 0; j < SubY; j++)
                {
                    int subgridX, subgridY;
                    //用于处理带余数的最后一行（列）
                    if (i == SubX - 1)
                    {
                        subgridX = LastX;
                    }
                    else
                    {
                        subgridX = SubGridX;
                    }
                    if (j == SubY - 1)
                    {
                        subgridY = LastY;
                    }
                    else
                    {
                        subgridY = SubGridY;
                    }
                    ComputeGEOFile(SubXsw[i], SubYsw[j], subgridX, subgridY);
                    //修改inp文件
                    string path = "CALMET.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
                    FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
                    string strcontent = sr.ReadToEnd();
                    string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
                    MatchCollection mc = Regex.Matches(strcontent, regex);
                    foreach (Match item in mc)
                    {
                        UpdateFileNameParameters(item, ref strcontent, "GEODAT", GeoPath + GEOFileName);
                        UpdateFileNameParameters(item, ref strcontent, "SRFDAT", SURFFileName);
                        UpdateFileNameParameters(item, ref strcontent, "UPDAT", UPFileName);
                        UpdateFileNameParameters(item, ref strcontent, "METLST", "CALMET" + i.ToString() + j.ToString() + ".LST");
                        UpdateFileNameParameters(item, ref strcontent, "METDAT", "CALMET" + i.ToString() + j.ToString() + ".DAT");
                        UpdateMETGridParameters(item, ref strcontent, SubXsw[i], SubYsw[j], subgridX, subgridY);
                        UpdateTimeParameters(item, ref strcontent);
                        UpdateMetStationParameters(item, ref strcontent);
                    }
                    sr.Close();
                    StreamWriter sw1 = new StreamWriter(path, false);
                    sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
                    sw1.Close();
                    fs.Close();

                    Process CALMET = new Process();
                    CALMET.StartInfo.FileName = "calmet_v6.5.0.exe";
                    CALMET.Start();
                    CALMET.WaitForExit();
                    CALMET.Close();
                    CALMET.Dispose();
                }
            }
            
            return 0;
        }

        //并行运行CALMET.EXE(集群版)
        public int ParallelRunCALMETExe2(int p)
        {
            ComputeSURFFile();
            ComputeUPFile();
            for (int i = 0; i < SubX; i++)
            {
                int j = p;
                int subgridX, subgridY;
                //用于处理带余数的最后一行（列）
                if (i == SubX - 1)
                {
                    subgridX = LastX;
                }
                else
                {
                    subgridX = SubGridX;
                }
                if (j == SubY - 1)
                {
                    subgridY = LastY;
                }
                else
                {
                    subgridY = SubGridY;
                }
                ComputeGEOFile(SubXsw[i], SubYsw[j], subgridX, subgridY);
                //修改inp文件
                string path = "CALMET.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
                string strcontent = sr.ReadToEnd();
                string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
                MatchCollection mc = Regex.Matches(strcontent, regex);
                foreach (Match item in mc)
                {
                    UpdateFileNameParameters(item, ref strcontent, "GEODAT", GeoPath + GEOFileName);
                    UpdateFileNameParameters(item, ref strcontent, "SRFDAT", SURFFileName);
                    UpdateFileNameParameters(item, ref strcontent, "UPDAT", UPFileName);
                    UpdateFileNameParameters(item, ref strcontent, "METLST", "CALMET" + i.ToString() + j.ToString() + ".LST");
                    UpdateFileNameParameters(item, ref strcontent, "METDAT", "CALMET" + i.ToString() + j.ToString() + ".DAT");
                    UpdateMETGridParameters(item, ref strcontent, SubXsw[i], SubYsw[j], subgridX, subgridY);
                    UpdateTimeParameters(item, ref strcontent);
                    UpdateMetStationParameters(item, ref strcontent);
                }
                sr.Close();
                StreamWriter sw1 = new StreamWriter(path, false);
                sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
                sw1.Close();
                fs.Close();

                Process CALMET = new Process();
                CALMET.StartInfo.FileName = "calmet_v6.5.0.exe";
                CALMET.Start();
                CALMET.WaitForExit();
                CALMET.Close();
                CALMET.Dispose();
            }

            return 0;
        }
        */

        //合并CALMET.DAT
        public int MergeCalmet()
        {
            FileStream outfile = new FileStream("CALMET.DAT", FileMode.Create);
            FileStream infile1 = new FileStream("CALMET00.DAT", FileMode.Open);
            BinaryReader br;
            BinaryWriter bw = new BinaryWriter(outfile);
            br = new BinaryReader(infile1, Encoding.Default);
            byte[] B1 = new byte[] { 0x30, 0x30, 0x2e, 0x4c };
            byte[] B2 = new byte[] { 0x53, 0x54, 0x20, 0x21 };  //CALMET.LST文件名修正，目前采用固定字节查找
            byte[] B3 = new byte[] { 0x30, 0x30, 0x2e, 0x44 };
            byte[] B4 = new byte[] { 0x41, 0x54, 0x20, 0x21 };  //CALMET.DAT文件名修正
            //修正NY = *的那一行及下一行
            byte[] B5 = new byte[4];
            byte[] B7 = new byte[4];
            byte[] B8 = new byte[] { 0x4e, 0x59, 0x20, 0x3d };
            string str1 = GridCellsY.ToString();
            switch (GridCellsY.ToString().Length)
            {
                case 1:
                    char c1 = str1[0];
                    B5 = new byte[] { 0x20, (byte)c1, 0x20, 0x21 };
                    B7 = new byte[] { 0x20, 0x20, 0x20, 0x20 };
                    break;
                case 2:
                    char c2 = str1[0];
                    char c3 = str1[1];
                    B5 = new byte[] { 0x20, (byte)c2, (byte)c3, 0x20 };
                    B7 = new byte[] { 0x21, 0x20, 0x20, 0x20 };
                    break;
                case 3:
                    char c4 = str1[0];
                    char c5 = str1[1];
                    char c6 = str1[2];
                    B5 = new byte[] { 0x20, (byte)c4, (byte)c5, (byte)c6 };
                    B7 = new byte[] { 0x20, 0x21, 0x20, 0x20 };
                    break;
                default:
                    return -1;
            }
            byte[] B6 = new byte[4];
            string str2 = SubGridY.ToString();
            switch (SubGridY.ToString().Length)
            {
                case 1:
                    char c1 = str2[0];
                    B6 = new byte[] { 0x20, (byte)c1, 0x20, 0x21 };
                    break;
                case 2:
                    char c2 = str2[0];
                    char c3 = str2[1];
                    B6 = new byte[] { 0x20, (byte)c2, (byte)c3, 0x20 };
                    break;
                case 3:
                    char c4 = str2[0];
                    char c5 = str2[1];
                    char c6 = str2[2];
                    B6 = new byte[] { 0x20, (byte)c4, (byte)c5, (byte)c6 };
                    break;
                default:
                    return -1;
            }
            bool flag1 = false;  //因为NY = *的下一行开头也有可能发生变化
            //修改Y方向网格数的十六进制值 
            byte[] B9 = HexReverse(GridCellsX);
            byte[] B10 = HexReverse(GridCellsY);
            byte[] B11 = HexReverse(SubGridY);
            byte[] B12 = new byte[] { 0x00, 0x00, 0xc8, 0x42 };  //用于改Y方向网格数的十六进制值那一行的第四字节，但未找到规律，暂时不用
            while (br.BaseStream.Position < br.BaseStream.Length)  //修改表头部分
            {
                byte[] b1 = br.ReadBytes(4);
                byte[] b2 = br.ReadBytes(4);
                byte[] b3 = br.ReadBytes(4);
                byte[] b4 = br.ReadBytes(4);
                //修改输出文件名
                if (BytesEquals(b3, B1) && BytesEquals(b4, B2))
                {
                    b3 = new byte[] { 0x2e, 0x4c, 0x53, 0x54 };
                    b4 = new byte[] { 0x20, 0x21, 0x20, 0x20 };
                }
                if (BytesEquals(b2, B3) && BytesEquals(b3, B4))
                {
                    b2 = new byte[] { 0x2e, 0x44, 0x41, 0x54 };
                    b3 = new byte[] { 0x20, 0x21, 0x20, 0x20 };
                }
                //修改NY = *的那一行及下一行
                if (flag1)
                {
                    b1 = B7;
                    flag1 = false;
                }
                if (BytesEquals(b3, B8) && BytesEquals(b4, B6))
                {
                    b4 = B5;
                    flag1 = true;
                }
                //修改Y方向网格数的十六进制值
                //if (BytesEquals(b1, B9) && BytesEquals(b2, B11) && BytesEquals(b4, B12))  //第四字节未找到规律，暂时不用
                if (BytesEquals(b1, B9) && BytesEquals(b2, B11))
                {
                    b2 = B10;
                    bw.Write(b1);
                    bw.Write(b2);
                    bw.Write(b3);
                    bw.Write(b4);
                    break;  //修改到这里表头部分结束
                }
                bw.Write(b1);
                bw.Write(b2);
                bw.Write(b3);
                bw.Write(b4);
            }
            //计算标识符，推测标识符指每一气象变量数据块的长度，其中气象变量名占6字节
            int id1 = 4 * SubGridX * SubGridY + 24;  //前n-1个文件
            int id2 = 4 * SubGridX * LastY + 24;  //最后一个文件
            int id0 = 4 * GridCellsX * GridCellsY + 24;  //原文件
            byte[] B13 = HexReverse(id1);
            byte[] B14 = HexReverse(id2);
            byte[] B15 = HexReverse(id0);
            while (br.BaseStream.Position < br.BaseStream.Length)  //继续复制至第一个标识符
            {
                byte[] b1 = br.ReadBytes(4);
                if (BytesEquals(b1, B13))
                {
                    br.BaseStream.Position -= 4;
                    break;
                }
                bw.Write(b1);
            }
            //根据子文件的个数建立一个文件流列表
            List<FileStream> fileStreams = new List<FileStream>();
            List<BinaryReader> binaryReaders = new List<BinaryReader>();
            for(int i = 1; i < SubY; i++)
            {
                FileStream fs = new FileStream("CALMET0" + i.ToString() + ".DAT", FileMode.Open);
                fileStreams.Add(fs);
                binaryReaders.Add(new BinaryReader(fs, Encoding.Default));
            }
            //这些文件流也推进至第一个标识符
            for(int i = 1; i < SubY - 1; i++)  //中间n-2个文件
            {
                while (binaryReaders[i - 1].BaseStream.Position < binaryReaders[i - 1].BaseStream.Length)
                {
                    byte[] b1 = binaryReaders[i - 1].ReadBytes(4);
                    if (BytesEquals(b1, B13))
                    {
                        binaryReaders[i - 1].BaseStream.Position -= 4;
                        break;
                    }
                }
            }
            if (SubY >= 2)  //最后一个文件，分割时至少也要分成2份，否则没有意义
            {
                while (binaryReaders[SubY - 2].BaseStream.Position < binaryReaders[SubY - 2].BaseStream.Length)
                {
                    byte[] b1 = binaryReaders[SubY - 2].ReadBytes(4);
                    if (BytesEquals(b1, B14))
                    {
                        binaryReaders[SubY - 2].BaseStream.Position -= 4;
                        break;
                    }
                }
            }
            //猜测数据顺序遵循由西向东由南向北，即XY坐标均递增，因此先写最下方的00文件
            //先写开始标识符，然后循环读取每个文件无标识符的部分，然后写结束标识符
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                br.BaseStream.Position += 4;
                bw.Write(B15);  //写原文件的开头标识符
                for(int i = 1; i <= id1; i++)  //写第一个文件的数据块，包含气象变量名
                {
                    bw.Write(br.ReadByte());
                }
                br.BaseStream.Position += 4;
                for (int i = 1; i < SubY - 1; i++)  //写中间n-2个文件的数据块
                {
                    binaryReaders[i - 1].BaseStream.Position += 28;
                    for (int j = 1; j <= id1 - 24; j++)
                    {
                        bw.Write(binaryReaders[i - 1].ReadByte());
                    }
                    binaryReaders[i - 1].BaseStream.Position += 4;
                }
                if (SubY >= 2)  //写最后一个文件的数据块
                {
                    binaryReaders[SubY - 2].BaseStream.Position += 28;
                    for (int j = 1; j <= id2 - 24; j++)
                    {
                        bw.Write(binaryReaders[SubY - 2].ReadByte());
                    }
                    binaryReaders[SubY - 2].BaseStream.Position += 4;
                }
                bw.Write(B15);  //写原文件的结束标识符
            }  //一次循环结束后推进至下一个气象变量
            //完成后释放所用的读写器及文件流
            foreach(BinaryReader binaryReader in binaryReaders)
            {
                binaryReader.Close();
            }
            br.Close();
            bw.Close();
            return 0;
        }

        //用于判断字节数组是否相等
        private bool BytesEquals(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length) return false;
            if (b1 == null || b2 == null) return false;
            for (int i = 0; i < b1.Length; i++)
                if (b1[i] != b2[i])
                    return false;
            return true;
        }

        //用于calmet.dat中十六进制的反写（类似于十位个位 千位百位 十万位万位）
        //后续发现反写实际上是因为存储方式存在从高位到低位与从低位到高位的差异，该函数实际作用等同于BitConverter.ToInt32及GetBytes
        private byte[] HexReverse(int dec)
        {
            if (dec >= 0 && dec < 256)
            {
                byte b = (byte)dec;
                return new byte[] { b, 0x00, 0x00, 0x00 };
            }
            else if (dec >= 256 && dec < 65536)
            {
                byte b1 = (byte)(dec % 256);
                byte b2 = (byte)(dec / 256);
                return new byte[] { b1, b2, 0x00, 0x00 };
            }
            else if (dec >= 65536 && dec < 16777216)
            {
                byte b3 = (byte)((dec % 65536) % 256);
                byte b4 = (byte)((dec % 65536) / 256);
                byte b5 = (byte)(dec / 65536);
                return new byte[] { b3, b4, b5, 0x00 };
            }
            else  //超出范围
            {
                return new byte[] { 0x00, 0x00, 0x00, 0x00 };
            }
        }

        //二维分割合并CALMET.DAT
        public int MergeCalmet2D(int buffer)
        {
            FileStream outfile = new FileStream("CALMET.DAT", FileMode.Create);
            FileStream infile1 = new FileStream("CALMET00.DAT", FileMode.Open);
            BinaryReader br;
            BinaryWriter bw = new BinaryWriter(outfile);
            br = new BinaryReader(infile1, Encoding.Default);
            //功能1 文件名修正
            byte[] B1 = new byte[] { 0x30, 0x30, 0x2e, 0x4c };  //"00.L"
            byte[] B2 = new byte[] { 0x53, 0x54, 0x20, 0x21 };  //"ST !" CALMET.LST文件名修正，原文件名称必须为CALMET00.LST
            byte[] B3 = new byte[] { 0x30, 0x30, 0x2e, 0x44 };  //"00.D"
            byte[] B4 = new byte[] { 0x41, 0x54, 0x20, 0x21 };  //"AT !" CALMET.DAT文件名修正
            //功能2 修正NX和NY = *的那一行及下一行文本
            //NY：B8为第三字节，数据为第四字节，数据尾为下一行第一字节
            byte[] B5 = new byte[4];
            byte[] B7 = new byte[4];
            byte[] B8 = new byte[] { 0x4e, 0x59, 0x20, 0x3d };  //"NY =" 注意NY这里是59(59为大写Y的十六进制)
            string str1 = GridCellsY.ToString();
            switch (GridCellsY.ToString().Length)  //NY是几位数会影响字符串长度
            {
                case 1:
                    char c1 = str1[0];
                    B5 = new byte[] { 0x20, (byte)c1, 0x20, 0x21 };
                    B7 = new byte[] { 0x20, 0x20, 0x20, 0x20 };
                    break;
                case 2:
                    char c2 = str1[0];
                    char c3 = str1[1];
                    B5 = new byte[] { 0x20, (byte)c2, (byte)c3, 0x20 };
                    B7 = new byte[] { 0x21, 0x20, 0x20, 0x20 };
                    break;
                case 3:
                    char c4 = str1[0];
                    char c5 = str1[1];
                    char c6 = str1[2];
                    B5 = new byte[] { 0x20, (byte)c4, (byte)c5, (byte)c6 };
                    B7 = new byte[] { 0x20, 0x21, 0x20, 0x20 };
                    break;
                default:
                    return -1;
            }
            byte[] B6 = new byte[4];
            string str2 = SubGridYb.ToString();
            switch (SubGridYb.ToString().Length)
            {
                case 1:
                    char c1 = str2[0];
                    B6 = new byte[] { 0x20, (byte)c1, 0x20, 0x21 };
                    break;
                case 2:
                    char c2 = str2[0];
                    char c3 = str2[1];
                    B6 = new byte[] { 0x20, (byte)c2, (byte)c3, 0x20 };
                    break;
                case 3:
                    char c4 = str2[0];
                    char c5 = str2[1];
                    char c6 = str2[2];
                    B6 = new byte[] { 0x20, (byte)c4, (byte)c5, (byte)c6 };
                    break;
                default:
                    return -1;
            }
            bool flag1 = false;  //因为NY = *的下一行开头也有可能发生变化
            //NX：与NY不同，B8a为上一行的第四字节，数据为第一字节，数据尾为第二字节
            byte[] B5a = new byte[4];
            byte[] B7a = new byte[4];
            byte[] B8a = new byte[] { 0x4e, 0x58, 0x20, 0x3d };  //"NX =" 注意NX这里是58，与NY不同，但由于在上一行，暂时不用
            string str1a = GridCellsX.ToString();
            switch (GridCellsX.ToString().Length)  //NX是几位数会影响字符串长度
            {
                case 1:
                    char c1a = str1a[0];
                    B5a = new byte[] { 0x20, (byte)c1a, 0x20, 0x21 };
                    B7a = new byte[] { 0x20, 0x20, 0x20, 0x20 };
                    break;
                case 2:
                    char c2a = str1a[0];
                    char c3a = str1a[1];
                    B5a = new byte[] { 0x20, (byte)c2a, (byte)c3a, 0x20 };
                    B7a = new byte[] { 0x21, 0x20, 0x20, 0x20 };
                    break;
                case 3:
                    char c4a = str1a[0];
                    char c5a = str1a[1];
                    char c6a = str1a[2];
                    B5a = new byte[] { 0x20, (byte)c4a, (byte)c5a, (byte)c6a };
                    B7a = new byte[] { 0x20, 0x21, 0x20, 0x20 };
                    break;
                default:
                    return -1;
            }
            byte[] B6a = new byte[4];
            byte[] B6a2 = new byte[4];
            string str2a = SubGridXb.ToString();
            switch (SubGridXb.ToString().Length)
            {
                case 1:
                    char c1a = str2a[0];
                    B6a = new byte[] { 0x20, (byte)c1a, 0x20, 0x21 };
                    B6a2 = new byte[] { 0x20, 0x20, 0x20, 0x20 };
                    break;
                case 2:
                    char c2a = str2a[0];
                    char c3a = str2a[1];
                    B6a = new byte[] { 0x20, (byte)c2a, (byte)c3a, 0x20 };
                    B6a2 = new byte[] { 0x21, 0x20, 0x20, 0x20 };
                    break;
                case 3:
                    char c4a = str2a[0];
                    char c5a = str2a[1];
                    char c6a = str2a[2];
                    B6a = new byte[] { 0x20, (byte)c4a, (byte)c5a, (byte)c6a };
                    B6a2 = new byte[] { 0x20, 0x21, 0x20, 0x20 };
                    break;
                default:
                    return -1;
            }//NX的下一行不会发生变化，不需要flag
            //功能2.5 带缓冲区时，需要修改西南角坐标文本
            byte[] B16 = new byte[] { 0x58, 0x4f, 0x52, 0x49 };  //"XORI"
            byte[] B17 = new byte[] { 0x47, 0x4b, 0x4d, 0x20 };  //"GKM "
            bool flag2 = false;
            byte[] B18 = new byte[] { 0x59, 0x4f, 0x52, 0x49 };  //"YORI"
            //功能3 修改X和Y方向网格数的十六进制值(int类)
            byte[] B9 = HexReverse(GridCellsX);
            byte[] B10 = HexReverse(GridCellsY);
            byte[] B11a = HexReverse(SubGridXb);
            byte[] B11 = HexReverse(SubGridYb);
            byte[] B12a = new byte[] { 0x0a, 0x00, 0x00, 0x00 };  //该行的第三字节，推测为NZ(气象网格Z方向划分数，一般为10)的十六进制
            byte[] B12 = new byte[] { 0x00, 0x00, 0xc8, 0x42 };  //该行的第四字节，但未找到规律，暂时不用
            //功能4 带缓冲区时，需要修改西南角坐标的十六进制值(fortran中的real类，等同于float)及XSSTA等变量
            //XSSTA等变量为气象站相对于西南角的坐标，实际上CALPUFF中不需要，不修改程序也可运行
            //例如:50*50间距1000米的网格，气象站位于区域中心，则原文件读出浮点数25000，加2格缓冲区读出浮点数27000
            //float存储方式:1位符号位(正0负1)+8位指数位(二进制科学计数法的指数+127)+23位尾数(二进制科学计数法的小数)
            //探索时发现，功能3中所用函数实际等同于BitConverter.ToInt32及GetBytes，而功能4则可用ToSingle
            byte[] B19 = BitConverter.GetBytes((float)SubXsw[0] * 1000);  //单位由千米转为米
            byte[] B19a = BitConverter.GetBytes((float)SubYsw[0] * 1000);
            byte[] B20 = BitConverter.GetBytes((float)Xsw * 1000);
            byte[] B20a = BitConverter.GetBytes((float)Ysw * 1000);
            while (br.BaseStream.Position < br.BaseStream.Length)  //修改表头部分
            {
                byte[] b1 = br.ReadBytes(4);
                byte[] b2 = br.ReadBytes(4);
                byte[] b3 = br.ReadBytes(4);
                byte[] b4 = br.ReadBytes(4);
                //1 修改输出文件名
                if (BytesEquals(b3, B1) && BytesEquals(b4, B2))
                {
                    b3 = new byte[] { 0x2e, 0x4c, 0x53, 0x54 };  //".LST"  去掉00
                    b4 = new byte[] { 0x20, 0x21, 0x20, 0x20 };  //" !  "
                }
                if (BytesEquals(b2, B3) && BytesEquals(b3, B4))
                {
                    b2 = new byte[] { 0x2e, 0x44, 0x41, 0x54 };  //".DAT"
                    b3 = new byte[] { 0x20, 0x21, 0x20, 0x20 };  //" !  "
                }
                //2 修改NX = *的那一行及下一行，即如果查找到第一字节为B6a，第二字节为B6a2，则第一字节改为B5a，第二字节改为B7a
                if (BytesEquals(b1, B6a) && BytesEquals(b2, B6a2))
                {
                    b1 = B5a;
                    b2 = B7a;
                }//这里NX要放前面
                //2续 修改NY = *的那一行及下一行，即如果查找到第三字节为B8，第四字节为B6，则第四字节改为B5，下一行第一字节改为B7
                if (flag1)  //判断放上边避免当次循环就执行这一段，下同
                {
                    b1 = B7;
                    flag1 = false;
                }
                if (BytesEquals(b3, B8) && BytesEquals(b4, B6))
                {
                    b4 = B5;
                    flag1 = true;
                }
                //2.5 修改西南角坐标文本
                //X坐标
                if (flag2)
                {
                    string str = Encoding.UTF8.GetString(b1).TrimEnd('\0') + Encoding.UTF8.GetString(b2).TrimEnd('\0') +
                        Encoding.UTF8.GetString(b3).TrimEnd('\0') + Encoding.UTF8.GetString(b4).TrimEnd('\0');  //原来一行
                    string[] strs = str.Split(' ');  //截取数字部分
                    string s1 = (Convert.ToDouble(strs[1]) + buffer * GridSpace).ToString();  //数字部分加上缓冲区行数*格网间距
                    s1 = ("= " + s1 + " !").PadRight(16);  //右侧用空格补齐一行
                    byte[] bytes = Encoding.UTF8.GetBytes(s1);
                    bw.Write(bytes);
                    flag2 = false;
                    continue;
                }
                if (BytesEquals(b3, B16) && BytesEquals(b4, B17))
                {
                    flag2 = true;
                }
                //Y坐标
                if (BytesEquals(b2, B18) && BytesEquals(b3, B17))
                {
                    //可能两行同时需要修改，这里直接把下一行也读进来
                    byte[] b5 = br.ReadBytes(4);
                    byte[] b6 = br.ReadBytes(4);
                    byte[] b7 = br.ReadBytes(4);
                    byte[] b8 = br.ReadBytes(4);
                    string str = Encoding.UTF8.GetString(b4).TrimEnd('\0') + Encoding.UTF8.GetString(b5).TrimEnd('\0') +
                        Encoding.UTF8.GetString(b6).TrimEnd('\0') + Encoding.UTF8.GetString(b7).TrimEnd('\0') + Encoding.UTF8.GetString(b8).TrimEnd('\0');
                    string[] strs = str.Split(' ');  //截取数字部分
                    string s1 = (Convert.ToDouble(strs[1]) + buffer * GridSpace).ToString();  //数字部分加上缓冲区行数*格网间距
                    s1 = ("= " + s1 + " !").PadRight(20);  //右侧用空格补齐一行
                    byte[] bytes = Encoding.UTF8.GetBytes(s1);
                    bw.Write(b1);
                    bw.Write(b2);
                    bw.Write(b3);
                    bw.Write(bytes);
                    continue;
                }
                //3 修改XY方向网格数的十六进制值
                //if (BytesEquals(b1, B9) && BytesEquals(b2, B11) && BytesEquals(b4, B12))  //第四字节未找到规律，暂时不用
                if (BytesEquals(b1, B11a) && BytesEquals(b2, B11) && BytesEquals(b3, B12a)) 
                {
                    b1 = B9;
                    b2 = B10;
                    bw.Write(b1);
                    bw.Write(b2);
                    bw.Write(b3);
                    bw.Write(b4);
                    if (buffer == 0)
                    {
                        break;
                    }
                    else
                    {
                        continue;  //带缓冲区
                    }    
                }
                //4 修改西南角坐标十六进制值及XSSTA等变量
                if (BytesEquals(b1, B19) && BytesEquals(b2, B19a))
                {
                    b1 = B20;
                    b2 = B20a;
                    bw.Write(b1);
                    bw.Write(b2);
                    bw.Write(b3);
                    bw.Write(b4);
                    break;
                }
                //无需修改的照原样写入
                bw.Write(b1);
                bw.Write(b2);
                bw.Write(b3);
                bw.Write(b4);
            }
            //计算标识符，推测标识符指每一气象变量数据块的长度，其中气象变量名占6字节
            //一维时只有两种情况，前n-1个和最后一个；现在需要四种，正常行正常列，正常行最后一列，最后一行正常列，最后一行最后一列
            int id1 = 4 * SubGridXb * SubGridYb + 24;  //正常行正常列
            int id1a = 4 * LastXb * SubGridYb + 24;  //先从左往右，正常行最后一列
            int id2 = 4 * SubGridXb * LastYb + 24;  //再从上往下，最后一行正常列
            int id2a = 4 * LastXb * LastYb + 24;  //最后一行最后一列
            int id0 = 4 * GridCellsX * GridCellsY + 24;  //原文件
            byte[] B13 = HexReverse(id1);
            byte[] B13a = HexReverse(id1a);
            byte[] B14 = HexReverse(id2);
            byte[] B14a = HexReverse(id2a);
            byte[] B15 = HexReverse(id0);
            while (br.BaseStream.Position < br.BaseStream.Length)  //继续复制至第一个标识符
            {
                byte[] b1 = br.ReadBytes(4);
                if (BytesEquals(b1, B13))
                {
                    br.BaseStream.Position -= 4;
                    break;
                }
                bw.Write(b1);
            }
            //根据子文件的个数建立一个文件流列表
            List<FileStream> fileStreams = new List<FileStream>();
            List<List<BinaryReader>> binaryReaders = new List<List<BinaryReader>>();  //二维列表
            for(int i = 0; i < SubX; i++)
            {
                binaryReaders.Add(new List<BinaryReader>());
                for (int j = 0; j < SubY; j++)
                {
                    if (i == 0 && j == 0)
                    {
                        binaryReaders[0].Add(br);
                    }
                    else
                    {
                        FileStream fs = new FileStream("CALMET" + i.ToString() + j.ToString() + ".DAT", FileMode.Open);
                        fileStreams.Add(fs);
                        binaryReaders[i].Add(new BinaryReader(fs, Encoding.Default));  //使列表[i][j]对应CALMETij.DAT
                    }
                }
            }
            //这些文件流也推进至第一个标识符
            for (int j = 0; j < SubY; j++)   
            {
                for (int i = 0; i < SubX; i++)
                {
                    if (i == 0 && j == 0)  //00文件无需推进
                    {
                        continue;
                    }
                    else if (i == SubX - 1)
                    {
                        if (j == SubY - 1)  //最后一行最后一列
                        {
                            while (binaryReaders[i][j].BaseStream.Position < binaryReaders[i][j].BaseStream.Length)
                            {
                                byte[] b1 = binaryReaders[i][j].ReadBytes(4);
                                if (BytesEquals(b1, B14a))
                                {
                                    binaryReaders[i][j].BaseStream.Position -= 4;
                                    break;
                                }
                            }
                        }
                        else  //最后一行正常列
                        {
                            while (binaryReaders[i][j].BaseStream.Position < binaryReaders[i][j].BaseStream.Length)
                            {
                                byte[] b1 = binaryReaders[i][j].ReadBytes(4);
                                if (BytesEquals(b1, B13a))
                                {
                                    binaryReaders[i][j].BaseStream.Position -= 4;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (j == SubY - 1)  //正常行最后一列
                        {
                            while (binaryReaders[i][j].BaseStream.Position < binaryReaders[i][j].BaseStream.Length)
                            {
                                byte[] b1 = binaryReaders[i][j].ReadBytes(4);
                                if (BytesEquals(b1, B14))
                                {
                                    binaryReaders[i][j].BaseStream.Position -= 4;
                                    break;
                                }
                            }
                        }
                        else  //正常行正常列
                        {
                            while (binaryReaders[i][j].BaseStream.Position < binaryReaders[i][j].BaseStream.Length)
                            {
                                byte[] b1 = binaryReaders[i][j].ReadBytes(4);
                                if (BytesEquals(b1, B13))
                                {
                                    binaryReaders[i][j].BaseStream.Position -= 4;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            //猜测数据顺序遵循由西向东由南向北，即XY坐标均递增，因此先写左下方的00文件
            //先写开始标识符，然后循环读取每个文件无标识符的部分，然后写结束标识符
            /*与一维不同，要按00文件读第一行，10文件读第一行，00文件读第二行，10文件读第二行......
            00文件读最后一行，10文件读最后一行(到此相当于一维的00文件)，01文件读第一行，11文件读第一行......的顺序*/
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                //写原文件的开头标识符
                br.BaseStream.Position += 4;
                bw.Write(B15);  
                //写气象变量名
                for (int i = 1; i <= 24; i++)
                {
                    bw.Write(br.ReadByte());
                }
                //文件流推进
                for (int i = 0; i < SubX; i++)
                {
                    for (int j = 0; j < SubY; j++)
                    {
                        if (i == 0 && j == 0)
                        {
                            continue;
                        }
                        else
                        {
                            binaryReaders[i][j].BaseStream.Position += 28;
                        }
                    }
                }
                //循环读取数据块
                for(int j = 0; j < SubY; j++)
                {
                    if (j== SubY - 1)
                    {
                        //每个文件的数据块开头跳过buffer行缓冲区
                        for(int i = 0; i < SubX; i++)
                        {
                            if (i == SubX - 1)
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * LastXb;
                            }
                            else
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * SubGridXb;
                            }
                        }
                        for (int k2 = 0; k2 < LastY; k2++)
                        {
                            for (int i = 0; i < SubX; i++)
                            {
                                if (i == SubX - 1)
                                {
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;  //数据行开头和结尾跳过buffer格缓冲区
                                    bw.Write(binaryReaders[i][j].ReadBytes(4 * LastX));
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;
                                }
                                else
                                {
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;
                                    bw.Write(binaryReaders[i][j].ReadBytes(4 * SubGridX));
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;
                                }
                            }
                        }
                        //每个文件的数据块结尾跳过buffer行缓冲区
                        for (int i = 0; i < SubX; i++)
                        {
                            if (i == SubX - 1)
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * LastXb;
                            }
                            else
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * SubGridXb;
                            }
                        }
                    }
                    else
                    {
                        //每个文件的数据块开头跳过buffer行缓冲区
                        for (int i = 0; i < SubX; i++)
                        {
                            if (i == SubX - 1)
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * LastXb;
                            }
                            else
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * SubGridXb;
                            }
                        }
                        for (int k1 = 0; k1 < SubGridY; k1++)
                        {
                            for (int i = 0; i < SubX; i++)
                            {
                                if (i == SubX - 1)
                                {
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;  //数据行开头和结尾跳过buffer格缓冲区
                                    bw.Write(binaryReaders[i][j].ReadBytes(4 * LastX));
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;
                                }
                                else
                                {
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;
                                    bw.Write(binaryReaders[i][j].ReadBytes(4 * SubGridX));
                                    binaryReaders[i][j].BaseStream.Position += 4 * buffer;
                                }
                            }
                        }
                        //每个文件的数据块结尾跳过buffer行缓冲区
                        for (int i = 0; i < SubX; i++)
                        {
                            if (i == SubX - 1)
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * LastXb;
                            }
                            else
                            {
                                binaryReaders[i][j].BaseStream.Position += 4 * buffer * SubGridXb;
                            }
                        }
                    }
                }
                //写原文件的结束标识符
                for (int i = 0; i < SubX; i++)
                {
                    for (int j = 0; j < SubY; j++)
                    {
                        binaryReaders[i][j].BaseStream.Position += 4;
                    }
                }
                bw.Write(B15);  
            }  //一次循环结束后推进至下一个气象变量
            //完成后释放所用的读写器及文件流
            foreach (List<BinaryReader> binaryReader in binaryReaders)
            {
                foreach (BinaryReader bR in binaryReader)
                {
                    bR.Close();
                }
            }
            bw.Close();
            return 0;
        }

        //运行CALPUFF.EXE
        public int RunCALPUFFExe()
        {
            //ComputeSourceFile();
            //修改inp文件
            string path = "CALPUFF.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
            string strcontent = sr.ReadToEnd();
            string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
            MatchCollection mc = Regex.Matches(strcontent, regex);
            foreach (Match item in mc)
            {
                UpdateFileNameParameters(item, ref strcontent, "METDAT", METDATAName);
                UpdateFileNameParameters(item, ref strcontent, "CONDAT", CONDAT);

                //修改外部污染源文件，暂时不用，改为内部污染源
                /*
                if ( RType == 2 )//面源
                {
                    UpdateFileNameParameters(item, ref strcontent, "ARDAT", EmissionFile);
                    UpdateFileNameParameters(item, ref strcontent, "PTDAT", "");
                    UpdateFileNameParameters(item, ref strcontent, "NPT2", "0");
                    UpdateFileNameParameters(item, ref strcontent, "NAR2", "1");
                } 
                else//点源
                {
                    UpdateFileNameParameters(item, ref strcontent, "PTDAT", EmissionFile);
                    UpdateFileNameParameters(item, ref strcontent, "ARDAT", "");
                    UpdateFileNameParameters(item, ref strcontent, "NPT2", "1");
                    UpdateFileNameParameters(item, ref strcontent, "NAR2", "0");
                }
                */

                UpdateSourceParameters(item, ref strcontent);

                UpdateMETGridParameters(item, ref strcontent, Xsw, Ysw, GridCellsX, GridCellsY);  //气象网格，计算网格和采样网格还需修改？？？？？？？？？？？？？？？？？？？？？？？？？？
                UpdateFileNameParameters(item, ref strcontent, "MESHDN", ReNest.ToString());  //受体网格因子
                UpdateCALPUFFTimeParameters(item, ref strcontent);
            }
            sr.Close();
            StreamWriter sw1 = new StreamWriter(path, false);
            sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
            sw1.Close();
            fs.Close();

            Process calpuff = new Process();
            calpuff.StartInfo.FileName = "calpuff_v7.2.1.exe";
            calpuff.Start();
            calpuff.WaitForExit();
            calpuff.Close();
            calpuff.Dispose();
            return 0;
        }

        //运行CALPOST.EXE
        public int RunCALPOSTExe()
        {
            //修改inp文件
            string path = "CALPOST.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
            string strcontent = sr.ReadToEnd();
            string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
            MatchCollection mc = Regex.Matches(strcontent, regex);
            foreach (Match item in mc)
            {
                UpdateFileNameParameters(item, ref strcontent, "MODDAT", CONDAT);
                UpdateCALPUFFTimeParameters(item, ref strcontent);
            }
            sr.Close();
            StreamWriter sw1 = new StreamWriter(path, false);
            sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
            sw1.Close();
            fs.Close();

            Process calpost = new Process();
            calpost.StartInfo.FileName = "calpost_v7.1.0.exe";
            calpost.Start();
            calpost.WaitForExit();
            calpost.Close();
            calpost.Dispose();
            return 0;
        }

        //更新输入输出路径+文件名
        public void UpdateFileNameParameters(Match item, ref string strcontent, string FileVar, string FileVal)
        {
            //文件名
            if (item.Value.StartsWith("! " + FileVar + " ="))
            {
                strcontent = strcontent.Replace(item.Value, "! " + FileVar + " = " + FileVal + " !");
            }
        }

        //更新格网相关参数
        public void UpdateMETGridParameters(Match item, ref string strcontent, double xsw, double ysw, int gridx, int gridy)
        {
            //带号
            if (item.Value.StartsWith("! IUTMZN = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IUTMZN = " + UTMZone.ToString() + " !");
            }
            //左下角X坐标
            else if (item.Value.StartsWith("! XORIGKM = "))
            {
                strcontent = strcontent.Replace(item.Value, "! XORIGKM = " + xsw.ToString() + " !");
            }
            //左下角Y坐标
            else if (item.Value.StartsWith("! YORIGKM = "))
            {
                strcontent = strcontent.Replace(item.Value, "! YORIGKM = " + ysw.ToString() + " !");
            }
            //气象格网划分X
            else if (item.Value.StartsWith("! NX = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NX = " + gridx.ToString() + " !");
            }
            //气象格网划分Y
            else if (item.Value.StartsWith("! NY = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NY = " + gridy.ToString() + " !");
            }
            //采样格网划分X
            else if (item.Value.StartsWith("! IESAMP = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IESAMP = " + gridx.ToString() + " !");
            }
            //采样格网划分Y
            else if (item.Value.StartsWith("! JESAMP = "))
            {
                strcontent = strcontent.Replace(item.Value, "! JESAMP = " + gridy.ToString() + " !");
            }
            //计算格网划分X
            else if (item.Value.StartsWith("! IECOMP = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IECOMP = " + gridx.ToString() + " !");
            }
            //计算格网划分Y
            else if (item.Value.StartsWith("! JECOMP = "))
            {
                strcontent = strcontent.Replace(item.Value, "! JECOMP = " + gridy.ToString() + " !");
            }
            //格网间隔
            else if (item.Value.StartsWith("! DGRIDKM = "))
            {
                strcontent = strcontent.Replace(item.Value, "! DGRIDKM = " + GridSpace.ToString() + " !");
            }
            //格网划分Z
            else if (item.Value.StartsWith("! NZ = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NZ = " + GridCellsZ.ToString() + " !");
            }
            //格网划分Z，每层的高度
            else if (item.Value.StartsWith("! ZFACE = "))
            {
                string zfaceStr = "! ZFACE = " + Zface[0].ToString();
                for (int i = 1; i < GridCellsZ+1; i++)
                {
                    zfaceStr += "," + Zface[i].ToString();
                }
                zfaceStr += " !";
                strcontent = strcontent.Replace(item.Value, zfaceStr);
            }
            else
            {

            }
        }
        
        //更新计算时间相关参数   时，秒
        public void UpdateTimeParameters(Match item, ref string strcontent)
        {
            //开始年
            if (item.Value.StartsWith("! IBYR = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBYR = " + StartTime.Year.ToString() + " !");
            }
            //开始月
            else if (item.Value.StartsWith("! IBMO = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBMO = " + StartTime.Month.ToString() + " !");
            }
            //开始天
            else if (item.Value.StartsWith("! IBDY = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBDY = " + StartTime.Day.ToString() + " !");
            }
            //开始小时
            else if (item.Value.StartsWith("! IBHR = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBHR = " + StartTime.Hour.ToString() + " !");
            }
            //开始秒
            else if (item.Value.StartsWith("! IBSEC = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBSEC = " + (StartTime.Minute*60 +StartTime.Second).ToString() + " !");
            }
            //结束年
            else if (item.Value.StartsWith("! IEYR = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEYR = " + EndTime.Year.ToString() + " !");
            }
            //结束月
            else if (item.Value.StartsWith("! IEMO = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEMO = " + EndTime.Month.ToString() + " !");
            }
            //结束天
            else if (item.Value.StartsWith("! IEDY = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEDY = " + EndTime.Day.ToString() + " !");
            }
            //结束小时
            else if (item.Value.StartsWith("! IEHR = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEHR = " + EndTime.Hour.ToString() + " !");
            }
            //结束秒
            else if (item.Value.StartsWith("! IESEC = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IESEC = " + (EndTime.Minute * 60 + EndTime.Second).ToString() + " !");
            }
            //计算时间步长
            else if (item.Value.StartsWith("! NSECDT = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NSECDT = " + METTimeStep.ToString() + " !");
            }
            else
            {

            }
        }

        //更新计算时间相关参数  时，分，秒
        public void UpdateCALPUFFTimeParameters(Match item, ref string strcontent)
        {
            //开始年
            if (item.Value.StartsWith("! IBYR = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBYR = " + StartTime.Year.ToString() + " !");
            }
            //开始月
            else if (item.Value.StartsWith("! IBMO = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBMO = " + StartTime.Month.ToString() + " !");
            }
            //开始天
            else if (item.Value.StartsWith("! IBDY = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBDY = " + StartTime.Day.ToString() + " !");
            }
            //开始小时
            else if (item.Value.StartsWith("! IBHR = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBHR = " + StartTime.Hour.ToString() + " !");
            }
            //开始分钟
            else if (item.Value.StartsWith("! IBMIN = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBMIN = " + StartTime.Minute.ToString() + " !");
            }
            //开始秒
            else if (item.Value.StartsWith("! IBSEC = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IBSEC = " + StartTime.Second.ToString() + " !");
            }
            //结束年
            else if (item.Value.StartsWith("! IEYR = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEYR = " + EndTime.Year.ToString() + " !");
            }
            //结束月
            else if (item.Value.StartsWith("! IEMO = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEMO = " + EndTime.Month.ToString() + " !");
            }
            //结束天
            else if (item.Value.StartsWith("! IEDY = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEDY = " + EndTime.Day.ToString() + " !");
            }
            //结束小时
            else if (item.Value.StartsWith("! IEHR = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEHR = " + EndTime.Hour.ToString() + " !");
            }
            //结束分钟
            else if (item.Value.StartsWith("! IEMIN = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IEMIN = " + EndTime.Minute.ToString() + " !");
            }
            //结束秒
            else if (item.Value.StartsWith("! IESEC = "))
            {
                DateTime EndTime = StartTime.AddMinutes(TimeSpan);
                strcontent = strcontent.Replace(item.Value, "! IESEC = " + EndTime.Second.ToString() + " !");
            }
            //计算时间步长
            else if (item.Value.StartsWith("! NSECDT = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NSECDT = " + PUFFTimeStep.ToString() + " !");
            }
            else
            {

            }
        }

        //修改气象站相关数据
        public void UpdateMetStationParameters(Match item, ref string strcontent)
        {
            //地面站
            if (item.Value.StartsWith("! SS1  = "))
            {
                string surfStr = "! SS1  = 'S1'    50000       " + SurfX.ToString() + "     " + SurfY.ToString() + "    8    " + SurfZ.ToString() + "  !";
                strcontent = strcontent.Replace(item.Value, surfStr);
            }
            //探空站
            if (item.Value.StartsWith("! US1  = "))
            {
                string upStr = "! US1  = 'U1'   50000    " + UpX.ToString() + "   " +UpY.ToString() + "    8  !";
                strcontent = strcontent.Replace(item.Value, upStr);
            }
        }
        
        //修改内部污染源信息
        public void UpdateSourceParameters(Match item, ref string strcontent)
        {
            if (item.Value.StartsWith("! X = "))
            {
                string srcStr = "! X = " + SourceX.ToString() + ", " + SourceY.ToString() + ", " + EmissionHeight.ToString() + ", " + Elevation.ToString() 
                    + ", 3.0, 40.0, 300.0, 0.0, " + EmissionRate.ToString() + " !";
                strcontent = strcontent.Replace(item.Value, srcStr);
            }
        }
    }
}
