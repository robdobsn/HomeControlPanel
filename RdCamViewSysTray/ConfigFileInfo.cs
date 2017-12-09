using NLog;
using System;
using System.Collections.Generic;
using System.Net;

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
}

// Data contained in the config file
public class ConfigFileInfo
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public Dictionary<string, DeviceInfo> devices;

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

}
