using MjpegProcessor;
using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HomeControlPanel
{
    internal class VideoStreamDisplay
    {
        // Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private MjpegDecoder _mjpegDecoder = null;
        private System.Windows.Controls.Image _dispImage;
        private int _rotationAngle = 0;
        private Uri _streamUri;
        private String _username;
        private String _password;
        CancellationToken _rtspCancelToken;

        public VideoStreamDisplay(System.Windows.Controls.Image img, int rotationAngle,
                    Uri streamUri, String username = "", String password = "")
        {
            _dispImage = img;
            _rotationAngle = rotationAngle;
            _streamUri = streamUri;
            _mjpegDecoder = new MjpegDecoder();
            _mjpegDecoder.FrameReady += handleMjpegFrameFn;
            _username = username;
            _password = password;
        }

        public void start()
        {
            try
            {
                if (_mjpegDecoder != null)
                {
                    if (_username != "")
                        _mjpegDecoder.ParseStream(_streamUri, _username, _password);
                    else
                        _mjpegDecoder.ParseStream(_streamUri);
                }
            }
            catch (Exception excp)
            {
                logger.Info("VideoStreamDisplay::start " + excp.ToString());
            }
        }

        public void stop(bool unsubscribeEvents = false)
        {
            _mjpegDecoder.StopStream();
            if (unsubscribeEvents)
            {
                _mjpegDecoder.FrameReady -= handleMjpegFrameFn;
            }
        }

        private void handleMjpegFrameFn(object sender, FrameReadyEventArgs e)
        {

            //    Int32Rect cropRect = new Int32Rect(400, 380, 300, 200);
            //    BitmapSource croppedImage = new CroppedBitmap(e.BitmapImage, cropRect);
            //    image4.Source = croppedImage;

            if (_rotationAngle != 0)
            {
                TransformedBitmap tmpImage = new TransformedBitmap();

                tmpImage.BeginInit();
                tmpImage.Source = e.BitmapImage; // of type BitmapImage

                RotateTransform transform = new RotateTransform(_rotationAngle);
                tmpImage.Transform = transform;
                tmpImage.EndInit();

                _dispImage.Source = tmpImage;
            }
            else
            {
                _dispImage.Source = e.BitmapImage;
            }
        }
    }

    internal class VideoStreamDisplays
    {
        private List<VideoStreamDisplay> _displays = new List<VideoStreamDisplay>();

        public void add(System.Windows.Controls.Image img, int rotationAngle, Uri streamUri,
                        String username, String password)
        {
            VideoStreamDisplay display = new VideoStreamDisplay(img, rotationAngle, streamUri,
                        username, password);
            _displays.Add(display);
        }

        public void start()
        {
            foreach (VideoStreamDisplay disp in _displays)
                disp.start();
        }

        public void stop(bool unsubscribeEvents = false)
        {
            foreach (VideoStreamDisplay disp in _displays)
                disp.stop(unsubscribeEvents);
        }

    }

}