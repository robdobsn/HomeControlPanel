using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace RdWebCamSysTrayApp
{
    class FrontDoorControl
    {
        public class DoorStatus
        {
            public string tagId = "";
            public string tagPresentInfo = "";
            public bool mainLocked = false;
            public bool mainOpen = false;
            public bool innerLocked = false;
            public bool bellPressed = false;

            public void Set(string s)
            {
                try
                {
                    string[] sVals = s.Split(new char[] { ',' });
                    tagId = sVals[0];
                    tagPresentInfo = sVals[1];
                    mainLocked = (sVals[2] == "Locked");
                    mainOpen = (sVals[3] != "Closed");
                    innerLocked = (sVals[4] == "Locked");
                    bellPressed = (sVals[5] != "No");
                }
                catch(Exception excp)
                {
                    Console.WriteLine("Exception in FrontDoorControl::DoorStatus:Set " + excp.Message);
                }
            }
        }
        private string _doorIPAddress;
        private Timer _doorStatusTimer;
        private DoorStatus _doorStatus = new DoorStatus();
        private DateTime _lastDoorStatusTime = DateTime.MinValue;

        public FrontDoorControl(string doorIPAddress)
        {
            _doorIPAddress = doorIPAddress;

            // Timer to update status
            _doorStatusTimer = new Timer(500);
            _doorStatusTimer.Elapsed += new ElapsedEventHandler(OnDoorStatusTimer);
        }

        public void StartUpdates()
        {
            _doorStatusTimer.Start();
        }

        public void UnlockMainDoor()
        {
            ControlDoor("main-unlock");
        }

        public void LockMainDoor()
        {
            ControlDoor("main-lock");
        }

        public void UnlockInnerDoor()
        {
            ControlDoor("inner-unlock");
        }

        public void LockInnerDoor()
        {
            ControlDoor("inner-lock");
        }

        private async void ControlDoor(string doorCommand)
        {
            try
            {
                // Create a New HttpClient object.
                HttpClient client = new HttpClient();

                HttpResponseMessage response = await client.GetAsync("http://" + _doorIPAddress + "/" + doorCommand);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                // Above three lines can be replaced with new helper method in following line 
                // string body = await client.GetStringAsync(uri);

                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException excp)
            {
                Console.WriteLine("FrontDoorControl::ControlDoor exception " + excp.Message);
            }
        }

        private void OnDoorStatusTimer(object source, ElapsedEventArgs e)
        {
            try
            {
                string requestURI = "http://" + _doorIPAddress + "/status";
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(requestURI);
                webReq.Method = "GET";
                webReq.BeginGetResponse(new AsyncCallback(DoorStatusCallback), webReq);
            }
            catch (Exception excp)
            {
                Console.WriteLine("Exception in FrontDoorControl::GetDoorStatus " + excp.Message);
            }
        }

        public bool GetDoorStatus(out DoorStatus doorStatus)
        {
            doorStatus = _doorStatus;
            return (DateTime.Now-_lastDoorStatusTime).TotalSeconds < 3;                
        }

        private void DoorStatusCallback(IAsyncResult res)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)res.AsyncState;
                if (request == null)
                    return;
                HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(res);
                if (response == null)
                    return;
                string body = new StreamReader(response.GetResponseStream()).ReadToEnd();

                _doorStatus.Set(body);

                // Console.WriteLine("DoorStatusResp " + body);
                _lastDoorStatusTime = DateTime.Now;

            }
            catch (Exception excp)
            {
                Console.WriteLine("Exception in FrontDoorControl::DoorStatusCallback " + excp.Message);
            }
        }

    }
}
