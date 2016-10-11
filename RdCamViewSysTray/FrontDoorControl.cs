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
using System.Text.RegularExpressions;

namespace RdWebCamSysTrayApp
{
    class FrontDoorControl
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private const int _doorControlNotifyPort = 34344;

        // This is the format received from the door controller
        public class JsonDoorStatus
        {
            public string d0l;
            public string d0o;
            public string d0ms;
            public string d1l;
            public string d1o;
            public string d1ms;
            public string b;
        }

        // Status of the door controller
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

            private void UpdateInternal()
            {
                _lastDoorStatusTime = DateTime.Now;
                _mainLocked = (_doorInfoFromJson.d0l == "Y") ? true : false;
                _mainOpen = (_doorInfoFromJson.d0o == "Y") ? true : false;
                _innerLocked = (_doorInfoFromJson.d1l == "Y") ? true : false;
                _bellPressed = (_doorInfoFromJson.b == "Y") ? true : false;
            }

            public void UpdateFromJson(string jsonStr)
            {
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

        // IP Address of door controller and door status
        private string _doorIPAddress;
        private DoorStatus _doorStatus = new DoorStatus();

        // Timer for re-requesting notifications - in case door controller restarts
        private Timer _doorStatusTimer;
        private int _doorStatusRequestNotifyCount = 0;
        private const int _doorStatusRequestResetAfter = 100;

        // TCP Listener for door status and thread signal.
        private TcpListener _tcpListenerForDoorStatus;

        // Callback into Main Window when door status has changed - used to pop-up window
        public delegate void DoorStatusRefreshCallback();
        private DoorStatusRefreshCallback _doorStatusRefreshCallback;

        // Front Doot Control Constructor
        public FrontDoorControl(string doorIPAddress, DoorStatusRefreshCallback doorStatusRefreshCallback)
        {
            _doorIPAddress = doorIPAddress;
            _doorStatusRefreshCallback = doorStatusRefreshCallback;

            // Timer to update status
            _doorStatusTimer = new Timer(1000);
            _doorStatusTimer.Elapsed += new ElapsedEventHandler(OnDoorStatusTimer);

            // TCP listener for door status
            DoorStatusListenerStart();
        }

        // Method to make call on door controller - currently using web server on door controller
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

        public bool IsDoorbellPressed()
        {
            return _doorStatus._bellPressed;
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
                if (_doorStatusRequestNotifyCount == 0)
                {
                    CallDoorApiFunction("no/" + GetLocalIPAddress() + ":" + _doorControlNotifyPort.ToString() + "/1/-60/doorstatus");
                    logger.Info("Requesting notification from door control");
                }
                #if REQUEST_STATUS_ON_TIMER
                    else if (_doorStatusRequestNotifyCount == 2)
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
                #endif
            }
            catch (Exception excp)
            {
                logger.Error("Exception in FrontDoorControl::OnDoorStatusTimer {0}", excp.Message);
            }
            // Update counter
            _doorStatusRequestNotifyCount++;
            if (_doorStatusRequestNotifyCount > _doorStatusRequestResetAfter)
                _doorStatusRequestNotifyCount = 0;
        }

        public bool GetDoorStatus(out DoorStatus doorStatus)
        {
            doorStatus = _doorStatus;
            return (DateTime.Now - _doorStatus._lastDoorStatusTime).TotalSeconds < 30;
        }

        // Door status listener - only one client connection asynchronously
        public void DoorStatusListenerStart()
        {
            _tcpListenerForDoorStatus = new TcpListener(IPAddress.Any, _doorControlNotifyPort);
            _tcpListenerForDoorStatus.Start();

            // Start to listen for connections from a client.
            logger.Debug("Door status listening ...");

            // Accept the connection. 
            _tcpListenerForDoorStatus.BeginAcceptTcpClient(
                new AsyncCallback(DoorStatusCallback),
                _tcpListenerForDoorStatus);
        }

        private void DoorStatusCallback(IAsyncResult res)
        {
            try
            {
                TcpListener listener = (TcpListener)res.AsyncState;
                if (listener == null)
                    return;
                TcpClient tcpClient = listener.EndAcceptTcpClient(res);
                using (var networkStream = tcpClient.GetStream())
                {
                    string req = new StreamReader(networkStream).ReadToEnd();
                    logger.Debug("DoorStatusResp " + req);
                    string[] reqLines = Regex.Split(req, "\r\n|\r|\n");
                    string payload = "";
                    for (int payloadLineIdx = 0; payloadLineIdx < reqLines.Length; payloadLineIdx++)
                    {
                        if (reqLines[payloadLineIdx].Trim().Length == 0)
                        {
                            if (payloadLineIdx + 1 < reqLines.Length)
                                payload = reqLines[payloadLineIdx + 1];
                        }

                    }
                    if (payload.Length > 0)
                    {
                        logger.Debug(payload);
                        _doorStatus.UpdateFromJson(payload);
                        _doorStatusRefreshCallback();
                    }
                }
            }
            catch (Exception excp)
            {
                logger.Error("Exception in FrontDoorControl::DoorStatusCallback {0}", excp.Message);
            }
            // Listen again
            _tcpListenerForDoorStatus.BeginAcceptTcpClient(
                    new AsyncCallback(DoorStatusCallback),
                    _tcpListenerForDoorStatus);

        }

    }
}
