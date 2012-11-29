using System;
using System.Net;
using LumiSoft.Net.UDP;
using LumiSoft.Net.Codec;
using LumiSoft.Media.Wave;

namespace BSUIR.Radiostation.Model.SoundOverNet
{
    /// <summary>
    /// Sound Sender. Encoding via aLaw codec
    /// </summary>
    public class SoundSender
    {
        private WaveIn _waveIn;
        private IPEndPoint _targetEndPoint;

        public bool IsRunning
        {
            get;
            private set;
        }

        public string TargetIP
        {
            get
            {
                return _targetEndPoint.ToString();
            }
        }

        public SoundSender(IPEndPoint targetPoint, WaveIn inputDevice)
        {
            _waveIn = inputDevice;
            _targetEndPoint = targetPoint;
        }

        public void Start()
        {
            _waveIn.BufferFull += WaveInBufferFull;
            _waveIn.Start();
            IsRunning = true;
        }

        public void Stop()
        {
            _waveIn.Stop();
            IsRunning = false;
        }

        private void WaveInBufferFull(byte[] buffer)
        {
            byte[] encodedData = G711.Encode_aLaw(buffer, 0, buffer.Length);
            if (encodedData != null)
            {
                SoundUdpServer.Instance.udpServer.SendPacket(encodedData, 0, encodedData.Length, _targetEndPoint);
            }
        }
    }
}
