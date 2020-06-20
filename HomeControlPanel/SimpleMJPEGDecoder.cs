using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Headers;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HomeControlPanel
{
    class SimpleMJPEGDecoder
    {
        Dispatcher _uiDispatcher = null;
        Action<BitmapImage> _frameCallback = null;

        // JPEG delimiters
        const byte picMarker = 0xFF;
        const byte picStart = 0xD8;
        const byte picEnd = 0xD9;

        /// <summary>
        /// Start a MJPEG on a http stream
        /// </summary>
        /// <param name="action">Delegate to run at each frame</param>
        /// <param name="url">url of the http stream (only basic auth is implemented)</param>
        /// <param name="login">optional login</param>
        /// <param name="password">optional password (only basic auth is implemented)</param>
        /// <param name="token">cancellation token used to cancel the stream parsing</param>
        /// <param name="chunkMaxSize">Max chunk byte size when reading stream</param>
        /// <param name="frameBufferSize">Maximum frame byte size</param>
        /// <returns></returns>
        /// 
        public async Task StartAsync(Dispatcher uiDispatcher, Action<BitmapImage> frameCallback, string url, string login = null, string password = null, CancellationToken? token = null, 
                    int chunkMaxSize = 1024, int frameBufferSize = 1024 * 1024)
        {
            _uiDispatcher = uiDispatcher;
            _frameCallback = frameCallback;

            var tok = token ?? CancellationToken.None;

            using (var cli = new HttpClient())
            {
                if (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(password))
                    cli.DefaultRequestHeaders.Authorization = 
                                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{login}:{password}")));

                using (var stream = await cli.GetStreamAsync(url).ConfigureAwait(false))
                {

                    var streamBuffer = new byte[chunkMaxSize];      // Stream chunk read
                    var frameBuffer = new byte[frameBufferSize];    // Frame buffer

                    var frameIdx = 0;       // Last written byte location in the frame buffer
                    var inPicture = false;  // Are we currently parsing a picture ?
                    byte current = 0x00;    // The last byte read
                    byte previous = 0x00;   // The byte before

                    // Continuously pump the stream. The cancellationtoken is used to get out of there
                    while (true)
                    {
                        var streamLength = await stream.ReadAsync(streamBuffer, 0, chunkMaxSize, tok).ConfigureAwait(false);
                        parseStreamBuffer(frameBuffer, ref frameIdx, streamLength, streamBuffer, ref inPicture, ref previous, ref current);
                    };
                }
            }
        }

        // Parse the stream buffer

        void parseStreamBuffer(byte[] frameBuffer, ref int frameIdx, int streamLength, byte[] streamBuffer, 
                            ref bool inPicture, ref byte previous, ref byte current)
        {
            var idx = 0;
            while (idx < streamLength)
            {
                if (inPicture)
                {
                    parsePicture(frameBuffer, ref frameIdx, ref streamLength, streamBuffer, ref idx, ref inPicture, ref previous, ref current);
                }
                else
                {
                    searchPicture(frameBuffer, ref frameIdx, ref streamLength, streamBuffer, ref idx, ref inPicture, ref previous, ref current);
                }
            }
        }

        // While we are looking for a picture, look for a FFD8 (end of JPEG) sequence.

        void searchPicture(byte[] frameBuffer, ref int frameIdx, ref int streamLength, byte[] streamBuffer, ref int idx, 
                                ref bool inPicture, ref byte previous, ref byte current)
        {
            do
            {
                previous = current;
                current = streamBuffer[idx++];

                // JPEG picture start ?
                if (previous == picMarker && current == picStart)
                {
                    frameIdx = 2;
                    frameBuffer[0] = picMarker;
                    frameBuffer[1] = picStart;
                    inPicture = true;
                    return;
                }
            } while (idx < streamLength);
        }

        // While we are parsing a picture, fill the frame buffer until a FFD9 is reach.

        void parsePicture(byte[] frameBuffer, ref int frameIdx, ref int streamLength, byte[] streamBuffer, 
                            ref int idx, ref bool inPicture, ref byte previous, ref byte current)
        {
            do
            {
                previous = current;
                current = streamBuffer[idx++];
                frameBuffer[frameIdx++] = current;

                // JPEG picture end ?
                if (previous == picMarker && current == picEnd)
                {
                    //Image img = null;
                    

                    // Using a memorystream this way prevent arrays copy and allocations
                    using (var s = new MemoryStream(frameBuffer, 0, frameIdx))
                    {
                        try
                        {
                            BitmapImage img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = s;
                            img.EndInit();
                            img.Freeze();
                            //img = Image.FromStream(s);
                            // Defer the image processing to prevent slow down
                            // The image processing delegate must dispose the image eventually.
                            _uiDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            {
                                _frameCallback(img);
                            }));
                            //Task.Run(() => action(img));
                        }
                        catch
                        {
                            // We dont care about badly decoded pictures
                        }
                    }
                    
                    inPicture = false;
                    return;
                }
            } while (idx < streamLength);
        }
    }

    //    /// <summary>
    //    /// Start a MJPEG on a http stream
    //    /// </summary>
    //    /// <param name="action">Delegate to run at each frame</param>
    //    /// <param name="url">url of the http stream (only basic auth is implemented)</param>
    //    /// <param name="login">optional login</param>
    //    /// <param name="password">optional password (only basic auth is implemented)</param>
    //    /// <param name="token">cancellation token used to cancel the stream parsing</param>
    //    /// <param name="chunkMaxSize">Max chunk byte size when reading stream</param>
    //    /// <param name="frameBufferSize">Maximum frame byte size</param>
    //    /// <returns></returns>
    //    public async Task StartAsync(Action<Image> action, string url, string login = null, string password = null, CancellationToken? token = null, int chunkMaxSize = 1024, int frameBufferSize = 1024 * 1024)
    //    {
    //        var tok = token ?? CancellationToken.None;
    //        tok.ThrowIfCancellationRequested();

    //        using (var cli = new HttpClient())
    //        {
    //            if (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(password))
    //                cli.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{login}:{password}")));

    //            using (var stream = await cli.GetStreamAsync(url).ConfigureAwait(false))
    //            {

    //                var streamBuffer = new byte[chunkMaxSize];
    //                var frameBuffer = new byte[frameBufferSize];

    //                var frameIdx = 0;
    //                var inPicture = false;
    //                var previous = (byte)0;
    //                var current = (byte)0;

    //                while (true)
    //                {
    //                    var streamLength = await stream.ReadAsync(streamBuffer, 0, chunkMaxSize, tok).ConfigureAwait(false);
    //                    ParseBuffer(action, frameBuffer, ref frameIdx, ref inPicture, ref previous, ref current, streamBuffer, streamLength);
    //                };
    //            }
    //        }
    //    }

    //    void ParseBuffer(Action<Image> action, byte[] frameBuffer, ref int frameIdx, ref bool inPicture, ref byte previous, ref byte current, byte[] streamBuffer, int streamLength)
    //    {
    //        var idx = 0;
    //    loop:
    //        if (idx < streamLength)
    //        {
    //            if (inPicture)
    //            {
    //                do
    //                {
    //                    previous = current;
    //                    current = streamBuffer[idx++];
    //                    frameBuffer[frameIdx++] = current;
    //                    if (previous == (byte)0xff && current == (byte)0xd9)
    //                    {
    //                        Image img = null;
    //                        using (var s = new MemoryStream(frameBuffer, 0, frameIdx))
    //                        {
    //                            try
    //                            {
    //                                img = Image.FromStream(s);
    //                            }
    //                            catch
    //                            {
    //                                // dont care about errors while decoding bad picture
    //                            }
    //                        }
    //                        Task.Run(() => action?.Invoke(img));
    //                        inPicture = false;
    //                        goto loop;
    //                    }
    //                } while (idx < streamLength);
    //            }
    //            else
    //            {
    //                do
    //                {
    //                    previous = current;
    //                    current = streamBuffer[idx++];
    //                    if (previous == (byte)0xff && current == (byte)0xd8)
    //                    {
    //                        frameIdx = 2;
    //                        frameBuffer[0] = (byte)0xff;
    //                        frameBuffer[1] = (byte)0xd8;
    //                        inPicture = true;
    //                        goto loop;
    //                    }
    //                } while (idx < streamLength);
    //            }
    //        }
    //    }
    //}
}
