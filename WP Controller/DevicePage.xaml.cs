using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WP_Controller
{
    public partial class DevicePage : PhoneApplicationPage
    {
        private int _red = 0;
        private int _green = 0;
        private int _blue = 0;

        private static int simpleCountdown = 0;
        DispatcherTimer updateTimer = null;

        public DevicePage()
        {
            InitializeComponent();

            updateTimer = new DispatcherTimer();
            updateTimer.Interval += TimeSpan.FromMilliseconds(200);
            updateTimer.Tick += OnTimerTick;
            updateTimer.Start();

            DeviceName.Title = App.selectedDevice.NetworkId + ":" + App.selectedDevice.DeviceId;

            App._adaptiveNodeControl.MessageReceived += _adaptiveNodeControl_MessageReceived;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (updateTimer != null && updateTimer.IsEnabled)
            {
                updateTimer.Stop();
            }

            base.OnNavigatingFrom(e);
        }

        private void OnTimerTick(Object sender, EventArgs args)
        {
            if (simpleCountdown < -5)
            {
                return;
            }

            byte[] payload = new byte[4];
            payload[0] = 0x88;  // Analog Port C3 
            payload[1] = 0x0;   // r
            payload[2] = 0x0;   // g
            payload[3] = 0x0;   // b

            if (simpleCountdown > -5 && simpleCountdown <= 0)
            {
                --simpleCountdown;
                Dispatcher.BeginInvoke(delegate()
                {
                    warningText.Text = "";

                    payload[2] = (byte)0x15;
                    App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);
                });
            }

            if (simpleCountdown > 0)
            {
                --simpleCountdown;
                payload[1] = (byte)0x20;
                App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);
            }
        }

        void _adaptiveNodeControl_MessageReceived(string message)
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

            if (message.Contains(">M>"))
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
                        case MessageType.MSGTYPE_UPDATE:
                            // "D�\0\0\0\0\0\0"
                            int value = messagePacket[8][0];

                            break;
                        case MessageType.MSGTYPE_TRIGGER:
                            // Sample format - port(B,C,D):MaskValue, eg: "D�\0\0\0\0\0\0"

                            for (int i = 0; i < 8; i += 2)
                            {
                                char port = messagePacket[8][i];
                                if (port == 0)
                                {
                                    break;
                                }
                                PortMasks ports = (PortMasks)messagePacket[8][i + 1];
                                // These double checks are used as error correcting checks incase we get bad/odd data back
                                if (port == 'B' && ports == (PortMasks.MASK_PORT_B1 | PortMasks.MASK_PORT_B2))
                                {
                                    App.portsTriggered |= ports;

                                    simpleCountdown = 15;
                                    Dispatcher.BeginInvoke(delegate()
                                    {
                                        warningText.Text = "PORTB TRIGGERED!";
                                    });
                                }
                                if (port == 'D' && ports == (PortMasks.MASK_PORT_D2 | PortMasks.MASK_PORT_D3 | PortMasks.MASK_PORT_D4 | PortMasks.MASK_PORT_D5 | PortMasks.MASK_PORT_D6))
                                {
                                    App.portsTriggered |= ports;
                                    simpleCountdown = 15;
                                    Dispatcher.BeginInvoke(delegate()
                                    {
                                        warningText.Text = "PORTD TRIGGERED!";
                                    });
                                }
                            }

                            break;
                        default:
                            break;
                    }
                }
            }
        }


        private void UpdateLEDButton(object sender, RoutedEventArgs e)
        {
            byte[] payload = new byte[4];
            payload[0] = 0x88; // Analog Port C3 
            payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
            payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
            payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
            App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);
        }

        MessageSetupOptions setupConfig;
        private void UpdateIOButton(object sender, RoutedEventArgs e)
        {
            byte[] payload = new byte[4];
            switch (Convert.ToUInt16(PB1IOSlider.Value))
            {
                case 0:
                    setupConfig = MessageSetupOptions.MSGSETUP_DIGITALPORT;
                    break;
                case 1:
                    setupConfig = MessageSetupOptions.MSGSETUP_TRIGGERPORT;
                    break;
                case 2:
                    setupConfig = MessageSetupOptions.MSGSETUP_DIGITALHIGH;
                    break;
                case 3:
                    setupConfig = MessageSetupOptions.MSGSETUP_DIGITALLOW;
                    break;
            }
            payload[0] = (byte)setupConfig;
            payload[1] = (byte)(PortMasks.MASK_PORT_B1); // NOTE: B1 is actually used for resetting EEPROM, dont actually use this with a pull down resistor!!
            // We can have up to 4 configuration settings per transmission, as long as they are in SetConfig + Port order.
            // A direct message can have upto 7 configuration settings and faster update time, but does not route through the mesh.
            
            App._adaptiveNodeControl.SendMessage( 
                App.selectedDevice.NetworkId, 
                App.selectedDevice.DeviceId, 
                MessageType.MSGTYPE_SETUP, payload);
        }

        private MessageSetupOptions GetMessageSetupOption(double sliderValue)
        {
            MessageSetupOptions setupConfig = MessageSetupOptions.MSGSETUP_INVALID;
            switch (Convert.ToUInt16(sliderValue))
            {
                case 0:
                    setupConfig = MessageSetupOptions.MSGSETUP_DIGITALPORT;
                    break;
                case 1:
                    setupConfig = MessageSetupOptions.MSGSETUP_TRIGGERPORT;
                    break;
                case 2:
                    setupConfig = MessageSetupOptions.MSGSETUP_DIGITALHIGH; 
                    break;
                case 3:
                    setupConfig = MessageSetupOptions.MSGSETUP_DIGITALLOW;
                    break;
            }

            return setupConfig;
        }

        // We can have up to 4 configuration settings per transmission, as long as they are in SetConfig + Port order.
        // A direct message can have upto 7 configuration settings and faster update time, but does not route through the mesh.
        // WARNING: Be weary of connecting 3V equipment and using a slider control, you may accidentally toast something if the
        //          node changes the port quickly while you toggle settings. Recommend using dedicated software that does not
        //          update the port in real time.
        private void SendMessageSetup(MessageSetupOptions msgSetupOption, PortMasks portMask, bool useMsgTypeSetupNew = false)
        {
            byte[] payload = new byte[2];
            payload[0] = (byte)msgSetupOption;
            payload[1] = (byte)portMask;
            
            App._adaptiveNodeControl.SendMessage(
                App.selectedDevice.NetworkId,
                App.selectedDevice.DeviceId,
                useMsgTypeSetupNew ? MessageType.MSGTYPE_NEW_SETUP : MessageType.MSGTYPE_SETUP, payload);
        }

        bool sliderLock = false;
        private void PB1IOSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderLock) return;
            sliderLock = true;

            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            PB1IOSlider.Value = newVal;

           // SendMessageSetup(GetMessageSetupOption(newVal), PortMasks.MASK_PORT_B1);

            sliderLock = false;
        }

        private void PB2IOSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderLock) return;
            sliderLock = true;

            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            PB2IOSlider.Value = newVal;

           // SendMessageSetup(GetMessageSetupOption(newVal), PortMasks.MASK_PORT_B2);

            sliderLock = false;
        }

        private void PD2IOSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderLock) return;
            sliderLock = true;

            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            PD2IOSlider.Value = newVal;

            SendMessageSetup(GetMessageSetupOption(newVal), PortMasks.MASK_PORT_D2);

            sliderLock = false;
        }

        private void RedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderLock) return;
            sliderLock = true;

            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            RedSlider.Value = newVal;

            byte[] payload = new byte[4];
            payload[0] = 0x88; // Analog Port C3 
            payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
            payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
            payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
            App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);

            sliderLock = false;
        }

        private void GreenSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderLock) return;
            sliderLock = true;

            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            GreenSlider.Value = newVal;

            byte[] payload = new byte[4];
            payload[0] = 0x88; // Analog Port C3 
            payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
            payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
            payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
            App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);

            sliderLock = false;
        }

        private void BlueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderLock) return;
            sliderLock = true;

            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            BlueSlider.Value = newVal;

            byte[] payload = new byte[4];
            payload[0] = 0x88; // Analog Port C3 
            payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
            payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
            payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
            App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);

            sliderLock = false;
        }
    }
}

/*
#define MASK_DIGITALPORT			0x22
#define MASK_ANALOGPORT				0x88

// Remembers the last triggered port (when in interrupt mode)
#define MASK_PORT_B1			0b00000001
#define MASK_PORT_B2			0b00000010
#define MASK_PORT_D2			0b00000100
#define MASK_PORT_D3			0b00001000
#define MASK_PORT_D4			0b00010000
#define MASK_PORT_D5			0b00100000
#define MASK_PORT_D6			0b01000000

#define MASK_PORT_C0			0b00000001
#define MASK_PORT_C1			0b00000010
#define MASK_PORT_C2			0b00000100
#define MASK_PORT_C4			0b00010000
#define MASK_PORT_C5			0b00100000
// Port C3 is used for LED status - either green LED or ws2812
#define MASK_PORT_C3			0b00001000
// C6 and C7 are used for accessory reading
#define MASK_PORT_C6			0b01000000
#define MASK_PORT_C7			0b10000000
 */