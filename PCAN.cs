<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;
using System.Runtime.InteropServices;
using System.Configuration;

using Peak.Can.Basic;


namespace PCAN {
    public partial class PCAN : Form {

        #region 导入时间精度有关函数
        [DllImport("kernel32")]
        static extern uint GetTickCount();

        [DllImport("winmm")]
        static extern uint timeGetTime();

        [DllImport("winmm")]
        static extern void timeBeginPeriod(int t);

        [DllImport("winmm")]
        static extern void timeEndPeriod(int t);
        #endregion

        #region 设备相关变量
        //
        private byte myChannel = 81;
        //波特率
        private TPCANBaudrate myBaudrate;
        //设备类型
        private TPCANType myType = TPCANType.PCAN_TYPE_ISA;
        //端口号
        private uint ioPort = 0100;

        private ushort interrupt = 3;
        //初始化完成状态
        private bool initFlag;
        #endregion

        //private delegate 

        //存放接收报文的List
        private System.Collections.ArrayList myReadMsgList;
        //接收线程
        //private System.Threading.Thread readThread;
        
        private Dictionary<string, string> testDataDic;    //测试页中存放数字量和模拟量配置的字典
        private Dictionary<string, string> sendDataDic;         //测试页中存放发送数据的字典
        private Dictionary<string, string> liveDataDic;

        //发送线程
        private System.Threading.Thread manualSendThread;       //用于报文页中手动发送的线程
        private bool sendThreadFlag;                            //发送线程标志
        private List<SendMsgInfo> mySendMsgList;                //发送高低边输出报文的List

        //配置文件相关变量
        ConfigInfo configInfo;

        public PCAN() {
            InitializeComponent();
            initFlag = false;       //初始化状态默认为false
            InitConfig();
        }

        //软件启动时的初始化配置
        private void InitConfig() {
                     
            HardwareCB.SelectedIndex = 0;    //硬件默认选择USB
            BaudrateCB.SelectedIndex = 3;   //波特率默认选择250Kb/s
            ModeCb.SelectedIndex = 1;       //模式默认选择Live

            //软件启动时，发送和接收按钮无法点击
            SendBtn.Enabled = false; 
            ReadBtn.Enabled = false;
            //发送线程标志false
            sendThreadFlag = false;
        }

        //初始化按钮
        private void InitBtn_Click(object sender, EventArgs e) {
            TPCANStatus initStatus;
            //初始化PCAN
            initStatus = PCANBasic.Initialize(myChannel, myBaudrate, myType, ioPort, interrupt);
            /*点击初始化按钮，
             * 如果初始化成功，发送、接收、停止Button使能，初始化Button禁用，
             * 硬件、波特率、模式ComboBox禁用，
             * 报文页中显示报文的定时器使能，
             * 清空ReadLv，
             *高低边输出的Button颜色和文字全部初始化
             *如果初始化失败，在InfomationTb中报告状态*/
            if(initStatus != TPCANStatus.PCAN_ERROR_OK)
                InformationTb.Text += GetFormatedError(initStatus);
            else {                          
                SendBtn.Enabled = true;
                ReadBtn.Enabled = true;
                DisplayTimer.Enabled = true;
                StopBtn.Enabled = true;
                InitBtn.Enabled = false;
                HardwareCB.Enabled = false;
                BaudrateCB.Enabled = false;
                ModeCb.Enabled = false;
                ReadLV.Items.Clear();
                foreach(Control btn in this.HighOutPanel.Controls) {
                    if(btn is Button) {
                        btn.BackColor = Color.LightGray;
                        btn.Text = "OFF";
                    }
                }

                foreach(Control btn in this.LowOutPanel.Controls) {
                    if(btn is Button) {
                        btn.BackColor = Color.LightGray;
                        btn.Text = "OFF";
                    }
                }

                myReadMsgList = new System.Collections.ArrayList();
                mySendMsgList = new List<SendMsgInfo>();
                NewSendMsg("");

                initFlag = true;
                sendThreadFlag = true;
                StartSendBGWorker();
  
                //ReadTimer.Enabled = true;
                InformationTb.Text += "初始化完成!\r\n";
            }
            
        }

        //获取错误内容的方法
        private string GetFormatedError(TPCANStatus error) {
            StringBuilder strTemp = new StringBuilder(256);

            if(PCANBasic.GetErrorText(error, 0, strTemp) != TPCANStatus.PCAN_ERROR_OK)
                return string.Format("An error occurred. Error-code's text ({0:X}) couldn't be retrieved", error);
            else
                return strTemp.ToString();
        }

        //关闭窗口时执行的操作
        private void PCAN_FormClosed(object sender, FormClosedEventArgs e) {
            //发送线程标志位false，关闭PCAN
            sendThreadFlag = false;
            PCANBasic.Uninitialize(myChannel);
            
        }

        #region 报文页中的手动发送功能
        //手动发送标志位
        bool manualSendFlag = false;
        bool manualCtrlFlag = false;

        private void SendBtn_Click(object sender, EventArgs e) {
            //新建一条报文，从报文页的相关Textbox中获得参数
            //TPCANStatus sendResult;
            TPCANMsg msg = new TPCANMsg();
            msg.DATA = new byte[8];
            msg.LEN = 8;
            msg.ID = Convert.ToUInt32(SendIDTb.Text, 16);
            msg.MSGTYPE = (ExternedCb.Checked) ? TPCANMessageType.PCAN_MESSAGE_EXTENDED : TPCANMessageType.PCAN_MESSAGE_STANDARD;

            for(int i = 0; i < 8; i++) {
                TextBox dataX = (TextBox)this.Controls.Find("DataTb" + i, true)[0];
                msg.DATA[i] = Convert.ToByte(dataX.Text,16);
            }
            //如果手动发送的线程已经存在，先结束线程后重新建立线程
            if(manualSendFlag) {
                manualSendThread.Abort();
                manualSendThread.Join();
            }
            manualSendThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(MSTimeToManual));
            manualSendThread.Start(msg);
            manualSendFlag = true;
        }

        private void SendCtrlBtn_Click(object sender, EventArgs e) {
            //MessageBox.Show("请确认发送内容！", "警告", MessageBoxButtons.OKCancel);
            if(DialogResult.OK == MessageBox.Show("请确认发送内容！", "警告", MessageBoxButtons.OKCancel)) {

                int startBit;
                int bitLength;
                int offsetValue;
                double scaleValue;
                if(HVCommandChb.Checked) {
                    NewSendMsg("HVCommand");
                    ProcessSendMsg("HVCommand", 1);
                }

            }
            else
                Console.WriteLine(0);          
        }

        //用于手动发送功能的精度1ms方法
        private void MSTimeToManual(object msg) {
            //TPCANStatus sendStatus;
            TPCANMsg manualMessage = (TPCANMsg)msg;
            timeBeginPeriod(1);
            uint start = timeGetTime();
            //bool sent = false;
            while(manualSendFlag) {
                System.Threading.Thread.Sleep(1);
                uint now = timeGetTime();
                if((now - start) >= Convert.ToUInt32(CycleTimeTb.Text)) {
                    PCANBasic.Write(myChannel, ref manualMessage);
                    start = now;
                }
            }
            timeEndPeriod(1);
        }

        
        /************************ END *************************/
        #endregion

        #region 接收报文
        //从PCAN中读取报文的定时器
        private void ReadTimer_Tick(object sender, EventArgs e) {
            ReadMsg();
        }

        //读取方法
        private void ReadMsg() {
            TPCANStatus readStatus;
            TPCANMsg CANMsg;
            TPCANTimestamp CANTimeStamp;

            do {
                readStatus = PCANBasic.Read(myChannel, out CANMsg, out CANTimeStamp);
                if(readStatus == TPCANStatus.PCAN_ERROR_OK) {     


                    //处理接收的数据并显示到ReadLv中
                    ProcessReadMsg(CANMsg, CANTimeStamp);
                }               
            } while(!Convert.ToBoolean(readStatus & TPCANStatus.PCAN_ERROR_QRCVEMPTY));
        }

        //实时数据中接收数据的方法
        int count = 0;
        private void LiveDisplay(TPCANMsg canMsg, ConfigInfo configInfo) {
            byte[] getLiveData = canMsg.DATA;
            int startBit;
            int bitLength;
            int offsetValue;
            double scaleValue;
            string id = "H" + canMsg.ID.ToString("X");
            UInt64 liveData = 0;

            for(int i = 0; i < getLiveData.Length; i++) {
                //Console.WriteLine(getLiveData[i]);
                liveData = liveData + ((UInt64)getLiveData[i] << (i * 8));
                
            }

            #region BMS发送的报文 - BMSR
            if(Array.IndexOf(configInfo.BMSRID, id) != -1) {
                liveDataDic = configInfo.GetSection(id, "BMSR");
                string[] relayArray = { "主负", "主正", "附件", "快充", "加热", "慢充", "空调", "PTC", "上装", "主驱预充", "附件预充", "空调预充" };
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    //BMS Life
                    if(key == "LifeB2V") {
                        if(result % 2 == 0)
                            LifeB2VLb.BackColor = Color.Gray;
                        else
                            LifeB2VLb.BackColor = Color.Red;
                    }
                    else if(key == "LeakLife") {
                        if(result % 2 == 0)
                            LeakLifeLb.BackColor = Color.Gray;
                        else
                            LeakLifeLb.BackColor = Color.Red;
                    }
                    //SOC
                    else if(key == "SOC") {
                        SOCValueLb.Text = result.ToString() + "%";
                        SOCPb.Value = (int)result;
                    }
                    else if(key == "HVRelay") {
                        uint tempValue = (uint)result;
                        uint hv1 = tempValue & 0x0f;
                        uint hv2 = (tempValue >> 4) & 0x0f;
                        HVRelay1Lb.ForeColor = Color.Red;
                        switch(hv1) {
                            case 0:
                                HVRelay1Lb.Text = "正常";
                                HVRelay1Lb.ForeColor = Color.Chartreuse;
                                break;
                            case 1:
                                HVRelay1Lb.Text = "粘连";                               
                                break;
                            case 2:
                                HVRelay1Lb.Text = "不能吸合";
                                break;
                            default:
                                HVRelay1Lb.Text = "???";
                                break;
                        }
                        switch(hv2) {
                            case 0:
                                HVRelay2Lb.Text = "断开";
                                break;
                            case 1:
                                HVRelay2Lb.Text = "闭合";
                                break;
                            case 2:
                                HVRelay2Lb.Text = "预充";
                                break;
                            case 3:
                                HVRelay2Lb.Text = "快速放电";
                                break;
                            default:
                                HVRelay2Lb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "LeakFault") {
                        if(result == 85)
                            LeakFaultLb.BackColor = Color.Red;
                        else if(result == 170)
                            LeakFaultLb.BackColor = Color.Gray;
                        else
                            LeakFaultLb.BackColor = Color.Blue;
                    }
                    else if(key == "RelayStatus") {
                        count++;
                        if(count % 10 == 0) {
                            RelayStatusLb.Text = "";
                            uint tempValue = (uint)result;
                            for(int i = 0; i < 12; i++) {
                                if((tempValue & 0x03) == 1)
                                    RelayStatusLb.Text += relayArray[i] + "\r\n";
                                else if((tempValue & 0x03) == 2)
                                    RelayStatusLb.Text += relayArray[i] + "\r\n";
                                else
                                    RelayStatusLb.Text += relayArray[i] + " X" + "\r\n";
                                tempValue = tempValue >> 2;
                            }
                        }
                    }
                    else if(key == "BMSXCode") {
                        BMSFaultsTb.Text = result.ToString();
                    }
                    //状态
                    else if(BMSRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label BMSRLb = (Label)BMSRGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            BMSRLb.BackColor = Color.Red;
                        else
                            BMSRLb.BackColor = Color.Gray;
                    }
                    //数值
                    else {
                        if(BMSRGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox BMSRTb = (TextBox)BMSRGroup.Controls.Find(key + "Tb", true)[0];
                            BMSRTb.Text = result.ToString();
                        }
                    }

                }
            }
            #endregion

            #region VCU发送给BMS的报文 - BMSW
            else if(id == "HC019ED0") {
                liveDataDic = configInfo.GetSection(id, "BMSW");
                string[] auxArray = { "DCDC", "油泵", "气泵", "空调", "PTC", "除霜" };
                foreach(string key in liveDataDic.Keys) {
                    if((key == "CycleTime") || (key == "MsgType"))
                        continue;

                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);
                 
                    if(key == "LifeV2B") {
                        if(result % 2 == 1)
                            LifeV2BLb.BackColor = Color.Red;
                        else
                            LifeV2BLb.BackColor = Color.Gray;
                    }
                    else if(key == "SpeedV2B") {
                        //TextBox BMSWTb = (TextBox)BMSWGroup.Controls.Find(key + "Tb", true)[0];
                        SpeedV2BTb.Text = result.ToString();
                    }
                    else if(key == "LVCommand") {
                        if(result == 1)
                            LVCommandLb.BackColor = Color.Red;
                        else if(result == 0)
                            LVCommandLb.BackColor = Color.Gray;
                        else
                            LVCommandLb.Text = "???";
                    }
                    else if(key == "AUXCtrl") {
                        uint tempValue = (uint)result;
                        if(tempValue == 0) {
                            AUXCtrlTb.Text = "0";
                            AUXCtrlLb.BackColor = Color.Gray;
                        }
                        else {
                            AUXCtrlTb.Text = "";
                            for(int i = 0; i < 8; i++) {
                                if((tempValue & 0x01) == 1) {
                                    if(i <= 5) 
                                        AUXCtrlTb.Text += auxArray[i] + "\r\n";
                                    if(i == 7)
                                        AUXCtrlLb.BackColor = Color.Red;                                                                       
                                }
                                else
                                    AUXCtrlLb.BackColor = Color.Gray;
                                tempValue = tempValue >> 1;
                            }
                        }                           
                    }
                    else {
                        Label BMSWLb = (Label)BMSWGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 170)
                            BMSWLb.BackColor = Color.Red;
                        else if(result == 85)
                            BMSWLb.BackColor = Color.Gray;
                        else
                            BMSWLb.BackColor = Color.Blue;
                    }
                }
            }
            #endregion

            #region VCU发送给仪表的报文 - MeterW
            else if(Array.IndexOf(configInfo.MeterWID, id) != -1) {
                string[] modeArray = { "爬坡模式", "手动模式", "动力模式", "拖车模式", "跛行模式", "回收模式" };
                liveDataDic = configInfo.GetSection(id, "MeterW");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    //整车系统代码
                    if(key == "VehicleStatus") {
                        if(result == 0)
                            VehicleStatusLb.Text = "WAIT";
                        else if(result == 1)
                            VehicleStatusLb.Text = "READY";
                        else
                            VehicleStatusLb.Text = "???";
                    }
                    else if(key == "SysInterLock") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                SysInterLockLb.Text = "没有互锁";
                                break;
                            case 1:
                                SysInterLockLb.Text = "充电互锁";
                                break;
                            case 2:
                                SysInterLockLb.Text = "后舱门互锁";
                                break;
                            case 3:
                                SysInterLockLb.Text = "乘客门互锁";
                                break;                               
                        }
                    }
                    else if(key == "VCUSelfTest") {
                        VCUSelfTestLb.BackColor = Color.Gray;
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                VCUSelfTestLb.Text = "正在自检";
                                break;
                            case 1:
                                VCUSelfTestLb.Text = "自检成功";
                                break;
                            case 2:
                                VCUSelfTestLb.Text = "自检失败";
                                VCUSelfTestLb.BackColor = Color.Red;
                                break;
                            default:
                                VCUSelfTestLb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "LifeV2Y") {
                        if(result % 2 == 1)
                            LifeV2YLb.BackColor = Color.Red;
                        else
                            LifeV2YLb.BackColor = Color.Gray;
                    }
                    else if(key == "VehicleMode") {
                        if(count % 10 == 0) {
                            VehicleModeTb.Text = "";
                            uint tempValue = (uint)result;
                            for(int i = 0; i < 5; i++) {
                                if((tempValue & 0x01) == 1)
                                    VehicleModeTb.Text += modeArray[i] + "\r\n";
                                tempValue = tempValue >> 1;
                            }
                        }
                    }
                    //电机状态
                    else if(key == "MotorStatusV2Y") {
                        switch(Convert.ToInt16(result)) {
                            case 1:
                                MotorStatusV2YLb.Text = "驱动";
                                break;
                            case 2:
                                MotorStatusV2YLb.Text = "制动";
                                break;
                            case 4:
                                MotorStatusV2YLb.Text = "反转";
                                break;
                            default:
                                MotorStatusV2YLb.Text = "   ";
                                break;
                        }
                    }
                    //MCU自检
                    else if(key == "MCUSelfTest") {
                        MCUStatusLb.BackColor = Color.Gray;
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                MCUStatusLb.Text = "自检中";
                                break;
                            case 1:
                                MCUStatusLb.Text = "自检成功";
                                break;
                            case 2:
                                MCUStatusLb.Text = "自检失败";
                                MCUStatusLb.BackColor = Color.Red;
                                break;
                            default:
                                MCUStatusLb.Text = "???";
                                break;
                        }
                    }
                    //DCDC状态
                    else if((key == "DCDCStatusV2Y") || (key == "APStatusV2Y") || (key == "EPSStatus")) {
                        Label statusLb = (Label)MeterWGroup.Controls.Find(key + "Lb", true)[0];
                        statusLb.BackColor = Color.Gray;
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                statusLb.Text = "停止";
                                break;
                            case 1:
                                statusLb.Text = "运行";
                                break;
                            case 2:
                                statusLb.Text = "故障";
                                statusLb.BackColor = Color.Red;
                                break;
                            default:
                                statusLb.Text = "???";
                                break;
                        }
                    }
                    //档位                        
                    else if(key == "Stall") {
                        switch(Convert.ToInt16(result)) {
                            case -2:
                                StallLb.Text = "D";
                                break;
                            case -1:
                                StallLb.Text = "R";
                                break;
                            case 0:
                                StallLb.Text = "N";
                                break;
                            default:
                                StallLb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "SysFault") {
                        if(result != 0)
                            VCUFaultsTb.Text += "系统故障：" + result + "\r\n";
                    }
                    else if(key == "VCUFault") {
                        if(result != 0)
                            VCUFaultsTb.Text += "VCU故障：" + result + "\r\n";
                    }
                    //电制动、气泵、风扇、断高压请求、水泵
                    else if(MeterWGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label Meterlb = (Label)MeterWGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            Meterlb.BackColor = Color.Red;
                        else
                            Meterlb.BackColor = Color.Gray;
                    }
                    //数据
                    else {
                        if(MeterWGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MeterWTb = (TextBox)MeterWGroup.Controls.Find(key + "Tb", true)[0];
                            MeterWTb.Text = result.ToString();
                        }
                    }
                }
            }
            #endregion

            #region 仪表发送给VCU的报文
            else if((id == "H18F40117")) {
                liveDataDic = configInfo.GetSection(id, "MeterR");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(MeterRGroup.Controls.Find(key + "Tb", true).Length != 0) {
                        TextBox MeterRTb = (TextBox)MeterRGroup.Controls.Find(key + "Tb", true)[0];
                        MeterRTb.Text = result.ToString();
                        continue;
                    }
                    if(MeterRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label MeterRlb = (Label)MeterRGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            MeterRlb.BackColor = Color.Red;
                        else
                            MeterRlb.BackColor = Color.Gray;
                    }
                }
            }
            #endregion

            #region 多合一发送给VCU的报文
            else if(Array.IndexOf(configInfo.MultiRID, id) != -1) {
                liveDataDic = configInfo.GetSection(id, "MultiR");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if((key == "DCDCStatus") || (key == "APStatus") || (key == "OPStatus")) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label statusLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            statusLb.BackColor = Color.Gray;
                            switch(Convert.ToInt16(result)) {
                                case 0:
                                    statusLb.Text = "停止";
                                    break;
                                case 1:
                                    statusLb.Text = "运行中";
                                    break;
                                case 2:
                                    statusLb.Text = "故障";
                                    statusLb.BackColor = Color.Red;
                                    break;
                                default:
                                    statusLb.Text = "???";
                                    break;
                            }
                        }
                    }
                    else if(key == "OPCtrlMode") {
                        if(result == 0)
                            OPCtrlModeLb.Text = "内部控制";
                        else
                            OPCtrlModeLb.Text = "外部控制";
                    }
                    else if((key == "DCDCLife") || (key == "OPLife") || (key == "APLife")) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label lifeLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            if(result % 2 == 1)
                                lifeLb.BackColor = Color.Red;
                            else
                                lifeLb.BackColor = Color.Gray;
                        }
                    }
                    else if((key == "OPPT100X") || (key == "APPT100X")) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label ptLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            switch(Convert.ToInt16(result)) {
                                case 0:
                                    ptLb.Text = "无故障";
                                    break;
                                case 1:
                                    ptLb.Text = "开路";
                                    break;
                                case 2:
                                    ptLb.Text = "短路";
                                    break;
                                default:
                                    ptLb.Text = "???";
                                    break;
                            }
                        }
                    }
                    else if(key == "APXCode") {
                        if(result != 0)
                            MultiFaultsTb.Text += "气泵故障：" + result + "\r\n";
                    }
                    else if(key == "OPXCode") {
                        if(result != 0)
                            MultiFaultsTb.Text += "油泵故障：" + result + "\r\n";
                    }
                    else if(key == "DCDCXCode") {
                        if(result != 0)
                            MultiFaultsTb.Text += "DCDC故障：" + result + "\r\n";
                    }
                    else if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label multiLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            if(result == 1)
                                multiLb.BackColor = Color.Red;
                            else
                                multiLb.BackColor = Color.Gray;
                        }
                    }
                    else {
                        if(MultiRGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MultiRTb = (TextBox)MultiRGroup.Controls.Find(key + "Tb", true)[0];
                            MultiRTb.Text = result.ToString();
                        }
                    }
                }
            }
            #endregion

            #region VCU发送给多合一的报文
            else if(id == "HCF104A7") {
                liveDataDic = configInfo.GetSection(id, "MultiW");
                foreach(string key in liveDataDic.Keys) {
                    if((key == "CycleTime") || (key == "MsgType"))
                        continue;

                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(key == "EPSSpeed") {
                        EPSSpeedTb.Text = result.ToString();
                    }
                    else {
                        if(MultiWGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label MultiWLb = (Label)MultiWGroup.Controls.Find(key + "Lb", true)[0];
                            if(result == 1)
                                MultiWLb.BackColor = Color.Red;
                            else
                                MultiWLb.BackColor = Color.Gray;
                        }
                    }
                }
            }
            #endregion

            #region MCU发送给VCU的报文
            else if(Array.IndexOf(configInfo.MCURID, id) != -1) {
                liveDataDic = configInfo.GetSection(id, "MCUR");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(key == "MotorMode") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                MotorModeLb.Text = "自由模式";
                                break;
                            case 1:
                                MotorModeLb.Text = "扭矩模式";
                                break;
                            case 2:
                                MotorModeLb.Text = "转速模式";
                                break;
                            default:
                                MotorModeLb.Text = "???";
                                break;
                        }
                    }
                    else if((key == "MCUX1") || (key == "MCUX2") || (key == "MCUX3") || (key == "MCUX4")) {
                        if(result != 0)
                            MCUFaultsTb.Text += key + ":" + result + "\r\n";
                    }
                    else if(MCURGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label MCURLb = (Label)MCURGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            MCURLb.BackColor = Color.Red;
                        else
                            MCURLb.BackColor = Color.Gray;
                    }
                    else {
                        if(MCURGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MCURTb = (TextBox)MCURGroup.Controls.Find(key + "Tb", true)[0];
                            MCURTb.Text = result.ToString();
                        }
                    }
                }
            }
            #endregion

            #region VCU发送给MCU的报文
            else if(id == "HCF103D0") {
                liveDataDic = configInfo.GetSection(id, "MCUW");
                foreach(string key in liveDataDic.Keys) {
                    if((key == "CycleTime") || (key == "MsgType"))
                        continue;

                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(key == "CtrlMode") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                CtrlModeLb.Text = "自由模式";
                                break;
                            case 1:
                                CtrlModeLb.Text = "扭矩模式";
                                break;
                            case 2:
                                CtrlModeLb.Text = "转速模式";
                                break;
                            default:
                                CtrlModeLb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "StallV2M") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                StallV2MLb.Text = "N";
                                break;
                            case 1:
                                StallV2MLb.Text = "D";
                                break;
                            case 2:
                                StallV2MLb.Text = "R";
                                break;
                            default:
                                StallV2MLb.Text = "???";
                                break;
                        }
                    }
                    else if(MCUWGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label MCUWLb = (Label)MCUWGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            MCUWLb.BackColor = Color.Red;
                        else
                            MCUWLb.BackColor = Color.Gray;
                    }
                    else {
                        if(MCUWGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MCUWLb = (TextBox)MCUWGroup.Controls.Find(key + "Tb", true)[0];
                            MCUWLb.Text = result.ToString();
                        }
                    }
                } 
            }
            #endregion

            else
                Console.WriteLine(10);
        }

        private double Calculate(UInt64 liveData, int startBit, int bitLength, int offsetValue, double scaleValue) {
            double result;
            UInt64 tempData = 0;

            for(int j = 0; j < bitLength; j++) {
                tempData = (tempData << 1) + 1;
            }
            result = (liveData >> startBit) & tempData;
            result = result * scaleValue + offsetValue;
            
            return result;
        }

        //测试页中解析接收数据的方法
        private void TestDisplay(TPCANMsg canMsg, ConfigInfo configInfo) {
            byte[] getTestData = canMsg.DATA;
            int startBit;
            int bitLength;
            string id = "H" + canMsg.ID.ToString("X");

            //InformationTb.Text += getTestData[0].ToString();

            //解析数字量
            if(Array.IndexOf(configInfo.DigitalID, id) != -1) {
                testDataDic = configInfo.GetSection(id, "Digital");
                foreach(string key in testDataDic.Keys) {
                    string[] temp = testDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    //bitLength = Convert.ToInt32(temp[1]);
                    if(TestTp.Controls.Find(key + "LED", true).Length != 0) {
                        Label led = (Label)TestTp.Controls.Find(key + "LED", true)[0];

                        if(((getTestData[startBit / 8] >> (startBit % 8)) & 1) == 1)
                            led.BackColor = Color.Red;
                        else
                            led.BackColor = Color.Gray;
                    }
                }               
            
                //string[] keys = configFile.DigitalSection
             /*   for(int i = 1; i < keys.Length; i++) {
                    string[] temp = digital.Settings[keys[i]].Value.Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    //bitLength = Convert.ToInt32(temp[1]);
                    Label led = (Label)this.Controls.Find(keys[i] + "LED", true)[0];

                    if(((getData[startBit/8] >> (startBit%8)) & 1) == 1)
                        led.BackColor = Color.Red;
                    else
                        led.BackColor = Color.Green;                        

                }*/
                //解析模拟量
            }else if(Array.IndexOf(configInfo.AnalogID, id) != -1) {
                testDataDic = configInfo.GetSection(id, "Analog");
                foreach(string key in testDataDic.Keys) {
                    string[] temp = testDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    if(TestTp.Controls.Find(key + "Tb", true).Length != 0) {
                        TextBox pinValue = (TextBox)TestTp.Controls.Find(key + "Tb", true)[0];

                        if(bitLength > 8) {
                            pinValue.Text = (getTestData[startBit / 8] + ((getTestData[startBit / 8 + 1]) << 8)).ToString();
                        }
                        else
                            pinValue.Text = getTestData[startBit / 8].ToString();
                    }
                }

              /*  string[] keys = analog.Settings.AllKeys;
                for(int i = 1; i < keys.Length; i++) {
                    string[] temp = analog.Settings[keys[i]].Value.Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    TextBox pinValue = (TextBox)this.Controls.Find(keys[i] + "Tb", true)[0];

                    if(bitLength > 8) {                        
                        pinValue.Text = (getData[startBit / 8] + ((getData[startBit / 8 + 1]) << 8)).ToString();
                    }else
                        pinValue.Text = "0";
                }*/
            }
          
        }
        
        //处理接收报文的方法
        private void ProcessReadMsg(TPCANMsg canMsg, TPCANTimestamp canTimeStamp) {
            lock(myReadMsgList.SyncRoot) {
                //如果接收到的ID已经在myReadMsgList中，就更新myReadMsgList中该报文数据
                //如果不在myReadMsgList中，就将新ID添加进myReadMsgList
                foreach(MessageStatus msg in myReadMsgList) {
                    if((msg.CANMsg.ID == canMsg.ID) && (msg.CANMsg.MSGTYPE == canMsg.MSGTYPE)) {
                        msg.Update(canMsg, canTimeStamp);
                        return;
                    }
                }
                CreatMsgEntry(canMsg, canTimeStamp);

            }
        }

        //在myReadMsgList中创建新报文
        private void CreatMsgEntry(TPCANMsg newMsg, TPCANTimestamp timeStamp) {
            MessageStatus currentMsg;
            ListViewItem msgListView;

            lock(myReadMsgList.SyncRoot) {
                currentMsg = new MessageStatus(newMsg, timeStamp, ReadLV.Items.Count);

                myReadMsgList.Add(currentMsg);

                //msgListView = new ListViewItem(currentMsg.TypeString);
                msgListView = ReadLV.Items.Add(currentMsg.TypeString);
                msgListView.SubItems.Add(currentMsg.IDString);
                msgListView.SubItems.Add(newMsg.LEN.ToString());
                msgListView.SubItems.Add(currentMsg.DataString);
                msgListView.SubItems.Add(currentMsg.Count.ToString());
                msgListView.SubItems.Add(currentMsg.TimeString);
                //ReadLV.Items.Add(msgListView);
            }
        }

        //显示报文的定时器
        private void DisplayTimer_Tick(object sender, EventArgs e) {
            DisplayMsg();
        }

        //在Listview中显示报文
        private void DisplayMsg() {
            ListViewItem currentItem;

            lock(myReadMsgList.SyncRoot) {
                foreach(MessageStatus msgStatus in myReadMsgList) {
                    if(msgStatus.UpdatedFlag) {
                        msgStatus.UpdatedFlag = false;
                        currentItem = ReadLV.Items[msgStatus.Position];
                        currentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
                        currentItem.SubItems[3].Text = msgStatus.DataString;
                        currentItem.SubItems[4].Text = msgStatus.Count.ToString();
                        currentItem.SubItems[5].Text = msgStatus.TimeString;
                    }

                    if(ModeCb.SelectedIndex == 1) {
                        TestDisplay(msgStatus.CANMsg, configInfo);
                    }
                    if(ModeCb.SelectedIndex == 0) {
                        LiveDisplay(msgStatus.CANMsg, configInfo);
                    }
                }

                
            }
        }

        //读取Button点击事件
        private void ReadBtn_Click(object sender, EventArgs e) {
            /*if(readThread != null) {
                readThread.Abort();
                readThread.Join();
                readThread = null;
            }*/
            //使能读取报文的定时器
            ReadTimer.Enabled = true;
        }
        /*********************** END *****************************/
        #endregion

        //波特率选择
        private void BaudrateCB_SelectedIndexChanged(object sender, EventArgs e) {
            switch(BaudrateCB.SelectedIndex) {
                case 0:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_1M;
                    break;
                case 1:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_800K;
                    break;
                case 2:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_500K;
                    break;
                case 3:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_250K;
                    break;
                case 4:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_125K;
                    break;
                case 5:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_100K;
                    break;
            }
        }

        //硬件选择
        private void HardwareCB_SelectedIndexChanged(object sender, EventArgs e) {
            switch(HardwareCB.SelectedIndex) {
                case 0:
                    myType = TPCANType.PCAN_TYPE_ISA;
                    break;
            }
        }

        //重绘Groupbox边框
        private void Gb_Repaint(object sender, PaintEventArgs e) {
            GroupBox gb = new GroupBox();
            gb = (GroupBox)sender;
            e.Graphics.Clear(gb.BackColor);
            e.Graphics.DrawString(gb.Text, gb.Font, Brushes.Black, 10, 1);
            e.Graphics.DrawLine(Pens.Purple, 1, 7, 8, 7);
            e.Graphics.DrawLine(Pens.Purple, e.Graphics.MeasureString(gb.Text, gb.Font).Width + 8, 7, gb.Width - 2, 7);
            e.Graphics.DrawLine(Pens.Purple, 1, 7, 1, gb.Height - 2);
            e.Graphics.DrawLine(Pens.Purple, 1, gb.Height - 2, gb.Width - 2, gb.Height - 2);
            e.Graphics.DrawLine(Pens.Purple, gb.Width - 2, 7, gb.Width - 2, gb.Height - 2);
        }

        //模式选择
        private void ModeCb_SelectedIndexChanged(object sender, EventArgs e) {
            
            switch(ModeCb.SelectedIndex) {
                case 0:
                    TestTp.Parent = null;
                    LiveDataTp.Parent = ModeTc;
                    VCUDataTp.Parent = ModeTc;
                    VCUCtrlTp.Parent = null;
                    FaultsTp.Parent = ModeTc;
                    configInfo = new ConfigInfo();
                    break;
                case 1:
                    TestTp.Parent = ModeTc;
                    LiveDataTp.Parent = null;
                    VCUDataTp.Parent = null;
                    VCUCtrlTp.Parent = null;
                    FaultsTp.Parent = null;
                    configInfo = new ConfigInfo(); 
                    //Console.WriteLine(digital.Settings.AllKeys.Length);
                    break;
                case 2:
                    TestTp.Parent = null;
                    LiveDataTp.Parent = ModeTc;
                    VCUDataTp.Parent = ModeTc;
                    VCUCtrlTp.Parent = ModeTc;
                    FaultsTp.Parent = ModeTc;
                    configInfo = new ConfigInfo();
                    break;
            }
        }

        private void CtrlCheck_Changed(object sender, EventArgs e) {
                CheckBox enableCtrl = (CheckBox)sender;
                string enableName = enableCtrl.Name.Replace("Chb", "");

                foreach(SendMsgInfo msg in mySendMsgList) {
                    if(msg.ID == msg.GetID(enableName)) {
                        if(enableCtrl.Checked)
                            ProcessSendMsg(enableName, 1);
                        else
                            ProcessSendMsg(enableName, 0);
                    }      
                }
                NewSendMsg(enableName);
                ProcessSendMsg(enableName, 1);
        }

        private void MCUCtrlMode_Changed(object sender, EventArgs e) {
            foreach(SendMsgInfo msg in mySendMsgList) {
                if(msg.ID == msg.GetID("CtrlMode")) {
                    if(CtrlModeCb.SelectedIndex == 0)
                        ProcessSendMsg("CtrlMode", 1);
                    if(CtrlModeCb.SelectedIndex == 1)
                        ProcessSendMsg("CtrlMode", 2);
                    return;
                }
            }
            NewSendMsg("CtrlMode");
            if(CtrlModeCb.SelectedIndex == 0)
                ProcessSendMsg("CtrlMode", 1);
            if(CtrlModeCb.SelectedIndex == 1)
                ProcessSendMsg("CtrlMode", 2);
        }

        private void CtrlText_Changed(object sender, EventArgs e) {
            TextBox ctrlTb = (TextBox)sender;
            if(!char.IsNumber(Convert.ToChar(ctrlTb.Text.Substring(ctrlTb.Text.Length-1, 1)))) 
                MessageBox.Show("wrong");
                            
            string modeName = "Demand" + ctrlTb.Name.Replace("CtrlCb", "");

            foreach(SendMsgInfo msg in mySendMsgList) {
                if(msg.ID == msg.GetID(modeName)) {
                    ProcessSendMsg(modeName, Convert.ToUInt32(ctrlTb.Text));
                    return;
                }
            }
            NewSendMsg(modeName);
            ProcessSendMsg(modeName, Convert.ToUInt32(ctrlTb.Text));

        }

        private void NumOnly_KeyPress(object sender, KeyPressEventArgs e) {
            TextBox ctrlTb = (TextBox)sender;

         /*   if(!(char.IsNumber(e.KeyChar)) && (e.KeyChar != (char)8))
                e.Handled = true;*/
        }

        private void Value_Scroll(object sender, EventArgs e) {
            TrackBar valueTbar = (TrackBar)sender;
            int value = valueTbar.Value;
            string tbName = valueTbar.Name.Replace("Tbar", "CtrlCb");
            TextBox targetTb = (TextBox)VCUCtrlTp.Controls.Find(tbName, true)[0];
            targetTb.Text = value.ToString();
        }

        #region 发送高低边输出报文
        private void ONOFF_Click(object sender, EventArgs e) {
            if(initFlag) {
                Button pinSwitch = (Button)sender;
                if(pinSwitch.Enabled == false)
                    return;
                string pinName = pinSwitch.Name.Replace("Btn", "");

                /*如果发送线程标志位true
                 *更新mySendMsgList中报文并发送
                 *如果发送线程标志位false
                 *新建报文加入到mySendMsgList中再启动发送线程*/
                foreach(SendMsgInfo msg in mySendMsgList) {
                    if(msg.ID == msg.GetID(pinName)) {
                        if(pinSwitch.Text == "OFF") {
                            pinSwitch.Text = "ON";
                            pinSwitch.BackColor = Color.CornflowerBlue;
                            ProcessSendMsg(pinName, 1);
                        }
                        else {
                            pinSwitch.Text = "OFF";
                            pinSwitch.BackColor = Color.LightGray;
                            ProcessSendMsg(pinName, 0);
                        }
                        return;
                    }
                }

                NewSendMsg(pinName);
                if(pinSwitch.Text == "OFF") {
                    pinSwitch.Text = "ON";
                    pinSwitch.BackColor = Color.CornflowerBlue;
                    ProcessSendMsg(pinName, 1);
                }
                else {
                    pinSwitch.Text = "OFF";
                    pinSwitch.BackColor = Color.LightGray;
                    ProcessSendMsg(pinName, 0);

                        //StartSendBGWorker();
                        //sendThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(ptptime));
                        //sendThread.Start(100);
                   
                }
            }
            else {
                MessageBox.Show("Please init first!");
            }
        }
        #endregion

        //新建报文并加入到mySendMsgList中
        private void NewSendMsg(string name) {
            SendMsgInfo sendMsgInfo = new SendMsgInfo(name);
            mySendMsgList.Add(sendMsgInfo);
        }

        //更新发送报文的内容
        private void ProcessSendMsg(string name, uint value) {
            int startBit;
            int bitLength;
            int offsetValue;
            double scaleValue;
            foreach(SendMsgInfo msg in mySendMsgList) {
                if(msg.ID == msg.GetID(name)) {
                    sendDataDic = msg.GetSection(msg.ID);
                    string[] temp = sendDataDic[name].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    //Console.WriteLine(msg.CanMsg.DATA[1]);

                    msg.SetData(DivideData(msg.Data,value, startBit, bitLength, offsetValue, scaleValue));
                }
            }
        }

        private byte[] DivideData(byte[] data, uint value, int startBit, int bitLength, int offsetValue, double scaleValue) {
            UInt64 result = 0;
            UInt64 toZero = ~result;            
            //byte[] data = new byte[8];
            for(int i = 0; i < data.Length; i++) 
                result = result + ((UInt64)data[i] << (i * 8));
            
            toZero = toZero << bitLength;
            for(int i = 0; i < startBit; i++)
                toZero = (toZero << 1) + 1;

            result = result & toZero;
            value = (uint)((value - offsetValue) / scaleValue);
            result = result | ((UInt64)value << startBit);            
            for(int i = 0; i < 8; i++) {
                data[i] = (byte)(result & 0xff);
                result = result >> 8;
            }
            return data;
        }

        //测试页的发送线程
        private void StartSendBGWorker() {
            SendBGWorker.WorkerReportsProgress = true;
            SendBGWorker.WorkerSupportsCancellation = true;
            SendBGWorker.DoWork += new DoWorkEventHandler(SendBGWorker_DoWork);
            SendBGWorker.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(SendBGWorker_RunWorkerCompleted);
            SendBGWorker.ProgressChanged +=
                new ProgressChangedEventHandler(SendBGWorker_ProgressChanged);
            sendThreadFlag = true;
            SendBGWorker.RunWorkerAsync();
        }

        private void SendBGWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            
        }

        private void SendBGWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            
        }

        private void SendBGWorker_DoWork(object sender, DoWorkEventArgs e) {
            MSTime();
        }

        //用于测试页发送数据的精度1ms方法
        private void MSTime() {
            //TPCANStatus sendStatus;

            timeBeginPeriod(1);
            uint start = timeGetTime();
            uint count = 0;
            while(sendThreadFlag) {
                System.Threading.Thread.Sleep(1);
                uint now = timeGetTime();
                if((now - start) >= 1)
                    count++;
                    
                foreach(SendMsgInfo sendMsgInfo in mySendMsgList) {
                    if(count % sendMsgInfo.CycleTime == 0) {
                        SendMsgMethod(sendMsgInfo.CanMsg);
                    }
                }
                //Console.WriteLine(sendThreadFlag);
                start = now;
            }
            timeEndPeriod(1);
        }
        
        private void SendMsgMethod(TPCANMsg msgToSend) {
            TPCANStatus sendStatus;

            /*for(int i = 0; i < CANMsg.LEN; i++) {
                CANMsg.DATA[i] = Convert.ToByte("11", 16);
            }*/

            sendStatus = PCANBasic.Write(myChannel, ref msgToSend);
            if(sendStatus == TPCANStatus.PCAN_ERROR_OK) { }
            // MessageBox.Show("OK");
            else
                InformationTb.Text += GetFormatedError(sendStatus);
        }


        /*停止按钮*
         * 禁用显示和读取的定时器
         *所有发送线程标志位false
         *关闭PCAN
         *初始化Button使能，发送和读取Button禁用
         *硬件、波特率、模式ComboBox使能*/
        private void StopBtn_Click(object sender, EventArgs e) {
            DisplayTimer.Enabled = false;
            ReadTimer.Enabled = false;
            sendThreadFlag = false;
            manualSendFlag = false;
            PCANBasic.Uninitialize(myChannel);
            InitBtn.Enabled = true;
            StopBtn.Enabled = false;
            SendBtn.Enabled = false;
            ReadBtn.Enabled = false;
            //ClearBtn.Enabled = true;
            HardwareCB.Enabled = true;
            BaudrateCB.Enabled = true;
            ModeCb.Enabled = true;
            InformationTb.Text += "已停止!\r\n";
        }

        private void PCAN_Load(object sender, EventArgs e) {
            
        }

        //滚动条可以用
        private void Scroll_MouseMove(object sender, MouseEventArgs e) {
            TabPage nowTp = (TabPage)sender;
            nowTp.Focus();
        }

        //InformationTb始终显示到最后一行
        private void InformationTb_TextChanged(object sender, EventArgs e) {
            InformationTb.Select(InformationTb.Text.Length, 0);
            InformationTb.ScrollToCaret();
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;
using System.Runtime.InteropServices;
using System.Configuration;

using Peak.Can.Basic;


namespace PCAN {
    public partial class PCAN : Form {

        #region 导入时间精度有关函数
        [DllImport("kernel32")]
        static extern uint GetTickCount();

        [DllImport("winmm")]
        static extern uint timeGetTime();

        [DllImport("winmm")]
        static extern void timeBeginPeriod(int t);

        [DllImport("winmm")]
        static extern void timeEndPeriod(int t);
        #endregion

        #region 设备相关变量
        //
        private byte myChannel = 81;
        //波特率
        private TPCANBaudrate myBaudrate;
        //设备类型
        private TPCANType myType = TPCANType.PCAN_TYPE_ISA;
        //端口号
        private uint ioPort = 0100;

        private ushort interrupt = 3;
        //初始化完成状态
        private bool initFlag;
        #endregion

        //private delegate 

        //存放接收报文的List
        private System.Collections.ArrayList myReadMsgList;
        //接收线程
        //private System.Threading.Thread readThread;
        
        private Dictionary<string, string> testDataDic;    //测试页中存放数字量和模拟量配置的字典
        private Dictionary<string, string> sendDataDic;         //测试页中存放发送数据的字典
        private Dictionary<string, string> liveDataDic;

        //发送线程
        private System.Threading.Thread manualSendThread;       //用于报文页中手动发送的线程
        private bool sendThreadFlag;                            //发送线程标志
        private List<SendMsgInfo> mySendMsgList;                //发送高低边输出报文的List

        //配置文件相关变量
        ConfigInfo configInfo;

        public PCAN() {
            InitializeComponent();
            initFlag = false;       //初始化状态默认为false
            InitConfig();
        }

        //软件启动时的初始化配置
        private void InitConfig() {
                     
            HardwareCB.SelectedIndex = 0;    //硬件默认选择USB
            BaudrateCB.SelectedIndex = 3;   //波特率默认选择250Kb/s
            ModeCb.SelectedIndex = 1;       //模式默认选择Live

            //软件启动时，发送和接收按钮无法点击
            SendBtn.Enabled = false; 
            ReadBtn.Enabled = false;
            //发送线程标志false
            sendThreadFlag = false;
        }

        //初始化按钮
        private void InitBtn_Click(object sender, EventArgs e) {
            TPCANStatus initStatus;
            //初始化PCAN
            initStatus = PCANBasic.Initialize(myChannel, myBaudrate, myType, ioPort, interrupt);
            /*点击初始化按钮，
             * 如果初始化成功，发送、接收、停止Button使能，初始化Button禁用，
             * 硬件、波特率、模式ComboBox禁用，
             * 报文页中显示报文的定时器使能，
             * 清空ReadLv，
             *高低边输出的Button颜色和文字全部初始化
             *如果初始化失败，在InfomationTb中报告状态*/
            if(initStatus != TPCANStatus.PCAN_ERROR_OK)
                InformationTb.Text += GetFormatedError(initStatus);
            else {                          
                SendBtn.Enabled = true;
                ReadBtn.Enabled = true;
                DisplayTimer.Enabled = true;
                StopBtn.Enabled = true;
                InitBtn.Enabled = false;
                HardwareCB.Enabled = false;
                BaudrateCB.Enabled = false;
                ModeCb.Enabled = false;
                ReadLV.Items.Clear();
                foreach(Control btn in this.HighOutPanel.Controls) {
                    if(btn is Button) {
                        btn.BackColor = Color.LightGray;
                        btn.Text = "OFF";
                    }
                }

                foreach(Control btn in this.LowOutPanel.Controls) {
                    if(btn is Button) {
                        btn.BackColor = Color.LightGray;
                        btn.Text = "OFF";
                    }
                }

                myReadMsgList = new System.Collections.ArrayList();
                mySendMsgList = new List<SendMsgInfo>();
                NewSendMsg("");

                initFlag = true;
                sendThreadFlag = true;
                StartSendBGWorker();
  
                //ReadTimer.Enabled = true;
                InformationTb.Text += "初始化完成!\r\n";
            }
            
        }

        //获取错误内容的方法
        private string GetFormatedError(TPCANStatus error) {
            StringBuilder strTemp = new StringBuilder(256);

            if(PCANBasic.GetErrorText(error, 0, strTemp) != TPCANStatus.PCAN_ERROR_OK)
                return string.Format("An error occurred. Error-code's text ({0:X}) couldn't be retrieved", error);
            else
                return strTemp.ToString();
        }

        //关闭窗口时执行的操作
        private void PCAN_FormClosed(object sender, FormClosedEventArgs e) {
            //发送线程标志位false，关闭PCAN
            sendThreadFlag = false;
            PCANBasic.Uninitialize(myChannel);
            
        }

        #region 报文页中的手动发送功能
        //手动发送标志位
        bool manualSendFlag = false;
        bool manualCtrlFlag = false;

        private void SendBtn_Click(object sender, EventArgs e) {
            //新建一条报文，从报文页的相关Textbox中获得参数
            //TPCANStatus sendResult;
            TPCANMsg msg = new TPCANMsg();
            msg.DATA = new byte[8];
            msg.LEN = 8;
            msg.ID = Convert.ToUInt32(SendIDTb.Text, 16);
            msg.MSGTYPE = (ExternedCb.Checked) ? TPCANMessageType.PCAN_MESSAGE_EXTENDED : TPCANMessageType.PCAN_MESSAGE_STANDARD;

            for(int i = 0; i < 8; i++) {
                TextBox dataX = (TextBox)this.Controls.Find("DataTb" + i, true)[0];
                msg.DATA[i] = Convert.ToByte(dataX.Text,16);
            }
            //如果手动发送的线程已经存在，先结束线程后重新建立线程
            if(manualSendFlag) {
                manualSendThread.Abort();
                manualSendThread.Join();
            }
            manualSendThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(MSTimeToManual));
            manualSendThread.Start(msg);
            manualSendFlag = true;
        }

        private void SendCtrlBtn_Click(object sender, EventArgs e) {
            //MessageBox.Show("请确认发送内容！", "警告", MessageBoxButtons.OKCancel);
            if(DialogResult.OK == MessageBox.Show("请确认发送内容！", "警告", MessageBoxButtons.OKCancel)) {

                int startBit;
                int bitLength;
                int offsetValue;
                double scaleValue;
                if(HVCommandChb.Checked) {
                    NewSendMsg("HVCommand");
                    ProcessSendMsg("HVCommand", 1);
                }

            }
            else
                Console.WriteLine(0);          
        }

        //用于手动发送功能的精度1ms方法
        private void MSTimeToManual(object msg) {
            //TPCANStatus sendStatus;
            TPCANMsg manualMessage = (TPCANMsg)msg;
            timeBeginPeriod(1);
            uint start = timeGetTime();
            //bool sent = false;
            while(manualSendFlag) {
                System.Threading.Thread.Sleep(1);
                uint now = timeGetTime();
                if((now - start) >= Convert.ToUInt32(CycleTimeTb.Text)) {
                    PCANBasic.Write(myChannel, ref manualMessage);
                    start = now;
                }
            }
            timeEndPeriod(1);
        }

        
        /************************ END *************************/
        #endregion

        #region 接收报文
        //从PCAN中读取报文的定时器
        private void ReadTimer_Tick(object sender, EventArgs e) {
            ReadMsg();
        }

        //读取方法
        private void ReadMsg() {
            TPCANStatus readStatus;
            TPCANMsg CANMsg;
            TPCANTimestamp CANTimeStamp;

            do {
                readStatus = PCANBasic.Read(myChannel, out CANMsg, out CANTimeStamp);
                if(readStatus == TPCANStatus.PCAN_ERROR_OK) {     


                    //处理接收的数据并显示到ReadLv中
                    ProcessReadMsg(CANMsg, CANTimeStamp);
                }               
            } while(!Convert.ToBoolean(readStatus & TPCANStatus.PCAN_ERROR_QRCVEMPTY));
        }

        //实时数据中接收数据的方法
        int count = 0;
        private void LiveDisplay(TPCANMsg canMsg, ConfigInfo configInfo) {
            byte[] getLiveData = canMsg.DATA;
            int startBit;
            int bitLength;
            int offsetValue;
            double scaleValue;
            string id = "H" + canMsg.ID.ToString("X");
            UInt64 liveData = 0;

            for(int i = 0; i < getLiveData.Length; i++) {
                //Console.WriteLine(getLiveData[i]);
                liveData = liveData + ((UInt64)getLiveData[i] << (i * 8));
                
            }

            #region BMS发送的报文 - BMSR
            if(Array.IndexOf(configInfo.BMSRID, id) != -1) {
                liveDataDic = configInfo.GetSection(id, "BMSR");
                string[] relayArray = { "主负", "主正", "附件", "快充", "加热", "慢充", "空调", "PTC", "上装", "主驱预充", "附件预充", "空调预充" };
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    //BMS Life
                    if(key == "LifeB2V") {
                        if(result % 2 == 0)
                            LifeB2VLb.BackColor = Color.Gray;
                        else
                            LifeB2VLb.BackColor = Color.Red;
                    }
                    else if(key == "LeakLife") {
                        if(result % 2 == 0)
                            LeakLifeLb.BackColor = Color.Gray;
                        else
                            LeakLifeLb.BackColor = Color.Red;
                    }
                    //SOC
                    else if(key == "SOC") {
                        SOCValueLb.Text = result.ToString() + "%";
                        SOCPb.Value = (int)result;
                    }
                    else if(key == "HVRelay") {
                        uint tempValue = (uint)result;
                        uint hv1 = tempValue & 0x0f;
                        uint hv2 = (tempValue >> 4) & 0x0f;
                        HVRelay1Lb.ForeColor = Color.Red;
                        switch(hv1) {
                            case 0:
                                HVRelay1Lb.Text = "正常";
                                HVRelay1Lb.ForeColor = Color.Chartreuse;
                                break;
                            case 1:
                                HVRelay1Lb.Text = "粘连";                               
                                break;
                            case 2:
                                HVRelay1Lb.Text = "不能吸合";
                                break;
                            default:
                                HVRelay1Lb.Text = "???";
                                break;
                        }
                        switch(hv2) {
                            case 0:
                                HVRelay2Lb.Text = "断开";
                                break;
                            case 1:
                                HVRelay2Lb.Text = "闭合";
                                break;
                            case 2:
                                HVRelay2Lb.Text = "预充";
                                break;
                            case 3:
                                HVRelay2Lb.Text = "快速放电";
                                break;
                            default:
                                HVRelay2Lb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "LeakFault") {
                        if(result == 85)
                            LeakFaultLb.BackColor = Color.Red;
                        else if(result == 170)
                            LeakFaultLb.BackColor = Color.Gray;
                        else
                            LeakFaultLb.BackColor = Color.Blue;
                    }
                    else if(key == "RelayStatus") {
                        count++;
                        if(count % 10 == 0) {
                            RelayStatusLb.Text = "";
                            uint tempValue = (uint)result;
                            for(int i = 0; i < 12; i++) {
                                if((tempValue & 0x03) == 1)
                                    RelayStatusLb.Text += relayArray[i] + "\r\n";
                                else if((tempValue & 0x03) == 2)
                                    RelayStatusLb.Text += relayArray[i] + "\r\n";
                                else
                                    RelayStatusLb.Text += relayArray[i] + " X" + "\r\n";
                                tempValue = tempValue >> 2;
                            }
                        }
                    }
                    else if(key == "BMSXCode") {
                        BMSFaultsTb.Text = result.ToString();
                    }
                    //状态
                    else if(BMSRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label BMSRLb = (Label)BMSRGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            BMSRLb.BackColor = Color.Red;
                        else
                            BMSRLb.BackColor = Color.Gray;
                    }
                    //数值
                    else {
                        if(BMSRGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox BMSRTb = (TextBox)BMSRGroup.Controls.Find(key + "Tb", true)[0];
                            BMSRTb.Text = result.ToString();
                        }
                    }

                }
            }
            #endregion

            #region VCU发送给BMS的报文 - BMSW
            else if(id == "HC019ED0") {
                liveDataDic = configInfo.GetSection(id, "BMSW");
                string[] auxArray = { "DCDC", "油泵", "气泵", "空调", "PTC", "除霜" };
                foreach(string key in liveDataDic.Keys) {
                    if((key == "CycleTime") || (key == "MsgType"))
                        continue;

                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);
                 
                    if(key == "LifeV2B") {
                        if(result % 2 == 1)
                            LifeV2BLb.BackColor = Color.Red;
                        else
                            LifeV2BLb.BackColor = Color.Gray;
                    }
                    else if(key == "SpeedV2B") {
                        //TextBox BMSWTb = (TextBox)BMSWGroup.Controls.Find(key + "Tb", true)[0];
                        SpeedV2BTb.Text = result.ToString();
                    }
                    else if(key == "LVCommand") {
                        if(result == 1)
                            LVCommandLb.BackColor = Color.Red;
                        else if(result == 0)
                            LVCommandLb.BackColor = Color.Gray;
                        else
                            LVCommandLb.Text = "???";
                    }
                    else if(key == "AUXCtrl") {
                        uint tempValue = (uint)result;
                        if(tempValue == 0) {
                            AUXCtrlTb.Text = "0";
                            AUXCtrlLb.BackColor = Color.Gray;
                        }
                        else {
                            AUXCtrlTb.Text = "";
                            for(int i = 0; i < 8; i++) {
                                if((tempValue & 0x01) == 1) {
                                    if(i <= 5) 
                                        AUXCtrlTb.Text += auxArray[i] + "\r\n";
                                    if(i == 7)
                                        AUXCtrlLb.BackColor = Color.Red;                                                                       
                                }
                                else
                                    AUXCtrlLb.BackColor = Color.Gray;
                                tempValue = tempValue >> 1;
                            }
                        }                           
                    }
                    else {
                        Label BMSWLb = (Label)BMSWGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 170)
                            BMSWLb.BackColor = Color.Red;
                        else if(result == 85)
                            BMSWLb.BackColor = Color.Gray;
                        else
                            BMSWLb.BackColor = Color.Blue;
                    }
                }
            }
            #endregion

            #region VCU发送给仪表的报文 - MeterW
            else if(Array.IndexOf(configInfo.MeterWID, id) != -1) {
                string[] modeArray = { "爬坡模式", "手动模式", "动力模式", "拖车模式", "跛行模式", "回收模式" };
                liveDataDic = configInfo.GetSection(id, "MeterW");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    //整车系统代码
                    if(key == "VehicleStatus") {
                        if(result == 0)
                            VehicleStatusLb.Text = "WAIT";
                        else if(result == 1)
                            VehicleStatusLb.Text = "READY";
                        else
                            VehicleStatusLb.Text = "???";
                    }
                    else if(key == "SysInterLock") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                SysInterLockLb.Text = "没有互锁";
                                break;
                            case 1:
                                SysInterLockLb.Text = "充电互锁";
                                break;
                            case 2:
                                SysInterLockLb.Text = "后舱门互锁";
                                break;
                            case 3:
                                SysInterLockLb.Text = "乘客门互锁";
                                break;                               
                        }
                    }
                    else if(key == "VCUSelfTest") {
                        VCUSelfTestLb.BackColor = Color.Gray;
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                VCUSelfTestLb.Text = "正在自检";
                                break;
                            case 1:
                                VCUSelfTestLb.Text = "自检成功";
                                break;
                            case 2:
                                VCUSelfTestLb.Text = "自检失败";
                                VCUSelfTestLb.BackColor = Color.Red;
                                break;
                            default:
                                VCUSelfTestLb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "LifeV2Y") {
                        if(result % 2 == 1)
                            LifeV2YLb.BackColor = Color.Red;
                        else
                            LifeV2YLb.BackColor = Color.Gray;
                    }
                    else if(key == "VehicleMode") {
                        if(count % 10 == 0) {
                            VehicleModeTb.Text = "";
                            uint tempValue = (uint)result;
                            for(int i = 0; i < 5; i++) {
                                if((tempValue & 0x01) == 1)
                                    VehicleModeTb.Text += modeArray[i] + "\r\n";
                                tempValue = tempValue >> 1;
                            }
                        }
                    }
                    //电机状态
                    else if(key == "MotorStatusV2Y") {
                        switch(Convert.ToInt16(result)) {
                            case 1:
                                MotorStatusV2YLb.Text = "驱动";
                                break;
                            case 2:
                                MotorStatusV2YLb.Text = "制动";
                                break;
                            case 4:
                                MotorStatusV2YLb.Text = "反转";
                                break;
                            default:
                                MotorStatusV2YLb.Text = "   ";
                                break;
                        }
                    }
                    //MCU自检
                    else if(key == "MCUSelfTest") {
                        MCUStatusLb.BackColor = Color.Gray;
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                MCUStatusLb.Text = "自检中";
                                break;
                            case 1:
                                MCUStatusLb.Text = "自检成功";
                                break;
                            case 2:
                                MCUStatusLb.Text = "自检失败";
                                MCUStatusLb.BackColor = Color.Red;
                                break;
                            default:
                                MCUStatusLb.Text = "???";
                                break;
                        }
                    }
                    //DCDC状态
                    else if((key == "DCDCStatusV2Y") || (key == "APStatusV2Y") || (key == "EPSStatus")) {
                        Label statusLb = (Label)MeterWGroup.Controls.Find(key + "Lb", true)[0];
                        statusLb.BackColor = Color.Gray;
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                statusLb.Text = "停止";
                                break;
                            case 1:
                                statusLb.Text = "运行";
                                break;
                            case 2:
                                statusLb.Text = "故障";
                                statusLb.BackColor = Color.Red;
                                break;
                            default:
                                statusLb.Text = "???";
                                break;
                        }
                    }
                    //档位                        
                    else if(key == "Stall") {
                        switch(Convert.ToInt16(result)) {
                            case -2:
                                StallLb.Text = "D";
                                break;
                            case -1:
                                StallLb.Text = "R";
                                break;
                            case 0:
                                StallLb.Text = "N";
                                break;
                            default:
                                StallLb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "SysFault") {
                        if(result != 0)
                            VCUFaultsTb.Text += "系统故障：" + result + "\r\n";
                    }
                    else if(key == "VCUFault") {
                        if(result != 0)
                            VCUFaultsTb.Text += "VCU故障：" + result + "\r\n";
                    }
                    //电制动、气泵、风扇、断高压请求、水泵
                    else if(MeterWGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label Meterlb = (Label)MeterWGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            Meterlb.BackColor = Color.Red;
                        else
                            Meterlb.BackColor = Color.Gray;
                    }
                    //数据
                    else {
                        if(MeterWGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MeterWTb = (TextBox)MeterWGroup.Controls.Find(key + "Tb", true)[0];
                            MeterWTb.Text = result.ToString();
                        }
                    }
                }
            }
            #endregion

            #region 仪表发送给VCU的报文
            else if((id == "H18F40117")) {
                liveDataDic = configInfo.GetSection(id, "MeterR");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(MeterRGroup.Controls.Find(key + "Tb", true).Length != 0) {
                        TextBox MeterRTb = (TextBox)MeterRGroup.Controls.Find(key + "Tb", true)[0];
                        MeterRTb.Text = result.ToString();
                        continue;
                    }
                    if(MeterRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label MeterRlb = (Label)MeterRGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            MeterRlb.BackColor = Color.Red;
                        else
                            MeterRlb.BackColor = Color.Gray;
                    }
                }
            }
            #endregion

            #region 多合一发送给VCU的报文
            else if(Array.IndexOf(configInfo.MultiRID, id) != -1) {
                liveDataDic = configInfo.GetSection(id, "MultiR");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if((key == "DCDCStatus") || (key == "APStatus") || (key == "OPStatus")) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label statusLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            statusLb.BackColor = Color.Gray;
                            switch(Convert.ToInt16(result)) {
                                case 0:
                                    statusLb.Text = "停止";
                                    break;
                                case 1:
                                    statusLb.Text = "运行中";
                                    break;
                                case 2:
                                    statusLb.Text = "故障";
                                    statusLb.BackColor = Color.Red;
                                    break;
                                default:
                                    statusLb.Text = "???";
                                    break;
                            }
                        }
                    }
                    else if(key == "OPCtrlMode") {
                        if(result == 0)
                            OPCtrlModeLb.Text = "内部控制";
                        else
                            OPCtrlModeLb.Text = "外部控制";
                    }
                    else if((key == "DCDCLife") || (key == "OPLife") || (key == "APLife")) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label lifeLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            if(result % 2 == 1)
                                lifeLb.BackColor = Color.Red;
                            else
                                lifeLb.BackColor = Color.Gray;
                        }
                    }
                    else if((key == "OPPT100X") || (key == "APPT100X")) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label ptLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            switch(Convert.ToInt16(result)) {
                                case 0:
                                    ptLb.Text = "无故障";
                                    break;
                                case 1:
                                    ptLb.Text = "开路";
                                    break;
                                case 2:
                                    ptLb.Text = "短路";
                                    break;
                                default:
                                    ptLb.Text = "???";
                                    break;
                            }
                        }
                    }
                    else if(key == "APXCode") {
                        if(result != 0)
                            MultiFaultsTb.Text += "气泵故障：" + result + "\r\n";
                    }
                    else if(key == "OPXCode") {
                        if(result != 0)
                            MultiFaultsTb.Text += "油泵故障：" + result + "\r\n";
                    }
                    else if(key == "DCDCXCode") {
                        if(result != 0)
                            MultiFaultsTb.Text += "DCDC故障：" + result + "\r\n";
                    }
                    else if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        if(MultiRGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label multiLb = (Label)MultiRGroup.Controls.Find(key + "Lb", true)[0];
                            if(result == 1)
                                multiLb.BackColor = Color.Red;
                            else
                                multiLb.BackColor = Color.Gray;
                        }
                    }
                    else {
                        if(MultiRGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MultiRTb = (TextBox)MultiRGroup.Controls.Find(key + "Tb", true)[0];
                            MultiRTb.Text = result.ToString();
                        }
                    }
                }
            }
            #endregion

            #region VCU发送给多合一的报文
            else if(id == "HCF104A7") {
                liveDataDic = configInfo.GetSection(id, "MultiW");
                foreach(string key in liveDataDic.Keys) {
                    if((key == "CycleTime") || (key == "MsgType"))
                        continue;

                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(key == "EPSSpeed") {
                        EPSSpeedTb.Text = result.ToString();
                    }
                    else {
                        if(MultiWGroup.Controls.Find(key + "Lb", true).Length != 0) {
                            Label MultiWLb = (Label)MultiWGroup.Controls.Find(key + "Lb", true)[0];
                            if(result == 1)
                                MultiWLb.BackColor = Color.Red;
                            else
                                MultiWLb.BackColor = Color.Gray;
                        }
                    }
                }
            }
            #endregion

            #region MCU发送给VCU的报文
            else if(Array.IndexOf(configInfo.MCURID, id) != -1) {
                liveDataDic = configInfo.GetSection(id, "MCUR");
                foreach(string key in liveDataDic.Keys) {
                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(key == "MotorMode") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                MotorModeLb.Text = "自由模式";
                                break;
                            case 1:
                                MotorModeLb.Text = "扭矩模式";
                                break;
                            case 2:
                                MotorModeLb.Text = "转速模式";
                                break;
                            default:
                                MotorModeLb.Text = "???";
                                break;
                        }
                    }
                    else if((key == "MCUX1") || (key == "MCUX2") || (key == "MCUX3") || (key == "MCUX4")) {
                        if(result != 0)
                            MCUFaultsTb.Text += key + ":" + result + "\r\n";
                    }
                    else if(MCURGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label MCURLb = (Label)MCURGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            MCURLb.BackColor = Color.Red;
                        else
                            MCURLb.BackColor = Color.Gray;
                    }
                    else {
                        if(MCURGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MCURTb = (TextBox)MCURGroup.Controls.Find(key + "Tb", true)[0];
                            MCURTb.Text = result.ToString();
                        }
                    }
                }
            }
            #endregion

            #region VCU发送给MCU的报文
            else if(id == "HCF103D0") {
                liveDataDic = configInfo.GetSection(id, "MCUW");
                foreach(string key in liveDataDic.Keys) {
                    if((key == "CycleTime") || (key == "MsgType"))
                        continue;

                    string[] temp = liveDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    double result = Calculate(liveData, startBit, bitLength, offsetValue, scaleValue);

                    if(key == "CtrlMode") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                CtrlModeLb.Text = "自由模式";
                                break;
                            case 1:
                                CtrlModeLb.Text = "扭矩模式";
                                break;
                            case 2:
                                CtrlModeLb.Text = "转速模式";
                                break;
                            default:
                                CtrlModeLb.Text = "???";
                                break;
                        }
                    }
                    else if(key == "StallV2M") {
                        switch(Convert.ToInt16(result)) {
                            case 0:
                                StallV2MLb.Text = "N";
                                break;
                            case 1:
                                StallV2MLb.Text = "D";
                                break;
                            case 2:
                                StallV2MLb.Text = "R";
                                break;
                            default:
                                StallV2MLb.Text = "???";
                                break;
                        }
                    }
                    else if(MCUWGroup.Controls.Find(key + "Lb", true).Length != 0) {
                        Label MCUWLb = (Label)MCUWGroup.Controls.Find(key + "Lb", true)[0];
                        if(result == 1)
                            MCUWLb.BackColor = Color.Red;
                        else
                            MCUWLb.BackColor = Color.Gray;
                    }
                    else {
                        if(MCUWGroup.Controls.Find(key + "Tb", true).Length != 0) {
                            TextBox MCUWLb = (TextBox)MCUWGroup.Controls.Find(key + "Tb", true)[0];
                            MCUWLb.Text = result.ToString();
                        }
                    }
                } 
            }
            #endregion

            else
                Console.WriteLine(10);
        }

        private double Calculate(UInt64 liveData, int startBit, int bitLength, int offsetValue, double scaleValue) {
            double result;
            UInt64 tempData = 0;

            for(int j = 0; j < bitLength; j++) {
                tempData = (tempData << 1) + 1;
            }
            result = (liveData >> startBit) & tempData;
            result = result * scaleValue + offsetValue;
            
            return result;
        }

        //测试页中解析接收数据的方法
        private void TestDisplay(TPCANMsg canMsg, ConfigInfo configInfo) {
            byte[] getTestData = canMsg.DATA;
            int startBit;
            int bitLength;
            string id = "H" + canMsg.ID.ToString("X");

            //InformationTb.Text += getTestData[0].ToString();

            //解析数字量
            if(Array.IndexOf(configInfo.DigitalID, id) != -1) {
                testDataDic = configInfo.GetSection(id, "Digital");
                foreach(string key in testDataDic.Keys) {
                    string[] temp = testDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    //bitLength = Convert.ToInt32(temp[1]);
                    if(TestTp.Controls.Find(key + "LED", true).Length != 0) {
                        Label led = (Label)TestTp.Controls.Find(key + "LED", true)[0];

                        if(((getTestData[startBit / 8] >> (startBit % 8)) & 1) == 1)
                            led.BackColor = Color.Red;
                        else
                            led.BackColor = Color.Gray;
                    }
                }               
            
                //string[] keys = configFile.DigitalSection
             /*   for(int i = 1; i < keys.Length; i++) {
                    string[] temp = digital.Settings[keys[i]].Value.Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    //bitLength = Convert.ToInt32(temp[1]);
                    Label led = (Label)this.Controls.Find(keys[i] + "LED", true)[0];

                    if(((getData[startBit/8] >> (startBit%8)) & 1) == 1)
                        led.BackColor = Color.Red;
                    else
                        led.BackColor = Color.Green;                        

                }*/
                //解析模拟量
            }else if(Array.IndexOf(configInfo.AnalogID, id) != -1) {
                testDataDic = configInfo.GetSection(id, "Analog");
                foreach(string key in testDataDic.Keys) {
                    string[] temp = testDataDic[key].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    if(TestTp.Controls.Find(key + "Tb", true).Length != 0) {
                        TextBox pinValue = (TextBox)TestTp.Controls.Find(key + "Tb", true)[0];

                        if(bitLength > 8) {
                            pinValue.Text = (getTestData[startBit / 8] + ((getTestData[startBit / 8 + 1]) << 8)).ToString();
                        }
                        else
                            pinValue.Text = getTestData[startBit / 8].ToString();
                    }
                }

              /*  string[] keys = analog.Settings.AllKeys;
                for(int i = 1; i < keys.Length; i++) {
                    string[] temp = analog.Settings[keys[i]].Value.Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    TextBox pinValue = (TextBox)this.Controls.Find(keys[i] + "Tb", true)[0];

                    if(bitLength > 8) {                        
                        pinValue.Text = (getData[startBit / 8] + ((getData[startBit / 8 + 1]) << 8)).ToString();
                    }else
                        pinValue.Text = "0";
                }*/
            }
          
        }
        
        //处理接收报文的方法
        private void ProcessReadMsg(TPCANMsg canMsg, TPCANTimestamp canTimeStamp) {
            lock(myReadMsgList.SyncRoot) {
                //如果接收到的ID已经在myReadMsgList中，就更新myReadMsgList中该报文数据
                //如果不在myReadMsgList中，就将新ID添加进myReadMsgList
                foreach(MessageStatus msg in myReadMsgList) {
                    if((msg.CANMsg.ID == canMsg.ID) && (msg.CANMsg.MSGTYPE == canMsg.MSGTYPE)) {
                        msg.Update(canMsg, canTimeStamp);
                        return;
                    }
                }
                CreatMsgEntry(canMsg, canTimeStamp);

            }
        }

        //在myReadMsgList中创建新报文
        private void CreatMsgEntry(TPCANMsg newMsg, TPCANTimestamp timeStamp) {
            MessageStatus currentMsg;
            ListViewItem msgListView;

            lock(myReadMsgList.SyncRoot) {
                currentMsg = new MessageStatus(newMsg, timeStamp, ReadLV.Items.Count);

                myReadMsgList.Add(currentMsg);

                //msgListView = new ListViewItem(currentMsg.TypeString);
                msgListView = ReadLV.Items.Add(currentMsg.TypeString);
                msgListView.SubItems.Add(currentMsg.IDString);
                msgListView.SubItems.Add(newMsg.LEN.ToString());
                msgListView.SubItems.Add(currentMsg.DataString);
                msgListView.SubItems.Add(currentMsg.Count.ToString());
                msgListView.SubItems.Add(currentMsg.TimeString);
                //ReadLV.Items.Add(msgListView);
            }
        }

        //显示报文的定时器
        private void DisplayTimer_Tick(object sender, EventArgs e) {
            DisplayMsg();
        }

        //在Listview中显示报文
        private void DisplayMsg() {
            ListViewItem currentItem;

            lock(myReadMsgList.SyncRoot) {
                foreach(MessageStatus msgStatus in myReadMsgList) {
                    if(msgStatus.UpdatedFlag) {
                        msgStatus.UpdatedFlag = false;
                        currentItem = ReadLV.Items[msgStatus.Position];
                        currentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
                        currentItem.SubItems[3].Text = msgStatus.DataString;
                        currentItem.SubItems[4].Text = msgStatus.Count.ToString();
                        currentItem.SubItems[5].Text = msgStatus.TimeString;
                    }

                    if(ModeCb.SelectedIndex == 1) {
                        TestDisplay(msgStatus.CANMsg, configInfo);
                    }
                    if(ModeCb.SelectedIndex == 0) {
                        LiveDisplay(msgStatus.CANMsg, configInfo);
                    }
                }

                
            }
        }

        //读取Button点击事件
        private void ReadBtn_Click(object sender, EventArgs e) {
            /*if(readThread != null) {
                readThread.Abort();
                readThread.Join();
                readThread = null;
            }*/
            //使能读取报文的定时器
            ReadTimer.Enabled = true;
        }
        /*********************** END *****************************/
        #endregion

        //波特率选择
        private void BaudrateCB_SelectedIndexChanged(object sender, EventArgs e) {
            switch(BaudrateCB.SelectedIndex) {
                case 0:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_1M;
                    break;
                case 1:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_800K;
                    break;
                case 2:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_500K;
                    break;
                case 3:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_250K;
                    break;
                case 4:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_125K;
                    break;
                case 5:
                    myBaudrate = TPCANBaudrate.PCAN_BAUD_100K;
                    break;
            }
        }

        //硬件选择
        private void HardwareCB_SelectedIndexChanged(object sender, EventArgs e) {
            switch(HardwareCB.SelectedIndex) {
                case 0:
                    myType = TPCANType.PCAN_TYPE_ISA;
                    break;
            }
        }

        //重绘Groupbox边框
        private void Gb_Repaint(object sender, PaintEventArgs e) {
            GroupBox gb = new GroupBox();
            gb = (GroupBox)sender;
            e.Graphics.Clear(gb.BackColor);
            e.Graphics.DrawString(gb.Text, gb.Font, Brushes.Black, 10, 1);
            e.Graphics.DrawLine(Pens.Purple, 1, 7, 8, 7);
            e.Graphics.DrawLine(Pens.Purple, e.Graphics.MeasureString(gb.Text, gb.Font).Width + 8, 7, gb.Width - 2, 7);
            e.Graphics.DrawLine(Pens.Purple, 1, 7, 1, gb.Height - 2);
            e.Graphics.DrawLine(Pens.Purple, 1, gb.Height - 2, gb.Width - 2, gb.Height - 2);
            e.Graphics.DrawLine(Pens.Purple, gb.Width - 2, 7, gb.Width - 2, gb.Height - 2);
        }

        //模式选择
        private void ModeCb_SelectedIndexChanged(object sender, EventArgs e) {
            
            switch(ModeCb.SelectedIndex) {
                case 0:
                    TestTp.Parent = null;
                    LiveDataTp.Parent = ModeTc;
                    VCUDataTp.Parent = ModeTc;
                    VCUCtrlTp.Parent = null;
                    FaultsTp.Parent = ModeTc;
                    configInfo = new ConfigInfo();
                    break;
                case 1:
                    TestTp.Parent = ModeTc;
                    LiveDataTp.Parent = null;
                    VCUDataTp.Parent = null;
                    VCUCtrlTp.Parent = null;
                    FaultsTp.Parent = null;
                    configInfo = new ConfigInfo(); 
                    //Console.WriteLine(digital.Settings.AllKeys.Length);
                    break;
                case 2:
                    TestTp.Parent = null;
                    LiveDataTp.Parent = ModeTc;
                    VCUDataTp.Parent = ModeTc;
                    VCUCtrlTp.Parent = ModeTc;
                    FaultsTp.Parent = ModeTc;
                    configInfo = new ConfigInfo();
                    break;
            }
        }

        private void CtrlCheck_Changed(object sender, EventArgs e) {
                CheckBox enableCtrl = (CheckBox)sender;
                string enableName = enableCtrl.Name.Replace("Chb", "");

                foreach(SendMsgInfo msg in mySendMsgList) {
                    if(msg.ID == msg.GetID(enableName)) {
                        if(enableCtrl.Checked)
                            ProcessSendMsg(enableName, 1);
                        else
                            ProcessSendMsg(enableName, 0);
                    }      
                }
                NewSendMsg(enableName);
                ProcessSendMsg(enableName, 1);
        }

        private void MCUCtrlMode_Changed(object sender, EventArgs e) {
            foreach(SendMsgInfo msg in mySendMsgList) {
                if(msg.ID == msg.GetID("CtrlMode")) {
                    if(CtrlModeCb.SelectedIndex == 0)
                        ProcessSendMsg("CtrlMode", 1);
                    if(CtrlModeCb.SelectedIndex == 1)
                        ProcessSendMsg("CtrlMode", 2);
                    return;
                }
            }
            NewSendMsg("CtrlMode");
            if(CtrlModeCb.SelectedIndex == 0)
                ProcessSendMsg("CtrlMode", 1);
            if(CtrlModeCb.SelectedIndex == 1)
                ProcessSendMsg("CtrlMode", 2);
        }

        private void CtrlText_Changed(object sender, EventArgs e) {
            TextBox ctrlTb = (TextBox)sender;
            if(!char.IsNumber(Convert.ToChar(ctrlTb.Text.Substring(ctrlTb.Text.Length-1, 1)))) 
                MessageBox.Show("wrong");
                            
            string modeName = "Demand" + ctrlTb.Name.Replace("CtrlCb", "");

            foreach(SendMsgInfo msg in mySendMsgList) {
                if(msg.ID == msg.GetID(modeName)) {
                    ProcessSendMsg(modeName, Convert.ToUInt32(ctrlTb.Text));
                    return;
                }
            }
            NewSendMsg(modeName);
            ProcessSendMsg(modeName, Convert.ToUInt32(ctrlTb.Text));

        }

        private void NumOnly_KeyPress(object sender, KeyPressEventArgs e) {
            TextBox ctrlTb = (TextBox)sender;

         /*   if(!(char.IsNumber(e.KeyChar)) && (e.KeyChar != (char)8))
                e.Handled = true;*/
        }

        private void Value_Scroll(object sender, EventArgs e) {
            TrackBar valueTbar = (TrackBar)sender;
            int value = valueTbar.Value;
            string tbName = valueTbar.Name.Replace("Tbar", "CtrlCb");
            TextBox targetTb = (TextBox)VCUCtrlTp.Controls.Find(tbName, true)[0];
            targetTb.Text = value.ToString();
        }

        #region 发送高低边输出报文
        private void ONOFF_Click(object sender, EventArgs e) {
            if(initFlag) {
                Button pinSwitch = (Button)sender;
                if(pinSwitch.Enabled == false)
                    return;
                string pinName = pinSwitch.Name.Replace("Btn", "");

                /*如果发送线程标志位true
                 *更新mySendMsgList中报文并发送
                 *如果发送线程标志位false
                 *新建报文加入到mySendMsgList中再启动发送线程*/
                foreach(SendMsgInfo msg in mySendMsgList) {
                    if(msg.ID == msg.GetID(pinName)) {
                        if(pinSwitch.Text == "OFF") {
                            pinSwitch.Text = "ON";
                            pinSwitch.BackColor = Color.CornflowerBlue;
                            ProcessSendMsg(pinName, 1);
                        }
                        else {
                            pinSwitch.Text = "OFF";
                            pinSwitch.BackColor = Color.LightGray;
                            ProcessSendMsg(pinName, 0);
                        }
                        return;
                    }
                }

                NewSendMsg(pinName);
                if(pinSwitch.Text == "OFF") {
                    pinSwitch.Text = "ON";
                    pinSwitch.BackColor = Color.CornflowerBlue;
                    ProcessSendMsg(pinName, 1);
                }
                else {
                    pinSwitch.Text = "OFF";
                    pinSwitch.BackColor = Color.LightGray;
                    ProcessSendMsg(pinName, 0);

                        //StartSendBGWorker();
                        //sendThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(ptptime));
                        //sendThread.Start(100);
                   
                }
            }
            else {
                MessageBox.Show("Please init first!");
            }
        }
        #endregion

        //新建报文并加入到mySendMsgList中
        private void NewSendMsg(string name) {
            SendMsgInfo sendMsgInfo = new SendMsgInfo(name);
            mySendMsgList.Add(sendMsgInfo);
        }

        //更新发送报文的内容
        private void ProcessSendMsg(string name, uint value) {
            int startBit;
            int bitLength;
            int offsetValue;
            double scaleValue;
            foreach(SendMsgInfo msg in mySendMsgList) {
                if(msg.ID == msg.GetID(name)) {
                    sendDataDic = msg.GetSection(msg.ID);
                    string[] temp = sendDataDic[name].Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                    offsetValue = Convert.ToInt32(temp[2]);
                    scaleValue = Convert.ToDouble(temp[3]);
                    //Console.WriteLine(msg.CanMsg.DATA[1]);

                    msg.SetData(DivideData(msg.Data,value, startBit, bitLength, offsetValue, scaleValue));
                }
            }
        }

        private byte[] DivideData(byte[] data, uint value, int startBit, int bitLength, int offsetValue, double scaleValue) {
            UInt64 result = 0;
            UInt64 toZero = ~result;            
            //byte[] data = new byte[8];
            for(int i = 0; i < data.Length; i++) 
                result = result + ((UInt64)data[i] << (i * 8));
            
            toZero = toZero << bitLength;
            for(int i = 0; i < startBit; i++)
                toZero = (toZero << 1) + 1;

            result = result & toZero;
            value = (uint)((value - offsetValue) / scaleValue);
            result = result | ((UInt64)value << startBit);            
            for(int i = 0; i < 8; i++) {
                data[i] = (byte)(result & 0xff);
                result = result >> 8;
            }
            return data;
        }

        //测试页的发送线程
        private void StartSendBGWorker() {
            SendBGWorker.WorkerReportsProgress = true;
            SendBGWorker.WorkerSupportsCancellation = true;
            SendBGWorker.DoWork += new DoWorkEventHandler(SendBGWorker_DoWork);
            SendBGWorker.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(SendBGWorker_RunWorkerCompleted);
            SendBGWorker.ProgressChanged +=
                new ProgressChangedEventHandler(SendBGWorker_ProgressChanged);
            sendThreadFlag = true;
            SendBGWorker.RunWorkerAsync();
        }

        private void SendBGWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            
        }

        private void SendBGWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            
        }

        private void SendBGWorker_DoWork(object sender, DoWorkEventArgs e) {
            MSTime();
        }

        //用于测试页发送数据的精度1ms方法
        private void MSTime() {
            //TPCANStatus sendStatus;

            timeBeginPeriod(1);
            uint start = timeGetTime();
            uint count = 0;
            while(sendThreadFlag) {
                System.Threading.Thread.Sleep(1);
                uint now = timeGetTime();
                if((now - start) >= 1)
                    count++;
                    
                foreach(SendMsgInfo sendMsgInfo in mySendMsgList) {
                    if(count % sendMsgInfo.CycleTime == 0) {
                        SendMsgMethod(sendMsgInfo.CanMsg);
                    }
                }
                //Console.WriteLine(sendThreadFlag);
                start = now;
            }
            timeEndPeriod(1);
        }
        
        private void SendMsgMethod(TPCANMsg msgToSend) {
            TPCANStatus sendStatus;

            /*for(int i = 0; i < CANMsg.LEN; i++) {
                CANMsg.DATA[i] = Convert.ToByte("11", 16);
            }*/

            sendStatus = PCANBasic.Write(myChannel, ref msgToSend);
            if(sendStatus == TPCANStatus.PCAN_ERROR_OK) { }
            // MessageBox.Show("OK");
            else
                InformationTb.Text += GetFormatedError(sendStatus);
        }


        /*停止按钮*
         * 禁用显示和读取的定时器
         *所有发送线程标志位false
         *关闭PCAN
         *初始化Button使能，发送和读取Button禁用
         *硬件、波特率、模式ComboBox使能*/
        private void StopBtn_Click(object sender, EventArgs e) {
            DisplayTimer.Enabled = false;
            ReadTimer.Enabled = false;
            sendThreadFlag = false;
            manualSendFlag = false;
            PCANBasic.Uninitialize(myChannel);
            InitBtn.Enabled = true;
            StopBtn.Enabled = false;
            SendBtn.Enabled = false;
            ReadBtn.Enabled = false;
            //ClearBtn.Enabled = true;
            HardwareCB.Enabled = true;
            BaudrateCB.Enabled = true;
            ModeCb.Enabled = true;
            InformationTb.Text += "已停止!\r\n";
        }

        private void PCAN_Load(object sender, EventArgs e) {
            
        }

        //滚动条可以用
        private void Scroll_MouseMove(object sender, MouseEventArgs e) {
            TabPage nowTp = (TabPage)sender;
            nowTp.Focus();
        }

        //InformationTb始终显示到最后一行
        private void InformationTb_TextChanged(object sender, EventArgs e) {
            InformationTb.Select(InformationTb.Text.Length, 0);
            InformationTb.ScrollToCaret();
        }
    }
}
>>>>>>> first update
