using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using BSUIR.Radiostation.Model.NetworkComponent;

namespace BSUIR.Radiostation.Model.NetworkComponent
{
    public class DataManager
    {
        private Client<Message> _networkClient;

        private const string timeoutConfigString = "NetworkMaxTimeout";

        private readonly int networkTimeout;

        private Server<Message> _networkServer;

        public DataManager(Client<Message> client, Server<Message> server) 
        {
            _networkClient = client;
            _networkServer = server;
            networkTimeout = -1;
            int.TryParse(ConfigurationManager.AppSettings[timeoutConfigString], out networkTimeout);
        }

        public void SendData(Dictionary<string, string> data,bool modal)
        {
            _networkClient.Send(new Message { message1 = "count", message2 = data.Count.ToString(), date = DateTime.Now });
            foreach (var pair in data) 
            {
                _networkClient.Send(new Message { message1 = pair.Key, message2 = pair.Value, date = DateTime.Now });
            }
        }

        private KeyValuePair<string, string> Transform(Message message) 
        {
            return new KeyValuePair<string, string>(message.message1, message.message2);
        }

        public bool Test() 
        {
            var testSeconds = 5;
            var restSeconds = 0;
            _networkClient.Send(new Message{ message1 = "networkTest", message2 = "send", date = DateTime.Now});
            while (restSeconds < testSeconds) 
            {
                if (_networkServer.Objects.Count > 0) 
                {
                    if (_networkServer.Objects[0].message1 == "networkTest" && _networkServer.Objects[0].message2 == "recived")
                    {
                        _networkServer.Objects.RemoveAt(0);
                        return true;
                    }
                    restSeconds += 1;
                    Thread.Sleep(1000);
                }
            }
            return false;
        }

        public Dictionary<string, string> ReciveData(bool modal)
        {
            var notNeedTimeout = networkTimeout < 0;
            var recordsCount = -1;
            var modalWindow = new NetworkSynchronizeForm();
            var passed = 0;
            Dictionary<string, string> result = new Dictionary<string, string>();
            try
            {
                if (modal)
                    modalWindow.Show();
                while (passed < networkTimeout || notNeedTimeout)
                {
                    if (_networkServer.Objects.Count > 0)
                    {
                        if (recordsCount <= 0)
                        {
                            if (_networkServer.Objects[0].message1 != "count")
                                throw new ArgumentNullException();
                            int.TryParse(_networkServer.Objects[0].message2, out recordsCount);
                            if (recordsCount <= 0)
                                throw new FormatException();
                            _networkServer.Objects.RemoveAt(0);
                        }
                        if (recordsCount > 0 && _networkServer.Objects.Count >= recordsCount)
                        {
                            result = _networkServer.Objects.Take(recordsCount).Select(x => Transform(x)).ToDictionary(x => x.Key, x => x.Value);
                            _networkServer.Objects.RemoveRange(0, recordsCount);
                            break;
                        }
                    }
                    Thread.Sleep(1000);
                    passed += 1;
                }
                if (modal)
                    modalWindow.Close();
            }
            catch (Exception)
            {

            }
            return result;
        }
    }
}
