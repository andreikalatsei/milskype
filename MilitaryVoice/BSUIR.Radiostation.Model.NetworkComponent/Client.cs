using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace BSUIR.Radiostation.Model.NetworkComponent
{
    public class Client<T>
    {
        private IPEndPoint serverIp;

        public Client(IPAddress address, Int32 port)
        {
            serverIp = new IPEndPoint(address, port);
        }

        public void Send(T sndObject)
        {
            var client = new TcpClient();
            client.Connect(serverIp);
            var stream = client.GetStream();

            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, sndObject);

            stream.Close();
            client.Close();
        }
    }
}
