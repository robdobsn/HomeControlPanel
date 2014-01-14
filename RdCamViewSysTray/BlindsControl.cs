using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Net.Http;

namespace RdWebCamSysTrayApp
{
    class BlindsControl
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string _ipAddress;

        public BlindsControl(string ipAddress)
        {
            _ipAddress = ipAddress;
        }

        public async void ControlBlind(int blindNumber, string direction)
        {
            try
            {
                string[] shadeNames = { "workroom-shade", "office1-shade", "office2-shade", "office3-shade", "office4-shade" };
                string blindsCommand = shadeNames[blindNumber] + "-" + direction + "/pulse";

                // Create a New HttpClient object.
                HttpClient client = new HttpClient();

                HttpResponseMessage response = await client.GetAsync("http://" + _ipAddress + "/" + blindsCommand);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                // Above three lines can be replaced with new helper method in following line 
                // string body = await client.GetStringAsync(uri);

                logger.Info("FrontDoorControl::ControlDoor response {0}", responseBody);
            }
            catch (HttpRequestException excp)
            {
                logger.Error("FrontDoorControl::ControlDoor exception {0}", excp.Message);
            }
        }


    }
}
