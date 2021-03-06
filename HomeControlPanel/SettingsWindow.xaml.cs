﻿using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NLog;

namespace HomeControlPanel
{
    public class ComboBoxItemString
    {
        public string ValueString { get; set; }
    }

    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : MetroWindow
    {
        private ConfigFileInfo _configFileInfo;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public SettingsWindow(ConfigFileInfo configFileInfo)
        {
            _configFileInfo = configFileInfo;

            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            //logger.Info("SettingsWindow::OkButton_Clicked speakerscombo.selvalue {0}", SpeakersCombo.SelectedValue);

            ////_audioDevices.SetWaveOutDeviceByName((string)SpeakersCombo.SelectedValue);
            ////_audioDevices.SetWaveInDeviceByName((string)MicrophoneCombo.SelectedValue);

            //Properties.Settings.Default.Save();

            //_configFileInfo.AcquireConfig();

            //if (_audioDevices != null)
            //{
            //    _audioDevices.SetupAudioDevicesFromConfig();
            //}
            //this.Close();
        }
    }
}
