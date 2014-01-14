using MathNet.Numerics.Transformations;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NLog;

namespace RdWebCamSysTrayApp
{
    public class RawSourceWaveInStream : WaveStream
    {
        private Stream sourceStream;
        private WaveFormat waveFormat;

        public RawSourceWaveInStream(Stream sourceStream, WaveFormat waveFormat)
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
            get { return sourceStream.Length; }
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
            int bytesRead = sourceStream.Read(buffer, 0, count);
//            Console.WriteLine("Read offset " + offset + ", count " + count + ", read " + bytesRead);
            return bytesRead;
        }
    }

    public class TalkToAxisCamera
    {
        private readonly object _semaphore = new object();
        private bool _bTalking = false;
        private string _username;
        private string _password;
        private string _cameraIP;
        private int _cameraPort;
        private TcpClient _tcpClient;
        private NetworkStream _avStream;
        private MemoryStream _memStream;
        private WaveFormatConversionStream _downsamplerStream;
        private WaveFormatConversionStream _converterStream;
        private RawSourceWaveInStream _waveStream;
        private mulaw mulawCodec = new mulaw();
        private int _peakTalkVolume = 0;
        private AudioDevices _localAudioDevices;
        private NAudio.Wave.WaveIn _waveInDevice;
        private NAudio.Wave.WaveFormat _waveFormat;
        private const int _NUM_SAMPLES = 3000;
        private byte[] _byteBuffer = new byte[_NUM_SAMPLES];
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public TalkToAxisCamera(string cameraIP, int cameraPort, string username, string password, AudioDevices localAudioDevices)
        {
            _username = username;
            _password = password;
            _cameraIP = cameraIP;
            _cameraPort = cameraPort;
            _localAudioDevices = localAudioDevices;
        }

        public bool IsTalking()
        {
            return _bTalking;
        }

        public int PeakTalkVolume()
        {
            return _peakTalkVolume;
        }

        public void StartTalk()
        {
            string sPost = "POST /axis-cgi/audio/transmit.cgi HTTP/1.0\r\n";
            //sPost += "Content-Type: multipart/x-mixed-replace; boundary=--myboundary\r\n";
            sPost += "Content-Type: audio/basic\r\n";
            sPost += "Content-Length: 9999999\r\n";
            sPost += "Connection: Keep-Alive\r\n";  // Have read about problems with .NET keep-alive but this seems to work ok
            sPost += "Cache-Control: no-cache\r\n";

            string usernamePassword = _username + ":" + _password;
            sPost += "Authorization: Basic " + Convert.ToBase64String(new ASCIIEncoding().GetBytes(usernamePassword)) + "\r\n\r\n";

            _tcpClient = new TcpClient(_cameraIP, _cameraPort);
            _avStream = _tcpClient.GetStream();

            byte[] hdr = Encoding.ASCII.GetBytes(sPost);
            _avStream.Write(hdr, 0, hdr.Length);

            _waveFormat = new NAudio.Wave.WaveFormat(44100, NAudio.Wave.WaveIn.GetCapabilities(_localAudioDevices.GetCurWaveInDeviceNumber()).Channels);
            _memStream = new MemoryStream();
            _waveStream = new RawSourceWaveInStream(_memStream, _waveFormat);

            // Downsampler converts from 44KHz to 8KHz
            _downsamplerStream = new WaveFormatConversionStream(new WaveFormat(8000, _waveFormat.BitsPerSample, 1), _waveStream);

            // MuLaw coder converts PCM to mulaw
            WaveFormat muLaw = WaveFormat.CreateMuLawFormat(8000, 1);
            _converterStream = new WaveFormatConversionStream(muLaw, _downsamplerStream);

            // Start microphone
            OpenWaveInDevice();

            logger.Info("Talk Starting");

            _localAudioDevices.StartingTalking();

            _bTalking = true;

        }

        public void OpenWaveInDevice()
        {
            _waveInDevice = new NAudio.Wave.WaveIn();
            _waveInDevice.DeviceNumber = _localAudioDevices.GetCurWaveInDeviceNumber();
            _waveInDevice.WaveFormat = _waveFormat;
            _waveInDevice.DataAvailable += new EventHandler<WaveInEventArgs>(wi_DataAvailable);
            _waveInDevice.StartRecording();
        }

        public void StopTalk()
        {
            _localAudioDevices.StoppingTalking();

            if (_bTalking)
            {
                lock (_semaphore)
                {
                    logger.Info("Talk Stopping");
                    _bTalking = false;

                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                        _tcpClient = null;
                    }

                    if (_avStream != null)
                    {
                        _avStream.Close();
                        _avStream.Dispose();
                        _avStream = null;
                    }

                    if (_waveStream != null)
                    {
                        _waveStream.Close();
                    }

                    if (_converterStream != null)
                    {
                        _converterStream.Close();
                        _converterStream = null;
                    }

                    if (_downsamplerStream != null)
                    {
                        _downsamplerStream.Close();
                        _downsamplerStream = null;
                    }

                    if (_waveInDevice != null)
                    {
                        _waveInDevice.StopRecording();
                        _waveInDevice = null;
                    }
                }
            }
        }

        private void wi_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_bTalking)
            {
                lock (_semaphore)
                {

                    try
                    {
                        _memStream.Write(e.Buffer, 0, e.BytesRecorded);
                        _memStream.Seek(0, SeekOrigin.Begin);
                        //                    Console.WriteLine("BytesRec " + e.BytesRecorded + ", MemStrLen " + _memStream.Length);
                    }
                    catch (Exception excp)
                    {
                        logger.Error("TalkToAxisCamera::wiDataAvailable _memStream.Write excp {0}", excp.Message);
                    }

                    try
                    {
                        int numRead;
                        int totalRead = 0;
                        int pkVal = 0;
                        while ((numRead = _converterStream.Read(_byteBuffer, 0, _byteBuffer.Length)) != 0)
                        {
                            // Send to camera
                            _avStream.Write(_byteBuffer, 0, numRead);
                            // logger.Info("converter read {0}", numRead);

                            // Get peak volume
                            for (int i = 0; i < numRead; i++)
                            {
                                short inVal = mulawCodec.decode(_byteBuffer[i]);

                                // Get peak value
                                if (pkVal < inVal)
                                    pkVal = inVal;

                            }
                            totalRead += numRead;

                        }

                        _peakTalkVolume = pkVal;
                        _localAudioDevices.SuppressAudioFeedback(_peakTalkVolume);

                    }
                    catch (Exception excp)
                    {
                        logger.Error("TalkToAxisCamera::wiDataAvailable _avStreamWrite() excp {0}", excp.Message);
                    }

                    try
                    {
                        _memStream.SetLength(0);
                    }
                    catch (Exception excp)
                    {
                        logger.Error("TalkToAxisCamera::wiDataAvailable _memStream.SetLength excp {0}", excp.Message);
                    }

                    //try
                    //{
                    //    if (_avStream.DataAvailable)
                    //    {
                    //        _avStream.re
                    //    }
                    //}

                }
            }
        }
    }
}
