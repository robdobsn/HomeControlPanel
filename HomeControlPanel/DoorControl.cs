#define USE_MANAGED_MQTT
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
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Client.Options;
using System.Security.Policy;

namespace HomeControlPanel
{
    /// Constructor
    class DoorControl : DeviceBase
    {
        // Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Callback into Main Window when door status has changed - used to pop-up window
        public delegate void DoorEventCallback(DoorControl doorControl);
        private DoorEventCallback _doorEventCallback;

        // Last time door status received
        private DateTime _lastDoorStatusTime = DateTime.MinValue;

        // Device info for door
        DeviceInfo _deviceInfo;

        // Multi MQTT client
        private MultiMQTTClient _multiMQTTClient;

        // This is the format received from the door controller
        public class JsonDoorStatus
        {
            public string name = "";
            public string locked = "";
            public string open = "";
            public string lockMs = "";
        }
        public class JsonStatus
        {
            public IList<JsonDoorStatus> doors;
            public string bell = "";
        }

        // Status of the door controller
        public class DoorStatus
        {
            public JsonStatus _statusInfoFromJson;
            public string [] _doorLockStrs = { "locked", "locked" };
            public string [] _doorOpenStrs = { "closed", "closed" };
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
                for (int doorIdx = 0; doorIdx < _statusInfoFromJson.doors.Count(); doorIdx++)
                {
                    if (doorIdx >= _doorLockStrs.Length)
                        continue;
                    JsonDoorStatus doorStatus = _statusInfoFromJson.doors[doorIdx];
                    _doorLockStrs[doorIdx] = (doorStatus.locked == "Y" ? "locked" : (doorStatus.locked == "N" ? "unlocked" : "moving"));
                    _doorOpenStrs[doorIdx] = (doorStatus.open == "Y" ? "open" : (doorStatus.open == "N" ? "closed" : "unknown"));
                }
                _bellPressed = (_statusInfoFromJson.bell == "Y") ? true : false;
            }

            public void UpdateFromJson(string jsonStr)
            {
                try
                {
                    if (jsonStr.Contains("bell"))
                    {
                        _statusInfoFromJson = JsonConvert.DeserializeObject<JsonStatus>(jsonStr);
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

        // Front Doot Control Constructor
        public DoorControl(ConfigFileInfo configFileInfo, DeviceInfo devInfo, 
                    DoorEventCallback doorEventCallback, MultiMQTTClient multiMQTTClient)
        {
            // Device info
            _deviceInfo = devInfo;
            _doorEventCallback = doorEventCallback;

            // Cache useful info
            _doorUserNumber = devInfo.userNum;
            _doorUserPin = devInfo.userPin;

            // MQTT
            _multiMQTTClient = multiMQTTClient;
            _multiMQTTClient.subscribe(_deviceInfo.mqttOutTopic,
                appMsg => {
                    // Update last time
                    _lastDoorStatusTime = DateTime.Now;

                    // Handle message received
                    _doorStatus.UpdateFromJson(Encoding.UTF8.GetString(appMsg.Payload));
                    _doorEventCallback(this);

                    // Debug
                    logger.Info($"{appMsg.Topic} {Encoding.UTF8.GetString(appMsg.Payload)} QoS = {appMsg.QualityOfServiceLevel} Retain = {appMsg.Retain}");

                });
        }

        /// CallDoorApiFunction
        private void CallDoorApiFunction(String functionAndArgs)
        {
            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_deviceInfo.mqttInTopic)
                    .WithPayload(functionAndArgs)
                    .WithExactlyOnceQoS()
                    .Build();

                _multiMQTTClient.publishAsync(message);
            }
            catch (Exception e)
            {
                logger.Error("FrontDoorControl::CallDoorApiFunction MQTT exception {0}", e.Message);
            }
        }

        /// DoorApiFnCompleted
        private void DoorApiFnCompleted(string rsltStr)
        {
            logger.Info("DoorControl::DoorApiCall ok {0}", rsltStr);
            _doorStatus.UpdateFromJson(rsltStr);
            _doorEventCallback(this);
        }

        public void Control(int idx, string cmd)
        {
            if (cmd == "unlock")
                ControlDoor("u/" + idx.ToString() + "/" + _doorUserNumber.ToString() + "/" + _doorUserPin);
            else
                ControlDoor("l/" + idx.ToString());
        }

        public int GetVal(int idx, string valType)
        {
            if (idx == 0 && valType == "bell")
                return _doorStatus._bellPressed ? 1 : 0;
            else if (valType == "locked")
                return _doorStatus._doorLockStrs[idx] == "locked" ? 1 : 0;
            else if (valType == "closed")
                return _doorStatus._doorOpenStrs[idx] == "closed" ? 1 : 0;
            else if (valType == "open")
                return _doorStatus._doorOpenStrs[idx] == "open" ? 1 : 0;
            else if (valType == "sinceUpdateSecs")
                return _lastDoorStatusTime == DateTime.MinValue ? -1 : (int)(DateTime.Now - _lastDoorStatusTime).TotalSeconds;
            return 0;
        }
        public string GetString(int idx, string valType)
        {
            return "";
        }

        private void ControlDoor(string doorCommand)
        {
                CallDoorApiFunction(doorCommand);
        }

        private void OnDoorStatusTimer(object source, ElapsedEventArgs e)
        {
#if POLL_FOR_TCP_DOOR_STATUS
            CallDoorApiFunction("q");
#endif
        }

        public bool GetDoorStatus(out DoorStatus doorStatus)
        {
            doorStatus = _doorStatus;
            return (DateTime.Now - _doorStatus._lastDoorStatusTime).TotalSeconds < 30;
        }

    }
}

