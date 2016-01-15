using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Net.Http;
using System.Net;

namespace RdWebCamSysTrayApp
{
    class LedMatrix
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string _ipAddress;

        public LedMatrix(string ipAddress)
        {
            _ipAddress = ipAddress;
        }

        private void RESTCommand(string cmd, string param)
        {
            try
            {
                Uri uri = new Uri("http://" + _ipAddress + "/" + param);

                // Using WebClient as can't get HttpClient to not block
                WebClient requester = new WebClient();
                requester.OpenReadCompleted += new OpenReadCompletedEventHandler(web_req_completed);
                requester.OpenReadAsync(uri, cmd);
            }
            catch (HttpRequestException excp)
            {
                logger.Error("LedMatrix::SendMessage exception {0}", excp.Message);
            }
        }

        public void SendMessage(string message)
        {
            RESTCommand("SendMessage", "text?" + message);
            logger.Info("LedMatrix::SendMessage " + message);
        }

        public void StopAlert()
        {
            RESTCommand("StopAlert", "stopAlert");
            logger.Info("LedMatrix::stopAlert");
        }

        public void Clear()
        {
            RESTCommand("Clear", "clear");
            logger.Info("LedMatrix::clear");
        }

        private void web_req_completed(object sender, OpenReadCompletedEventArgs e)
        {
            string cmd = (string)e.UserState;
            if (e.Error == null)
            {
                logger.Info("LedMatrix::{0} ok", cmd);
            }
            else
            {
                logger.Info("LedMatrix::{0} error {1}", cmd, e.Error.ToString());
            }
        }
    }
}
