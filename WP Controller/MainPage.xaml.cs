using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Networking.Proximity;
using System.Diagnostics;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media;

using WP_Controller.Resources;

namespace WP_Controller
{
    public partial class MainPage : PhoneApplicationPage
    {
        public const string DefaultAppTitle = "Adaptive Node Controller ";

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Set the data context of the listbox control to the sample data
            DataContext = App.ViewModel;

            App._adaptiveNodeControl = new AdaptiveNodeControl();
            App._adaptiveNodeControl.MessageReceived += _adaptiveNodeControl_MessageReceived;

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }

        void _adaptiveNodeControl_DebugCodeReceived(byte[] debugData) // TODO: switch to byte[] data, have serial handler do the >D> parsing
        {

        }


        void _adaptiveNodeControl_MessageReceived(Message message)
        {
            // Sample message: >M>B:0:0D:C3:FF:FF:427F::��������  (NOTE: there is a hidden byte between the "empty" ::)
            // Format: ">M>" - Header
            //          B - Direction. Options are (B)roadcast, (I)ncoming, (D)irect.
            //          # - Pipe #. Broadcasts are usually on pipe 0, network broadcasts on pipe 1 
            //              and direct messages on pipe 2. Encrypted streams usually are on pipe 3.
            //          0D:C3 - Network ID and Device ID
            //          FF:FF - Recipient Network ID and Device ID
            //          427F  - Message Header (TTL and ID)
            //          9-26 bytes - Message Data Field

            if (message == null)
            {
                return;
            }

            if (message.Error != null && message.Error.Length != 0)
            {
                // TODO: Handle error, put it into a dianostics section 
                MessageBox.Show("Error on incoming message:" + message.Error);
            }

            if (message.Debug != null && message.Debug.Length != 0)
            {
                // TODO: Handle debug data if the user enabled advanced logging
            }

            if (message.Error == null &&
                message.Debug == null &&
                message.MessageType != MessageType.MSGTYPE_IGNORED)
            {
                string data = "";
                if (message.MessageData != null)
                {
                    foreach (byte val in message.MessageData)
                    {
                        data += String.Format("{0:X2}", val);
                    }
                }

                if (message.MessageType == MessageType.MSGTYPE_HEARTBEAT)
                {
                    WP_Controller.ViewModels.ItemViewModel device = new ViewModels.ItemViewModel
                    {
                        NetworkId = String.Format("{0:X2}", message.NetworkId),
                        DeviceId = String.Format("{0:X2}", message.DeviceId),
                        DeviceType = DeviceType.Unknown,
                        UniqueAddress = String.Format("{0:X2}{1:X2}{2:X2}{3:X2}{4:X2}", message.MessageData[0], message.MessageData[1], message.MessageData[2], message.MessageData[3], message.MessageData[0]),
                    };

                    Dispatcher.BeginInvoke(delegate()
                    {
                        App.ViewModel.AddOrUpdateDevice(device);
                        int deviceCount = App.ViewModel.Items.Count;
                        PivotTitle.Title = String.Format("{0} - {1}({2})", DefaultAppTitle, App._adaptiveNodeControl.IsConnected ? "Connected" : "Disconnected", deviceCount);
                    });
                }

                        //case MessageType.MSGTYPE_TRIGGER:
                        //    // Sample format - port(B,C,D):MaskValue, eg: "D�\0\0\0\0\0\0"
                        //    for (int i = 0; i < 8; i += 2)
                        //    {
                        //        char port = messagePacket[8][i];
                        //        if (port == 0)
                        //        {
                        //            break;
                        //        }
                        //        PortMasks ports = (PortMasks)messagePacket[8][i + 1];
                        //        // These double checks are used as error correcting checks incase we get bad/odd data back
                        //        if (port == 'B' && ports == (PortMasks.MASK_PORT_B1 | PortMasks.MASK_PORT_B2))
                        //        {
                        //            App.portsTriggered |= ports;
                        //        }
                        //        if (port == 'D' && ports == (PortMasks.MASK_PORT_D2 | PortMasks.MASK_PORT_D3 | PortMasks.MASK_PORT_D4 | PortMasks.MASK_PORT_D5 | PortMasks.MASK_PORT_D6))
                        //        {
                        //            App.portsTriggered |= ports;
                        //        }
                        //    }
            }
        }

        // Load data for the ViewModel Items
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }

            StartBtConnection();
        }


        private async void StartBtConnection()
        {
            HostName hostname = await QueryAdaptiveNodeBT();
            await ConnectToDevice(hostname);
        }

        // Quick way of keeping the user from being redirected over and over to the settings page 
        private static bool hasShownBtReminder = false;
        private async Task<HostName> QueryAdaptiveNodeBT()
        {
            PivotTitle.Title = DefaultAppTitle + "- Connecting...";
            PeerFinder.AlternateIdentities["Bluetooth:PAIRED"] = "";

            IReadOnlyList<PeerInformation> pairedDevices = null;

            try
            {
                // Get a list of matching devices
                pairedDevices = await PeerFinder.FindAllPeersAsync();
            }
            catch { }

            if (pairedDevices == null || pairedDevices.Count == 0)
            {
                if (!hasShownBtReminder)
                {
                    Debug.WriteLine("No paired devices were found.");
                    MessageBox.Show("No Adaptive Node BT Device Found");

                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-bluetooth:"));
                    hasShownBtReminder = true;
                }
                return null;
            }

            PeerInformation peerInfo = pairedDevices.FirstOrDefault(c => c.DisplayName.Contains("AdaptiveNode"));

            if (peerInfo == null)
            {
                peerInfo = pairedDevices.FirstOrDefault(c => c.DisplayName.Contains("HC-05"));
            }

            if (peerInfo == null)
            {
                MessageBox.Show("Could not find any Adaptive Node BT devices");
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-bluetooth:"));
                return null;
            }

            return peerInfo.HostName;
        }

        private async Task<bool> ConnectToDevice(Windows.Networking.HostName deviceName, bool withUi = false)
        {
            StreamSocket s = new StreamSocket();

            try
            {
                // Try to connect to the device on the first port
                try
                {
                    await s.ConnectAsync(deviceName, "1");
                }
                catch (Exception e)
                {
                    if (withUi)
                    {
                        MessageBox.Show("Failed to connect to device. You can try fixing the issue by going to your Bluetooth Settings page and removing and re-adding the Bluetooth device. Debug message: " + e.Message);
                    }
                    PivotTitle.Title = DefaultAppTitle + "- Not Connected";
                    return false;
                }

                App._adaptiveNodeControl.Socket = s;

                // Wake up device if it's not broadcasting serial data
                App._adaptiveNodeControl.SendMessage("FF", "FF", MessageType.MSGTYPE_ACK, new byte[] { 0 });

                //if (_adaptiveNodeControl.IsConnected)
                //{
                //    WaitForData(s);
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            if (App._adaptiveNodeControl.IsConnected)
            {
                PivotTitle.Title = DefaultAppTitle + "- Connected";
            }
            else
            {
                PivotTitle.Title = DefaultAppTitle + "- Not Connected";
            }

            return App._adaptiveNodeControl.IsConnected;
        }

        private async void WaitForData(StreamSocket socket)
        {
            try
            {
                DataReader dr = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

                uint numStrBytes = await dr.LoadAsync(1);
                if (numStrBytes > 0)
                {
                    byte bDataLength = dr.ReadByte();
                    uint dataLength = (uint)bDataLength;

                    uint downloadedDataLength = await dr.LoadAsync(dataLength);
                    if (downloadedDataLength == dataLength)
                    {
                        byte[] buffer = new byte[downloadedDataLength];
                        for (int i = 0; i < downloadedDataLength; i++)
                        {
                            buffer[i] = dr.ReadByte();
                        }

                        string message = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                        // Sample message: >M>B:0:0D:C3:FF:FF:427F::��������  (NOTE: there is a hidden byte between the "empty" ::)
                        // Format: ">M>" - Header
                        //          B - Direction. Options are (B)roadcast, (I)ncoming, (D)irect.
                        //          # - Pipe #. Broadcasts are usually on pipe 0, network broadcasts on pipe 1 
                        //              and direct messages on pipe 2. Encrypted streams usually are on pipe 3.
                        //          0D:C3 - Network ID and Device ID
                        //          FF:FF - Recipient Network ID and Device ID
                        //          427F  - Message Header (TTL and ID)
                        //          9-26 bytes - Message Data Field

                        if (message != null && message.Length > 4)
                        {
                            string[] messagePacket = message.Split(new char[] { ':' }, StringSplitOptions.None);
                            if (messagePacket.Length >= 9 && messagePacket[0].StartsWith(">M>"))
                            {
                                string networkId = messagePacket[2];
                                string deviceId = messagePacket[3];

                                MessageType msgType = MessageType.MSGTYPE_IGNORED;
                                try
                                {
                                    msgType = (MessageType)messagePacket[7][0];
                                }
                                catch (InvalidCastException)
                                {

                                }

                                switch (msgType)
                                {
                                    case MessageType.MSGTYPE_HEARTBEAT:
                                        StringBuilder friendlyName = new StringBuilder();

                                        // Remove null bytes
                                        for (int i = 0; i < messagePacket[8].Length; i++)
                                        {
                                            // As soon as we see any NUL bytes, bail.
                                            if ((int)messagePacket[8][i] == '�')
                                            {
                                                break;
                                            }

                                            friendlyName.Append(messagePacket[8][i]);
                                        }

                                        WP_Controller.ViewModels.ItemViewModel device = new ViewModels.ItemViewModel
                                        {
                                            NetworkId = networkId,
                                            DeviceId = deviceId,
                                            DeviceType = DeviceType.Unknown,
                                            FriendlyName = friendlyName.ToString()
                                        };

                                        App.ViewModel.AddOrUpdateDevice(device);
                                        break;
                                    case MessageType.MSGTYPE_ACK:
                                        // ACKs include the device type; update the internal database
                                        break;
                                    case MessageType.MSGTYPE_UPDATE:
                                        // Update the device type here
                                        break;
                                    default:
                                        break;
                                }

                            }
                        }
                    }
                    else
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                if (App._adaptiveNodeControl.IsConnected)
                {
                    PivotTitle.Title = DefaultAppTitle + "- Connected (EX)";
                }
                else
                {
                    PivotTitle.Title = DefaultAppTitle + "- Not Connected";
                }
            }

            WaitForData(socket);
        }

        private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1)
            {
                return;
            }

            if (!(e.AddedItems[0] is WP_Controller.ViewModels.ItemViewModel))
            {
                e.AddedItems.Clear();
                return;
            }

            App.selectedDevice = (WP_Controller.ViewModels.ItemViewModel)e.AddedItems[0]; 
            NavigationService.Navigate(new Uri("/DevicePage.xaml", UriKind.Relative));

            e.AddedItems.Clear();
        }

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1)
            {
                return;
            }

            if (!(e.AddedItems[0] is WP_Controller.ViewModels.SettingViewModel))
            {
                e.AddedItems.Clear();
                return;
            }

            WP_Controller.ViewModels.SettingViewModel selectedItem = (WP_Controller.ViewModels.SettingViewModel)e.AddedItems[0];

            switch (selectedItem.ItemType)
            {
                case SettingsMenuItem.Connect:
                    StartBtConnection();
                    break;
                default:
                    break;
            }

            e.AddedItems.Clear();
        }

        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}
    }
}