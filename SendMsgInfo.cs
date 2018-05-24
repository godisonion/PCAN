<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Peak.Can.Basic;

namespace PCAN {
    class SendMsgInfo {
        private Configuration config;
        private ConfigurationSectionGroup controlGroup;

        private TPCANMsg canMsg;
        //private TPCANMessageType msgType;
        private uint cycleTime = 100000;

        private string idLocation;

        public SendMsgInfo(string name) {
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            controlGroup = config.SectionGroups["Control"];
            canMsg = new TPCANMsg();
            SetInfo(name); 
            canMsg.LEN = 8;
            canMsg.DATA = new byte[8];
        }

        public TPCANMsg CanMsg { get { return canMsg; } }
        public uint CycleTime { get { return cycleTime; } }
        public uint ID { get { return canMsg.ID; } }
        public TPCANMessageType MsgType { get { return canMsg.MSGTYPE; } }
        public byte[] Data { get { return canMsg.DATA; } }

        //配置报文参数
        private void SetInfo(string name) {
            uint id;
            foreach(ConfigurationSectionGroup group in config.SectionGroups) {
                if(group.GetType().ToString() == "System.Configuration.ConfigurationSectionGroup") {
                    foreach(ConfigurationSection configrationSection in group.Sections) {
                        string tempLocation = configrationSection.SectionInformation.SectionName;
                        NameValueCollection tempSection = (NameValueCollection)ConfigurationManager.GetSection(tempLocation);
                        foreach(string key in tempSection.AllKeys) {
                            TPCANMessageType type = new TPCANMessageType();
                            if(key == "MsgType") {
                                if(tempSection.Get(key) == "0X00")
                                    type = TPCANMessageType.PCAN_MESSAGE_STANDARD;
                                else
                                    type = TPCANMessageType.PCAN_MESSAGE_EXTENDED;
                            }                                
                            if(key == name) {
                                idLocation = tempLocation;
                                id = Convert.ToUInt32(configrationSection.SectionInformation.Name.Substring(1), 16);
                                canMsg.ID = id;
                                canMsg.MSGTYPE = type;
                                cycleTime = Convert.ToUInt32(tempSection["CycleTime"]);
                                break;
                            }
                        }
                    }
                }
                else
                    continue;
            }
        }

        //获取指定ID
        public uint GetID(string name) {
            uint id;
            foreach(ConfigurationSectionGroup group in config.SectionGroups) {
                if(group.GetType().ToString() == "System.Configuration.ConfigurationSectionGroup") {
                    foreach(ConfigurationSection configrationSection in group.Sections) {
                        string tempLocation = configrationSection.SectionInformation.SectionName;
                        NameValueCollection tempSection = (NameValueCollection)ConfigurationManager.GetSection(tempLocation);
                        foreach(string key in tempSection.AllKeys) {
                            if(key == name) {
                                idLocation = tempLocation;
                                id = Convert.ToUInt32(configrationSection.SectionInformation.Name.Substring(1), 16);
                                return id;
                            }
                        }
                    }
                }
                else
                    continue;
            }
            return 0;
        }

        public void SetData(byte[] data) {
            canMsg.DATA = data;
        }

        //获取配置信息
        public Dictionary<string, string> GetSection(uint id) {
            Dictionary<string, string> sectionInfo = new Dictionary<string, string>();
            string sectionLocation = "Control/H" + id.ToString("X");

            NameValueCollection getSection = (NameValueCollection)ConfigurationManager.GetSection(idLocation);
            foreach(string key in getSection.AllKeys) {
                sectionInfo.Add(key, getSection.Get(key));
            }
            sectionInfo.Remove("CycleTime");
            sectionInfo.Remove("MsgType");
            return sectionInfo;           
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Peak.Can.Basic;

namespace PCAN {
    class SendMsgInfo {
        private Configuration config;
        private ConfigurationSectionGroup controlGroup;

        private TPCANMsg canMsg;
        //private TPCANMessageType msgType;
        private uint cycleTime = 100000;

        private string idLocation;

        public SendMsgInfo(string name) {
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            controlGroup = config.SectionGroups["Control"];
            canMsg = new TPCANMsg();
            SetInfo(name); 
            canMsg.LEN = 8;
            canMsg.DATA = new byte[8];
        }

        public TPCANMsg CanMsg { get { return canMsg; } }
        public uint CycleTime { get { return cycleTime; } }
        public uint ID { get { return canMsg.ID; } }
        public TPCANMessageType MsgType { get { return canMsg.MSGTYPE; } }
        public byte[] Data { get { return canMsg.DATA; } }

        //配置报文参数
        private void SetInfo(string name) {
            uint id;
            foreach(ConfigurationSectionGroup group in config.SectionGroups) {
                if(group.GetType().ToString() == "System.Configuration.ConfigurationSectionGroup") {
                    foreach(ConfigurationSection configrationSection in group.Sections) {
                        string tempLocation = configrationSection.SectionInformation.SectionName;
                        NameValueCollection tempSection = (NameValueCollection)ConfigurationManager.GetSection(tempLocation);
                        foreach(string key in tempSection.AllKeys) {
                            TPCANMessageType type = new TPCANMessageType();
                            if(key == "MsgType") {
                                if(tempSection.Get(key) == "0X00")
                                    type = TPCANMessageType.PCAN_MESSAGE_STANDARD;
                                else
                                    type = TPCANMessageType.PCAN_MESSAGE_EXTENDED;
                            }                                
                            if(key == name) {
                                idLocation = tempLocation;
                                id = Convert.ToUInt32(configrationSection.SectionInformation.Name.Substring(1), 16);
                                canMsg.ID = id;
                                canMsg.MSGTYPE = type;
                                cycleTime = Convert.ToUInt32(tempSection["CycleTime"]);
                                break;
                            }
                        }
                    }
                }
                else
                    continue;
            }
        }

        //获取指定ID
        public uint GetID(string name) {
            uint id;
            foreach(ConfigurationSectionGroup group in config.SectionGroups) {
                if(group.GetType().ToString() == "System.Configuration.ConfigurationSectionGroup") {
                    foreach(ConfigurationSection configrationSection in group.Sections) {
                        string tempLocation = configrationSection.SectionInformation.SectionName;
                        NameValueCollection tempSection = (NameValueCollection)ConfigurationManager.GetSection(tempLocation);
                        foreach(string key in tempSection.AllKeys) {
                            if(key == name) {
                                idLocation = tempLocation;
                                id = Convert.ToUInt32(configrationSection.SectionInformation.Name.Substring(1), 16);
                                return id;
                            }
                        }
                    }
                }
                else
                    continue;
            }
            return 0;
        }

        public void SetData(byte[] data) {
            canMsg.DATA = data;
        }

        //获取配置信息
        public Dictionary<string, string> GetSection(uint id) {
            Dictionary<string, string> sectionInfo = new Dictionary<string, string>();
            string sectionLocation = "Control/H" + id.ToString("X");

            NameValueCollection getSection = (NameValueCollection)ConfigurationManager.GetSection(idLocation);
            foreach(string key in getSection.AllKeys) {
                sectionInfo.Add(key, getSection.Get(key));
            }
            sectionInfo.Remove("CycleTime");
            sectionInfo.Remove("MsgType");
            return sectionInfo;           
        }
    }
}
>>>>>>> first update
