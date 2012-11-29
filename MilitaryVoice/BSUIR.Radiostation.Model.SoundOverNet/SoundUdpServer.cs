using System;
using System.Net;
using LumiSoft.Net.UDP;

namespace BSUIR.Radiostation.Model.SoundOverNet
{
    internal class SoundUdpServer
    {
        private static SoundUdpServer _server;

        public UdpServer udpServer;

        private SoundUdpServer()
        {
            udpServer = new UdpServer();
        }

        public static SoundUdpServer Instance
        {
            get
            {
                if (_server == null)
                {
                    _server = new SoundUdpServer();
                }
                return _server;
            }
        }
    }
}
