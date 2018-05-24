<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peak.Can.Basic;

namespace PCAN {
    class MessageStatus {
        private TPCANMsg msg;
        private TPCANTimestamp timeStamp;
        private TPCANTimestamp oldTimeStamp;
        private int index;
        private int count;
        private bool showPeriod;
        private bool changedFlag;


        public MessageStatus(TPCANMsg canMsg,TPCANTimestamp canTimeStamp, int listIndex) {
            this.msg = canMsg;
            this.timeStamp = canTimeStamp;
            this.oldTimeStamp = canTimeStamp;
            this.index = listIndex;
            this.count = 1;
            this.showPeriod = true;
            this.changedFlag = false;
        }

        public void Update(TPCANMsg canMsg, TPCANTimestamp canTimeStamp) {
            this.msg = canMsg;
            this.oldTimeStamp = timeStamp;
            this.timeStamp = canTimeStamp;
            this.changedFlag = true;
            count++;
        }

        public TPCANMsg CANMsg { get { return msg; } }
        public TPCANTimestamp TimeStamp { get { return timeStamp; } }
        public int Position { get { return index; } }
        public string TypeString { get { return GetMsgTypeString(); } }

        private string GetMsgTypeString() {
            string getType = "";

            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
                getType = "EXTENDED";
            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_STANDARD) == TPCANMessageType.PCAN_MESSAGE_STANDARD)
                getType = "STANDARD";
            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
                getType += "/RTR";
            return getType;
        }

        public string IDString { get { return GetIDString(); } }

        private string GetIDString() {
            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
                return string.Format("{0:X8}h", msg.ID);
            else
                return string.Format("{0:X3}h", msg.ID);
        }

        public string DataString { get { return GetDataString(); } }

        private string GetDataString() {
            string getData = "";

            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
                return "Remote Request";
            else {
                for(int i = 0; i < msg.LEN; i++)
                    getData += string.Format("{0:x2} ", msg.DATA[i]);
            }

            return getData;
        }

        public int Count { get { return count; } }

        public bool ShowPeriod {
            get { return showPeriod; }
            set {
                if(showPeriod ^ value) {
                    showPeriod = value;
                    changedFlag = true;
                }
            }
        }

        public bool UpdatedFlag {
            get { return changedFlag; }
            set { changedFlag = value; }
        }

        public string TimeString { get { return GetTimeString(); } }

        private string GetTimeString() {
            double getTime;

            getTime = timeStamp.millis + (timeStamp.micros / 1000.0);
            if(showPeriod)
                getTime -= (oldTimeStamp.millis + (oldTimeStamp.micros / 1000.0));
            return getTime.ToString("F1");
        }

    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peak.Can.Basic;

namespace PCAN {
    class MessageStatus {
        private TPCANMsg msg;
        private TPCANTimestamp timeStamp;
        private TPCANTimestamp oldTimeStamp;
        private int index;
        private int count;
        private bool showPeriod;
        private bool changedFlag;


        public MessageStatus(TPCANMsg canMsg,TPCANTimestamp canTimeStamp, int listIndex) {
            this.msg = canMsg;
            this.timeStamp = canTimeStamp;
            this.oldTimeStamp = canTimeStamp;
            this.index = listIndex;
            this.count = 1;
            this.showPeriod = true;
            this.changedFlag = false;
        }

        public void Update(TPCANMsg canMsg, TPCANTimestamp canTimeStamp) {
            this.msg = canMsg;
            this.oldTimeStamp = timeStamp;
            this.timeStamp = canTimeStamp;
            this.changedFlag = true;
            count++;
        }

        public TPCANMsg CANMsg { get { return msg; } }
        public TPCANTimestamp TimeStamp { get { return timeStamp; } }
        public int Position { get { return index; } }
        public string TypeString { get { return GetMsgTypeString(); } }

        private string GetMsgTypeString() {
            string getType = "";

            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
                getType = "EXTENDED";
            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_STANDARD) == TPCANMessageType.PCAN_MESSAGE_STANDARD)
                getType = "STANDARD";
            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
                getType += "/RTR";
            return getType;
        }

        public string IDString { get { return GetIDString(); } }

        private string GetIDString() {
            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
                return string.Format("{0:X8}h", msg.ID);
            else
                return string.Format("{0:X3}h", msg.ID);
        }

        public string DataString { get { return GetDataString(); } }

        private string GetDataString() {
            string getData = "";

            if((msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
                return "Remote Request";
            else {
                for(int i = 0; i < msg.LEN; i++)
                    getData += string.Format("{0:x2} ", msg.DATA[i]);
            }

            return getData;
        }

        public int Count { get { return count; } }

        public bool ShowPeriod {
            get { return showPeriod; }
            set {
                if(showPeriod ^ value) {
                    showPeriod = value;
                    changedFlag = true;
                }
            }
        }

        public bool UpdatedFlag {
            get { return changedFlag; }
            set { changedFlag = value; }
        }

        public string TimeString { get { return GetTimeString(); } }

        private string GetTimeString() {
            double getTime;

            getTime = timeStamp.millis + (timeStamp.micros / 1000.0);
            if(showPeriod)
                getTime -= (oldTimeStamp.millis + (oldTimeStamp.micros / 1000.0));
            return getTime.ToString("F1");
        }

    }
}
>>>>>>> first update
