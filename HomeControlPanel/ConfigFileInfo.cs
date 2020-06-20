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
    public string name;
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
    public string mqttServer;
    public int mqttPort;
    public string mqttInTopic;
    public string mqttOutTopic;
    public int rotation;

    DeviceInfo()
    {
        port = 0;
        notifyPort = 0;
        imageGrabPoll = 0;
        userNum = 0;
        motionDetectAutoShow = 0;
        mqttPort = 0;
        rotation = 0;
    }
}

public class HomeScene
{
    public string room;
    public string nom;
    public string uniqueName;
    public string type;
    public string icon;
    public List<string> urls;
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

    public class SceneInfo
    {
        public List<HomeScene> scenes = new List<HomeScene>();
    }

    public SceneInfo _scenes = new SceneInfo();

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
    public Dictionary<string, DeviceInfo> GetDevices()
    {
        return _configInfo.devices;
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

    public void AcquireConfig(MainConfigAcquiredCallback acquireOkCallback, MainConfigAcquiredCallback acquireFailedCallback)
    {
        _acquireOkCallback = acquireOkCallback;
        _acquireFailedCallback = acquireFailedCallback;

        string configSource = HomeControlPanel.Properties.Settings.Default.ConfigSource;
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
            _acquireFailedCallback();
            logger.Error("ConfigFileInfo AcquireConfig from {0} exception {1}", configSource, excp.Message);
        }
        catch (Exception excp)
        {
            _acquireFailedCallback();
            logger.Error("ConfigFileInfo AcquireConfig from {0} exception {1}", configSource, excp.Message);
        }
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
            logger.Info("Read configuration from server");

            // Get scenes
            string configSource = HomeControlPanel.Properties.Settings.Default.ConfigSource;
            try
            {
                Uri uri = new Uri("http://" + configSource + ":5076/scenes");

                // Using WebClient as can't get HttpClient to not block
                WebClient requester = new WebClient();
                requester.OpenReadAsync(uri);
                requester.OpenReadCompleted += new OpenReadCompletedEventHandler(OpenScenesCallback);
            }
            catch (HttpRequestException excp)
            {
                _acquireFailedCallback();
                logger.Error("ConfigFileInfo AcquireScenes from {0} exception {1}", configSource, excp.Message);
            }
            catch (Exception excp)
            {
                _acquireFailedCallback();
                logger.Error("ConfigFileInfo AcquireScenes from {0} exception {1}", configSource, excp.Message);
            }

        }
        catch (Exception excp)
        {
            logger.Info("ConfigFileInfo: Exception getting config {0}", excp.Message);
            _acquireFailedCallback();
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

    private void OpenScenesCallback(Object sender, OpenReadCompletedEventArgs e)
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
            _scenes = JsonConvert.DeserializeObject<SceneInfo>(jsonStr);
            logger.Info("Read scenes from server");
            _acquireOkCallback();
        }
        catch (Exception excp)
        {
            _acquireFailedCallback();
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

    public List<HomeScene> getScenes()
    {
        return _scenes.scenes;
    }

}
