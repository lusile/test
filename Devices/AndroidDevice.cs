﻿using Minicap;
using MiniTouch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Devices
{

    public class AndroidDevice {

        /// <summary>
        /// CPU abi版本
        /// </summary>
        public string abi { get; private set; }
        /// <summary>
        /// 系统SDK版本
        /// </summary>
        public string sdk { get; private set; }
        /// <summary>
        /// 手机型号
        /// </summary>
        public string model { get; private set; }

        /// <summary>
        /// 是否可用
        /// </summary>
        public bool visable { get; set; }



        private MinicapStream minicap;

        private MiniTouchStream minitouch;

        private Process minicapServerProcess;

        private Process minitouhServerProcess;

        public static string adbFile { get; set; }

        readonly string jarpath = "/data/local/tmp";

        public string deviceName { get; set; }

        /// <summary>
        /// 相关库文件的地址
        /// </summary>
        public string MiniLibPath { get; set; }


        /// <summary>
        /// 手机实际像素 宽
        /// </summary>
        public int width { get; private set; }
        /// <summary>
        /// 手机实际像素 高
        /// </summary>
        public int height { get; private set; }
        /// <summary>
        /// 图像输出宽度
        /// </summary>
        public int virtualwidth {  get; private set; }
        /// <summary>
        /// 图像输出高度
        /// </summary>
        public int virtualheight { get; private set; }

        /// <summary>
        /// 图像输出比
        /// </summary>
        public int minicapScale { get; set; }


        /// <summary>
        /// 触摸坐标实际比例
        /// </summary>
        public int minitouchScale { get; set; }


        readonly private string GET_SIZE_COMMAND = "shell dumpsys window windows | grep mScreenRect";
        readonly private string GET_DEVICE_ABI_COMMAND = "shell getprop ro.product.cpu.abi";
        readonly private string GET_DEVICE_SDK_COMMAND = "shell getprop ro.build.version.sdk";
        readonly private string GET_DEVICE_MODEL_COMMAND = "shell getprop ro.product.model";


        readonly private int orientation = 0;//旋转角度?


        static AndroidDevice() {
            adbFile = "adb";
        }


        public AndroidDevice(string _deviceName) {

            minicapScale = 3;//默认比例1:3

            minitouchScale = 3;//默认比例1:3

            deviceName = _deviceName;

            abi = adbByDevice(GET_DEVICE_ABI_COMMAND).Result.Trim();
            sdk = adbByDevice(GET_DEVICE_SDK_COMMAND).Result.Trim();
            model = adbByDevice(GET_DEVICE_MODEL_COMMAND).Result.Trim();


            var result = adbByDevice(GET_SIZE_COMMAND).Result;
            Match match = Regex.Match(result, @"\d{3,4}\,\d{3,4}");
            string size = match.Groups[0].Value;
            width = Convert.ToInt32(size.Split(',').ToArray()[0]);
            height = Convert.ToInt32(size.Split(',').ToArray()[1]);
            virtualwidth = width / minicapScale;
            virtualheight = height / minicapScale;


            

        }



        /// <summary>
        /// 为Minicap添加push 事件
        /// </summary>
        public MinicapEventHandler AddMinicapEvent (MinicapEventHandler e) {
            minicap.push += e;
            return e;
        }
        public void RemoveMinicapEvent(MinicapEventHandler e) {
            minicap.push -= e;
        }

        /// <summary>
        /// 为Minicap添加stop 事件
        /// </summary>
        public Action AddMinicapStopNotice(Action e) {
            minicap.stopNotice += e;
            return e;
        }

        public void RemoveMinicapStopNotice(Action e) {
            minicap.stopNotice -= e;
        }
        /// <summary>
        /// 开启相关的服务
        /// </summary>
        public void startShareServers() {
            this.InitMinicap();

            this.StartMinicapServer();

            this.InitMiniTouch();

            this.StartMiniTouchServer();

            Thread.Sleep(3000);

            this.StartMinicap();
            this.StartMiniTouch();

            visable = true;
        }

        public void InitMinicap() {

            adbByDevice("forward --remove tcp:1313").Wait();

            minicap = new MinicapStream();
            var MINICAP_FILE_PATH = Path.Combine(MiniLibPath, $"minicap/bin/{abi}/minicap");
            var MINICAPSO_FILE_PATH = Path.Combine(MiniLibPath, $"minicap/shared/android-{sdk}/{abi}/minicap.so");


            pushFile(MINICAP_FILE_PATH, jarpath);
            pushFile(MINICAPSO_FILE_PATH, jarpath);

            adbByDevice($"shell chmod 777 {jarpath}/minicap").Wait();


            //Shell(adbFile, "forward --remove-all").Wait();

            string command = $"forward tcp:{minicap.PORT} localabstract:minicap";
           
            adbByDevice(command).Wait();


        }


        public void StartMinicapServer() {

            string tmp = $"-s {deviceName} shell LD_LIBRARY_PATH={jarpath} /data/local/tmp/minicap -P {width}x{height}@{virtualwidth}x{virtualheight}/{orientation}";
            //string tmp = string.Format("shell LD_LIBRARY_PATH=/data/local/tmp /data/local/tmp/minicap -P 1080x1920@360x640/0");

            //启动server
            minicapServerProcess = StartProcess(adbFile, tmp);

        }

        /// <summary>
        /// 启动截图相关服务
        /// </summary>
        public void StartMinicap() {
            minicap.stopNotice += StopMinicap;
            minicap.Run();
        }

        public void StopMinicap() {
            visable = false;
            if (minicapServerProcess.HasExited) {
                try {
                    minicapServerProcess.Kill();
                } catch (Exception) {
                }
            }
            //顺便把MiniTouch 也关掉
            StopMiniTouch();
            
        }




        public void InitMiniTouch() {

            adbByDevice("forward --remove tcp:1111").Wait();

            minitouch = new MiniTouchStream();

            var MINITOUCH_FILE_PATH = Path.Combine(MiniLibPath, $"minitouch/{abi}/minitouch");

            pushFile(MINITOUCH_FILE_PATH, jarpath);

            adbByDevice($"shell chmod 777 {jarpath}/minitouch").Wait();


            string forward = string.Format("forward tcp:{0} localabstract:minitouch", minitouch.PORT);

            adbByDevice(forward).Wait();
            

        }

        public void StartMiniTouchServer() {
            string serverCommand = $"-s {deviceName} shell {jarpath}/minitouch";

            //启动server
            minitouhServerProcess = StartProcess(adbFile, serverCommand);
            
        }

        /// <summary>
        /// 启动点击相关的服务
        /// </summary>
        public void StartMiniTouch() {

            minitouch.Start();
        }

        public void StopMiniTouch() {
            minitouch.Stop();
            if (minitouhServerProcess.HasExited) {
                try {
                    minitouhServerProcess.Kill();
                } catch (Exception) {
                }
            }
        }


        public static List<AndroidDevice>  getAllDevices() {


            var result = Shell(adbFile, "devices").Result;

            var rts= new List<AndroidDevice>();

            foreach (Match mch in Regex.Matches(result, "\\n.*\\tdevice")) {
                string x = mch.Value;
                x = x.Substring(0, x.LastIndexOf("device")).Trim();

                AndroidDevice ad = new AndroidDevice(x);
                rts.Add(ad);
            }
            return rts;
        }






        /// <summary>
        /// 按下
        /// </summary>
        public void TouchDown(int X, int Y) {
            minitouch.TouchDown(X* minitouchScale, Y* minitouchScale);
        }

        /// <summary>
        /// 松开
        /// </summary>
        public void TouchUp() {
            minitouch.TouchUp();
        }

        /// <summary>
        /// 按键
        /// </summary>
        public void ClickKeycode(int key) {
            adbByDevice($"shell input keyevent {key}").Wait();
        }

        /// <summary>
        /// 移动
        /// </summary>
        public void TouchMove(int X, int Y) {
            minitouch.TouchMove(X* minitouchScale, Y* minitouchScale);
        }


      

        private Process StartProcess(string fileName, string arguments) {
            
            var psi = new ProcessStartInfo(fileName, arguments);
            psi.RedirectStandardOutput = false;


            return Process.Start(psi);
        }


      

        /// <summary>
        /// push文件到手机
        /// </summary>
        /// <param name="localpath">文件地址</param>
        /// <param name="devicepath">手机位置</param>
        private void pushFile(string localpath, string devicepath) {
            Shell(adbFile, $"-s {deviceName} push {localpath} {devicepath}").Wait();
        }



        private async Task<string> adbByDevice(string arguments) {
            return await Shell(adbFile, $"-s {deviceName} {arguments}");
        }


        private static async Task<string> Shell(string fileName, string arguments) {

            return await Task.Run(() => {
                try {
                    var psi = new ProcessStartInfo(fileName, arguments);
                    psi.RedirectStandardOutput = true;

                    using (var process = Process.Start(psi)) {
                        string rt = process.StandardOutput.ReadToEnd();
                        Console.WriteLine(rt);
                        return rt;
                    }
                } catch (Exception e) {

                    return e.StackTrace; ;
                }

            });
        }

    }
}
