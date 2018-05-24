<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCAN {
    class ConfigInfo {
        private Configuration config; 
        //private ConfigurationSectionGroup digitalGroup;
        //private ConfigurationSectionGroup analogGroup;
        private ConfigurationSectionGroup controlGroup;
        //private ConfigurationSectionGroup bmsGroup;
        private ConfigurationSectionGroup msgGroup;
        //private NameValueCollection digitalSection;
        //private NameValueCollection analogSection;
        private string[] digitalID;
        private string[] analogID;
        private string[] bmsRID;
        private string[] bmsWID;
        private string[] meterRID;
        private string[] meterWID;
        private string[] multiRID;
        private string[] multiWID;
        private string[] msgID;
        private string[] mcuRID;
        private string[] mcuWID;

        private int msg_i;

        public ConfigInfo() {
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
           /* digitalGroup = config.SectionGroups["Digital"];
            digitalID = new string[digitalGroup.Sections.Count];
            foreach(ConfigurationSection configurationSection in digitalGroup.Sections) {
                digitalID[digital_i++] = configurationSection.SectionInformation.Name;
                Console.WriteLine(configurationSection.SectionInformation.SectionName);
            }*/

            /*   analogGroup = config.SectionGroups["Analog"];
               analogID = new string[analogGroup.Sections.Count];
               foreach(ConfigurationSection configurationSection in analogGroup.Sections) {
                   analogID[janalog_i++] = configurationSection.SectionInformation.Name;              
               }*/
            digitalID = GetIDArray("Digital");
            analogID = GetIDArray("Analog");
            bmsRID = GetIDArray("BMSR");
            bmsWID = GetIDArray("BMSW");
            meterRID = GetIDArray("MeterR");
            meterWID = GetIDArray("MeterW");
            multiRID = GetIDArray("MultiR");
            multiWID = GetIDArray("MultiW");
            mcuRID = GetIDArray("MCUR");
            mcuWID = GetIDArray("MCUW");
        }

        public string[] DigitalID { get { return digitalID; } }
        public string[] AnalogID { get { return analogID; } }
        public string[] BMSRID { get { return bmsRID; } }
        public string[] BMSWID { get { return bmsWID; } }
        public string[] MeterRID { get { return meterRID; } }
        public string[] MeterWID { get { return meterWID; } }
        public string[] MultiRID { get { return multiRID; } }
        public string[] MultiWID { get { return multiWID; } }
        public string[] MCURID { get { return mcuRID; } }
        public string[] MCUWID { get { return mcuWID; } }

        private string[] GetIDArray(string groupName) {
            msg_i = 0;
            msgGroup = config.SectionGroups[groupName];
            msgID = new string[msgGroup.Sections.Count];
            foreach(ConfigurationSection configSection in msgGroup.Sections) {
                msgID[msg_i++] = configSection.SectionInformation.Name;
            }
            return msgID;
        }
        //返回指定ID的配置信息
        public Dictionary<string,string> GetSection(string id, string groupName) {
            Dictionary<string, string> sectionInfo = new Dictionary<string, string>();
            string sectionLocation = groupName + "/" + id;
            NameValueCollection getSection = (NameValueCollection)ConfigurationManager.GetSection(sectionLocation);
            foreach(string key in getSection.AllKeys) {
                sectionInfo.Add(key, getSection.Get(key));
            }
            return sectionInfo;
        }

        //获取指定ID
        public uint GetID(string name) {
            uint id;
            foreach(ConfigurationSection configrationSection in controlGroup.Sections) {
                string tempLocation = configrationSection.SectionInformation.SectionName;
                NameValueCollection tempSection = (NameValueCollection)ConfigurationManager.GetSection(tempLocation);
                foreach(string key in tempSection.AllKeys) {
                    if(key == name) {
                        id = Convert.ToUInt32(configrationSection.SectionInformation.Name.Substring(1), 16);
                        return id;
                    }
                }
            }
                return 0;
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

namespace PCAN {
    class ConfigInfo {
        private Configuration config; 
        //private ConfigurationSectionGroup digitalGroup;
        //private ConfigurationSectionGroup analogGroup;
        private ConfigurationSectionGroup controlGroup;
        //private ConfigurationSectionGroup bmsGroup;
        private ConfigurationSectionGroup msgGroup;
        //private NameValueCollection digitalSection;
        //private NameValueCollection analogSection;
        private string[] digitalID;
        private string[] analogID;
        private string[] bmsRID;
        private string[] bmsWID;
        private string[] meterRID;
        private string[] meterWID;
        private string[] multiRID;
        private string[] multiWID;
        private string[] msgID;
        private string[] mcuRID;
        private string[] mcuWID;

        private int msg_i;

        public ConfigInfo() {
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
           /* digitalGroup = config.SectionGroups["Digital"];
            digitalID = new string[digitalGroup.Sections.Count];
            foreach(ConfigurationSection configurationSection in digitalGroup.Sections) {
                digitalID[digital_i++] = configurationSection.SectionInformation.Name;
                Console.WriteLine(configurationSection.SectionInformation.SectionName);
            }*/

            /*   analogGroup = config.SectionGroups["Analog"];
               analogID = new string[analogGroup.Sections.Count];
               foreach(ConfigurationSection configurationSection in analogGroup.Sections) {
                   analogID[janalog_i++] = configurationSection.SectionInformation.Name;              
               }*/
            digitalID = GetIDArray("Digital");
            analogID = GetIDArray("Analog");
            bmsRID = GetIDArray("BMSR");
            bmsWID = GetIDArray("BMSW");
            meterRID = GetIDArray("MeterR");
            meterWID = GetIDArray("MeterW");
            multiRID = GetIDArray("MultiR");
            multiWID = GetIDArray("MultiW");
            mcuRID = GetIDArray("MCUR");
            mcuWID = GetIDArray("MCUW");
        }

        public string[] DigitalID { get { return digitalID; } }
        public string[] AnalogID { get { return analogID; } }
        public string[] BMSRID { get { return bmsRID; } }
        public string[] BMSWID { get { return bmsWID; } }
        public string[] MeterRID { get { return meterRID; } }
        public string[] MeterWID { get { return meterWID; } }
        public string[] MultiRID { get { return multiRID; } }
        public string[] MultiWID { get { return multiWID; } }
        public string[] MCURID { get { return mcuRID; } }
        public string[] MCUWID { get { return mcuWID; } }

        private string[] GetIDArray(string groupName) {
            msg_i = 0;
            msgGroup = config.SectionGroups[groupName];
            msgID = new string[msgGroup.Sections.Count];
            foreach(ConfigurationSection configSection in msgGroup.Sections) {
                msgID[msg_i++] = configSection.SectionInformation.Name;
            }
            return msgID;
        }
        //返回指定ID的配置信息
        public Dictionary<string,string> GetSection(string id, string groupName) {
            Dictionary<string, string> sectionInfo = new Dictionary<string, string>();
            string sectionLocation = groupName + "/" + id;
            NameValueCollection getSection = (NameValueCollection)ConfigurationManager.GetSection(sectionLocation);
            foreach(string key in getSection.AllKeys) {
                sectionInfo.Add(key, getSection.Get(key));
            }
            return sectionInfo;
        }

        //获取指定ID
        public uint GetID(string name) {
            uint id;
            foreach(ConfigurationSection configrationSection in controlGroup.Sections) {
                string tempLocation = configrationSection.SectionInformation.SectionName;
                NameValueCollection tempSection = (NameValueCollection)ConfigurationManager.GetSection(tempLocation);
                foreach(string key in tempSection.AllKeys) {
                    if(key == name) {
                        id = Convert.ToUInt32(configrationSection.SectionInformation.Name.Substring(1), 16);
                        return id;
                    }
                }
            }
                return 0;
        }

    }
}
>>>>>>> first update
