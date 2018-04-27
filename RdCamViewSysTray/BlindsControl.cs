#define USE_HTTP_REST_API
//#define USE_UDP_REST_API

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;

namespace RdWebCamSysTrayApp
{
    class BlindsControl
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string _ipAddress;
        private int __blindsControlRestAPIPort;

        public BlindsControl(DeviceInfo devInfo)
        {
            _ipAddress = ConfigFileInfo.getIPAddressForName(devInfo.hostname);
            __blindsControlRestAPIPort = devInfo.port;
        }

        public void ControlBlind(int blindNumber, string direction)
        {
        string blindsCommand = "shade/" + (blindNumber + 1).ToString() + "/" + direction + "/pulse";
#if USE_HTTP_REST_API
            try
            {
//                string[] shadeNames = { "workroom-shade", "office1-shade", "office2-shade", "office3-shade", "office4-shade" };
//                string blindsCommand = shadeNames[blindNumber] + "-" + direction + "/pulse";
                Uri uri = new Uri("http://" + _ipAddress + "/" + blindsCommand);

                // Using WebClient as can't get HttpClient to not block
                WebClient requester = new WebClient();
                requester.OpenReadCompleted += new OpenReadCompletedEventHandler(web_req_completed);
                requester.OpenReadAsync(uri);
                
                logger.Info("BlindsControl::ControlBlind " + uri.ToString());
            }
            catch (HttpRequestException excp)
            {
                logger.Error("BlindsControl::ControlBlind exception {0}", excp.Message);
            }
#endif
#if USE_UDP_REST_API

            try
            {
                string ipAddr = _ipAddress;
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPAddress serverAddr = IPAddress.Parse(ipAddr);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, __blindsControlRestAPIPort);
                byte[] send_buffer = Encoding.ASCII.GetBytes(blindsCommand);
                sock.SendTo(send_buffer, endPoint);
                logger.Debug("Sent command to blinds " + ipAddr + " port " + __blindsControlRestAPIPort.ToString() + " by UDP " + blindsCommand);
            }
            catch (Exception excp)
            {
                logger.Error("BlindsControl::ControlBlind exception {0}", excp.Message);
            }
#endif

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

        // The following code doesn't work asynchronously - despite use of async!

        //    try
        //    {
        //        string[] shadeNames = { "workroom-shade", "office1-shade", "office2-shade", "office3-shade", "office4-shade" };
        //        string blindsCommand = shadeNames[blindNumber] + "-" + direction + "/pulse";

        //        // Create a New HttpClient object.
        //        HttpClient client = new HttpClient();

        //        HttpResponseMessage response = await client.GetAsync("http://" + _ipAddress + "/" + blindsCommand);
        //        response.EnsureSuccessStatusCode();
        //        string responseBody = await response.Content.ReadAsStringAsync();
        //        // Above three lines can be replaced with new helper method in following line 
        //        // string body = await client.GetStringAsync(uri);

        //        logger.Info("FrontDoorControl::ControlDoor response {0}", responseBody);
        //    }
        //    catch (HttpRequestException excp)
        //    {
        //        logger.Error("FrontDoorControl::ControlDoor exception {0}", excp.Message);
        //    }
        //}


    }
}
