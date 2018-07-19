//#define USE_PARTICLE_API
#define USE_HTTP_REST_API
//#define USE_UDP_REST_API
//#define USE_MANAGED_MQTT
//#define LISTEN_FOR_UDP_DOOR_STATUS
//#define LISTEN_FOR_TCP_DOOR_STATUS
#define POLL_FOR_TCP_DOOR_STATUS

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
using System.Net.NetworkInformation;
using MQTTnet;
using MQTTnet.ManagedClient;
using MQTTnet.Client;

namespace HomeControlPanel
{

    /// <summary>
    /// Constructor
    /// </summary>
    class DoorControl
    {
        // Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Callback into Main Window when door status has changed - used to pop-up window
        public delegate void DoorStatusRefreshCallback();
        private DoorStatusRefreshCallback _doorStatusRefreshCallback;

        // Device info for door
        DeviceInfo _deviceInfo;

        // MQTT client
        private IManagedMqttClient _mqttClient;

#if USE_HTTP_REST_API || USE_UDP_REST_API
        private string _doorIPAddress;
#endif

        // This is the format received from the door controller
        public class JsonDoorStatus
        {
            public string d0l = "";
            public string d0o = "";
            public string d0ms = "";
            public string d1l = "";
            public string d1o = "";
            public string d1ms = "";
            public string b = "";
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
                    if (jsonStr.Contains("d0l"))
                    {
                        _doorInfoFromJson = JsonConvert.DeserializeObject<JsonDoorStatus>(jsonStr);
                        UpdateInternal();
                    }
                }
                catch (Exception excp)
                {
                    logger.Error("Exception in DoorStatus::UpdateFromJson {0}", excp.Message);
                }
            }
        }

        // Door status
        private DoorStatus _doorStatus = new DoorStatus();

        // Number and PIN of door user
        private int _doorUserNumber;
        private string _doorUserPin;

#if USE_HTTP_REST_API || USE_UDP_REST_API
        // Timer for re-requesting notifications - in case door controller restarts
        private Timer _doorStatusTimer;
        private int _doorStatusRequestNotifyCount = 0;
        private const int _doorStatusRequestResetTo = 1;
        private const int _doorStatusRequestResetAfter = 100;
#endif

#if LISTEN_FOR_TCP_DOOR_STATUS
        // TCP Listener for door status and thread signal.
        private TcpListener _tcpListenerForDoorStatus;
#endif
#if LISTEN_FOR_UDP_DOOR_STATUS
        private UdpClient _udpClientForDoorStatus;
#endif

        // Front Doot Control Constructor
        public DoorControl(DeviceInfo devInfo, DoorStatusRefreshCallback doorStatusRefreshCallback)
        {
            // Device info
            _deviceInfo = devInfo;
            _doorStatusRefreshCallback = doorStatusRefreshCallback;

            // Cache useful info
#if USE_HTTP_REST_API || USE_UDP_REST_API
            _doorIPAddress = ConfigFileInfo.getIPAddressForName(_deviceInfo.hostname);
#endif
            _doorUserNumber = devInfo.userNum;
            _doorUserPin = devInfo.userPin;

#if USE_MANAGED_MQTT
            String clientName = Environment.MachineName + "_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(clientName)
                    .WithTcpServer(_deviceInfo.mqttServer, _deviceInfo.mqttPort)
                    .Build())
                .Build();
            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            var topic = new TopicFilterBuilder().WithTopic(_deviceInfo.mqttOutTopic).Build();
            _mqttClient.SubscribeAsync(topic);
            _mqttClient.StartAsync(options);

            _mqttClient.ApplicationMessageReceived += (s, e) =>
            {
                Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
                Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
                Console.WriteLine();
            };
#endif

#if USE_HTTP_REST_API || USE_UDP_REST_API
            // Timer to update status
            _doorStatusTimer = new Timer(1000);
            _doorStatusTimer.Elapsed += new ElapsedEventHandler(OnDoorStatusTimer);

            // TCP listener for door status
            DoorStatusListenerStart();
#endif
        }

        /// <summary>
        /// CallDoorApiFunction
        /// </summary>
        /// <param name="functionAndArgs"></param>
        /// Method to make call on door controller using whatever connection method is current
        /// 
        private void CallDoorApiFunction(String functionAndArgs)
        {
#if USE_PARTICLE_API
            // Perform action through Particle API
            Uri uri = new Uri("https://api.particle.io/v1/devices/" + Properties.Settings.Default.FrontDoorParticleDeviceID + "/apiCall?access_token=" + Properties.Settings.Default.FrontDoorParticleAccessToken);

            // Using WebClient as can't get HttpClient to not block
            WebClient requester = new WebClient();
            requester.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            requester.UploadStringCompleted += new UploadStringCompletedEventHandler(ParticleApiFnCompleted);
            requester.UploadStringAsync(uri, "POST", "arg=" + functionAndArgs);
            logger.Info("FrontDoorControl::DoorAPICall " + functionAndArgs);

            private void ParticleApiFnCompleted(object sender, UploadStringCompletedEventArgs e)
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
#endif
#if USE_HTTP_REST_API
            try
            {
                string uriStr = "http://" + _doorIPAddress + "/" + functionAndArgs;
                Uri uri = new Uri(uriStr, UriKind.Absolute);

                // Using WebClient as can't get HttpClient to not block
                //logger.Info("FrontDoorControl::CallDoorApiFunction " + uriStr);

                using (WebClient client = new WebClient())
                {

                    client.DownloadStringCompleted += (sender, e) =>
                    {
                        if (e.Error != null)
                        {
                            logger.Error("FrontDoorControl:Error in http response {0}", e.Error.ToString());
                        }
                        else
                        {
                            DoorApiFnCompleted(e.Result);
                            Console.WriteLine(e.Result);
                        }
                    };

                    client.DownloadStringAsync(uri);
                }
            }
            catch (Exception e)
            {
                logger.Error("FrontDoorControl::CallDoorApiFunction exception {0}", e.Message);
            }
#endif
#if USE_UDP_REST_API
            try
            {
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPAddress serverAddr = IPAddress.Parse(_doorIPAddress);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, _doorControlRestAPIPort);
                byte[] send_buffer = Encoding.ASCII.GetBytes(functionAndArgs);
                sock.SendTo(send_buffer, endPoint);
                logger.Debug("Sent command to door " + _doorIPAddress + " port " + _doorControlRestAPIPort.ToString() + " by UDP " + functionAndArgs);
            }
            catch (Exception excp)
            {
                logger.Error("FrontDoorControl::CallDoorApiFunction exception {0}", excp.Message);
            }
#endif

            //TODO
        }

        /// <summary>
        /// DoorApiFnCompleted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoorApiFnCompleted(string rsltStr)
        {
            logger.Info("DoorControl::DoorApiCall ok {0}", rsltStr);
            _doorStatus.UpdateFromJson(rsltStr);
            _doorStatusRefreshCallback();
        }

        /// <summary>
        /// Start the process of generating periodic status updates - for polled connections
        /// </summary>
        public void StartUpdates()
        {
#if USE_HTTP_REST_API || USE_UDP_REST_API
            _doorStatusTimer.Start();
#endif
        }

        /// <summary>
        /// Unlock main door
        /// </summary>
        public void UnlockMainDoor()
        {
            ControlDoor("u/0/" + _doorUserNumber.ToString() + "/" + _doorUserPin);
        }

        public void LockMainDoor()
        {
            ControlDoor("l/0");
        }

        public void UnlockInnerDoor()
        {
            ControlDoor("u/1/" + _doorUserNumber.ToString() + "/" + _doorUserPin);
        }

        public void LockInnerDoor()
        {
            ControlDoor("l/1");
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
                logger.Error("DoorControl::ControlDoor exception {0}", excp.Message);
            }
        }

        public static IPAddress GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .Where(a => a != null)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)  // IP4
                .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
                .FirstOrDefault();
        }

        public static string GetLocalIPAddress()
        {
            IPAddress ipGateway = GetDefaultGateway();

            var host = Dns.GetHostEntry(Dns.GetHostName());
            // Favour IP4 192.168.x.x addresses on the same 255.255.255.0 subnet as the default gateway
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] ipGWBytes = ipGateway.GetAddressBytes();
                    byte[] ipBytes = ip.GetAddressBytes();
                    bool bMatch = true;
                    for (int i = 0; i < 3; i++)
                        if (ipGWBytes[i] != ipBytes[i])
                            bMatch = false;
                    if (bMatch)
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
#if POLL_FOR_TCP_DOOR_STATUS
            CallDoorApiFunction("q");
#endif

#if LISTEN_FOR_TCP_DOOR_STATUS || LISTEN_FOR_UDP_DOOR_STATUS || USE_PARTICLE_API
            try
            {
                if (_doorStatusRequestNotifyCount == 0)
                {
#if LISTEN_FOR_TCP_DOOR_STATUS
                    CallDoorApiFunction("no/" + GetLocalIPAddress() + ":" + _doorControlNotifyPort.ToString() + "/1/-60/doorstatus");
                    logger.Info("Requesting TCP notification from door control");
#endif
#if LISTEN_FOR_UDP_DOOR_STATUS
                    CallDoorApiFunction("no/" + GetLocalIPAddress() + ":" + _doorControlNotifyPort.ToString() + "/0/-60/doorstatus");
                    logger.Info("Requesting UDP notification from door control");
#endif
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
#endif
        }

        public bool GetDoorStatus(out DoorStatus doorStatus)
        {
            doorStatus = _doorStatus;
            return (DateTime.Now - _doorStatus._lastDoorStatusTime).TotalSeconds < 30;
        }

        // Door status listener - only one client connection asynchronously
        public void DoorStatusListenerStart()
        {
#if LISTEN_FOR_TCP_DOOR_STATUS
            _tcpListenerForDoorStatus = new TcpListener(IPAddress.Any, _doorControlNotifyPort);
            _tcpListenerForDoorStatus.Start();

            // Start to listen for connections from a client.
            logger.Debug("Door status listening ...");

            // Accept the connection. 
            _tcpListenerForDoorStatus.BeginAcceptTcpClient(
                new AsyncCallback(DoorStatusCallback),
                _tcpListenerForDoorStatus);
#endif
#if LISTEN_FOR_UDP_DOOR_STATUS
            _udpClientForDoorStatus = new UdpClient(_doorControlNotifyPort);
            _udpClientForDoorStatus.BeginReceive(new AsyncCallback(DoorStatusCallback), null);
            logger.Info("Socket bound to RFID door lock port {0}", _doorControlNotifyPort);
#endif

        }

        private void DoorStatusCallback(IAsyncResult res)
        {
#if LISTEN_FOR_TCP_DOOR_STATUS
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
#endif
#if LISTEN_FOR_UDP_DOOR_STATUS
            try
            {
                // Process data
                IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, _doorControlNotifyPort);
                byte[] received = _udpClientForDoorStatus.EndReceive(res, ref remoteIpEndPoint);
                string recvStr = Encoding.UTF8.GetString(received);
//                logger.Debug("Received UDP from door control port " + remoteIpEndPoint.ToString());
                if (remoteIpEndPoint.Address.Equals(IPAddress.Parse(_doorIPAddress)))
                {
                    _doorStatus.UpdateFromJson(recvStr);
                    _doorStatusRefreshCallback();
                    //                    logger.Debug("Updating UI with  " + recvStr);
                    // Reset the notification counter so we don't keep requesting notifications when we're already getting them
                    _doorStatusRequestNotifyCount = _doorStatusRequestResetTo;
                }
                else
                {
//                    logger.Debug("Not from the door we're managing " + recvStr);
                }
            }
            catch (Exception excp)
            {
                logger.Error("Exception in FrontDoorControl::DoorStatusCallback {0}", excp.Message);
            }
            // Restart receive
            _udpClientForDoorStatus.BeginReceive(new AsyncCallback(DoorStatusCallback), null);

#endif
        }
    }
}

