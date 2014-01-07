﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace RdWebCamSysTrayApp
{
    public class AudioDevices
    {
        private NAudio.CoreAudioApi.MMDevice _curInDevice;
        private NAudio.CoreAudioApi.MMDevice _curOutDevice;
        private int _curWaveInDeviceInfoIdx = 0;
        private int _curWaveOutDeviceInfoIdx = 0;
        List<DeviceInfo> inDeviceInfo;
        List<DeviceInfo> outDeviceInfo;
        private bool _isListening = false;
        private int _talkVolumeThresholdForFeedbackSuppress { get; set; }
        private float _outVolumeWhenListening;
        private float OutVolumeFeedbackSuppressFull { get; set; }
        private float _outVolumeWhenListenStarted;
        private bool _outMuteSettingWhenListenStarted;
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

        public class DeviceInfo
        {
            public string deviceName { get; set; }
            public string deviceId { get; set; }
            public int waveDeviceNumber { get; set; }
            public WaveInCapabilities waveInCaps { get; set; }
            public WaveOutCapabilities waveOutCaps { get; set; }

        }

        public AudioDevices()
        {
            _outVolumeWhenListening = 0.5f;
            OutVolumeFeedbackSuppressFull = 0.0f;
            _audioFeedbackSuppressionTimer = new Timer(100);
            _audioFeedbackSuppressionTimer.Elapsed += new ElapsedEventHandler(OnFeedbackSuppressTimer);
            _talkVolumeThresholdForFeedbackSuppress = 3000;
            _inVolumeWhenTalking = 0.5f;

            // Get and cache device info
            UpdateDeviceInfo();
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
                Console.WriteLine("Device {0}: {1}, {2} channels",
                    waveInDevice, devCaps.ProductName, devCaps.Channels);
            }

            List<WaveOutCapabilities> outDevCaps = new List<WaveOutCapabilities>();
            int waveOutDevices = WaveOut.DeviceCount;
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
            {
                WaveOutCapabilities devCaps = WaveOut.GetCapabilities(waveOutDevice);
                outDevCaps.Add(devCaps);
                Console.WriteLine("Device {0}: {1}, {2} channels",
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
                                inDeviceInfo.Add(devInfo);
                                if (_curInDevice == null)
                                {
                                    _curInDevice = dev;
                                    _curWaveInDeviceInfoIdx = inDeviceInfo.Count - 1;
                                }
                            }
                        for (int idx = 0; idx < outDevCaps.Count; idx++)
                            if (DevicesMatch(outDevCaps[idx].ProductName, dev.FriendlyName))
                            {
                                DeviceInfo devInfo = new DeviceInfo();
                                devInfo.waveDeviceNumber = idx;
                                devInfo.deviceName = dev.FriendlyName;
                                devInfo.deviceId = dev.ID;
                                devInfo.waveOutCaps = outDevCaps[idx];
                                outDeviceInfo.Add(devInfo);
                                bool bUseThis = false;
                                if (_curOutDevice == null)
                                    bUseThis = true;
                                else if (!_curOutDevice.FriendlyName.Contains("Speakers") && dev.FriendlyName.Contains("Speakers"))
                                    bUseThis = true;
                                if (bUseThis)
                                {
                                    _curOutDevice = dev;
                                    _curWaveOutDeviceInfoIdx = outDeviceInfo.Count - 1;
                                }
                            }                            

                    }
                    catch (Exception ex)
                    {
                        //Do something with exception when an audio endpoint could not be muted
                        System.Diagnostics.Debug.Print("Exception in Update " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                //When something happend that prevent us to iterate through the devices
                System.Diagnostics.Debug.Print("Could not enumerate devices due to an excepion: " + ex.Message);
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
        }

        public void StoppingListening()
        {
            _isListening = false;
            _audioFeedbackSuppressionTimer.Stop();
            SetOutVolume(_outVolumeWhenListenStarted, _outMuteSettingWhenListenStarted);
        }

        public void StartingTalking()
        {
            _inVolumeWhenTalkingStarted = GetInVolume();
            _inMuteSettingWhenTalkingStarted = GetInMute();
            SetInVolume(_inVolumeWhenTalking);
            _isTalking = true;
        }

        public void StoppingTalking()
        {
            _isTalking = false;
        }

        public void SuppressAudioFeedback(int peakLocalMicVolume)
        {
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
                _curOutDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volLevel;
                _curOutDevice.AudioEndpointVolume.Mute = bMute;

                //Get its audio volume
                System.Diagnostics.Debug.Print("Volume of " + _curOutDevice.FriendlyName + " is " + _curOutDevice.AudioEndpointVolume.MasterVolumeLevelScalar.ToString());
            }
            catch (Exception excp)
            {
                Console.WriteLine("Exception in AudioDevices::SetOutVolume " + excp.Message);
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
            return _curOutDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
        }

        public bool GetOutMute()
        {
            return _curOutDevice.AudioEndpointVolume.Mute;
        }

        public float GetInVolume()
        {
            float lev = 0;
            try
            {
                lev = _curInDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
            catch (Exception excp)
            {
                Console.WriteLine("Exception in AudioDevices::GetInVolume " + excp.Message);

            }
            return lev;
        }

        public bool GetInMute()
        {
            bool mute = false;
            try
            {
                mute = _curInDevice.AudioEndpointVolume.Mute;
            }
            catch (Exception excp)
            {
                Console.WriteLine("Exception in AudioDevices::GetInMute " + excp.Message);
            }
            return mute;
        }

        public void SetInVolume(float volLevel, bool bMute = false)
        {
            try
            {
                //Set at required volume
                _curInDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volLevel;
                _curInDevice.AudioEndpointVolume.Mute = bMute;

                //Get its audio volume
                System.Diagnostics.Debug.Print("Volume of " + _curInDevice.FriendlyName + " is " + _curInDevice.AudioEndpointVolume.MasterVolumeLevelScalar.ToString());
            }
            catch (Exception excp)
            {
                Console.WriteLine("Exception in AudioDevices::SetInVolume " + excp.Message);
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
            if (_curWaveOutDeviceInfoIdx < outDeviceInfo.Count)
                return outDeviceInfo[_curWaveOutDeviceInfoIdx].waveDeviceNumber;
            return 0;
        }

        public int GetCurWaveInDeviceNumber()
        {
            if (_curWaveInDeviceInfoIdx < inDeviceInfo.Count)
                return inDeviceInfo[_curWaveInDeviceInfoIdx].waveDeviceNumber;
            return 0;
        }

        public string GetCurWaveOutDeviceName()
        {
            if (_curWaveOutDeviceInfoIdx < outDeviceInfo.Count)
                return outDeviceInfo[_curWaveOutDeviceInfoIdx].deviceName;
            return "";
        }

        public string GetCurWaveInDeviceName()
        {
            if (_curWaveInDeviceInfoIdx < inDeviceInfo.Count)
                return inDeviceInfo[_curWaveInDeviceInfoIdx].deviceName;
            return "";
        }

        public void SetWaveOutDeviceName(string devName)
        {
            for (int i = 0; i < outDeviceInfo.Count; i++)
            {
                if (devName == outDeviceInfo[i].deviceName)
                {
                    _curWaveOutDeviceInfoIdx = i;
                    break;
                }
            }
        }

        public void SetWaveInDeviceName(string devName)
        {
            for (int i = 0; i < inDeviceInfo.Count; i++)
            {
                if (devName == inDeviceInfo[i].deviceName)
                {
                    _curWaveInDeviceInfoIdx = i;
                    break;
                }
            }
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