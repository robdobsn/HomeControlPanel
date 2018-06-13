using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HomeControlPanel
{
    class CameraMotion
    {
        // Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Udp for camera movement
        private UdpClient _udpClientForCameraMovement;

        public delegate void ActionDetectedCallback();
        ActionDetectedCallback _actionDetectedCallback;

        public CameraMotion(int cameraMovementNotifyPort, ActionDetectedCallback callback)
        {
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
    }
}
