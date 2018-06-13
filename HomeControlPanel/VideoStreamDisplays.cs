using MjpegProcessor;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HomeControlPanel
{
    internal class VideoStreamDisplay
    {
        private MjpegDecoder _decoder = new MjpegDecoder();
        private System.Windows.Controls.Image _dispImage;
        private int _rotationAngle = 0;
        private Uri _streamUri;

        public VideoStreamDisplay(System.Windows.Controls.Image img, int rotationAngle, Uri streamUri)
        {
            _dispImage = img;
            _rotationAngle = rotationAngle;
            _streamUri = streamUri;
            _decoder.FrameReady += handleFrameFn;
        }

        public void start()
        {
            _decoder.ParseStream(_streamUri);
        }

        public void stop(bool unsubscribeEvents = false)
        {
            _decoder.StopStream();
            if (unsubscribeEvents)
            {
                _decoder.FrameReady -= handleFrameFn;
            }
        }

        private void handleFrameFn(object sender, FrameReadyEventArgs e)
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

        public void add(System.Windows.Controls.Image img, int rotationAngle, Uri streamUri)
        {
            VideoStreamDisplay display = new VideoStreamDisplay(img, rotationAngle, streamUri);
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