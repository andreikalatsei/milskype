using BSUIR.Radiostation.Model.NetworkComponent;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Timers;

using LumiSoft.Net.RTP;
using LumiSoft.Net.RTP.Debug;
using LumiSoft.Net.Media.Codec.Audio;
using LumiSoft.Net.Media;
using System.Configuration;

namespace milskype
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Server<string> _server;
        private Client<string> _client;

        private bool m_IsRunning = false;
        private bool m_IsSendingTest = false;
        private RTP_MultimediaSession m_pRtpSession = null;
        private AudioOut m_pWaveOut = null;
       // private FileStream m_pRecordStream = null;
        private string m_PlayFile = "";
        private Dictionary<int, AudioCodec> m_pAudioCodecs = null;
        private AudioCodec m_pActiveCodec = null;
        private AudioOut_RTP m_pAudioOut = null;

        private RTP_Session m_pSession = null;
        private AudioIn_RTP m_pAudioInRTP = null;
        private RTP_SendStream m_pSendStream = null;
        DispatcherOperation d_SendStream = null;
        DispatcherOperation d_RecieveStream = null;

        private int _samplesPerSecond;  // 8000, 22500, 44100, 48000 kHz
        private int _bitsPerSample;     // 16 or 8 bit
        private int _portBase;          // base port
        private int _portDifference;    // differrence between base port and input/output ports
        private string _selectedCodec;  // selected codec - PCMU or PCMA

        
        public ObservableCollection<string> History { get; private set; }
        public ObservableCollection<IPAddress> LocalIpList { get; private set; }
        public ObservableCollection<AudioInDevice> AudioInDeviceList { get; private set; }
        public ObservableCollection<AudioOutDevice> AudioOutDeviceList { get; private set; }

        public static readonly DependencyProperty IsConnectedProperty = DependencyProperty.Register("IsConnected", typeof(bool), typeof(MainWindow));
        public bool IsConnected
        {
            get
            {
                return this.GetValue(IsConnectedProperty) as bool? ?? false;
            }
            set
            {
                this.SetValue(IsConnectedProperty, value);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            lbHistory.DataContext = this;
            cbLocalIp.DataContext = this;
            btnCall.DataContext = this;
            cbAudioInDevices.DataContext = this;
            cbAudioOutDevices.DataContext = this;

            History = new ObservableCollection<string>();
            Refresh();

            cbAudioInDevices.SelectedIndex = 0;
            cbAudioOutDevices.SelectedIndex = 0;

            Loaded += (s, e) =>
                {
                    LoadSettings();
                };

            IsConnected = false;
        }

        private void LoadSettings()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                if (config.HasFile)
                {
                    var partnerIp = LoadParam(config, "LastPartnerIp") ?? String.Empty;
                    tbxPartnerIp.Text = partnerIp;

                    var audioInName = LoadParam(config, "LastAudioInDevice") ?? String.Empty;
                    var inDevice = AudioInDeviceList.FirstOrDefault(d => d.Name == audioInName);
                    var i = AudioInDeviceList.IndexOf(inDevice);
                    if (i >= 0)
                        cbAudioInDevices.SelectedIndex = i;

                    var audioOutName = LoadParam(config, "LastAudioOutDevice") ?? String.Empty;
                    var outDevice = AudioOutDeviceList.FirstOrDefault(d => d.Name == audioOutName);
                    i = AudioOutDeviceList.IndexOf(outDevice);
                    if (i >= 0)
                        cbAudioOutDevices.SelectedIndex = i;

                    var samplesPerSecond = LoadParam(config, "SamplesPerSecond") ?? String.Empty;
                    int.TryParse(samplesPerSecond, out _samplesPerSecond);
                    if (_samplesPerSecond < 8000 || _samplesPerSecond > 48000)
                        _samplesPerSecond = 44100;

                    var bitsPerSample = LoadParam(config, "BitsPerSample") ?? String.Empty;
                    int.TryParse(bitsPerSample, out _bitsPerSample);
                    if (_bitsPerSample != 8 || _bitsPerSample != 16)
                        _bitsPerSample = 16;

                    var portDifference = LoadParam(config, "PortDifference") ?? String.Empty;
                    int.TryParse(portDifference, out _portDifference);
                    if (_portDifference < 1)
                        _portDifference = 500;

                    var basePort = LoadParam(config, "PortBase") ?? String.Empty;
                    int.TryParse(basePort, out _portBase);
                    if (_portBase < 2000 + _portDifference)    // 2000 because of large amount of reserved ports in this range
                        _portBase = 10500;

                    _selectedCodec = LoadParam(config, "Codec") ?? String.Empty;
                }
                // There is no .config file
                else
                {
                    _samplesPerSecond = 44100;
                    _bitsPerSample = 16;
                    _portDifference = 500;
                    _portBase = 10500;
                }
            }
            catch (ConfigurationErrorsException exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        /// <summary>
        /// Refreshes audio in/out devices lists and local ip list
        /// </summary>
        private void Refresh()
        {
            AudioInDeviceList = new ObservableCollection<AudioInDevice>(AudioIn.Devices);
            AudioOutDeviceList = new ObservableCollection<AudioOutDevice>(AudioOut.Devices);
            LocalIpList = new ObservableCollection<IPAddress>(Dns.GetHostAddresses(Dns.GetHostName()));
        }

        private void StopMessageServer()
        {
            if (_server != null)
                _server.Dispose();
        }

        private void StopSoundSender()
        {
            /*if (_soundSender != null &&
                _soundSender.IsRunning)
                _soundSender.Stop();*/
        }

        private void StopSoundReceiver()
        {
            /*if (_soundReceiver != null &&
                _soundReceiver.IsRunning)
                _soundReceiver.Stop();*/
        }

        private void InitializeServer()
        {
            StopMessageServer();

            // TODO: add message port to config file
            try
            {
                _server = new Server<string>(cbLocalIp.SelectedItem as IPAddress, 15000, _client);
            }
            catch (Exception exc)
            {
                MessageBox.Show("Невозможно установить соединение: неверный IP и/или занят порт\n" + exc.Message);
            }

            if (_server != null)
            {
                _server.ObjectsChanged += (s, e) =>
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        History.Add(_server.Objects.Last());
                    }));
                };
            }
        }

        private void InitializeClient()
        {
            var ipParts = tbxPartnerIp.Text.Split('.').
                Select(t => Byte.Parse(t));
            _client = new Client<string>(new IPAddress(ipParts.ToArray()), 15000);
        }

        private void SendMessage(string message)
        {
            _client.Send(message);
            History.Add(message);
        }

        private void InitializeSoundReceiver()
        {
            StopSoundReceiver();

           // var device = WaveOut.Devices.First();
           // _soundReceiver = new SoundReciever(new IPEndPoint(cbLocalIp.SelectedItem as IPAddress, 15001), new LumiSoft.Media.Wave.WaveOut(device, 22500, 16, 1));
            //_soundReceiver.Start();
        }

        private void InitializeSoundSender()
        {
            StopSoundSender();

            var ipParts = tbxPartnerIp.Text.Split('.').
                Select(t => Byte.Parse(t));
        }

        #region Handlers

        private void btnSendMessage_Click_1(object sender, RoutedEventArgs e)
        {
            SendMessage(tbxMessage.Text);
        }

        private void btnCall_Click_1(object sender, RoutedEventArgs e)
        {
            IsConnected = false;
            m_pSession = m_pRtpSession.Sessions[0];
            if (m_pAudioInRTP == null)
            {
                m_pSendStream = m_pSession.CreateSendStream();               
            }
            else
            {
                m_pAudioInRTP.Dispose();
                m_pAudioInRTP = null;
                m_pSendStream.Close();
                m_pSendStream = null;

            }
           // _soundSender.Start();
           // _soundReceiver.Start();
        }

        private void btnRestartConnection_Click_1(object sender, RoutedEventArgs e)
        {
            if (cbLocalIp.SelectedIndex < 0)
            {
                MessageBox.Show("Choose local IP");
                return;
            }

            IsConnected = true;
            InitializeClient();
            InitializeServer();
            /*InitializeSoundSender();
            InitializeSoundReceiver();*/
            if (m_IsRunning)
            {
                m_IsRunning = false;
                m_IsSendingTest = false;

                m_pRtpSession.Dispose();
                m_pRtpSession = null;

                m_pWaveOut.Dispose();
                m_pWaveOut = null;
            }
            else
            {
                m_IsRunning = true;

                switch (_selectedCodec)
                {
                    case "PCMU":
                        m_pActiveCodec = new PCMU();
                        break;
                    case "PCMA":
                    default:
                        m_pActiveCodec = new PCMA();
                        break;
                }

                var selectedOutDevice = cbAudioOutDevices.SelectedItem as AudioOutDevice;
                m_pWaveOut = new AudioOut(selectedOutDevice, _samplesPerSecond, _bitsPerSample, 1); // 1 - one channel (mono)
                m_pRtpSession = new RTP_MultimediaSession(RTP_Utils.GenerateCNAME());

                string localIp = cbLocalIp.SelectedItem.ToString();
                string partnerIp = tbxPartnerIp.Text;
                int k = string.Compare(localIp, partnerIp);

                m_pRtpSession.CreateSession(new RTP_Address(IPAddress.Parse(cbLocalIp.SelectedItem.ToString()), (int)10000 + k * 500/*m_pLocalPort.Value*/, (int)/*m_pLocalPort.Value*/11000 + k * 500 + 1), new RTP_Clock(0, _samplesPerSecond));
                m_pRtpSession.Sessions[0].AddTarget(new RTP_Address(IPAddress.Parse(tbxPartnerIp.Text), (int)/*m_pRemotePort.Value*/10000 - k * 500, (int)/*m_pRemotePort.Value*/10000 - k * 500 + 1));
                m_pRtpSession.Sessions[0].NewSendStream += new EventHandler<RTP_SendStreamEventArgs>(m_pRtpSession_NewSendStream);
                m_pRtpSession.Sessions[0].NewReceiveStream += new EventHandler<RTP_ReceiveStreamEventArgs>(m_pRtpSession_NewReceiveStream);
                m_pRtpSession.Sessions[0].Payload = 0;
                m_pRtpSession.Sessions[0].Start();

                m_pAudioCodecs = new Dictionary<int, AudioCodec>();
                m_pAudioCodecs.Add(0, new PCMU());
                m_pAudioCodecs.Add(8, new PCMA());
            }                
        }

        private void wndMain_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            SaveParam(config, "LastPartnerIp", tbxPartnerIp.Text);
            SaveParam(config, "LastAudioInDevice", (cbAudioInDevices.SelectedValue as AudioInDevice).Name);
            SaveParam(config, "LastAudioOutDevice", (cbAudioOutDevices.SelectedValue as AudioOutDevice).Name);
            SaveParam(config, "SamplesPerSecond", _samplesPerSecond.ToString());

            config.Save();

            //StopSoundReceiver();
            // StopSoundSender();
            if (d_RecieveStream != null)
                d_RecieveStream.Dispatcher.InvokeShutdown();

            if (d_SendStream != null)
                d_SendStream.Dispatcher.InvokeShutdown();

            if (m_pAudioInRTP != null)
                m_pAudioInRTP.Dispose();

            if (m_pSendStream != null)
                m_pSendStream.Close();

            if (m_pAudioOut != null)
                m_pAudioOut.Dispose();

            if (m_pRtpSession != null)
                m_pRtpSession.Dispose();

            StopMessageServer(); 
        }

        #endregion

        private void SaveParam(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] == null)
                config.AppSettings.Settings.Add(key, value);
            else
                config.AppSettings.Settings[key].Value = value;
        }

        private string LoadParam(Configuration config, string key)
        {
            string result = null;
            if (config != null)
            {
                var param = config.AppSettings.Settings[key];
                result = param != null ?
                    param.Value : null;
            }
            return result;
        }

        #region method m_pRtpSession_NewReceiveStream

        /// <summary>
        /// This method is called when RTP session gets new receive stream.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pRtpSession_NewReceiveStream(object sender, RTP_ReceiveStreamEventArgs e)
        {
             d_RecieveStream =  Dispatcher.BeginInvoke(new Action(delegate()
            {
                /*wfrm_Receive frm = new wfrm_Receive(e.Stream, m_pAudioCodecs);
                frm.Show();*/
                var selectedOutDevice = cbAudioOutDevices.SelectedItem as AudioOutDevice;
                m_pAudioOut = new AudioOut_RTP(selectedOutDevice, e.Stream, m_pAudioCodecs);
                m_pAudioOut.Start();
            }));
        }

        #endregion

        #region method m_pRtpSession_NewSendStream

        /// <summary>
        /// This method is called when RTP session creates new send stream.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pRtpSession_NewSendStream(object sender, RTP_SendStreamEventArgs e)
        {
            d_SendStream = Dispatcher.BeginInvoke(new Action(delegate()
            {
                var selectedInDevice = cbAudioInDevices.SelectedItem as AudioInDevice;
                m_pAudioInRTP = new AudioIn_RTP(selectedInDevice, 20, m_pAudioCodecs, m_pSendStream);
                m_pAudioInRTP.Start();
            }));
        }
        #endregion
    }
}
