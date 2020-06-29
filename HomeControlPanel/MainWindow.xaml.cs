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
using NLog.Fluent;

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
        private int _autoHideRequiredSecs = AUTO_HIDE_AFTER_AUTO_SHOW_SECS;

        // UI Update timer
        private const int UI_UPDATE_TIMER_PERIOD_SECS = 1;
        private DispatcherTimer _uiUpdateTimer = new DispatcherTimer();

        // Device manager
        private DeviceManager _deviceManager = null;

        // Show still images instead of video
        private bool _stillImagesDisplayed = false;

        //        // Image display
        //        private int _curImageAgeToDisplay = 0;
        //        private string _lastFrontDoorImageName = "";

        public MainWindow()
        {
            InitializeComponent();

            // Create device manager
            _deviceManager = new DeviceManager(UIUpdateCallback);

            // Resizer
            ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
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

                // Start Video
                StartVideo();
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

            // UI Update timer
            _uiUpdateTimer.Tick += new EventHandler(UIUpdateTimerFn);
            _uiUpdateTimer.Interval = new TimeSpan(0, 0, UI_UPDATE_TIMER_PERIOD_SECS);
            _uiUpdateTimer.Start();

            // Log startup
            logger.Info("App loaded complete");
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            logger.Info("Window closing");
            base.OnClosing(e);
            e.Cancel = true;
            WindowState = WindowState.Minimized;
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
            //Win32Helper.ShowWindowNoActive(this);
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            //this.Focus();
        }

        public void ShowPopupWindow(int autoHideSecs)
        {
            _autoHideRequiredSecs = autoHideSecs;
            BringWindowToFront();
            //StartVideo();
            logger.Info("Popup Shown");
        }

        public void HidePopupWindow()
        {
            logger.Info("Popup Hidden");
            //StopVideo();
            //StopTalkAndListen();
            this.Hide();
        }

        public async void ExitApp(object sender, EventArgs e)
        {
            await Video1Area.Close();

            HidePopupWindow();

            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Shutdown();
        }

        private async void StartVideo()
        {
            List<(Unosquare.FFME.MediaElement, string)> mediaElemCameras = new List<(Unosquare.FFME.MediaElement, string)> 
            {
                ( Video1Area, "frontDoorCamera" ),
                ( Video2Area, "garageCamera" ),
                ( Video3Area, "axis1054Camera" ),
            };

            // Start video
            foreach (var mec in mediaElemCameras)
            {
                try
                {
                    string imgURL = _deviceManager.GetString(mec.Item2, 0, "videoURL");
                    logger.Info("Video URL " + imgURL);
                    if (imgURL.Length > 0)
                    {
                        bool videoOk = await mec.Item1.Open(new Uri(imgURL));
                        logger.Info(mec.Item2 + " video open result " + videoOk);
                    }
                }
                catch (Exception excp)
                {
                    logger.Error("Failed to open video " + excp.ToString());
                }
            }
        }

        private void StartListen_Click(object sender, RoutedEventArgs e)
        {
            Video1Area.IsMuted = false;
        }

        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
            Video1Area.IsMuted = true;
        }

        private void AutoShowWindowFn()
        {
            //System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
            //        (System.Windows.Forms.MethodInvoker)delegate ()
            //        {
            //            ShowPopupWindow(AUTO_HIDE_AFTER_AUTO_SHOW_SECS);
            //        });
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
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
            // Front door
            if (_deviceManager.GetVal("frontDoorLock", 0, "locked") != 0)
                mainDoorLockState.Source = doorLockedImages.Img1();
            else
                mainDoorLockState.Source = doorLockedImages.Img2();
            if (_deviceManager.GetVal("frontDoorLock", 1, "locked") != 0)
                innerDoorLockState.Source = doorLockedImages.Img1();
            else
                innerDoorLockState.Source = doorLockedImages.Img2();
            if (_deviceManager.GetVal("frontDoorLock", 0, "closed") != 0)
                mainDoorOpenState.Source = doorClosedImages.Img1();
            else
                mainDoorOpenState.Source = doorClosedImages.Img2();

            // Garage
            if (_deviceManager.GetVal("garageDoorLock", 0, "locked") != 0)
                garageDoorOpenState.Source = garageClosedImages.Img1();
            else if (_deviceManager.GetVal("garageDoorLock", 0, "open") != 0)
                garageDoorOpenState.Source = garageClosedImages.Img2();
            else
                garageDoorOpenState.Source = garageUnknownImages.Img1();

            // Bell
            if (_deviceManager.GetVal("frontDoorLock", 0, "bell") != 0)
                doorBellState.Source = doorBellImages.Img1();
            else
                doorBellState.Source = null;
        }

        private void UIUpdateTimerFn(object sender, EventArgs e)
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

            // Show door update times
            SetDoorUpdateStatus(DoorStatusTextBox, "frontDoorLock");
            SetDoorUpdateStatus(GarageStatusTextBox, "garageDoorLock");
        }

        private void SetDoorUpdateStatus(System.Windows.Controls.TextBox box, string doorName)
        {
            int timeVal = _deviceManager.GetVal(doorName, 0, "sinceUpdateSecs");
            ShowTextInBox(box, timeVal >= 0 ? timeVal.ToString() + "s old" : "No Data", timeVal < 0 || timeVal > 30);
        }

        private void ShowTextInBox(System.Windows.Controls.TextBox box, string textToShow, bool isAlert)
        {
            if (isAlert)
            {
                box.Background = Brushes.Red;
                box.Foreground = Brushes.White;
            }
            else
            {
                box.Background = Brushes.White;
                box.Foreground = Brushes.Black;
            }
            box.Text = textToShow;
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
            Video2Area.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
            Video3Area.Visibility = showImages ? Visibility.Hidden : Visibility.Visible;
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
