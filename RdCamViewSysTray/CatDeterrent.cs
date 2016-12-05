using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RdWebCamSysTrayApp
{
    class CatDeterrent
    {
        // Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Udp for camera movement
        private UdpClient _udpClientForCameraMovement;

        // Squirt IP and port
        private string _squirtIPAddr;
        private int _squirtPort;

        public delegate void ActionDetectedCallback();
        ActionDetectedCallback _actionDetectedCallback;

        public CatDeterrent(int cameraMovementNotifyPort, ActionDetectedCallback callback,
                        string squirtIPAddr, int squirtPort)
        {
            _squirtIPAddr = squirtIPAddr;
            _squirtPort = squirtPort;
            _actionDetectedCallback = callback;
            try
            {
                _udpClientForCameraMovement = new UdpClient(cameraMovementNotifyPort);
                _udpClientForCameraMovement.BeginReceive(new AsyncCallback(CameraMovementCallback), null);
                logger.Info("Socket bound to camera movement port {0}", cameraMovementNotifyPort);
            }
            catch (SocketException excp)
            {
                logger.Error("Socket failed to bind to camera movement port {1} ({0})", excp.ToString(), cameraMovementNotifyPort);
            }
            catch (Exception excp)
            {
                logger.Error("Other failed to bind to camera movement port {1} ({0})", excp.ToString(), cameraMovementNotifyPort);
            }
        }

        private void CameraMovementCallback(IAsyncResult ar)
        {
            logger.Info("Camera movement {0}", ar.ToString());

            try
            {
                // Restart receive
                _udpClientForCameraMovement.BeginReceive(new AsyncCallback(CameraMovementCallback), null);
                _actionDetectedCallback();
            }
            catch (Exception excp)
            {
                logger.Error("Exception in MainWindow::CameraMovementCallback2 {0}", excp.Message);
            }
        }

        public void squirt(bool squirtOn)
        {
            String msg = "0";
            if (squirtOn)
                msg = "1";
            try
            {
                //IPAddress multicastaddress = IPAddress.Parse(catDeterrentIPAddress);
                //UdpClient udpclient = new UdpClient(41252, AddressFamily.InterNetwork); ;
                //udpclient.JoinMulticastGroup(multicastaddress);
                //IPEndPoint remoteep = new IPEndPoint(multicastaddress, _catDeterrentUDPPort);
                //byte[] send_buffer = Encoding.ASCII.GetBytes(msg);
                //udpclient.Send(send_buffer, send_buffer.Length, remoteep);

                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPAddress serverAddr = IPAddress.Parse(_squirtIPAddr);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, _squirtPort);
                byte[] send_buffer = Encoding.ASCII.GetBytes(msg);
                sock.SendTo(send_buffer, endPoint);

                //Uri uri = new Uri("http://" + catDeterrentIPAddress + "/control.cgi?squirt=1");
                //// Using WebClient as can't get HttpClient to not block
                //WebClient requester = new WebClient();
                //requester.OpenReadCompleted += new OpenReadCompletedEventHandler(web_req_completed);
                //requester.OpenReadAsync(uri);

                logger.Info("MainWindow::SquirtButton activated");
            }
            catch (HttpRequestException excp)
            {
                logger.Error("MainWindow::SquirtButton exception {0}", excp.Message);
            }
        }


    }
}
