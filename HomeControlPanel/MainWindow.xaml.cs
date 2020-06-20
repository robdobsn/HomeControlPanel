//#define LISTEN_TO_CAMERA
//#define TALK_TO_CAMERA

using System;
using System.Collections.Generic;
using System.IO;
using MahApps.Metro.Controls;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;
using NLog;
using System.Windows.Media;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace HomeControlPanel
{
    public partial class MainWindow : MetroWindow
    {
        // Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Config data
        ConfigFileInfo _configFileInfo = new ConfigFileInfo();

        // Systray icon and hiding
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private const int AUTO_HIDE_AFTER_MANUAL_SHOW_SECS = 120;
        private const int AUTO_HIDE_AFTER_AUTO_SHOW_SECS = 30;

        // Device manager
        private DeviceManager _deviceManager = null;

        // Show still images instead of video
        private bool _stillImagesDisplayed = false;

        //        // Settings
        //        bool listenToCameraOnShow = false;
        //#if (TALK_TO_CAMERA)
        //        private TalkToAxisCamera _talkToAxisCamera;
        //#endif
        //#if (LISTEN_TO_CAMERA)
        //        private ListenToAxisCamera _listenToAxisCamera;
        //#endif

        //        // Door control
        //        private DoorControl _frontDoorControl;
        //        private DateTime? _doorStatusRefreshTime = null;

        //        // Garage control
        //        private DoorControl _garageDoorControl;
        //        private DateTime? _garageStatusRefreshTime = null;

        //        // Cat deterrent
        //        private CatDeterrent _catDeterrent;

        //        // Camera motion detect
        //        private CameraMotion _cameraMotion;

        //        // Blinds
        //        private BlindsControl _officeBlindsControl;

        //        // Domoticz units
        //        private DomoticzControl _domoticzControl;

        //        // RobHomeServer
        //        private HomeScenes _homeScenes;

        //        // LED Matrix message board
        //        private LedMatrix _ledMatrix;

        //        // Local audio devices - used for listening and talking to cameras
        //        private AudioDevices _localAudioDevices;


        //        // Audio listen and window auto display
        //        private int _timeToListenAfterDoorbellRingInSecs = 300;
        //        private int _autoHideRequiredSecs = 0;
        //        private const int DOOR_STATUS_REFRESH_SECS = 2;
        //        private DispatcherTimer _dTimer = new DispatcherTimer();

        //        // Image display
        //        private int _curImageAgeToDisplay = 0;
        //        private string _lastFrontDoorImageName = "";

        //        // Delay before starting streaming
        //        private bool configAcquiredOk = false;
        //        private int ticksSinceConfigAcquired = 0;
        //        private bool dataAcqStarted = false;

        public MainWindow()
        {
            InitializeComponent();

            // Create device manager
            _deviceManager = new DeviceManager(UIUpdateCallback);

            // Log startup
            //logger.Info("App Starting ...");

            //ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;

            //// Door status images
            //doorLockedImages = new EasyButtonImage(@"res/locked-large.png", @"res/unlocked-large.png");
            //doorClosedImages = new EasyButtonImage(@"res/doorclosed-large.png", @"res/dooropen-large.png");
            //garageClosedImages = new EasyButtonImage(@"res/garageclosed-large.png", @"res/garageopen-large.png");
            //garageUnknownImages = new EasyButtonImage(@"res/garageunknown-large.png", @"res/garageopen-large.png");
            //doorBellImages = new EasyButtonImage(@"res/doorbell-large-sq.png", @"res/doorbell-large.png");

            //// Start update timer for status
            //_dTimer.Tick += new EventHandler(dtimer_Tick);
            //_dTimer.Interval = new TimeSpan(0, 0, 1);
            //_dTimer.Start();

            // Log startup
            //logger.Info("App Started");
        }

        // Window loaded
        private void Home_Control_SysTrayApp_Loaded(object sender, RoutedEventArgs e)
        {
            // Log startup
            logger.Info("App window loaded");

            // Get config
            _configFileInfo.AcquireConfig(() =>
            {
                logger.Info("Got config ok");
                _deviceManager.Setup(_configFileInfo);
            },
            () =>
            {
                ProgramStatusBox.Text = "Failed to get config";
                ProgramStatusBox.Foreground = new SolidColorBrush(Colors.Red);
                logger.Info("Failed to get config");
            });

            // Position window
            SysTrayPosition();

            // SysTray Icon
            SysTrayIcon();

            // UI Media
            InitUIMedia();

            // Set audio levels
            Video1Area.Volume = 0.5;
            outSlider.Value = 50;

            // Start Video
            StartVideo();

            // Log startup
            logger.Info("App loaded complete");
        }


        private void StartDataAcquisition()
        {
            // Start Video
            StartVideo();

            //            // Start getting updates from front door
            //            _frontDoorControl.StartUpdates();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //_notifyIcon.Visible = false;
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            logger.Info("Window closing");
            await Video1Area.Close();
            //base.OnClosing(e);
            //e.Cancel = true;
            //WindowState = WindowState.Minimized;
            //Properties.Settings.Default.Save();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            //if (WindowState == WindowState.Minimized)
            //{
            //    HidePopupWindow();
            //}

            //if (WindowState == WindowState.Maximized)
            //    WindowState = WindowState.Normal;

            //base.OnStateChanged(e);
        }

        public void BringWindowToFront()
        {
            ////Win32Helper.ShowWindowNoActive(this);
            //this.Show();
            //this.WindowState = WindowState.Normal;
            //this.Activate();
            //this.Topmost = true;
            //this.Topmost = false;
            ////this.Focus();
        }

        public void ShowPopupWindow(int autoHideSecs)
        {
//            _autoHideRequiredSecs = autoHideSecs;
//            BringWindowToFront();
//            StartVideo();
//#if (LISTEN_TO_CAMERA)
//            if (this.listenToCameraOnShow)
//                _listenToAxisCamera.Start();
//#endif
//            logger.Info("Popup Shown");
        }

        public void HidePopupWindow()
        {
            //logger.Info("Popup Hidden");
            //StopVideo();
            //StopTalkAndListen();
            //this.Hide();
        }

        public void ExitApp(object sender, EventArgs e)
        {
            //HidePopupWindow();
            //_dTimer.Stop();
            //StopVideo(true);
            //StopTalkAndListen();
            //if (System.Windows.Application.Current != null)
            //    System.Windows.Application.Current.Shutdown();
        }

        private async void StartVideo()
        {
            if (!_stillImagesDisplayed)
            {
                bool videoOk = await Video1Area.Open(new Uri("rtsp://192.168.86.246:7447/5ebeefe771918b365853ae4a_1"));
                logger.Info("Video open result " + videoOk);
            }
            //_videoStreamDisplays.start();
        }

        private void StopVideo(bool unsubscribeEvents = false)
        {
            //_videoStreamDisplays.stop(unsubscribeEvents);
        }

        private void StartListen_Click(object sender, RoutedEventArgs e)
        {
            Video1Area.IsMuted = false;
        }

        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
            Video1Area.IsMuted = true;
        }

        private void GarageStatusRefresh()
        {
//            _garageStatusRefreshTime = DateTime.Now;

//            // Update the door status using a delegate as it is UI update
//            this.Dispatcher.BeginInvoke(
//                (Action)delegate ()
//                {
//                    ShowDoorStatus();
//                }
//                );
//            // Check if popup window and start listing to audio from camera
//            if (_frontDoorControl.IsDoorbellPressed())
//            {
//                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
//                    (System.Windows.Forms.MethodInvoker)delegate ()
//                    {
//                        ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
//#if (LISTEN_TO_CAMERA)
//                        _listenToAxisCamera.ListenForAFixedPeriod(_timeToListenAfterDoorbellRingInSecs);
//#endif
//                    });
//            }
        }

        private void CameraMotionDetectFn()
        {
            // This is here to soak up camera motion events which currently do nothing - used to AutoShowWindowFn
            DeviceInfo devInfo = _configFileInfo.GetDevice("frontDoorCamera");
            if (devInfo != null)
            {
                if (devInfo.motionDetectAutoShow != 0)
                    AutoShowWindowFn();
            }
        }

        private void AutoShowWindowFn()
        {
            //System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
            //        (System.Windows.Forms.MethodInvoker)delegate ()
            //        {
            //            ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
            //        });
        }

        private void Unlock_Inner_Click(object sender, RoutedEventArgs e)
        {
            //if (_frontDoorControl != null)
            //    _frontDoorControl.UnlockInnerDoor();
            //_controlToReceiveFocus.Focus();
        }

        private void Lock_Inner_Click(object sender, RoutedEventArgs e)
        {
            //if (_frontDoorControl != null)
            //    _frontDoorControl.LockInnerDoor();
            //_controlToReceiveFocus.Focus();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            //_controlToReceiveFocus.Focus();
            //SettingsWindow sw = new SettingsWindow(_localAudioDevices, _configFileInfo);
            //sw.Show();
        }

        private void outSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Video1Area.Volume = (float)(e.NewValue / 100);
        }

        private BitmapImage GetImageFromFolder(string folder, int imageAgeZeroNewest)
        {
            if (folder.Trim().Length == 0)
                return null;
            // Find images in folder sorted by age
            try
            {
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
            }
            finally
            {
            }
            return null;
        }

        private void ShowDoorStatus()
        {
            //DoorControl.DoorStatus doorStatus;
            //_frontDoorControl.GetDoorStatus(out doorStatus);
            //if (doorStatus._doorLockStrs[0] == "locked")
            //    mainDoorLockState.Source = doorLockedImages.Img1();
            //else
            //    mainDoorLockState.Source = doorLockedImages.Img2();
            //if (doorStatus._doorLockStrs[1] == "locked")
            //    innerDoorLockState.Source = doorLockedImages.Img1();
            //else
            //    innerDoorLockState.Source = doorLockedImages.Img2();
            //if (doorStatus._doorOpenStrs[0] == "closed")
            //    mainDoorOpenState.Source = doorClosedImages.Img1();
            //else
            //    mainDoorOpenState.Source = doorClosedImages.Img2();
            //DoorControl.DoorStatus garageStatus;
            //_garageDoorControl.GetDoorStatus(out garageStatus);
            //if (garageStatus._doorOpenStrs[0] == "closed")
            //    garageDoorOpenState.Source = garageClosedImages.Img1();
            //else if (garageStatus._doorOpenStrs[0] == "open")
            //    garageDoorOpenState.Source = garageClosedImages.Img2();
            //else
            //    garageDoorOpenState.Source = garageUnknownImages.Img1();

            //if (doorStatus._bellPressed)
            //    doorBellState.Source = doorBellImages.Img1();
            //else
            //    doorBellState.Source = null;
        }

        private void dtimer_Tick(object sender, EventArgs e)
        {
            //// Check for start acquisition
            //if (!dataAcqStarted && configAcquiredOk)
            //{
            //    if (ticksSinceConfigAcquired > 1)
            //    {
            //        dataAcqStarted = true;
            //        StartDataAcquisition();
            //    }
            //    ticksSinceConfigAcquired++;
            //}

            //// Check for auto-hide required
            //if (_autoHideRequiredSecs > 0)
            //{
            //    _autoHideRequiredSecs--;
            //    if (_autoHideRequiredSecs == 0)
            //    {
            //        HidePopupWindow();
            //    }
            //}

            //// Show time since last door status update
            //if (_doorStatusRefreshTime == null)
            //{
            //    DoorStatusTextBox.Text = "Door no info";
            //}
            //else
            //{
            //    TimeSpan? ts = DateTime.Now - _doorStatusRefreshTime;
            //    if (ts != null)
            //    {
            //        DoorStatusTextBox.Text = "Door info " + Math.Round(ts.Value.TotalSeconds).ToString() + "s old";
            //    }
            //}

            //// Show time since last garage status update
            //if (_garageStatusRefreshTime == null)
            //{
            //    GarageStatusTextBox.Text = "Garage no info";
            //}
            //else
            //{
            //    TimeSpan? ts = DateTime.Now - _garageStatusRefreshTime;
            //    if (ts != null)
            //    {
            //        GarageStatusTextBox.Text = "Garage info " + Math.Round(ts.Value.TotalSeconds).ToString() + "s old";
            //    }
            //}

            //// Poll for new motion detected if required
            //DeviceInfo devInfo = _configFileInfo.GetDevice("frontDoorCamera");
            //if (devInfo != null)
            //{
            //    if (devInfo.imageGrabPoll != 0)
            //    {
            //        string folder = devInfo.imageGrabPath;
            //        DirectoryInfo info = new DirectoryInfo(folder);
            //        FileInfo[] files = info.GetFiles("*.jpg").OrderBy(p => p.CreationTime).ToArray();
            //        if (files.Length <= 0)
            //            return;
            //        string newestFname = files[files.Length - 1].Name;
            //        if (_lastFrontDoorImageName != newestFname)
            //        {
            //            _lastFrontDoorImageName = newestFname;
            //            AutoShowWindowFn();
            //            showImages();
            //        }
            //    }
            //}
        }

        private void TextMatrixSendButton_Click(object sender, RoutedEventArgs e)
        {
            //if (_ledMatrix != null)
            //    _ledMatrix.SendMessage(LEDMatrixText.Text);
        }
        private void TextMatrixStopAlertButton_Click(object sender, RoutedEventArgs e)
        {
            //if (_ledMatrix != null)
            //    _ledMatrix.StopAlert();
        }
        private void TextMatrixClearButton_Click(object sender, RoutedEventArgs e)
        {
            //if (_ledMatrix != null)
            //    _ledMatrix.Clear();
        }

        private void SquirtButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //if (_catDeterrent != null)
            //    _catDeterrent.squirt(true);
            //_controlToReceiveFocus.Focus();
        }

        private void SquirtButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //if (_catDeterrent != null)
            //    _catDeterrent.squirt(false);
            //_controlToReceiveFocus.Focus();
        }

        private void BtnShowVideo_Click(object sender, RoutedEventArgs e)
        {
            switchImageDisplay(false);
        }

        private void switchImageDisplay(bool showImages)
        {
            _stillImagesDisplayed = showImages;
            image1.Visibility = showImages ? Visibility.Visible : Visibility.Hidden;
            image2.Visibility = showImages ? Visibility.Visible : Visibility.Hidden;
            image3.Visibility = showImages ? Visibility.Visible : Visibility.Hidden;
            Video1Area.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
            video2.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
            video3.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
        }

        private void BtnShowImage_Click(object sender, RoutedEventArgs e)
        {
            //switchImageDisplay(true);
            //// Find latest images
            //_curImageAgeToDisplay = 0;
            //showImages();
        }

        private void showImages()
        {
            //DeviceInfo devInfo = _configFileInfo.GetDevice("frontDoorCamera");
            //image1.Source = GetImageFromFolder(devInfo != null ? devInfo.imageGrabPath : "", _curImageAgeToDisplay);
            //devInfo = _configFileInfo.GetDevice("garageCamera");
            //image2.Source = GetImageFromFolder(devInfo != null ? devInfo.imageGrabPath : "", _curImageAgeToDisplay);
            //devInfo = _configFileInfo.GetDevice("catCamera");
            //image3.Source = GetImageFromFolder(devInfo != null ? devInfo.imageGrabPath : "", _curImageAgeToDisplay);
        }

        private void BtnImageNext_Click(object sender, RoutedEventArgs e)
        {
            //switchImageDisplay(true);
            //_curImageAgeToDisplay--;
            //if (_curImageAgeToDisplay < 0)
            //    _curImageAgeToDisplay = 0;
            //showImages();
        }

        private void BtnImagePrev_Click(object sender, RoutedEventArgs e)
        {
            //switchImageDisplay(true);
            //_curImageAgeToDisplay++;
            //showImages();
        }

        private void Toggle_Garage_Click(object sender, RoutedEventArgs e)
        {

        }
        
        private void SysTrayPosition()
        {
            // Position window
            PresentationSource MainWindowPresentationSource = PresentationSource.FromVisual(this);
            Matrix m = MainWindowPresentationSource.CompositionTarget.TransformToDevice;
            double DpiWidthFactor = m.M11;
            double DpiHeightFactor = m.M22;
            double ScreenHeight = SystemParameters.PrimaryScreenHeight * DpiHeightFactor;
            double ScreenWidth = SystemParameters.PrimaryScreenWidth * DpiWidthFactor;

            logger.Info("W " + System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width.ToString() + " . " + Width.ToString() + " . " + ScreenWidth.ToString() +
                    " H " + System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height.ToString() + " . " + Height.ToString() + " . " + ScreenHeight.ToString());
            if ((ScreenWidth - Width > 0) && (ScreenHeight - Height > 0))
            {
                Left = ScreenWidth - Width;
                Top = ScreenHeight - Height - 100;
            }
        }

        private void SysTrayIcon()
        {
            // Notify icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            Stream iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/res/door48x48.ico")).Stream;
            _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            _notifyIcon.Visible = true;
            _notifyIcon.MouseUp +=
            new System.Windows.Forms.MouseEventHandler(delegate (object sender, System.Windows.Forms.MouseEventArgs args)
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
        }

        // Images and icons
        private EasyButtonImage doorLockedImages;
        private EasyButtonImage doorClosedImages;
        private EasyButtonImage garageClosedImages;
        private EasyButtonImage garageUnknownImages;
        private EasyButtonImage doorBellImages;

        private void InitUIMedia()
        {
            // Door status images
            doorLockedImages = new EasyButtonImage(@"res/locked-large.png", @"res/unlocked-large.png");
            doorClosedImages = new EasyButtonImage(@"res/doorclosed-large.png", @"res/dooropen-large.png");
            garageClosedImages = new EasyButtonImage(@"res/garageclosed-large.png", @"res/garageopen-large.png");
            garageUnknownImages = new EasyButtonImage(@"res/garageunknown-large.png", @"res/garageopen-large.png");
            doorBellImages = new EasyButtonImage(@"res/doorbell-large-sq.png", @"res/doorbell-large.png");
        }

        private void ActionButtonClick(object sender, RoutedEventArgs e)
        {
            //var buttonActions = new Dictionary<string, Action>()
            //{
            //    { "Unlock_Main", () => { _deviceManager.Control("frontDoorLock", 0, "unlock"); } },
            //    { "Lock_Main", () => { _deviceManager.Control("frontDoorLock", 0, "lock"); } },
            //};

            string buttonName = (sender as System.Windows.Controls.Button).Name.ToString();
            string tag = (sender as System.Windows.Controls.Button).Tag.ToString();

            logger.Info("Button " + buttonName + " tag " + tag + " sender " +  sender.ToString() + " event " + e.ToString());

            // Split tag
            string[] tagElems = tag.Split('_');
            if (tagElems.Length < 3)
                return;

            // Execute
            _deviceManager.Control(tagElems[0], Int32.Parse(tagElems[1]), tagElems[2]);

            // Put the focus on the settings control
            this.Settings.Focus();
        }

        private void UIUpdateCallback(bool popup)
        {
            // Check if window should popup
            if (popup)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    (System.Windows.Forms.MethodInvoker)delegate ()
                    {
                        ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
#if (LISTEN_TO_CAMERA)
                        _listenToAxisCamera.ListenForAFixedPeriod(_timeToListenAfterDoorbellRingInSecs);
#endif
                    }
                );
            }

            //_doorStatusRefreshTime = DateTime.Now;

            // Update UI
            this.Dispatcher.BeginInvoke(
                (Action)delegate ()
                {
                    ShowDoorStatus();
                }
            );
        }
    }
}
