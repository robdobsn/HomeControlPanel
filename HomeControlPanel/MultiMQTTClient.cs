using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeControlPanel
{
    class MultiMQTTClient
    {
        // MQTT client
        private IManagedMqttClient _mqttClient;

        // Delegate for rx
        public delegate void MQTTRxCallback(MqttApplicationMessage message);

        // Device info
        DeviceInfo _deviceInfo;

        // Sub info
        class MQTTSub
        {
            public MQTTSub(string topic, MQTTRxCallback rxCB)
            {
                this.topic = topic;
                this.rxCB = rxCB;
            }
            public string topic;
            public MQTTRxCallback rxCB;
        }
        List<MQTTSub> _mqttSubs = new List<MQTTSub>();

        public MultiMQTTClient(DeviceInfo deviceInfo)
        {
            _deviceInfo = deviceInfo;
            start();
        }

        public void start()
        {

            // MQTT
            String clientName = Environment.MachineName + "_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(clientName)
                    .WithTcpServer(_deviceInfo.mqttServer, _deviceInfo.mqttPort)
                    .Build())
                .Build();
            _mqttClient = new MqttFactory().CreateManagedMqttClient();

            _mqttClient.StartAsync(options);

            // Handler
            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                foreach (MQTTSub subsc in _mqttSubs)
                {
                    if (subsc.topic.Equals(e.ApplicationMessage.Topic))
                    {
                        subsc.rxCB(e.ApplicationMessage);
                        break;
                    }
                }
            });
        }

        public void subscribe(string topicStr, MQTTRxCallback rxCallback)
        {
            var topic = new MqttTopicFilterBuilder().WithTopic(topicStr).Build();
            _mqttClient.SubscribeAsync(topic);
            _mqttSubs.Add(new MQTTSub(topicStr, rxCallback));
        }

        public void publishAsync(MqttApplicationMessage msg)
        {
            _mqttClient.PublishAsync(msg);
        }

    }
}
