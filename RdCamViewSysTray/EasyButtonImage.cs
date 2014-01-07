using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RdWebCamSysTrayApp
{
    class EasyButtonImage
    {
        BitmapImage b1, b2;

        public EasyButtonImage(string res1, string res2)
        {
            b1 = new BitmapImage();
            b1.BeginInit();
            b1.UriSource = new Uri(res1, UriKind.Relative);
            b1.EndInit();
            b2 = new BitmapImage();
            b2.BeginInit();
            b2.UriSource = new Uri(res2, UriKind.Relative);
            b2.EndInit();
        }

        public BitmapImage Img1()
        {
            return b1;
        }

        public BitmapImage Img2()
        {
            return b2;
        }

    }
}
