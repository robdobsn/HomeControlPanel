using MahApps.Metro.Controls;
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

namespace RdWebCamSysTrayApp
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
        private AudioDevices _audioDevices;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public SettingsWindow(AudioDevices audioDevices)
        {
            _audioDevices = audioDevices;

            InitializeComponent();

            // Populate the combos
            SpeakersCombo.ItemsSource = _audioDevices.GetOutDeviceInfo();
            SpeakersCombo.SelectedValue = _audioDevices.GetCurWaveOutDeviceName();
            MicrophoneCombo.ItemsSource = _audioDevices.GetInDeviceInfo();
            MicrophoneCombo.SelectedValue = _audioDevices.GetCurWaveInDeviceName();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            logger.Info("SettingsWindow::OkButton_Clicked speakerscombo.selvalue {0}", SpeakersCombo.SelectedValue);

            _audioDevices.SetWaveOutDeviceName((string)SpeakersCombo.SelectedValue);
            _audioDevices.SetWaveInDeviceName((string)MicrophoneCombo.SelectedValue);

            Properties.Settings.Default.Save();
            this.Close();
        }
    }
}
