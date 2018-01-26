﻿#define USE_SIMPLEX_AUDIO
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using NLog;

namespace RdWebCamSysTrayApp
{
    public class AudioDevices
    {
        private DeviceInfo _curInDeviceInfo;
        private DeviceInfo _curOutDeviceInfo;
        List<DeviceInfo> inDeviceInfo;
        List<DeviceInfo> outDeviceInfo;
        private bool _isListening = false;
        private int _talkVolumeThresholdForFeedbackSuppress { get; set; }
        private float _outVolumeWhenListening;
        private float OutVolumeFeedbackSuppressFull { get; set; }
        private float _outVolumeWhenListenStarted;
        private bool _outMuteSettingWhenListenStarted;
        private bool _outVolumeMuteWhenTalking;
        private bool _outMuteSettingWhenTalkingStarted;
        private Timer _audioFeedbackSuppressionTimer;
        DateTime _timeOfFirstFeedbackSuppress = DateTime.MinValue;
        DateTime _timeOfLastFeedbackSuppress = DateTime.MinValue;
        private bool _feedbackSuppressing = false;
        private double _audioRoundtripTimeLower = 0;
        private double _audioRoundtripTimeUpper = 750;
        private float _inVolumeWhenTalking;
        private bool _isTalking = false;
        private float _inVolumeWhenTalkingStarted = 0;
        private bool _inMuteSettingWhenTalkingStarted = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public class DeviceInfo
        {
            public string deviceName { get; set; }
            public string deviceId { get; set; }
            public int waveDeviceNumber { get; set; }
            public WaveInCapabilities waveInCaps { get; set; }
            public WaveOutCapabilities waveOutCaps { get; set; }
            public NAudio.CoreAudioApi.MMDevice device { get; set; }

        }

        public AudioDevices()
        {
            _outVolumeWhenListening = 0.5f;
            _outVolumeMuteWhenTalking = true;
            _outMuteSettingWhenTalkingStarted = false;
            OutVolumeFeedbackSuppressFull = 0.0f;
            _audioFeedbackSuppressionTimer = new Timer(100);
            _audioFeedbackSuppressionTimer.Elapsed += new ElapsedEventHandler(OnFeedbackSuppressTimer);
            _talkVolumeThresholdForFeedbackSuppress = 3000;
            _inVolumeWhenTalking = 0.5f;

            // Get and cache device info
            UpdateDeviceInfo();
            SetupAudioDevicesFromConfig();
        }

        public void UpdateDeviceInfo()
        {
            inDeviceInfo = new List<DeviceInfo>();
            outDeviceInfo = new List<DeviceInfo>();

            // Get devices for in and out
            List<WaveInCapabilities> inDevCaps = new List<WaveInCapabilities>();
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities devCaps = WaveIn.GetCapabilities(waveInDevice);
                inDevCaps.Add(devCaps);
                logger.Info("AudioDevices::UpdateDeviceInfo Device {0}: {1}, {2} channels",
                    waveInDevice, devCaps.ProductName, devCaps.Channels);
            }

            List<WaveOutCapabilities> outDevCaps = new List<WaveOutCapabilities>();
            int waveOutDevices = WaveOut.DeviceCount;
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
            {
                WaveOutCapabilities devCaps = WaveOut.GetCapabilities(waveOutDevice);
                outDevCaps.Add(devCaps);
                logger.Info("AudioDevices::UpdateDeviceInfo Device {0}: {1}, {2} channels",
                    waveOutDevice, devCaps.ProductName, devCaps.Channels);
            }

            // Now go through MM devices to match up
            try
            {
                //Instantiate an Enumerator to find audio devices
                NAudio.CoreAudioApi.MMDeviceEnumerator MMDE = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                //Get all the devices, no matter what condition or status
                NAudio.CoreAudioApi.MMDeviceCollection DevCol = MMDE.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.All, NAudio.CoreAudioApi.DeviceState.All);
                //Loop through all devices
                foreach (NAudio.CoreAudioApi.MMDevice dev in DevCol)
                {
                    try
                    {
                        for (int idx = 0; idx < inDevCaps.Count; idx++)
                            if (DevicesMatch(inDevCaps[idx].ProductName, dev.FriendlyName))
                            {
                                DeviceInfo devInfo = new DeviceInfo();
                                devInfo.waveDeviceNumber = idx;
                                devInfo.deviceName = dev.FriendlyName;
                                devInfo.deviceId = dev.ID;
                                devInfo.waveInCaps = inDevCaps[idx];
                                devInfo.device = dev;
                                inDeviceInfo.Add(devInfo);
                            }
                        for (int idx = 0; idx < outDevCaps.Count; idx++)
                            if (DevicesMatch(outDevCaps[idx].ProductName, dev.FriendlyName))
                            {
                                DeviceInfo devInfo = new DeviceInfo();
                                devInfo.waveDeviceNumber = idx;
                                devInfo.deviceName = dev.FriendlyName;
                                devInfo.deviceId = dev.ID;
                                devInfo.waveOutCaps = outDevCaps[idx];
                                devInfo.device = dev;
                                outDeviceInfo.Add(devInfo);
                            }

                    }
                    catch (Exception ex)
                    {
                        //Do something with exception when an audio endpoint could not be muted
                        logger.Error("AudioDevices::UpdateDeviceInfo Exception in Update {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                //When something happend that prevent us to iterate through the devices
                logger.Error("AudioDevices::UpdateDeviceInfo Could not enumerate devices due to an excepion: {0}", ex.Message);
            }
        }

        public void SetupAudioDevicesFromConfig()
        {
            // Go through out devices and find a match
            foreach (DeviceInfo devInfo in outDeviceInfo)
            {
                if (_curOutDeviceInfo == null)
                    _curOutDeviceInfo = devInfo;
                if (devInfo.deviceName == Properties.Settings.Default.Speakers)
                {
                    _curOutDeviceInfo = devInfo;
                    break;
                }
            }
            // Go through in devices and find a match
            foreach (DeviceInfo devInfo in inDeviceInfo)
            {
                if (_curInDeviceInfo == null)
                    _curInDeviceInfo = devInfo;
                if (devInfo.deviceName == Properties.Settings.Default.Microphone)
                {
                    _curInDeviceInfo = devInfo;
                    break;
                }
            }

        }

        private bool DevicesMatch(string dev1, string dev2)
        {
            int stLen = Math.Min(dev1.Length, dev2.Length);
            if (stLen > 31)
                stLen = 31;
            if (stLen == 0)
                return false;
            return dev1.Substring(0, stLen) == dev2.Substring(0, stLen);
        }
    
        // Functions for audio feedback suppression

        public void StartingListening()
        {
            _outVolumeWhenListenStarted = GetOutVolume();
            _outMuteSettingWhenListenStarted = GetOutMute();
            SetOutVolume(_outVolumeWhenListening);
            _audioFeedbackSuppressionTimer.Start();
            _isListening = true;
            logger.Info("AudioDevices::StartingListenting");
        }

        public void StoppingListening()
        {
            _isListening = false;
            _audioFeedbackSuppressionTimer.Stop();
            SetOutVolume(_outVolumeWhenListenStarted, _outMuteSettingWhenListenStarted);
        }

        public void StartingTalking()
        {
            if (_outVolumeMuteWhenTalking)
            {
                _outMuteSettingWhenTalkingStarted = GetOutMute();
                SetOutMute(true);
            }
            _inVolumeWhenTalkingStarted = GetInVolume();
            _inMuteSettingWhenTalkingStarted = GetInMute();
            SetInVolume(_inVolumeWhenTalking);
            _isTalking = true;
        }

        public void StoppingTalking()
        {
            if (_outVolumeMuteWhenTalking)
            {
                SetOutMute(_outMuteSettingWhenTalkingStarted);
            }
            _isTalking = false;
        }

        public void SuppressAudioFeedback(int peakLocalMicVolume)
        {
#if (USE_SIMPLEX_AUDIO)

            return;         // This is disabled for now - using simplex audio - muting speaker when talking
#else
            if (peakLocalMicVolume > _talkVolumeThresholdForFeedbackSuppress)
            {
                if (_timeOfFirstFeedbackSuppress == DateTime.MinValue)
                    _timeOfFirstFeedbackSuppress = DateTime.Now;
                _timeOfLastFeedbackSuppress = DateTime.Now;
            }

            // Set volume
            /*int _talkVolumeThreshold = 1500;

            _oldVolume = _localAudioDevices.GetOutVolume();
            _localAudioDevices.SetOutVolume(_localAudioDevices.RequiredOutVolLevel);*/
#endif
        }

        private void OnFeedbackSuppressTimer(object source, ElapsedEventArgs e)
        {
            if (_timeOfFirstFeedbackSuppress == DateTime.MinValue)
                return;

            double msSinceFirstSuppress = (DateTime.Now - _timeOfFirstFeedbackSuppress).TotalMilliseconds;
            double msSinceLastSuppress = (DateTime.Now - _timeOfLastFeedbackSuppress).TotalMilliseconds;
            if ((msSinceFirstSuppress > _audioRoundtripTimeLower) && (msSinceLastSuppress < _audioRoundtripTimeUpper))
            {
/*                if (!_feedbackSuppressing)
                {
                    SetOutVolume(_outVolumeFeedbackSuppress);
                    _feedbackSuppressing = true;
                }
 */
                SetOutVolume(OutVolumeFeedbackSuppressFull);
                _feedbackSuppressing = true;
            }
            else
            {
                if (_feedbackSuppressing)
                {
                    SetOutVolume(_outVolumeWhenListening);
                    _feedbackSuppressing = false;
                    _timeOfFirstFeedbackSuppress = DateTime.MinValue;
                }
            }
        }
        

        public void SetOutVolume(float volLevel, bool bMute = false)
        {

            try
            {
                //Set at required volume
                if (_curOutDeviceInfo != null)
                {
                    _curOutDeviceInfo.device.AudioEndpointVolume.MasterVolumeLevelScalar = volLevel;
                    _curOutDeviceInfo.device.AudioEndpointVolume.Mute = bMute;
                }

                //Get its audio volume
                logger.Info("AudioDevices::SetOutVolume Volume of {0} is {1}", _curOutDeviceInfo.deviceName, 
                                _curOutDeviceInfo.device.AudioEndpointVolume.MasterVolumeLevelScalar.ToString());
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::SetOutVolume {0}", excp.Message);
            }
        }

/*        public void StepDownOutVolume()
        {
            try
            {
                _curOutDevice.AudioEndpointVolume.MasterVolumeLevelScalar = _curOutDevice.AudioEndpointVolume.MasterVolumeLevelScalar * OutVolumeFeedbackSuppressStep;

                //Get its audio volume
                System.Diagnostics.Debug.Print("Volume of " + _curOutDevice.FriendlyName + " is " + _curOutDevice.AudioEndpointVolume.MasterVolumeLevelScalar.ToString());
            }
            catch (Exception e)
            {
            }

        }
*/
        public float GetOutVolume()
        {
            try
            {
                if (_curOutDeviceInfo != null)
                    return _curOutDeviceInfo.device.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::GetOutVolume {0}", excp.Message);
            }
            return 0;
        }

        public bool GetOutMute()
        {
            try
            {
                if (_curOutDeviceInfo != null)
                    return _curOutDeviceInfo.device.AudioEndpointVolume.Mute;
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::GetOutMute {0}", excp.Message);
            }
            return false;
        }

        public void SetOutMute(bool mute)
        {
            try
            {
                if (_curOutDeviceInfo != null)
                    _curOutDeviceInfo.device.AudioEndpointVolume.Mute = mute;
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::SetOutMute {0}", excp.Message);
            }
        }

        public float GetInVolume()
        {
            float lev = 0;
            try
            {
                if (_curInDeviceInfo != null)
                    lev = _curInDeviceInfo.device.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::GetInVolume {0}", excp.Message);

            }
            return lev;
        }

        public bool GetInMute()
        {
            bool mute = false;
            try
            {
                if (_curInDeviceInfo != null)
                    mute = _curInDeviceInfo.device.AudioEndpointVolume.Mute;
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::GetInMute {0}", excp.Message);
            }
            return mute;
        }

        public void SetInVolume(float volLevel, bool bMute = false)
        {
            try
            {
                //Set at required volume
                if (_curInDeviceInfo != null)
                {
                    _curInDeviceInfo.device.AudioEndpointVolume.MasterVolumeLevelScalar = volLevel;
                    _curInDeviceInfo.device.AudioEndpointVolume.Mute = bMute;
                }

                //Get its audio volume
                logger.Info("Volume of {0} is {1}", _curInDeviceInfo.deviceName,
                                _curInDeviceInfo.device.AudioEndpointVolume.MasterVolumeLevelScalar.ToString());
            }
            catch (Exception excp)
            {
                logger.Error("Exception in AudioDevices::SetInVolume {0}", excp.Message);
            }
        }

        public List<DeviceInfo> GetInDeviceInfo()
        {
            return inDeviceInfo;
        }

        public List<DeviceInfo> GetOutDeviceInfo()
        {
            return outDeviceInfo;
        }

        public int GetCurWaveOutDeviceNumber()
        {
            if (_curOutDeviceInfo != null)
                return _curOutDeviceInfo.waveDeviceNumber;
            return 0;
        }

        public int GetCurWaveInDeviceNumber()
        {
            if (_curInDeviceInfo != null)
                return _curInDeviceInfo.waveDeviceNumber;
            return 0;
        }

        public string GetCurWaveOutDeviceName()
        {
            if (_curOutDeviceInfo != null)
                return _curOutDeviceInfo.deviceName;
            return "";
        }

        public string GetCurWaveInDeviceName()
        {
            if (_curInDeviceInfo != null)
                return _curInDeviceInfo.deviceName;
            return "";
        }

        public void SetOutVolumeWhenListening(float ov)
        {
            _outVolumeWhenListening = ov;
            if (_isListening && !_feedbackSuppressing)
                SetOutVolume(ov);
        }

        public void SetInVolumeWhenTalking(float iv)
        {
            _inVolumeWhenTalking = iv;
            if (_isTalking)
                SetInVolume(iv);
        }
    }
}
