using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace dispersion
{
    class MakeGeoDAT  //暂时不用
    {
        //风场格网相关参数
        public int UTMZone;  //UTM投影带号，48/49
        public double Xsw, Ysw;  // 风场格网左下角（西南角）坐标，km, (230.642, 3471.161)
        public int GridCellsX, GridCellsY;  //格网数 (50,50)
        public double GridSpace; //格网间隔 km  0.1

        //输入输出数据的文件路径及文件名参数
        //public string DEMFile; //N31E107  N31E108写死
        public string TerrelFile; //TERREL.OUT
        public string GLAZASfile; //eausgs2_0la.img
        public string LULCfile;  //CTGPROC.DAT
        public string GEODAT; //GEO.DAT

        //文件路径（exe,inp,DAT）
        public string GeoPath;

        public MakeGeoDAT(int UTMZ, double X, double Y, int Xn, int Yn, double Gs, string youGeoPath)
        {
            UTMZone = UTMZ;
            Xsw = X;
            Ysw = Y;
            GridCellsX = Xn;
            GridCellsY = Yn;
            GridSpace = Gs;
            GeoPath = youGeoPath;
            TerrelFile = "TERREL.OUT";
            GLAZASfile = "eausgs2_0la.img";
            LULCfile = "CTGPROC.DAT";
            GEODAT = "GEO.DAT";
        }

        private void ProcessTerrel()
        {
            //修改inp文件
            string path = "TERREL.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
            string strcontent = sr.ReadToEnd();
            string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
            MatchCollection mc = Regex.Matches(strcontent, regex);
            foreach (Match item in mc)
            {
                UpdateFileNameParameters(item, ref strcontent, "OUTFIL", GeoPath + TerrelFile);
                //UpdateFileNameParameters(item, strcontent, "SRTM3", GeoPath + DEMFile);
                UpdateGridParameters(item, ref strcontent);
            }
            sr.Close();
            StreamWriter sw1 = new StreamWriter(path, false);
            sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
            sw1.Close();
            fs.Close();

            //调用terrel.exe
            Process terrel = new Process();
            terrel.StartInfo.FileName = GeoPath + "terrel_v7.0.0.exe";
            terrel.Start();
            terrel.WaitForExit();
            terrel.Close();
            terrel.Dispose();
        }

        private void ProcessLandUse()
        {
            //修改inp文件
            string path = "CTGPROC.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
            string strcontent = sr.ReadToEnd();
            string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
            MatchCollection mc = Regex.Matches(strcontent, regex);
            foreach (Match item in mc)
            {
                UpdateFileNameParameters(item, ref strcontent, "LUDAT", GeoPath + LULCfile);
                UpdateFileNameParameters(item, ref strcontent, "GLAZAS", GeoPath + GLAZASfile);
                UpdateGridParameters(item, ref strcontent);
            }
            sr.Close();
            StreamWriter sw1 = new StreamWriter(path, false);
            sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
            sw1.Close();
            fs.Close();

            //调用Ctgproc.exe
            Process Ctgproc = new Process();
            Ctgproc.StartInfo.FileName = GeoPath + "ctgproc_v7.0.0.exe";
            Ctgproc.Start();
            Ctgproc.WaitForExit();
            Ctgproc.Close();
            Ctgproc.Dispose();
        }

        public int MakeGeo()
        {
            //预处理数据
            ProcessTerrel();
            ProcessLandUse();
            //修改inp文件
            string path = "MAKEGEO.INP"; //@ 过滤字符串中转义符，路径必须是C:\\blabla\\blabla
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("utf-8"));
            string strcontent = sr.ReadToEnd();
            string regex = "!((.|\n)*?)!";//这是识别两个感叹号之间的正则表达式
            MatchCollection mc = Regex.Matches(strcontent, regex);
            foreach (Match item in mc)
            {
                UpdateFileNameParameters(item, ref strcontent, "LUDAT", GeoPath + LULCfile);
                UpdateFileNameParameters(item, ref strcontent, "TERRDAT", GeoPath + TerrelFile);
                UpdateFileNameParameters(item, ref strcontent, "GEODAT", GeoPath + GEODAT);
                UpdateGridParameters(item, ref strcontent);
            }
            sr.Close();
            StreamWriter sw1 = new StreamWriter(path, false);
            sw1.Write(strcontent, false);//是true的话 会把前面的用你写的串替换
            sw1.Close();
            fs.Close();
            //调用MAKEGEO.exe
            Process makegeo = new Process();
            makegeo.StartInfo.FileName = "makegeo_v3.2.exe";
            makegeo.Start();
            makegeo.WaitForExit();
            makegeo.Close();
            makegeo.Dispose();
            return 0;
        }

        //更新输入输出路径+文件名
        public void UpdateFileNameParameters(Match item, ref string strcontent, string FileVar, string FileVal)
        {
            //文件名
            if (item.Value.StartsWith("! " + FileVar))
            {
                strcontent = strcontent.Replace(item.Value, "! " + FileVar + " = " + FileVal + " !");
            }
        }

        //更新格网相关参数
        public void UpdateGridParameters(Match item, ref string strcontent)
        {
            //带号
            if (item.Value.StartsWith("! IUTMZN = "))
            {
                strcontent = strcontent.Replace(item.Value, "! IUTMZN = " + UTMZone.ToString() + " !");
            }
            //左下角X坐标
            else if (item.Value.StartsWith("! XREFKM = "))
            {
                strcontent = strcontent.Replace(item.Value, "! XREFKM = " + Xsw.ToString() + " !");
            }
            //左下角Y坐标
            else if (item.Value.StartsWith("! YREFKM = "))
            {
                strcontent = strcontent.Replace(item.Value, "! YREFKM = " + Ysw.ToString() + " !");
            }
            //格网划分X
            else if (item.Value.StartsWith("! NX = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NX = " + GridCellsX.ToString() + " !");
            }
            //格网划分Y
            else if (item.Value.StartsWith("! NY = "))
            {
                strcontent = strcontent.Replace(item.Value, "! NY = " + GridCellsY.ToString() + " !");
            }
            //格网间隔
            else if (item.Value.StartsWith("! DGRIDKM = "))
            {
                strcontent = strcontent.Replace(item.Value, "! DGRIDKM = " + GridSpace.ToString() + " !");
            }
            else
            {

            }
        }
        
    }
}
