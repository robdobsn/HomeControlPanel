// #define INCREASE_POLL_RATE_WHEN_VIS

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

        public class JsonDoorStatus
        {
            public class DoorInfo
            {
                public string name;
                public string num;
                public string locked;
                public string open;
                public string ms;
            }
            public DoorInfo[] doors;
            public string bell;
            public string card;
            public string learn;
            public string learnMs;
            public string learnUserName;
            public string learnLastUserIdx;
        }

        public class DoorStatus
        {
            public JsonDoorStatus _doorInfoFromJson;

            public bool _mainLocked = false;
            public bool _mainOpen = false;
            public bool _innerLocked = false;
            public bool _bellPressed = false;
            public DateTime _lastDoorStatusTime = DateTime.MinValue;
            public string _tagId = "";
            public string _tagPresentInfo = "";

            public DoorStatus()
            {
            }

            //public DoorStatus(string s)
            //{
            //    Set(s);
            //}

            //public void Set(string s)
            //{
            //    try
            //    {
            //        string[] sVals = s.Split(',');
            //        tagId = sVals[0];
            //        tagPresentInfo = sVals[1];
            //        mainLocked = (sVals[2] == "Y");
            //        mainOpen = (sVals[3] == "Y");
            //        innerLocked = (sVals[4] == "Y");
            //        bellPressed = (sVals[5] == "Y");
            //    }
            //    catch (Exception excp)
            //    {
            //        logger.Error("Exception in FrontDoorControl::DoorStatus:Set {0}", excp.Message);
            //    }
            //}

            private void UpdateInternal()
            {
                _lastDoorStatusTime = DateTime.Now;
                _mainLocked = (_doorInfoFromJson.doors[0].locked == "Y") ? true : false;
                _mainOpen = (_doorInfoFromJson.doors[0].open == "Y") ? true : false;
                _innerLocked = (_doorInfoFromJson.doors[1].locked == "Y") ? true : false;
                _bellPressed = (_doorInfoFromJson.bell == "Y") ? true : false;
                _tagId = _doorInfoFromJson.card;
            }

            public void UpdateFromJson(string jsonStr)
            {
                //                body = "{ 'result': { 'door0IsLocked': 'true' } }";
                try
                {
                    _doorInfoFromJson = JsonConvert.DeserializeObject<JsonDoorStatus>(jsonStr);
                    UpdateInternal();
                }
                catch (Exception excp)
                {
                    logger.Error("Exception in DoorStatus::UpdateFromJson {0}", excp.Message);
                }
            }
        }

        public class DoorStatusResult
        {
            public String result;
        }

        private string _doorIPAddress;
        private Timer _doorStatusTimer;
        private DoorStatus _doorStatus = new DoorStatus();
        private int _doorStatusRequestNotifyCount = 0;
        private const int _doorStatusRequestResetAfter = 100;
        private bool _updateStatusRateHigh = false;

        public delegate void DoorStatusRefreshCallback();
        private DoorStatusRefreshCallback _doorStatusRefreshCallback;

        public FrontDoorControl(string doorIPAddress, DoorStatusRefreshCallback doorStatusRefreshCallback)
        {
            _doorIPAddress = doorIPAddress;
            _doorStatusRefreshCallback = doorStatusRefreshCallback;

            // Timer to update status
            _doorStatusTimer = new Timer(1000);
            _doorStatusTimer.Elapsed += new ElapsedEventHandler(OnDoorStatusTimer);
        }

        public void SetUpdateHighRate(bool highRate)
        {
#if INCREASE_POLL_RATE_WHEN_VIS
            _updateStatusRateHigh = highRate;
#endif
        }

        private void CallDoorApiFunction(String functionAndArgs)
        {
#if USE_PARTICLE_API
            // Perform action through Particle API
            Uri uri = new Uri("https://api.particle.io/v1/devices/" + Properties.Settings.Default.FrontDoorParticleDeviceID + "/apiCall?access_token=" + Properties.Settings.Default.FrontDoorParticleAccessToken);

            // Using WebClient as can't get HttpClient to not block
            WebClient requester = new WebClient();
            requester.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            requester.UploadStringCompleted += new UploadStringCompletedEventHandler(web_req_completed);
            requester.UploadStringAsync(uri, "POST", "arg=" + functionAndArgs);
            logger.Info("FrontDoorControl::DoorAPICall " + functionAndArgs);
#else
            string uriStr = "http://" + _doorIPAddress + "/" + functionAndArgs;
            Uri uri = new Uri(uriStr);

            // Using WebClient as can't get HttpClient to not block
            WebClient requester = new WebClient();
            requester.OpenReadCompleted += new OpenReadCompletedEventHandler(web_read_completed);
            requester.OpenReadAsync(uri);

            logger.Info("FrontDoorControl::CallDoorApiFunction " + uriStr);
#endif
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

        private void web_read_completed(object sender, OpenReadCompletedEventArgs e)
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
                    if (ip.ToString().Substring(0, 8) == "192.168.")
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
                if (_updateStatusRateHigh ? (_doorStatusRequestNotifyCount % 5 == 2) : (_doorStatusRequestNotifyCount == 0))
                {
#if USE_PARTICLE_API
                    Uri uri = new Uri("https://api.particle.io/v1/devices/" + Properties.Settings.Default.FrontDoorParticleDeviceID + "/status?access_token=" + Properties.Settings.Default.FrontDoorParticleAccessToken);

                    HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(uri);
                    webReq.Method = "GET";
                    webReq.BeginGetResponse(new AsyncCallback(DoorStatusCallback), webReq);
#else
                    string uriStr = "http://" + _doorIPAddress + "/q";
                    Uri uri = new Uri(uriStr);

                    // Using WebClient as can't get HttpClient to not block
                    WebClient requester = new WebClient();
                    requester.OpenReadCompleted += new OpenReadCompletedEventHandler(DoorStatusCallback);
                    requester.OpenReadAsync(uri);

                    logger.Info("FrontDoorControl::OnDoorStatusTimer " + uriStr);
#endif
                }
                else if (_doorStatusRequestNotifyCount == 2)
                {
                    CallDoorApiFunction("no/" + GetLocalIPAddress() + ":34344");
                    logger.Info("Requesting notification from door control");
                    //string requestURI = "http://" + _doorIPAddress + "/no/" + GetLocalIPAddress() + "/34344";
                    //HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(requestURI);
                    //webReq.Method = "GET";
                    //webReq.BeginGetResponse(new AsyncCallback(DoorNotifyCallback), webReq);
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
            return (DateTime.Now - _doorStatus._lastDoorStatusTime).TotalSeconds < 30;
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

        public  void SetDoorStatusFromJson(string jsonStr)
        {
            _doorStatus.UpdateFromJson(jsonStr);
        }

#if USE_PARTICLE_API
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
#else
        private void DoorStatusCallback(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                try
                {
                    logger.Info("FrontDoorControl::DoorQueryCallback ok");
                    Stream response = (Stream)e.Result;
                    string body = new StreamReader(response).ReadToEnd();
                    Console.WriteLine("DoorStatusResp " + body);
                    SetDoorStatusFromJson(body);
                    _doorStatusRefreshCallback();
                }
                catch (Exception excp)
                {
                    logger.Error("Exception in FrontDoorControl::DoorStatusCallback {0}", excp.Message);
                }
            }
            else
            {
                logger.Info("FrontDoorControl::DoorQueryCallback error {0}", e.Error.ToString());
            }
        }

#endif
    }
}
