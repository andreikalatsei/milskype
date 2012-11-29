using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace BSUIR.Radiostation.Model.NetworkComponent
{
    public class Server<T> : IDisposable where T:class
    {
        private readonly Client<T> sender;
        private TcpListener listener;
        private Thread listenThread;
        private List<T> objectList;

        /// <summary>
        /// List of recieved objects
        /// </summary>
        public List<T> Objects
        {
            get { return objectList; }
        }

        public event EventHandler ObjectsChanged;

        /// <summary>
        /// Creates and starts server instance
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public Server(IPAddress address, Int32 port, Client<T> sender)
        {
            this.listener = new TcpListener(address, port);
            this.sender = sender;
            objectList = new List<T>();
            this.listenThread = new Thread(new ThreadStart(WaitClients));
            this.listenThread.Start();
        }

        private void WaitClients()
        {
            this.listener.Start();
            while (true)
            {
                TcpClient client = this.listener.AcceptTcpClient();
                var clientThread = new Thread(new ParameterizedThreadStart(ReadClientObjects));
                clientThread.Start(client);
            }
        }

        private void ReadClientObjects(object tcpClient)
        {
            var client = (TcpClient)tcpClient;
            var stream = client.GetStream();

            while (true)
            {
                try
                {
                    var formatter = new BinaryFormatter();
                    var recievedObject = (T)formatter.Deserialize(stream);
                    if (typeof(T) == typeof(Message)) 
                    {
                        Message mes = recievedObject as Message;
                        if (mes.message1 == "networkTest" && mes.message2 == "send") 
                        {
                            sender.Send( new Message { message1 = "networkTest", message2 = "recived", date = DateTime.Now } as T);
                        }else
                        {
                            this.objectList.Add(recievedObject);
                        }
                    }else{
                    this.objectList.Add(recievedObject);
                    }
                }
                catch
                {
                    break;
                }
            }
            stream.Close();
            client.Close();

            if (ObjectsChanged != null)
                ObjectsChanged(this, new EventArgs());
        }

        public void Dispose()
        {
            listenThread.Abort();
            listener.Stop();
            objectList.Clear();
        }
    }
}
