using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;

namespace HomeControlPanel
{
    class BlindsControl : DeviceBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string _ipAddress;
        private int __blindsControlRestAPIPort;

        public BlindsControl(ConfigFileInfo configFileInfo, DeviceInfo devInfo)
        {
            _ipAddress = ConfigFileInfo.getIPAddressForName(devInfo.hostname);
            __blindsControlRestAPIPort = devInfo.port;
        }

        public void Control(int idx, string cmd)
        {
            ControlBlind(idx, cmd);
        }

        public int GetVal(int idx, string valType)
        {
            return 0;
        }

        public string GetString(int idx, string valType)
        {
            return "";
        }

        public void ControlBlind(int blindNumber, string direction)
        {
            string blindsCommand = "shade/" + (blindNumber + 1).ToString() + "/" + direction + "/pulse";
            try
            {
                Uri uri = new Uri("http://" + _ipAddress + "/" + blindsCommand);

                WebClient requester = new WebClient();
                requester.OpenReadCompleted += new OpenReadCompletedEventHandler(web_req_completed);
                requester.OpenReadAsync(uri);
                
                logger.Info("BlindsControl::ControlBlind " + uri.ToString());
            }
            catch (HttpRequestException excp)
            {
                logger.Error("BlindsControl::ControlBlind exception {0}", excp.Message);
            }
        }

        private void web_req_completed(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                logger.Info("BlindsControl::ControlBlind ok");
            }
            else
            {
                logger.Info("BlindsControl::ControlBlind error {0}", e.Error.ToString());
            }
        }
    }
}
