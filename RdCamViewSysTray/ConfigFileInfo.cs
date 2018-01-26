using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

// Info on a device in the config file
public class DeviceInfo
{

    public string description;
    public string deviceType;
    public string hostname;
    public string username;
    public string password;
    public int port;
    public int notifyPort;
    public string videoURL;
    public string imageGrabPath;
    public int imageGrabPoll;
    public int userNum;
    public string userPin;
    public int motionDetectAutoShow;
}

public delegate void MainConfigAcquiredCallback();

// Data contained in the config file
public class ConfigFileInfo
{
    private static Logger logger = LogManager.GetCurrentClassLogger();
    private MainConfigAcquiredCallback _acquireOkCallback;
    private MainConfigAcquiredCallback _acquireFailedCallback;

    public class ConfigInfo
    {
        public Dictionary<string, DeviceInfo> devices = new Dictionary<string, DeviceInfo>();
    }

    public ConfigInfo _configInfo = new ConfigInfo();

    public void SetCallbacks(MainConfigAcquiredCallback acquireOkCallback, MainConfigAcquiredCallback acquireFailedCallback)
    {
        _acquireOkCallback = acquireOkCallback;
        _acquireFailedCallback = acquireFailedCallback;
    }
    public static string getIPAddressForName(string hname)
    {
        IPAddress[] ips;
        try
        {
            ips = Dns.GetHostAddresses(hname);
            if (ips.Length > 0)
                return ips[0].ToString();
        }
        catch
        {
            logger.Info("Failed to find ip for name " + hname);
        }
        return "";
    }

    public DeviceInfo GetDevice(String deviceName)
    {
        if (!_configInfo.devices.ContainsKey(deviceName))
            return null;
        return _configInfo.devices[deviceName];
    }

    public List<string> GetIPAddrByType(string deviceType)
    {
        List<string> deviceList = new List<string>();
        foreach (KeyValuePair<string, DeviceInfo> devInfo in _configInfo.devices)
        {
            if (devInfo.Value.deviceType == deviceType)
            {
                string ipAddr = ConfigFileInfo.getIPAddressForName(devInfo.Value.hostname);
                if (ipAddr.Length > 0)
                    deviceList.Add(ipAddr);
            }
        }
        return deviceList;
    }

    public void AcquireConfig()
    {

        string configSource = RdWebCamSysTrayApp.Properties.Settings.Default.ConfigSource;
        try
        {
            Uri uri = new Uri("http://" + configSource + ":5076/mainconfig");

            // Using WebClient as can't get HttpClient to not block
            WebClient requester = new WebClient();
            requester.OpenReadAsync(uri);
            requester.OpenReadCompleted += new OpenReadCompletedEventHandler(OpenConfigSourceCallback);
        }
        catch (HttpRequestException excp)
        {
            logger.Error("ConfigFileInfo AcquireConfig from {0} exception {1}", configSource, excp.Message);
        }
        catch (Exception excp)
        {
            logger.Error("DomoticzControl::SendGroupCommand {0} exception {1}", configSource, excp.Message);
        }

        //    try
        //    {
        //        using (StreamReader sr = new StreamReader("//domoticzoff/PiShare/nodeuser/config/MasterDevices.json"))
        //        {
        //            string jsonData = sr.ReadToEnd();
        //            _configInfo = JsonConvert.DeserializeObject<ConfigInfo>(jsonData);
        //            logger.Info("Read configuration from DomoticzOFF");
        //            _acquireOkCallback();
        //        }
        //    }
        //    catch (Exception excp)
        //    {
        //        try
        //        {
        //            using (StreamReader sr = new StreamReader("//macallan/Admin/Config/MasterDevices.json"))
        //            {
        //                string jsonData = sr.ReadToEnd();
        //                _configInfo = JsonConvert.DeserializeObject<ConfigInfo>(jsonData);
        //                logger.Info("Read configuration from Macallan");
        //                _acquireOkCallback();
        //            }
        //        }
        //        catch (Exception excp2)
        //        {
        //            logger.Info("Failed to read configuration from DomoticzOFF or Macallan " + excp.Message + " and " + excp2.Message);

        //        }
        //    }
        //    _acquireFailedCallback();
        //}

    }

    private void OpenConfigSourceCallback(Object sender, OpenReadCompletedEventArgs e)
    {
        Stream reply = null;
        StreamReader s = null;

        try
        {
            reply = (Stream)e.Result;
            s = new StreamReader(reply);
            // Convert from JSON
            //Console.WriteLine(s.ReadToEnd());
            string jsonStr = s.ReadToEnd();
            _configInfo = JsonConvert.DeserializeObject<ConfigInfo>(jsonStr);
            logger.Info("Read configuration from DomoticzOFF");
            _acquireOkCallback();
        }
        catch (Exception excp)
        {
            logger.Info("ConfigFileInfo: Exception getting config {0}", excp.Message);
        }
        finally
        {
            if (s != null)
            {
                s.Close();
            }

            if (reply != null)
            {
                reply.Close();
            }
        }
    }

}
