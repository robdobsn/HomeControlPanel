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

namespace RdWebCamSysTrayApp
{
    class DomoticzControl
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private List<string> _ipAddresses;

        public DomoticzControl(List<string> ipAddresses)
        {
            _ipAddresses = new List<string>(ipAddresses);
        }

        public void SendGroupCommand(string groupCommand)
        {
            foreach (string ipAddress in _ipAddresses)
            {
                try
                {
                    Uri uri = new Uri("http://" + ipAddress + "/json.htm?type=scenes");

                    // Using WebClient as can't get HttpClient to not block
                    WebClient requester = new WebClient();
                    Dictionary<string, string> reqContext = new Dictionary<string, string>();
                    reqContext.Add("IP", ipAddress);
                    reqContext.Add("cmd", groupCommand);
                    requester.OpenReadAsync(uri, reqContext);
                    requester.OpenReadCompleted += new OpenReadCompletedEventHandler(getScenesCompleted);
                }
                catch (HttpRequestException excp)
                {
                    logger.Error("DomoticzControl::SendGroupCommand {0} exception {1}", groupCommand, excp.Message);
                }
            }
        }

        private class Scene
        {
            public string Name;
            public string idx;
            public string Type;
        };

        private void getScenesCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                logger.Error("DomoticzControl::SendGroupCommand completed but exception {0}", e.Error.Message);
                return;
            }

            Dictionary<string, string> reqContext = (Dictionary<string, string>)e.UserState;
            string ipAddress = reqContext["IP"];
            string command = reqContext["cmd"];
            string sceneIdx = "";

            Stream reply = null;
            StreamReader s = null;

            try
            {
                // Get the scene data
                reply = (Stream)e.Result;
                s = new StreamReader(reply);
                string jsonData = s.ReadToEnd();
                JObject sceneResults = JObject.Parse(jsonData);
                IList<JToken> results = sceneResults["result"].Children().ToList();
                IList<Scene> scenes = new List<Scene>();
                foreach (JToken result in results)
                {
                    Scene scene = JsonConvert.DeserializeObject<Scene>(result.ToString());
                    scenes.Add(scene);
                }

                foreach (Scene scene in scenes)
                {
                    if (scene.Name == command)
                        sceneIdx = scene.idx;
                }

                // Check if scene found - may not be as some units may not support the scene
                if (sceneIdx == "")
                    return;

                // Now call out to actually set the scene
                try
                {
                    Uri uri = new Uri("http://" + ipAddress + "/json.htm?type=command&param=switchscene&idx=" + sceneIdx + "&switchcmd=On");

                    // Using WebClient as can't get HttpClient to not block
                    WebClient requester = new WebClient();
                    requester.OpenReadAsync(uri, reqContext);
                    requester.OpenReadCompleted += new OpenReadCompletedEventHandler(domoticzCommandCompleted);
                }
                catch (HttpRequestException excp)
                {
                    logger.Error("DomoticzControl::SendGroupCommandStage2 {0} exception {1}", command, excp.Message);
                }
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

        private void domoticzCommandCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                logger.Info("DomoticzControl::SendGroupCommand ok");
            }
            else
            {
                logger.Info("DomoticzControl::SendGroupCommandStage3 error {0}", e.Error.ToString());
            }
        }

    }
}
