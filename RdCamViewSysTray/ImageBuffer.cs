using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RdWebCamSysTrayApp
{
    class ImageBuffer
    {
        private BitmapImage[] _imgBuf;
        //private int _tailIdx = 0;
        //private int _len = 0;

        public ImageBuffer(int bufferSize)
        {
            _imgBuf = new BitmapImage[bufferSize];


        }

        public void Add(BitmapImage bi)
        {
        }

        public BitmapImage GetNthImage(int N)
        {
            return null;
        }
    }
}
