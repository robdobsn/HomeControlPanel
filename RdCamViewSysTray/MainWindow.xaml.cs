using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.Wave;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MahApps.Metro.Controls;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;
using MjpegProcessor;
using System.Net.Http;
using NLog;

namespace RdWebCamSysTrayApp
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MjpegDecoder _mjpeg1;
        private MjpegDecoder _mjpeg2;
        private MjpegDecoder _mjpeg3;
        private int rotationAngle = 0;
        private TalkToAxisCamera talkToAxisCamera;
        private ListenToAxisCamera listenToAxisCamera;
        private const string frontDoorCameraIPAddress = "192.168.0.210";
        private const string secondCameraIPAddress = "192.168.0.211";
        private const string thirdCameraIPAddress = "192.168.0.213";
        private const string frontDoorIPAddress = "192.168.0.221";
        private const string catDeterrentIPAddress = "192.168.0.223";
        private const string officeBlindsIPAddress = "192.168.0.220";
        private const string configFileSource = "//macallan/main/RobDev/ITConfig/RdCamViewSysTray.txt";
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private IPEndPoint _ipEndPointBroadcastListen;
        private UdpClient _udpClientForDoorbellListener;
        private FrontDoorControl _frontDoorControl;
        private AudioDevices _localAudioDevices;
        private int _timeToListenAfterDoorbellRingInSecs = 300;
        private System.Windows.Controls.Control ControlToReceiveFocus;
        private DispatcherTimer dtimer;
        private EasyButtonImage doorLockedImages;
        private EasyButtonImage doorClosedImages;
        private EasyButtonImage doorBellImages;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private const int _udpDoorbellPort = 34343;
        private BlindsControl _officeBlindsControl;
        private int _autoHideRequiredSecs = 0;
        private const int AUTO_HIDE_AFTER_AUTO_SHOW_SECS = 30;
        private const int AUTO_HIDE_AFTER_MANUAL_SHOW_SECS = 120;
        private const int DOOR_STATUS_REFRESH_SECS = 30;
        private int _doorStatusRefreshCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Log startup
            logger.Info("App Starting ...");

            // Position window
            Left = Screen.PrimaryScreen.WorkingArea.Width - Width;
            Top = Screen.PrimaryScreen.WorkingArea.Height - Height;
            ResizeMode = System.Windows.ResizeMode.NoResize;
            ControlToReceiveFocus = this.Settings;

            // Notify icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            Stream iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/res/door48x48.ico")).Stream;
            _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            _notifyIcon.Visible = true;
            _notifyIcon.MouseUp +=
                new System.Windows.Forms.MouseEventHandler(delegate(object sender, System.Windows.Forms.MouseEventArgs args)
                {
                    if (args.Button == MouseButtons.Left)
                    {
                        if (!this.IsVisible)
                            ShowPopupWindow(AUTO_HIDE_AFTER_MANUAL_SHOW_SECS);
                        else
                            HidePopupWindow();
                    }
                    else
                    {
                        System.Windows.Forms.ContextMenu cm = new System.Windows.Forms.ContextMenu();
                        cm.MenuItems.Add("Exit...", new System.EventHandler(ExitApp));
                        _notifyIcon.ContextMenu = cm;
                        MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                        mi.Invoke(_notifyIcon, null);
                    }
                });

            // Listen for doorbell ringing - UDP broadcast
            _ipEndPointBroadcastListen = new IPEndPoint(IPAddress.Any, _udpDoorbellPort);
            _udpClientForDoorbellListener = new UdpClient();
            _udpClientForDoorbellListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try
            {
                _udpClientForDoorbellListener.Client.Bind(_ipEndPointBroadcastListen);
                _udpClientForDoorbellListener.BeginReceive(new AsyncCallback(DoorbellRingCallback), null);
            }
            catch (SocketException excp)
            {
                logger.Info("Socket failed to bind to doorbell broadcast port {1} ({0})", excp.ToString(), _udpDoorbellPort);
            }
            catch (Exception excp)
            {
                logger.Info("Other failed to bind to doorbell broadcast port {1} ({0})", excp.ToString(), _udpDoorbellPort);
            }

            // Front door
            _frontDoorControl = new FrontDoorControl(frontDoorIPAddress);

            // Office blinds
            _officeBlindsControl = new BlindsControl(officeBlindsIPAddress);

            // Create the video decoder
            _mjpeg1 = new MjpegDecoder();
            _mjpeg1.FrameReady += mjpeg1_FrameReady;
            _mjpeg2 = new MjpegDecoder();
            _mjpeg2.FrameReady += mjpeg2_FrameReady;
            _mjpeg3 = new MjpegDecoder();
            _mjpeg3.FrameReady += mjpeg3_FrameReady;

            // Volume control
            _localAudioDevices = new AudioDevices();
            _localAudioDevices.SetOutVolumeWhenListening((float)Properties.Settings.Default.SpkrVol);
            outSlider.Value = Properties.Settings.Default.SpkrVol * 100;
            _localAudioDevices.SetInVolumeWhenTalking((float)Properties.Settings.Default.MicVol);
            inSlider.Value = Properties.Settings.Default.MicVol * 100;

            // Audio in/out
            string username = "";
            string password = "";
            try
            {
                string[] lines = File.ReadAllLines(configFileSource);
                username = lines[0];
                password = lines[1];
            }
            catch (Exception excp)
            {
                logger.Error("Cannot read username and password for Axis camera from " + configFileSource + " excp " + excp.ToString());
            }

            talkToAxisCamera = new TalkToAxisCamera(frontDoorCameraIPAddress, 80, username, password, _localAudioDevices);
            listenToAxisCamera = new ListenToAxisCamera(frontDoorCameraIPAddress, _localAudioDevices);

            // Start Video
            StartVideo();

            // Door status images
            doorLockedImages = new EasyButtonImage(@"res/locked-large.png", @"res/unlocked-large.png");
            doorClosedImages = new EasyButtonImage(@"res/doorclosed-large.png", @"res/dooropen-large.png");
            doorBellImages = new EasyButtonImage(@"res/doorbell-large-sq.png", @"res/doorbell-large.png");

            // Start getting updates from front door
            _frontDoorControl.StartUpdates();

            // Start update timer for status
            dtimer = new DispatcherTimer();
            dtimer.Tick += new EventHandler(dtimer_Tick);
            dtimer.Interval = new TimeSpan(0, 0, 1);
            dtimer.Start();

            // Log startup
            logger.Info("App Started");
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Properties.Settings.Default.Save();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                HidePopupWindow();
            }

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;

            base.OnStateChanged(e);
        }

        public void BringWindowToFront()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }

        public void ShowPopupWindow(int autoHideSecs)
        {
            _autoHideRequiredSecs = autoHideSecs;
            BringWindowToFront();
            StartVideo();
            logger.Info("Popup Shown");
        }

        public void HidePopupWindow()
        {
            logger.Info("Popup Hidden");
            StopVideo();
            StopTalkAndListen();
            this.Hide();
        }

        public void ExitApp(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void StartVideo()
        {
            _mjpeg1.ParseStream(new Uri("http://" + frontDoorCameraIPAddress + "/axis-cgi/mjpg/video.cgi"));
            _mjpeg2.ParseStream(new Uri("http://" + secondCameraIPAddress + "/img/video.mjpeg"));
            _mjpeg3.ParseStream(new Uri("http://" + thirdCameraIPAddress + "/img/video.mjpeg"));
        }

        private void StopVideo()
        {
            _mjpeg1.StopStream();
            _mjpeg2.StopStream();
            _mjpeg3.StopStream();
        }

        private void mjpeg1_FrameReady(object sender, FrameReadyEventArgs e)
        {
            if (rotationAngle != 0)
            {
                TransformedBitmap tmpImage = new TransformedBitmap();

                tmpImage.BeginInit();
                tmpImage.Source = e.BitmapImage; // of type BitmapImage

                RotateTransform transform = new RotateTransform(rotationAngle);
                tmpImage.Transform = transform;
                tmpImage.EndInit();

                image1.Source = tmpImage;
            }
            else
            {
                image1.Source = e.BitmapImage;
            }
        }

        private void mjpeg2_FrameReady(object sender, FrameReadyEventArgs e)
        {
            image2.Source = e.BitmapImage;
        }

        private void mjpeg3_FrameReady(object sender, FrameReadyEventArgs e)
        {
            image3.Source = e.BitmapImage;
        }

        private void StartListen_Click(object sender, RoutedEventArgs e)
        {
            if (!listenToAxisCamera.IsListening())
                listenToAxisCamera.Start();
            ControlToReceiveFocus.Focus();

        }

        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
            if (listenToAxisCamera.IsListening())
                listenToAxisCamera.Stop();
            ControlToReceiveFocus.Focus();

        }

        private void StopTalkAndListen()
        {
            talkToAxisCamera.StopTalk();
            listenToAxisCamera.Stop();
        }

        private void DoorbellRingCallback(IAsyncResult ar)
        {
            logger.Info("Doorbell callback {0}", ar.ToString());
            try
            {
                // Get broadcast

                IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, _udpDoorbellPort);
                byte[] received = _udpClientForDoorbellListener.EndReceive(ar, ref remoteIpEndPoint);
                string recvStr = Encoding.UTF8.GetString(received, 0, received.Length);
                FrontDoorControl.DoorStatus doorStatus = new FrontDoorControl.DoorStatus(recvStr);
                this.Dispatcher.BeginInvoke(
                    (Action)delegate()
                        {
                            ShowDoorStatus(doorStatus);
                        }
                    );
            }
            catch (Exception excp)
            {
                logger.Error("Exception in MainWindow::DoorbellRingCallback1 {0}", excp.Message);
            }

            try
            {
                // Restart receive
                _udpClientForDoorbellListener.BeginReceive(new AsyncCallback(DoorbellRingCallback), null);

                // Popup window and start listing to audio from camera
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        (System.Windows.Forms.MethodInvoker)delegate()
                            {
                                ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
                                listenToAxisCamera.ListenForAFixedPeriod(_timeToListenAfterDoorbellRingInSecs);
                            });
            }
            catch (Exception excp)
            {
                logger.Error("Exception in MainWindow::DoorbellRingCallback2 {0}", excp.Message);
            }
        }

        private void Unlock_Main_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.UnlockMainDoor();
            ControlToReceiveFocus.Focus();
        }

        private void Lock_Main_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.LockMainDoor();
            ControlToReceiveFocus.Focus();
        }

        private void Unlock_Inner_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.UnlockInnerDoor();
            ControlToReceiveFocus.Focus();
        }

        private void Lock_Inner_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.LockInnerDoor();
            ControlToReceiveFocus.Focus();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ControlToReceiveFocus.Focus();
            SettingsWindow sw = new SettingsWindow(_localAudioDevices);
            sw.Show();
        }

        private void outSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            float ov = (float)(e.NewValue / 100);
            _localAudioDevices.SetOutVolumeWhenListening(ov);
            Properties.Settings.Default.SpkrVol = ov;
        }

        private void inSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            float iv = (float)(e.NewValue / 100);
            _localAudioDevices.SetInVolumeWhenTalking(iv);
            Properties.Settings.Default.MicVol = iv;
        }


        private void TalkButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!talkToAxisCamera.IsTalking())
            {
                // TalkButton.Background = System.Windows.Media.Brushes.Red;
                talkToAxisCamera.StartTalk();
            }
        }

        private void TalkButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (talkToAxisCamera.IsTalking())
            {
                talkToAxisCamera.StopTalk();
            }
            ControlToReceiveFocus.Focus();
        }

        private void ShowDoorStatus(FrontDoorControl.DoorStatus doorStatus)
        {
            if (doorStatus.mainLocked)
                mainDoorLockState.Source = doorLockedImages.Img1();
            else
                mainDoorLockState.Source = doorLockedImages.Img2();
            if (doorStatus.innerLocked)
                innerDoorLockState.Source = doorLockedImages.Img1();
            else
                innerDoorLockState.Source = doorLockedImages.Img2();
            if (!doorStatus.mainOpen)
                mainDoorOpenState.Source = doorClosedImages.Img1();
            else
                mainDoorOpenState.Source = doorClosedImages.Img2();
            if (doorStatus.bellPressed)
                doorBellState.Source = doorBellImages.Img1();
            else
                doorBellState.Source = null;
        }

        private void dtimer_Tick(object sender, EventArgs e)
        {
            // Show door status
            _doorStatusRefreshCount--;
            if (_doorStatusRefreshCount <= 0)
            {
                FrontDoorControl.DoorStatus doorStatus;
                bool valid = _frontDoorControl.GetDoorStatus(out doorStatus);
                if (valid)
                    ShowDoorStatus(doorStatus);
                _doorStatusRefreshCount = DOOR_STATUS_REFRESH_SECS;
            }

            // Check for auto-hide required
            if (_autoHideRequiredSecs > 0)
            {
                _autoHideRequiredSecs--;
                if (_autoHideRequiredSecs == 0)
                {
                    HidePopupWindow();
                }
            }

        }

        private async void SquirtButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a New HttpClient object.
                HttpClient client = new HttpClient();

                HttpResponseMessage response = await client.GetAsync("http://" + catDeterrentIPAddress + "/control.cgi?squirt=1");
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                // Above three lines can be replaced with new helper method in following line 
                // string body = await client.GetStringAsync(uri);

                logger.Info("SquirtButton_Click response {0}", responseBody);
            }
            catch (HttpRequestException excp)
            {
                logger.Info("MainWindow::SquirtButton_Click exception {0}", excp.Message);
            }
            ControlToReceiveFocus.Focus();
        }

        private void RobsUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(0, "up");
            ControlToReceiveFocus.Focus();
        }

        private void LeftUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(1, "up");
            ControlToReceiveFocus.Focus();
        }

        private void LeftMidUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(2, "up");
            ControlToReceiveFocus.Focus();
        }

        private void RightMidUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(3, "up");
            ControlToReceiveFocus.Focus();
        }

        private void RightUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(4, "up");
            ControlToReceiveFocus.Focus();
        }

        private void RobsStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(0, "stop");
            ControlToReceiveFocus.Focus();
        }

        private void LeftStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(1, "stop");
            ControlToReceiveFocus.Focus();
        }

        private void LeftMidStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(2, "stop");
            ControlToReceiveFocus.Focus();
        }

        private void RightMidStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(3, "stop");
            ControlToReceiveFocus.Focus();
        }

        private void RightStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(4, "stop");
            ControlToReceiveFocus.Focus();
        }

        private void RobsDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(0, "down");
            ControlToReceiveFocus.Focus();
        }

        private void LeftDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(1, "down");
            ControlToReceiveFocus.Focus();
        }

        private void LeftMidDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(2, "down");
            ControlToReceiveFocus.Focus();
        }

        private void RightMidDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(3, "down");
            ControlToReceiveFocus.Focus();
        }

        private void RightDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(4, "down");
            ControlToReceiveFocus.Focus();
        }

    }
}
