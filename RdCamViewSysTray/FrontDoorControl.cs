using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using Newtonsoft.Json;
using System.Net.Sockets;

namespace RdWebCamSysTrayApp
{
    class FrontDoorControl
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
       
        public class DoorStatus
        {
            public string door0IsLocked;
            public string door0OpenSense;
            public string door0MsBeforeRelock;
            public string door1IsLocked;
            public string door1OpenSense;
            public string door1MsBeforeRelock;
            public string bellSense;
            public string cardNoPresentNow;
            public string learnModeActive;
            public string learnModeTimoutInMs;
            public string learnModeUserName;
            public string learnModeLastUserAddedIdx;

            public string tagId = "";
            public string tagPresentInfo = "";
            public bool mainLocked = false;
            public bool mainOpen = false;
            public bool innerLocked = false;
            public bool bellPressed = false;

            public DoorStatus()
            {
            }

            public DoorStatus(string s)
            {
                Set(s);
            }

            public void Set(string s)
            {
                try
                {
                    string[] sVals = s.Split(',');
                    tagId = sVals[0];
                    tagPresentInfo = sVals[1];
                    mainLocked = (sVals[2] == "Locked");
                    mainOpen = (sVals[3] != "Closed");
                    innerLocked = (sVals[4] == "Locked");
                    bellPressed = (sVals[5] != "No");
                }
                catch (Exception excp)
                {
                    logger.Error("Exception in FrontDoorControl::DoorStatus:Set {0}", excp.Message);
                }
            }

            public void Update()
            {
                mainLocked = (door0IsLocked == "true") ? true : false;
                mainOpen = (door0OpenSense == "Open") ? true : false;
                innerLocked = (door1IsLocked == "true") ? true : false;
                bellPressed = (bellSense == "on") ? true : false;
                tagId = cardNoPresentNow;
            }
        }

        public class DoorStatusResult
        {
            public String result;
        }

        private string _doorIPAddress;
        private Timer _doorStatusTimer;
        private DoorStatus _doorStatus = new DoorStatus();
        private DateTime _lastDoorStatusTime = DateTime.MinValue;
        private int _doorStatusRequestNotifyCount = 0;
        private const int _doorStatusRequestResetAfter = 100;
        private bool _updateStatusRateHigh = false;

        public FrontDoorControl(string doorIPAddress)
        {
            _doorIPAddress = doorIPAddress;

            // Timer to update status
            _doorStatusTimer = new Timer(1000);
            _doorStatusTimer.Elapsed += new ElapsedEventHandler(OnDoorStatusTimer);
        }

        public void SetUpdateHighRate(bool highRate)
        {
            _updateStatusRateHigh = highRate;
        }

        private void CallDoorApiFunction(String functionAndArgs)
        {
            // Perform action through Particle API
            Uri uri = new Uri("https://api.particle.io/v1/devices/" + Properties.Settings.Default.FrontDoorParticleDeviceID + "/apiCall?access_token=" + Properties.Settings.Default.FrontDoorParticleAccessToken);

            // Using WebClient as can't get HttpClient to not block
            WebClient requester = new WebClient();
            requester.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            requester.UploadStringCompleted += new UploadStringCompletedEventHandler(web_req_completed);
            requester.UploadStringAsync(uri, "POST", "arg=" + functionAndArgs);
            logger.Info("FrontDoorControl::DoorAPICall " + functionAndArgs);
        }

        public void StartUpdates()
        {
            _doorStatusTimer.Start();
        }

        public void UnlockMainDoor()
        {
            ControlDoor("main-unlock");
        }

        public void LockMainDoor()
        {
            ControlDoor("main-lock");
        }

        public void UnlockInnerDoor()
        {
            ControlDoor("inner-unlock");
        }

        public void LockInnerDoor()
        {
            ControlDoor("inner-lock");
        }

        private void ControlDoor(string doorCommand)
        {
            try
            {
                CallDoorApiFunction(doorCommand);
            }
            catch (HttpRequestException excp)
            {
                logger.Error("FrontDoorControl::ControlDoor exception {0}", excp.Message);
            }
        }

        private void web_req_completed(object sender, UploadStringCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                logger.Info("FrontDoorControl::DoorApiCall ok");
            }
            else
            {
                logger.Info("FrontDoorControl::DoorApiCall error {0}", e.Error.ToString());
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            // Favour 192.168.x.x addresses
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString().Substring(0,8) == "192.168.")
                        return ip.ToString();
                }
            }
            // Then 172.x.x.x addresses
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString().Substring(0, 4) == "172.")
                        return ip.ToString();
                }
            }
            // Otherwise any address
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "";
        }

        private void OnDoorStatusTimer(object source, ElapsedEventArgs e)
        {
            try
            {
                if (_doorStatusRequestNotifyCount == 0)
                {
                    CallDoorApiFunction("no/" + GetLocalIPAddress() + "/34344");
                    //string requestURI = "http://" + _doorIPAddress + "/no/" + GetLocalIPAddress() + "/34344";
                    //HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(requestURI);
                    //webReq.Method = "GET";
                    //webReq.BeginGetResponse(new AsyncCallback(DoorNotifyCallback), webReq);
                }
                else if (_updateStatusRateHigh ? (_doorStatusRequestNotifyCount % 5 == 2) : (_doorStatusRequestNotifyCount == 2))
                {
                    Uri uri = new Uri("https://api.particle.io/v1/devices/" + Properties.Settings.Default.FrontDoorParticleDeviceID + "/status?access_token=" + Properties.Settings.Default.FrontDoorParticleAccessToken);

                    HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(uri);
                    webReq.Method = "GET";
                    webReq.BeginGetResponse(new AsyncCallback(DoorStatusCallback), webReq);
                }
            }
            catch (Exception excp)
            {
                logger.Error("Exception in FrontDoorControl::GetDoorStatus {0}", excp.Message);
            }
            _doorStatusRequestNotifyCount++;
            if (_doorStatusRequestNotifyCount > _doorStatusRequestResetAfter)
                _doorStatusRequestNotifyCount = 0;
        }

        public bool GetDoorStatus(out DoorStatus doorStatus)
        {
            doorStatus = _doorStatus;
            return (DateTime.Now-_lastDoorStatusTime).TotalSeconds < 30;                
        }


        //private void DoorNotifyCallback(IAsyncResult res)
        //{
        //    try
        //    {
        //        HttpWebRequest request = (HttpWebRequest)res.AsyncState;
        //        if (request == null)
        //            return;
        //        HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(res);
        //        if (response == null)
        //            return;
        //        string body = new StreamReader(response.GetResponseStream()).ReadToEnd();
        //        logger.Info("NotifyRequest returned {0}", body);
        //    }
        //    catch (Exception excp)
        //    {
        //        logger.Error("Exception in FrontDoorControl::DoorNotifyCallback {0}", excp.Message);
        //    }

        //}

        private void DoorStatusCallback(IAsyncResult res)
        {
            try
             {
                HttpWebRequest request = (HttpWebRequest)res.AsyncState;
                if (request == null)
                    return;
                HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(res);
                if (response == null)
                    return;
                string body = new StreamReader(response.GetResponseStream()).ReadToEnd();
                Console.WriteLine("DoorStatusResp " + body);

//                body = "{ 'result': { 'door0IsLocked': 'true' } }";
                string resultStr = JsonConvert.DeserializeObject<DoorStatusResult>(body).result;
                Console.WriteLine("DoorStatus result " + resultStr);
                _doorStatus = JsonConvert.DeserializeObject<DoorStatus>(resultStr);
                _doorStatus.Update();

                _lastDoorStatusTime = DateTime.Now;

            }
            catch (Exception excp)
            {
                logger.Error("Exception in FrontDoorControl::DoorStatusCallback {0}", excp.Message);
            }
        }

    }
}
