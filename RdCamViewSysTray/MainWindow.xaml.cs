#define LISTEN_TO_CAMERA
#define TALK_TO_CAMERA

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.IO;
using MahApps.Metro.Controls;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;
using NLog;
using Newtonsoft.Json;
using System.Windows.Media;
using System.Linq;
using System.Windows.Media.Imaging;

namespace RdWebCamSysTrayApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        // Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Config data
        ConfigFileInfo _configFileInfo;

        // Handlers for video streams - they display in an image
        private VideoStreamDisplays _videoStreamDisplays = new VideoStreamDisplays();

        // Settings
        bool listenToCameraOnShow = false;
#if (TALK_TO_CAMERA)
        private TalkToAxisCamera _talkToAxisCamera;
#endif
#if (LISTEN_TO_CAMERA)
        private ListenToAxisCamera _listenToAxisCamera;
#endif
        
        // Door control
        private FrontDoorControl _frontDoorControl;
        private DateTime? _doorStatusRefreshTime = null;

        // Cat deterrent
        private CatDeterrent _catDeterrent;

        // Camera motion detect
        private CameraMotion _cameraMotion;

        // Blinds
        private BlindsControl _officeBlindsControl;

        // Domoticz units
        private DomoticzControl _domoticzControl;

        // LED Matrix message board
        private LedMatrix _ledMatrix;

        // Local audio devices - used for listening and talking to cameras
        private AudioDevices _localAudioDevices;

        // Control to receive focus next
        private System.Windows.Controls.Control _controlToReceiveFocus;

        // Audio listen and window auto display
        private int _timeToListenAfterDoorbellRingInSecs = 300;
        private int _autoHideRequiredSecs = 0;
        private const int AUTO_HIDE_AFTER_AUTO_SHOW_SECS = 30;
        private const int AUTO_HIDE_AFTER_MANUAL_SHOW_SECS = 120;
        private const int DOOR_STATUS_REFRESH_SECS = 2;
        private DispatcherTimer _dTimer = new DispatcherTimer();

        // Images and icons
        private EasyButtonImage doorLockedImages;
        private EasyButtonImage doorClosedImages;
        private EasyButtonImage doorBellImages;
        private System.Windows.Forms.NotifyIcon _notifyIcon;

        // Image display
        private int _curImageAgeToDisplay = 0;
        private string _lastFrontDoorImageName = "";

        // Info on a device in the config file
        public class DeviceInfo
        {
            public string description;
            public string deviceType;
            public string IP;
            public string username;
            public string password;
            public int port;
            public int notifyPort;
            public string videoURL;
            public string imageGrabPath;
            public int imageGrabPoll;
        }

        // Data contained in the config file
        public class ConfigFileInfo
        {
            public Dictionary<string, DeviceInfo> devices;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Log startup
            logger.Info("App Starting ...");

            ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
            _controlToReceiveFocus = this.Settings;

            // Get configuration
            using (StreamReader sr = new StreamReader("//macallan/Admin/Config/8DPDevices.json"))
            {
                string jsonData = sr.ReadToEnd();
                _configFileInfo = JsonConvert.DeserializeObject<ConfigFileInfo>(jsonData);
             }

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

            // Cat deterrent
            _catDeterrent = new CatDeterrent(_configFileInfo.devices["catCamera"].notifyPort, AutoShowWindowFn,
                _configFileInfo.devices["catDeterrent"].IP, _configFileInfo.devices["catDeterrent"].port);

            // Camera motion
            _cameraMotion = new CameraMotion(_configFileInfo.devices["frontDoorCamera"].notifyPort, AutoShowWindowFn);

            // Front door
            _frontDoorControl = new FrontDoorControl(_configFileInfo.devices["frontDoorLock"].IP,
                            _configFileInfo.devices["frontDoorLock"].notifyPort,
                            _configFileInfo.devices["frontDoorLock"].port,
                            DoorStatusRefresh);

            // Office blinds
            string officeBlindsIPAddress = _configFileInfo.devices["officeBlinds"].IP;
            _officeBlindsControl = new BlindsControl(officeBlindsIPAddress);

            // Domoticz
            List<string> domoticzIPAddresses = new List<string>();
            foreach (KeyValuePair<string, DeviceInfo> devInfo in _configFileInfo.devices)
            {
                if (devInfo.Value.deviceType == "domoticz")
                {
                    domoticzIPAddresses.Add(devInfo.Value.IP);
                }
            }
            _domoticzControl = new DomoticzControl(domoticzIPAddresses);

            // LedMatrix
            string ledMatrixIpAddress = _configFileInfo.devices["officeMessageBoard"].IP;
            _ledMatrix = new LedMatrix(ledMatrixIpAddress);

            // Create the video decoder
            _videoStreamDisplays.add(video1, 0, new Uri(_configFileInfo.devices["frontDoorCamera"].videoURL));
            _videoStreamDisplays.add(video2, 0, new Uri(_configFileInfo.devices["garageCamera"].videoURL));
            _videoStreamDisplays.add(video3, 0, new Uri(_configFileInfo.devices["catCamera"].videoURL));

            // Volume control
            _localAudioDevices = new AudioDevices();
            _localAudioDevices.SetOutVolumeWhenListening((float)Properties.Settings.Default.SpkrVol);
            outSlider.Value = Properties.Settings.Default.SpkrVol * 100;
            _localAudioDevices.SetInVolumeWhenTalking((float)Properties.Settings.Default.MicVol);
            inSlider.Value = Properties.Settings.Default.MicVol * 100;

            // Audio in/out
#if (TALK_TO_CAMERA)
            if (_configFileInfo.devices.ContainsKey("frontDoorCamera"))
                _talkToAxisCamera = new TalkToAxisCamera(_configFileInfo.devices["frontDoorCamera"].IP, 80,
                            _configFileInfo.devices["frontDoorCamera"].username,
                            _configFileInfo.devices["frontDoorCamera"].password,
                            _localAudioDevices);
#endif
#if (LISTEN_TO_CAMERA)
            if (_configFileInfo.devices.ContainsKey("frontDoorCamera"))
                _listenToAxisCamera = new ListenToAxisCamera(_configFileInfo.devices["frontDoorCamera"].IP, _localAudioDevices);
#endif
            // Start Video
            StartVideo();

            // Door status images
            doorLockedImages = new EasyButtonImage(@"res/locked-large.png", @"res/unlocked-large.png");
            doorClosedImages = new EasyButtonImage(@"res/doorclosed-large.png", @"res/dooropen-large.png");
            doorBellImages = new EasyButtonImage(@"res/doorbell-large-sq.png", @"res/doorbell-large.png");

            // Start getting updates from front door
            _frontDoorControl.StartUpdates();

            // Start update timer for status
            _dTimer.Tick += new EventHandler(dtimer_Tick);
            _dTimer.Interval = new TimeSpan(0, 0, 1);
            _dTimer.Start();

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
#if (LISTEN_TO_CAMERA)
            if (this.listenToCameraOnShow)
                _listenToAxisCamera.Start();
#endif
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
            HidePopupWindow();
            _dTimer.Stop();
            StopVideo(true);
            StopTalkAndListen();
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Shutdown();
        }

        private void StartVideo()
        {
            _videoStreamDisplays.start();
        }

        private void StopVideo(bool unsubscribeEvents = false)
        {
            _videoStreamDisplays.stop(unsubscribeEvents);
        }

        private void StartListen_Click(object sender, RoutedEventArgs e)
        {
#if (LISTEN_TO_CAMERA)
            if (!_listenToAxisCamera.IsListening())
                _listenToAxisCamera.Start();
#endif
            _controlToReceiveFocus.Focus();

        }

        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
#if (LISTEN_TO_CAMERA)
            if (_listenToAxisCamera.IsListening())
                _listenToAxisCamera.Stop();
#endif
            _controlToReceiveFocus.Focus();

        }

        private void StopTalkAndListen()
        {
#if (TALK_TO_CAMERA)
            _talkToAxisCamera.StopTalk();
#endif
#if (LISTEN_TO_CAMERA)
            _listenToAxisCamera.Stop();
#endif
        }

        private void DoorStatusRefresh()
        {
            _doorStatusRefreshTime = DateTime.Now;

            // Update the door status using a delegate as it is UI update
            this.Dispatcher.BeginInvoke(
                (Action)delegate ()
                    {
                        ShowDoorStatus();
                    }
                );
            // Check if popup window and start listing to audio from camera
            if (_frontDoorControl.IsDoorbellPressed())
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    (System.Windows.Forms.MethodInvoker)delegate ()
                        {
                            ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
#if (LISTEN_TO_CAMERA)
                            _listenToAxisCamera.ListenForAFixedPeriod(_timeToListenAfterDoorbellRingInSecs);
#endif
                        });
            }
        }

        private void AutoShowWindowFn()
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    (System.Windows.Forms.MethodInvoker)delegate ()
                    {
                        ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
                    });
        }

        private void Unlock_Main_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.UnlockMainDoor();
            _controlToReceiveFocus.Focus();
        }

        private void Lock_Main_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.LockMainDoor();
            _controlToReceiveFocus.Focus();
        }

        private void Unlock_Inner_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.UnlockInnerDoor();
            _controlToReceiveFocus.Focus();
        }

        private void Lock_Inner_Click(object sender, RoutedEventArgs e)
        {
            _frontDoorControl.LockInnerDoor();
            _controlToReceiveFocus.Focus();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _controlToReceiveFocus.Focus();
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
#if (TALK_TO_CAMERA)
            if (!_talkToAxisCamera.IsTalking())
            {
                // TalkButton.Background = System.Windows.Media.Brushes.Red;
                _talkToAxisCamera.StartTalk();
            }
#endif
        }

        private void TalkButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
#if (TALK_TO_CAMERA)
            if (_talkToAxisCamera.IsTalking())
            {
                _talkToAxisCamera.StopTalk();
            }
#endif
            _controlToReceiveFocus.Focus();
        }

        private BitmapImage GetImageFromFolder(string folder, int imageAgeZeroNewest)
        {
            // Find images in folder sorted by age
            DirectoryInfo info = new DirectoryInfo(folder);
            FileInfo[] files = info.GetFiles("*.jpg").OrderBy(p => p.CreationTime).ToArray();
            if (files.Length > 0)
            {
                if (imageAgeZeroNewest >= files.Length)
                    imageAgeZeroNewest = files.Length - 1;
                int fileIdx = files.Length - 1 - imageAgeZeroNewest;
                try
                {
                    return new BitmapImage(new Uri(Path.Combine(folder, files[fileIdx].Name)));
                }
                finally
                {
                }
            }
            return null;
        }

        private void ShowDoorStatus()
        {
            FrontDoorControl.DoorStatus doorStatus;
            _frontDoorControl.GetDoorStatus(out doorStatus);
            if (doorStatus._mainLocked)
                mainDoorLockState.Source = doorLockedImages.Img1();
            else
                mainDoorLockState.Source = doorLockedImages.Img2();
            if (doorStatus._innerLocked)
                innerDoorLockState.Source = doorLockedImages.Img1();
            else
                innerDoorLockState.Source = doorLockedImages.Img2();
            if (!doorStatus._mainOpen)
                mainDoorOpenState.Source = doorClosedImages.Img1();
            else
                mainDoorOpenState.Source = doorClosedImages.Img2();
            if (doorStatus._bellPressed)
                doorBellState.Source = doorBellImages.Img1();
            else
                doorBellState.Source = null;
        }

        private void dtimer_Tick(object sender, EventArgs e)
        {
            // Check for auto-hide required
            if (_autoHideRequiredSecs > 0)
            {
                _autoHideRequiredSecs--;
                if (_autoHideRequiredSecs == 0)
                {
                    HidePopupWindow();
                }
            }

            // Show time since last status update
            if (_doorStatusRefreshTime == null)
            {
                DoorStatusTextBox.Text = "No Updates";
            }
            else
            {
                TimeSpan? ts = DateTime.Now - _doorStatusRefreshTime;
                if (ts != null)
                {
                    DoorStatusTextBox.Text = "Info is " + Math.Round(ts.Value.TotalSeconds).ToString() + "s old";
                }
            }

            // Poll for new motion detected if required
            if (_configFileInfo.devices["frontDoorCamera"].imageGrabPoll != 0)
            {
                string folder = _configFileInfo.devices["frontDoorCamera"].imageGrabPath;
                DirectoryInfo info = new DirectoryInfo(folder);
                FileInfo[] files = info.GetFiles("*.jpg").OrderBy(p => p.CreationTime).ToArray();
                if (files.Length <= 0)
                    return;
                string newestFname = files[files.Length - 1].Name;
                if (_lastFrontDoorImageName != newestFname)
                {
                    _lastFrontDoorImageName = newestFname;
                    AutoShowWindowFn();
                    showImages();
                }
            }

        }

        private void RobsUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(0, "up");
            _controlToReceiveFocus.Focus();
        }

        private void LeftUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(1, "up");
            _controlToReceiveFocus.Focus();
        }

        private void LeftMidUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(2, "up");
            _controlToReceiveFocus.Focus();
        }

        private void RightMidUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(3, "up");
            _controlToReceiveFocus.Focus();
        }

        private void RightUpButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(4, "up");
            _controlToReceiveFocus.Focus();
        }

        private void RobsStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(0, "stop");
            _controlToReceiveFocus.Focus();
        }

        private void LeftStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(1, "stop");
            _controlToReceiveFocus.Focus();
        }

        private void LeftMidStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(2, "stop");
            _controlToReceiveFocus.Focus();
        }

        private void RightMidStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(3, "stop");
            _controlToReceiveFocus.Focus();
        }

        private void RightStopButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(4, "stop");
            _controlToReceiveFocus.Focus();
        }

        private void RobsDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(0, "down");
            _controlToReceiveFocus.Focus();
        }

        private void LeftDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(1, "down");
            _controlToReceiveFocus.Focus();
        }

        private void LeftMidDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(2, "down");
            _controlToReceiveFocus.Focus();
        }

        private void RightMidDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(3, "down");
            _controlToReceiveFocus.Focus();
        }

        private void RightDownButton_Click(object sender, RoutedEventArgs e)
        {
            _officeBlindsControl.ControlBlind(4, "down");
            _controlToReceiveFocus.Focus();
        }

        private void OfficeLightsMoodButton_Click(object sender, RoutedEventArgs e)
        {
            _domoticzControl.SendGroupCommand("Office - Mood");
        }

        private void OfficeLightsOffButton_Click(object sender, RoutedEventArgs e)
        {
            _domoticzControl.SendGroupCommand("Office - Off");
        }
        private void TextMatrixSendButton_Click(object sender, RoutedEventArgs e)
        {
            _ledMatrix.SendMessage(LEDMatrixText.Text);
        }
        private void TextMatrixStopAlertButton_Click(object sender, RoutedEventArgs e)
        {
            _ledMatrix.StopAlert();
        }
        private void TextMatrixClearButton_Click(object sender, RoutedEventArgs e)
        {
            _ledMatrix.Clear();
        }

        private void SquirtButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _catDeterrent.squirt(true);
            _controlToReceiveFocus.Focus();
        }

        private void SquirtButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _catDeterrent.squirt(false);
            _controlToReceiveFocus.Focus();
        }

        private void Home_Control_SysTrayApp_Loaded(object sender, RoutedEventArgs e)
        {
            // Position window
            PresentationSource MainWindowPresentationSource = PresentationSource.FromVisual(this);
            Matrix m = MainWindowPresentationSource.CompositionTarget.TransformToDevice;
            double DpiWidthFactor = m.M11;
            double DpiHeightFactor = m.M22;
            double ScreenHeight = SystemParameters.PrimaryScreenHeight * DpiHeightFactor;
            double ScreenWidth = SystemParameters.PrimaryScreenWidth * DpiWidthFactor;

            logger.Info("W " + Screen.PrimaryScreen.WorkingArea.Width.ToString() + " . " + Width.ToString() + " . " + ScreenWidth.ToString() + 
                " H " + Screen.PrimaryScreen.WorkingArea.Height.ToString() + " . " + Height.ToString() + " . " + ScreenHeight.ToString());
            if ((ScreenWidth - Width > 0) && (ScreenHeight - Height > 0))
            {
                Left = ScreenWidth - Width;
                Top = ScreenHeight - Height - 100;
            }

        }

        private void BtnShowVideo_Click(object sender, RoutedEventArgs e)
        {
            switchImageDisplay(false);
        }

        private void switchImageDisplay(bool showImages)
        {
            image1.Visibility = showImages ? Visibility.Visible : Visibility.Hidden;
            image2.Visibility = showImages ? Visibility.Visible : Visibility.Hidden;
            image3.Visibility = showImages ? Visibility.Visible : Visibility.Hidden;
            video1.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
            video2.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
            video3.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
        }

        private void BtnShowImage_Click(object sender, RoutedEventArgs e)
        {
            switchImageDisplay(true);
            // Find latest images
            _curImageAgeToDisplay = 0;
            showImages();
        }

        private void showImages()
        {
            image1.Source = GetImageFromFolder(_configFileInfo.devices["frontDoorCamera"].imageGrabPath, _curImageAgeToDisplay);
            image2.Source = GetImageFromFolder(_configFileInfo.devices["garageCamera"].imageGrabPath, _curImageAgeToDisplay);
            image3.Source = GetImageFromFolder(_configFileInfo.devices["catCamera"].imageGrabPath, _curImageAgeToDisplay);
        }

        private void BtnImageNext_Click(object sender, RoutedEventArgs e)
        {
            switchImageDisplay(true);
            _curImageAgeToDisplay--;
            if (_curImageAgeToDisplay < 0)
                _curImageAgeToDisplay = 0;
            showImages();
        }

        private void BtnImagePrev_Click(object sender, RoutedEventArgs e)
        {
            switchImageDisplay(true);
            _curImageAgeToDisplay++;
            showImages();
        }
    }
}
