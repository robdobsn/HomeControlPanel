using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeControlPanel
{
    class DeviceManager
    {
        // Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Devices
        private Dictionary<string, DeviceBase> _devices = new Dictionary<string, DeviceBase>();

        public delegate void UIUpdateCallbackType(bool popup);
        UIUpdateCallbackType _uiUpdateCallback;

        public DeviceManager(UIUpdateCallbackType uiUpdateCallback)
        {
            _uiUpdateCallback = uiUpdateCallback;
        }

        // Control device
        public void Control(string deviceName, int idx, string cmd)
        {
            if (_devices.ContainsKey(deviceName))
                _devices[deviceName].Control(idx, cmd);
        }

        public int getVal(string deviceName, int idx, string valType)
        {
            if (_devices.ContainsKey(deviceName))
                return _devices[deviceName].getVal(idx, valType);
            return 0;
        }

        public void Setup(ConfigFileInfo configFileInfo)
        {
            // Iterate devices
            foreach (KeyValuePair<string, DeviceInfo> entry in configFileInfo.GetDevices())
            {
                // Check type
                if (entry.Value.deviceType == "doorlock")
                {
                    // Door controls
                    _devices.Add(entry.Key, new DoorControl(configFileInfo, entry.Value, DoorEventCallback));
                }
                else if (entry.Value.deviceType == "blinds")
                {
                    // Door controls
                    _devices.Add(entry.Key, new BlindsControl(configFileInfo, entry.Value));
                }
                else if (entry.Value.deviceType == "scenecontrol")
                {
                    // Scene control
                    _devices.Add(entry.Key, new HomeScenes(ref configFileInfo, entry.Value));
                }

                //            // Cat deterrent
                //            DeviceInfo devInfo = _configFileInfo.GetDevice("catDeterrent");
                //            if (devInfo != null)
                //                _catDeterrent = new CatDeterrent(devInfo.notifyPort, AutoShowWindowFn,
                //                        ConfigFileInfo.getIPAddressForName(devInfo.hostname), devInfo.port);

                //            // Camera motion
                //            devInfo = _configFileInfo.GetDevice("frontDoorCamera");
                //            if (devInfo != null)
                //                _cameraMotion = new CameraMotion(devInfo.notifyPort, CameraMotionDetectFn);

                //            // Front door
                //            devInfo = _configFileInfo.GetDevice("frontDoorLock");
                //            if (devInfo != null)
                //            {
                //                _frontDoorControl = new DoorControl(devInfo, DoorStatusRefresh);
                //            }

                //            // Garage door
                //            devInfo = _configFileInfo.GetDevice("garageDoorLock");
                //            if (devInfo != null)
                //                _garageDoorControl = new DoorControl(devInfo, GarageStatusRefresh);

                //            // Office blinds
                //            devInfo = _configFileInfo.GetDevice("officeBlinds");
                //            if (devInfo != null)
                //            {
                //                _officeBlindsControl = new BlindsControl(devInfo);
                //            }

                //            // Domoticz
                //            List<string> domoticzIPAddresses = _configFileInfo.GetIPAddrByType("domoticz");
                //            _domoticzControl = new DomoticzControl(domoticzIPAddresses);

                //            // HomeScenes
                //            _homeScenes = new HomeScenes(ref _configFileInfo);

                //            // LedMatrix
                //            devInfo = _configFileInfo.GetDevice("frontDoorLock");
                //            if (devInfo != null)
                //            {
                //                string ledMatrixIpAddress = ConfigFileInfo.getIPAddressForName(devInfo.hostname);
                //                _ledMatrix = new LedMatrix(ledMatrixIpAddress);
                //            }

                //            // Create the video decoders for each video window
                //            devInfo = _configFileInfo.GetDevice("frontDoorCamera");
                //            if (devInfo != null)
                //                _videoStreamDisplays.add(video1, devInfo.rotation, new Uri(devInfo.videoURL), devInfo.username, devInfo.password);
                //            //devInfo = _configFileInfo.GetDevice("garageCamera");
                //            //if (devInfo != null)
                //            //    _videoStreamDisplays.add(video2, devInfo.rotation, new Uri(devInfo.videoURL), devInfo.username, devInfo.password);
                //            //devInfo = _configFileInfo.GetDevice("catCamera");
                //            //if (devInfo != null)
                //            //    _videoStreamDisplays.add(video3, devInfo.rotation, new Uri(devInfo.videoURL), devInfo.username, devInfo.password);

                //            // Volume control
                //            _localAudioDevices = new AudioDevices();
                //            _localAudioDevices.SetOutVolumeWhenListening((float)Properties.Settings.Default.SpkrVol);
                //            outSlider.Value = Properties.Settings.Default.SpkrVol * 100;
                //            _localAudioDevices.SetInVolumeWhenTalking((float)Properties.Settings.Default.MicVol);
                //            inSlider.Value = Properties.Settings.Default.MicVol * 100;

                //            // Audio in/out
                //#if (TALK_TO_CAMERA)
                //            devInfo = _configFileInfo.GetDevice("frontDoorCamera");
                //            if (devInfo != null)
                //                _talkToAxisCamera = new TalkToAxisCamera(ConfigFileInfo.getIPAddressForName(devInfo.hostname), 80,
                //                            devInfo.username, devInfo.password, _localAudioDevices);
                //#endif
                //#if (LISTEN_TO_CAMERA)
                //            devInfo = _configFileInfo.GetDevice("frontDoorCamera");
                //            if (devInfo != null)
                //                _listenToAxisCamera = new ListenToAxisCamera(ConfigFileInfo.getIPAddressForName(devInfo.hostname), 
                //                            _localAudioDevices, devInfo.username, devInfo.password);
                //#endif
            }

        }

        private void DoorEventCallback(DeviceBase device)
        {
            // Update UI
            _uiUpdateCallback(device.getVal(0, "bell") != 0);
        }
    }
}
