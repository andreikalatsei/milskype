using System;
using System.Net;
using LumiSoft.Net.UDP;
using LumiSoft.Net.Codec;
using LumiSoft.Media.Wave;

namespace BSUIR.Radiostation.Model.SoundOverNet
{
    /// <summary>
    /// Sound reciever. Decoding via aLaw codec
    /// </summary>
    public class SoundReciever
    {
        private WaveOut _waveOut;
        private IPEndPoint _udpEndpoint;

        public bool IsRunning
        {
            get;
            private set;
        }

        public string LocalIP
        {
            get
            {
                return _udpEndpoint.ToString();
            }
        }

        public SoundReciever(IPEndPoint endPoint, WaveOut outputDevice)
        {
            _waveOut = outputDevice;
            _udpEndpoint = endPoint;
        }

        /// <summary>
        /// Starts server for recieving sounds
        /// </summary>
        public void Start()
        {
            SoundUdpServer.Instance.udpServer.Bindings = new[] { _udpEndpoint };
            SoundUdpServer.Instance.udpServer.PacketReceived += PacketRecieved;
            SoundUdpServer.Instance.udpServer.Start();

            IsRunning = true;
        }

        /// <summary>
        /// Stop receiving server
        /// </summary>
        public void Stop()
        {
            SoundUdpServer.Instance.udpServer.Stop();

            IsRunning = false;
        }

        private void PacketRecieved(UdpPacket_eArgs e)
        {
            byte[] decodedData = G711.Decode_aLaw(e.Data, 0, e.Data.Length);
            _waveOut.Play(decodedData, 0, decodedData.Length);
        }
    }
}
