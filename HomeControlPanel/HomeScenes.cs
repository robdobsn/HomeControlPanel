using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Net.Http;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace HomeControlPanel
{
    class HomeScenes : DeviceBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private ConfigFileInfo _configFileInfo;

        public HomeScenes(ref ConfigFileInfo configFileInfo, DeviceInfo deviceInfo)
        {
            _configFileInfo = configFileInfo;
        }

        public void Control(int idx, string cmd)
        {
            SendGroupCommand(cmd);
        }

        public int GetVal(int idx, string valType)
        {
            return 0;
        }

        public string GetString(int idx, string valType)
        {
            return "";
        }

        public void SendGroupCommand(string groupCommand)
        {
            string configSource = HomeControlPanel.Properties.Settings.Default.ConfigSource;
            Uri uri = new Uri("http://" + configSource + ":5076/setscene/" + groupCommand);

            // Using WebClient as can't get HttpClient to not block
            WebClient requester = new WebClient();
            requester.OpenReadAsync(uri);
            requester.OpenReadCompleted += new OpenReadCompletedEventHandler(execSceneCompleted);

            //// Get the scene urls
            //List<HomeScene> scenes = _configFileInfo.getScenes();
            //foreach (HomeScene scene in scenes)
            //{
            //    if (scene.uniqueName == groupCommand)
            //    {
            //        foreach (string url in scene.urls)
            //        {
            //            try
            //            {
            //                Uri uri = new Uri(url);

            //                // Using WebClient as can't get HttpClient to not block
            //                WebClient requester = new WebClient();
            //                Dictionary<string, string> reqContext = new Dictionary<string, string>();
            //                reqContext.Add("url", url);
            //                reqContext.Add("cmd", groupCommand);
            //                requester.OpenReadAsync(uri, reqContext);
            //                requester.OpenReadCompleted += new OpenReadCompletedEventHandler(execSceneCompleted);
            //            }
            //            catch (HttpRequestException excp)
            //            {
            //                logger.Error("HomeScenes::SendGroupCommand {0} exception {1}", groupCommand, excp.Message);
            //            }
            //            catch (Exception excp)
            //            {
            //                logger.Error("HomeScenes::SendGroupCommand {0} exception {1}", groupCommand, excp.Message);
            //            }
            //        }
            //        break;
            //    }
            //}
        }

        private class Scene
        {
            public string Name = "";
            public string idx = "";
            public string Type = "";
        };

        private void execSceneCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                logger.Info("HomeScenes::SendGroupCommand ok");
            }
            else
            {
                logger.Info("HomeScenes::SendGroupCommandStage3 error {0}", e.Error.ToString());
            }
        }

    }
}
