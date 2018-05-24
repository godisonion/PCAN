<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Windows.Forms;

using Peak.Can.Basic;

namespace PCAN {
    public class TestJT {
        private TPCANMsg msg;
        private AppSettingsSection digital;

        public TestJT(TPCANMsg canMsg, AppSettingsSection digital) {
            this.msg = canMsg;
            this.digital = digital;
        }

        public string[] Process() {            
            string[] pins = new string[digital.Settings.AllKeys.Length];            
            byte[] getData = msg.DATA;
            int n = 0;
            int startBit = 0;
            int bitLength = 0;

            if(msg.ID == Convert.ToUInt32(digital.Settings["ID"].Value, 16)) {
                Array.Reverse(getData);
                string[] keys = digital.Settings.AllKeys;
                for(int i = 1; i < keys.Length; i++) {
                   // uint bit = 0;
                    string[] temp = digital.Settings[keys[i]].Value.Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                  /*  for(int i = 0; i < bitLength; i++) {
                        bit = (bit << 1) + 1;
                    }*/
                    if(((getData[startBit/8] >> (startBit%8)) & 1) == 1) {
                        pins[n++] = keys[i];
                    }
                }
                //Console.WriteLine(pins[0].ToString());
                return pins;
            }
            return null;
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Windows.Forms;

using Peak.Can.Basic;

namespace PCAN {
    public class TestJT {
        private TPCANMsg msg;
        private AppSettingsSection digital;

        public TestJT(TPCANMsg canMsg, AppSettingsSection digital) {
            this.msg = canMsg;
            this.digital = digital;
        }

        public string[] Process() {            
            string[] pins = new string[digital.Settings.AllKeys.Length];            
            byte[] getData = msg.DATA;
            int n = 0;
            int startBit = 0;
            int bitLength = 0;

            if(msg.ID == Convert.ToUInt32(digital.Settings["ID"].Value, 16)) {
                Array.Reverse(getData);
                string[] keys = digital.Settings.AllKeys;
                for(int i = 1; i < keys.Length; i++) {
                   // uint bit = 0;
                    string[] temp = digital.Settings[keys[i]].Value.Split(',');
                    startBit = Convert.ToInt32(temp[0]);
                    bitLength = Convert.ToInt32(temp[1]);
                  /*  for(int i = 0; i < bitLength; i++) {
                        bit = (bit << 1) + 1;
                    }*/
                    if(((getData[startBit/8] >> (startBit%8)) & 1) == 1) {
                        pins[n++] = keys[i];
                    }
                }
                //Console.WriteLine(pins[0].ToString());
                return pins;
            }
            return null;
        }
    }
}
>>>>>>> first update
