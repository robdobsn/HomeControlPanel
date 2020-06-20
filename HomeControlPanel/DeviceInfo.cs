using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeControlPanel
{
    // Info on a device in the config file
    public class DeviceInfo
    {
        public string name;
        public string description;
        public string deviceType;
        public string hostname;
        public string username;
        public string password;
        public int port;
        public int notifyPort;
        public string videoURL;
        public string imageGrabPath;
        public int imageGrabPoll;
        public int userNum;
        public string userPin;
        public int motionDetectAutoShow;
        public string mqttServer;
        public int mqttPort;
        public string mqttInTopic;
        public string mqttOutTopic;
        public int rotation;

        DeviceInfo()
        {
            port = 0;
            notifyPort = 0;
            imageGrabPoll = 0;
            userNum = 0;
            motionDetectAutoShow = 0;
            mqttPort = 0;
            rotation = 0;
        }
    }
}
