using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Threading;
using System.Timers;
using NLog;

namespace HomeControlPanel
{
    public class RawSourceWaveStream : WaveStream
    {
        private Stream sourceStream;
        private WaveFormat waveFormat;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public RawSourceWaveStream(Stream sourceStream, WaveFormat waveFormat)
        {
            this.sourceStream = sourceStream;
            this.waveFormat = waveFormat;
        }

        public override WaveFormat WaveFormat
        {
            get { return this.waveFormat; }
        }

        public override long Length
        {
            get { return 1000; }  // This value doesn't seem to matter! RD
        }

        public override long Position
        {
            get
            {
                return this.sourceStream.Position;
            }
            set
            {
                this.sourceStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            try
            {
                bytesRead = sourceStream.Read(buffer, 0, count);
            }
            catch (Exception e)
            {
                logger.Error("Exception in RawSourceWaveStream::Read {0}", e.Message);
            }

            return bytesRead;
        }
    }

    class ListenToAxisCamera
    {
        private string _ipAddress;
        private bool _isListening = false;
        private bool _reqToStop = false;
        private AudioDevices _audioDevices;
        private System.Timers.Timer _timerForListeningForAFixedTime;
        private System.Timers.Timer _timeOutForStoppingListening;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private String _username;
        private String _password;

        public ListenToAxisCamera(string ipAddress, AudioDevices audioDevices,
                        String username, String password)
        {
            _ipAddress = ipAddress;
            _audioDevices = audioDevices;
            _username = username;
            _password = password;
        }

        public bool IsListening()
        {
            return _isListening;
        }

        public void Start()
        {
            if (!_isListening)
            {
                requestAudio();
                _audioDevices.StartingListening();
                _isListening = true;
            }
        }

        public void Stop()
        {
            if (_isListening)
            {
                _reqToStop = true;
                _audioDevices.StoppingListening();
                _timeOutForStoppingListening = new System.Timers.Timer(500.0);
                _timeOutForStoppingListening.Elapsed += new ElapsedEventHandler(OnStoppingListeningTimoutTimer);
                //                _timeOutForStoppingListening.Start();
                _isListening = false;
            }
        }

        public void ListenForAFixedPeriod(int listenTimeInSecs)
        {
            _timerForListeningForAFixedTime = new System.Timers.Timer(listenTimeInSecs * 1000.0);
            _timerForListeningForAFixedTime.Elapsed += new ElapsedEventHandler(OnListenTimoutTimer);
            _timerForListeningForAFixedTime.Start();
            Start();
        }

        public void OnListenTimoutTimer(object source, ElapsedEventArgs e)
        {
            _timerForListeningForAFixedTime.Stop();
            Stop();
        }

        public void OnStoppingListeningTimoutTimer(object source, ElapsedEventArgs e)
        {
            if (_reqToStop)
            {
                _isListening = false;
                _reqToStop = false;
            }
        }

        private void requestAudio()
        {
            string getVars = "";
            //Initialization
            try
            {
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(string.Format("http://" + _ipAddress + "/axis-cgi/audio/receive.cgi?httptype=singlepart", getVars));
                webReq.Method = "GET";
                webReq.AllowReadStreamBuffering = false;
                String svcCredentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(_username + ":" + _password));
                webReq.Headers.Add("Authorization", "Basic " + svcCredentials);
                webReq.BeginGetResponse(new AsyncCallback(GetAudioAsync), webReq);
            }
            catch (Exception excp)
            {
                logger.Error("Exception in ListenToAxisCamera::requestAudio {0}", excp.Message);
            }
        }

        private void GetAudioAsync(IAsyncResult res)
        {
            HttpWebRequest request = (HttpWebRequest)res.AsyncState;
            if (request == null)
                return;
            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(res);
            if (response == null)
                return;

            var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);
            Stream respStream = response.GetResponseStream();

            var reader = new RawSourceWaveStream(respStream, waveFormat);
            using (WaveStream convertedStream = WaveFormatConversionStream.CreatePcmStream(reader))
            {
                using (WaveOutEvent waveOut = new WaveOutEvent())
                {
                    waveOut.DeviceNumber = _audioDevices.GetCurWaveOutDeviceNumber();
                    waveOut.Init(convertedStream);
                    while (true)
                    {
                        // Check if we should be stopping
                        if (_reqToStop)
                        {
                            request.Abort();
                            _isListening = false;
                            _reqToStop = false;
                            logger.Info("ListenToAxisCamera::GetAudioAsync::Request aborted");
                            break;
                        }

                        // Play the audio
                        waveOut.Play();
//                        Thread.Sleep(1);
                    }
                }
            }

            /*
             *          TEST CODE TO JUST SHOW STREAM READS - MUST COMMENT OUT ABOVE TO TRY
             *          
                        Stream r = response.GetResponseStream();
                        byte[] data = new byte[4096];
                        int read;
                        while ((read = r.Read(data, 0, data.Length)) > 0)
                        {
                            Console.WriteLine(read);
                        }
                        */
        }
    }
}
