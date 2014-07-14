using System;
using System.Collections.Generic;
using System.Collections;
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
        private static int simpleCountdown = 0;
        DispatcherTimer updateTimer = null;

        private static Object mutex = new Object();

        public DevicePage()
        {
            InitializeComponent();

            updateTimer = new DispatcherTimer();
            updateTimer.Interval += TimeSpan.FromMilliseconds(500);
            updateTimer.Tick += OnTimerTick;
            updateTimer.Start();

            DeviceName.Title = App.selectedDevice.NetworkId + ":" + App.selectedDevice.DeviceId;
            BroadcastAddress.Text = App.selectedDevice.NetworkId + ":" + App.selectedDevice.DeviceId;
            UniqueAddress.Text = App.selectedDevice.UniqueAddress;

            // Default UI is ports on input/low
            LevelSlider.Visibility = Visibility.Collapsed;
            LevelLabel.Visibility = Visibility.Collapsed;
            LowLabel.Visibility = Visibility.Collapsed;
            HighLabel.Visibility = Visibility.Collapsed;
            AnalogHighLabel.Visibility = Visibility.Collapsed;

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
                    PortStatus.Text = "-updates cleared-";

                    payload[2] = (byte)0x15;
                    // Very noisy update, but we want to really be sure it's updated fast....
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

        Dictionary<PortMasks, bool> parsedDigitalPortValues = null;
        Dictionary<PortMasks, uint> parsedAnalogPortValues = null;
        void _adaptiveNodeControl_MessageReceived(Message message)
        {
            if (message == null)
            {
                return;
            }

            if (message.Error != null && message.Error.Length != 0)
            {
                // ignore errors for now
                return;
            }

            if (message.Debug != null && message.Debug.Length != 0)
            {
                // ignore debug messages for now
                return;
            }

            if (message.Error == null &&
                message.Debug == null)
            {
                string data = "";
                if (message.MessageData != null)
                {
                    foreach (byte val in message.MessageData)
                    {
                        data += String.Format("{0:X2}", val);
                    }
                }

                switch (message.MessageType)
                {
                    case MessageType.MSGTYPE_UPDATE:
                        // "D�\0\0\0\0\0\0"
                        int portIdentifier = message.MessageData[0];
                        byte portDataToRead = message.MessageData[1];
                        parsedDigitalPortValues = new Dictionary<PortMasks, bool>();
                        parsedAnalogPortValues = new Dictionary<PortMasks, uint>();
                        // Handle analog values
                        if ((portIdentifier & (byte)PortMasks.MASK_PORT_ADC7) > 0)
                        {
                            foreach (PortMasks port in new PortMasks[] { PortMasks.MASK_PORT_C0, PortMasks.MASK_PORT_C1, PortMasks.MASK_PORT_C2, PortMasks.MASK_PORT_C3, PortMasks.MASK_PORT_C4, PortMasks.MASK_PORT_C5, PortMasks.MASK_PORT_ADC6, PortMasks.MASK_PORT_ADC7 })
                            {
                                if (UpdateAnalogPortData(portIdentifier, portDataToRead, port))
                                {
                                    portDataToRead >>= 10; // shift the remaining data for the next port read
                                }
                            }
                            if (parsedAnalogPortValues.Count > 0)
                            {
                                string finalStatus = "";

                                foreach (PortMasks port in parsedAnalogPortValues.Keys)
                                {
                                    finalStatus += port.ToString() + " - " + parsedAnalogPortValues[port] + "\t";
                                }

                                Dispatcher.BeginInvoke(delegate()
                                {
                                    AnalogPortStatus.Text = finalStatus;
                                });
                            }
                        }
                        // Handle the digital values
                        else if (portIdentifier > 0)
                        {
                            foreach (PortMasks port in new PortMasks[] { PortMasks.MASK_PORT_B1, PortMasks.MASK_PORT_B2, PortMasks.MASK_PORT_D2, PortMasks.MASK_PORT_D3, PortMasks.MASK_PORT_D4, PortMasks.MASK_PORT_D5, PortMasks.MASK_PORT_D6 })
                            {
                                if (UpdateDigitalPortData(portIdentifier, portDataToRead, port))
                                {
                                    portDataToRead >>= 1; // shift the remaining data for the next port read
                                }
                            }

                            if (parsedDigitalPortValues.Count > 0)
                            {
                                string finalStatus = "";

                                foreach (PortMasks port in parsedDigitalPortValues.Keys)
                                {
                                    finalStatus += port.ToString() + " - " + (parsedDigitalPortValues[port] ? "HIGH" : "LOW") + "\t";
                                }

                                Dispatcher.BeginInvoke(delegate()
                                {
                                    PortStatus.Text = finalStatus;
                                });
                            }
                        }

                        break;
                    case MessageType.MSGTYPE_TRIGGER:
                        // Sample format - port(B,C,D):MaskValue, eg: "D�\0\0\0\0\0\0"

                        for (int i = 0; i < 8; i += 2)
                        {
                            char port = (char)message.MessageData[i];
                            if (port == 0)
                            {
                                break;
                            }
                            PortMasks ports = (PortMasks)message.MessageData[i + 1];
                            // These double checks are used as error correcting checks incase we get bad/odd data back
                            if (port == 'B' && ports == (PortMasks.MASK_PORT_B1 | PortMasks.MASK_PORT_B2))
                            {
                                App.portsTriggered |= ports;

                                simpleCountdown = 15;
                                Dispatcher.BeginInvoke(delegate()
                                {
                                    PortStatus.Text = "PortB has been triggered.";
                                });
                            }
                            if (port == 'D' && ports == (PortMasks.MASK_PORT_D2 | PortMasks.MASK_PORT_D3 | PortMasks.MASK_PORT_D4 | PortMasks.MASK_PORT_D5 | PortMasks.MASK_PORT_D6))
                            {
                                App.portsTriggered |= ports;
                                simpleCountdown = 15;
                                Dispatcher.BeginInvoke(delegate()
                                {
                                    PortStatus.Text = "PortD has been triggered.";
                                });
                            }
                        }

                        break;
                    case MessageType.MSGTYPE_DESC_REPLY:
                        int messagePart = (int)message.MessageData[0];
                        string messageData = System.Text.Encoding.UTF8.GetString(message.MessageData, 1, 8);

                        Dispatcher.BeginInvoke(delegate()
                        {
                            // TODO: Have this code update the devices current name on request
                            MessageBox.Show(messagePart + " - \"" + messageData + "\"");
                        });
                        break;
                    default:
                        break;
                }
            }
        }

        private bool UpdateAnalogPortData(int portIdentifier, byte portDataToRead, PortMasks currentPort)
        {
            if ((portIdentifier & (byte)currentPort) > 0)
            {
                uint portValue = (uint)(portDataToRead & 0x3FF);

                parsedAnalogPortValues.Add(currentPort, portValue);
                return true;
            }

            return false;
        }

        private bool UpdateDigitalPortData(int portIdentifier, byte portDataToRead, PortMasks currentPort)
        {
            if ((portIdentifier & (byte)currentPort) > 0)
            {
                int portValue = (portDataToRead & 0x1);
                
                parsedDigitalPortValues.Add(currentPort, portValue > 0);
                return true;
            }

            return false;
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
        private void SendPortSetupMessage(MessageSetupOptions msgSetupOption, PortMasks portMask, bool useMsgTypeSetupNew = false)
        {
            byte[] payload = new byte[2];
            payload[0] = (byte)msgSetupOption;
            payload[1] = (byte)portMask;
            
            App._adaptiveNodeControl.SendMessage(
                App.selectedDevice.NetworkId,
                App.selectedDevice.DeviceId,
                useMsgTypeSetupNew ? MessageType.MSGTYPE_NEW_SETUP : MessageType.MSGTYPE_SETUP, payload);
        }

        private void RedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
                    if (newVal == null) return;
                    RedSlider.Value = newVal;

                    byte[] payload = new byte[4];
                    payload[0] = 0x88; // Analog Port C3 
                    payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
                    payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
                    payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
                    App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);

                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void GreenSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
                    if (newVal == null) return;
                    GreenSlider.Value = newVal;

                    byte[] payload = new byte[4];
                    payload[0] = 0x88; // Analog Port C3 
                    payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
                    payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
                    payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
                    App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void BlueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
                    if (newVal == null) return;
                    BlueSlider.Value = newVal;

                    byte[] payload = new byte[4];
                    payload[0] = 0x88; // Analog Port C3 
                    payload[1] = (byte)Convert.ToUInt16(RedSlider.Value);   // r
                    payload[2] = (byte)Convert.ToUInt16(GreenSlider.Value); // g
                    payload[3] = (byte)Convert.ToUInt16(BlueSlider.Value);  // b
                    App._adaptiveNodeControl.SendMessage(App.selectedDevice.NetworkId, App.selectedDevice.DeviceId, MessageType.MSGTYPE_LEDUPDATE, payload);
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void HighLowSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    var currentSlider = DirectionSlider;

                    if (sender.Equals(LevelSlider))
                    {
                        currentSlider = LevelSlider;
                    }
                    else if (sender.Equals(TriggerSlider))
                    {
                        currentSlider = TriggerSlider;
                    }
                    else if (sender.Equals(DirectionSlider))
                    {
                        currentSlider = DirectionSlider;
                    }
                    else if (sender.Equals(AnalogLevelSlider))
                    {
                        currentSlider = AnalogLevelSlider;
                    }
                    else if (sender.Equals(AnalogTriggerSlider))
                    {
                        currentSlider = AnalogTriggerSlider;
                    }
                    else if (sender.Equals(AnalogDirectionSlider))
                    {
                        currentSlider = AnalogDirectionSlider;
                    }
                    else
                    {
                        return;
                    }

                    double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
                    if (newVal == null) return;
                    currentSlider.Value = newVal;

                    UpdateDisplayedSlider();
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void UpdateDisplayedSlider()
        {
            if (DirectionSlider.Value == 0)
            {
                LevelSlider.Visibility = Visibility.Collapsed;
                LevelLabel.Visibility = Visibility.Collapsed;
                LowLabel.Visibility = Visibility.Collapsed;
                HighLabel.Visibility = Visibility.Collapsed;
                TriggerSlider.Visibility = Visibility.Visible;
                TriggerLabel.Visibility = Visibility.Visible;
                DisabledLabel.Visibility = Visibility.Visible;
                EnabledLabel.Visibility = Visibility.Visible;
            } 
            
            if (AnalogDirectionSlider.Value == 0)
            {
                AnalogLevelSlider.Visibility = Visibility.Collapsed;
                AnalogLevelLabel.Visibility = Visibility.Collapsed;
                AnalogLowLabel.Visibility = Visibility.Collapsed;
                AnalogHighLabel.Visibility = Visibility.Collapsed;
                AnalogTriggerLabel.Visibility = Visibility.Visible;
                AnalogTriggerSlider.Visibility = Visibility.Visible;
                AnalogDisabledLabel.Visibility = Visibility.Visible;
                AnalogEnabledLabel.Visibility = Visibility.Visible;
            }
            
            if (AnalogDirectionSlider.Value == 1)
            {
                AnalogLevelSlider.Visibility = Visibility.Visible;
                AnalogLevelLabel.Visibility = Visibility.Visible;
                AnalogLowLabel.Visibility = Visibility.Visible;
                AnalogHighLabel.Visibility = Visibility.Visible;
                AnalogTriggerSlider.Visibility = Visibility.Collapsed;
                AnalogTriggerLabel.Visibility = Visibility.Collapsed;
                AnalogDisabledLabel.Visibility = Visibility.Collapsed;
                AnalogEnabledLabel.Visibility = Visibility.Collapsed;
            }
            
            if (DirectionSlider.Value == 1)
            {
                LevelSlider.Visibility = Visibility.Visible;
                LevelLabel.Visibility = Visibility.Visible;
                LowLabel.Visibility = Visibility.Visible;
                HighLabel.Visibility = Visibility.Visible;
                TriggerSlider.Visibility = Visibility.Collapsed;
                TriggerLabel.Visibility = Visibility.Collapsed;
                DisabledLabel.Visibility = Visibility.Collapsed;
                EnabledLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void PortSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var currentSlider = PortSelectionSlider;

            if (sender.Equals(PortSelectionSlider))
            {
                currentSlider = PortSelectionSlider;
            }
            else if (sender.Equals(AnalogPortSelectionSlider))
            {
                currentSlider = AnalogPortSelectionSlider;
            }
            else if (sender.Equals(PWMPortSelectionSlider))
            {
                currentSlider = PWMPortSelectionSlider;
            }
            else if (sender.Equals(ServoPortSelectionSlider))
            {
                currentSlider = ServoPortSelectionSlider;
            }
            else if (sender.Equals(AnalogDirectionSlider))
            {
                currentSlider = AnalogDirectionSlider;
            }
            else if (sender.Equals(AnalogLevelSlider))
            {
                currentSlider = AnalogLevelSlider;
            }

            double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
            if (currentSlider == null) return;
            currentSlider.Value = newVal;

            // Reset UI to become input low
            if (currentSlider != PWMPortSelectionSlider)
            {
                DirectionSlider.Value = 0;
                UpdateDisplayedSlider();
            }
        }

        private void DigitalButton_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = PortMasks.NONE;
            MessageSetupOptions msgSetupOption = MessageSetupOptions.MSGSETUP_INVALID;

            switch ((int)PortSelectionSlider.Value)
            {
                case 0:
                    portMask |= PortMasks.MASK_PORT_B1;
                    break;
                case 1:
                    portMask |= PortMasks.MASK_PORT_B2;
                    break;
                case 2:
                    portMask |= PortMasks.MASK_PORT_D2;
                    break;
                case 3:
                    portMask |= PortMasks.MASK_PORT_D3;
                    break;
                case 4:
                    portMask |= PortMasks.MASK_PORT_D4;
                    break;
                case 5:
                    portMask |= PortMasks.MASK_PORT_D5;
                    break;
                case 6:
                    portMask |= PortMasks.MASK_PORT_D6;
                    break;
            }

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port detected, please select a valid port.");
                return;
            }

            // Input
            if (DirectionSlider.Value == 0)
            {
                if (TriggerSlider.Value == 0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_DIGITALPORT;
                }
                else if (TriggerSlider.Value == 1.0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_TRIGGERPORT;
                }
            } // Output
            else if (DirectionSlider.Value == 1.0)
            {
                if (LevelSlider.Value == 0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_DIGITALLOW;
                }
                else if (LevelSlider.Value == 1.0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_DIGITALHIGH;
                }
            }

            if (msgSetupOption == MessageSetupOptions.MSGSETUP_INVALID)
            {
                MessageBox.Show("No operation detected. Please select direction and command.");
                return;
            }

            SendPortSetupMessage(msgSetupOption, portMask);
        }

        private void TextBlock_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {

        }

        private void NewDevice_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            MessageBox.Show("When set to 'new device', only devices with no previous configuration accept the message. Useful for updating a duplicated device ID on first boot.");
        }

        private void NewDevice_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    var currentSlider = NewDeviceSetup;

                    double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
                    if (newVal == null) return;
                    currentSlider.Value = newVal;
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AnalogButton_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = PortMasks.NONE;
            MessageSetupOptions msgSetupOption = MessageSetupOptions.MSGSETUP_INVALID;

            switch ((int)AnalogPortSelectionSlider.Value)
            {
                case 0:
                    portMask |= PortMasks.MASK_PORT_C0;
                    break;
                case 1:
                    portMask |= PortMasks.MASK_PORT_C1;
                    break;
                case 2:
                    portMask |= PortMasks.MASK_PORT_C2;
                    break;
                case 3:
                    portMask |= PortMasks.MASK_PORT_C4;
                    break;
                case 4:
                    portMask |= PortMasks.MASK_PORT_C5;
                    break;
                case 5:
                    portMask |= PortMasks.MASK_PORT_ADC6;
                    break;
                case 6:
                    portMask |= PortMasks.MASK_PORT_ADC7;
                    break;
            }

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port detected, please select a valid port.");
                return;
            }

            // Input
            if (AnalogDirectionSlider.Value == 0)
            {
                if (AnalogTriggerSlider.Value == 0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_ANALOGPORT;
                }
                else if (AnalogTriggerSlider.Value > 0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_TRIGGERPORT;
                }
            } // Output
            else if (AnalogDirectionSlider.Value > 0)
            {
                if (AnalogLevelSlider.Value == 0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_ANALOGLOW;
                }
                else if (AnalogLevelSlider.Value > 0)
                {
                    msgSetupOption = MessageSetupOptions.MSGSETUP_ANALOGHIGH;
                }
            }

            if (msgSetupOption == MessageSetupOptions.MSGSETUP_INVALID)
            {
                MessageBox.Show("No operation detected. Please select direction and command.");
                return;
            }

            SendPortSetupMessage(msgSetupOption, portMask);
        }

        uint dutyCyclePercentage = 1;
        private void PWMSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.ToEven);
                    if (PWMLevelSlider == null) return;
                    PWMLevelSlider.Value = newVal;
                    dutyCyclePercentage = (uint)newVal;
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void ProgramButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SendSetupMessage(MessageSetupOptions msgSetupOption, PortMasks portMask, bool useMsgTypeSetupNew = false)
        {
            byte[] payload = new byte[2];
            payload[0] = (byte)msgSetupOption;
            payload[1] = (byte)portMask;
        }

        private void UpdateDescription_Click(object sender, RoutedEventArgs e)
        {
            string descriptionText = Description.Text;

            if (descriptionText.Contains("(not yet retrieved)"))
            {
                MessageBox.Show("Not a valid description. Update ignored.");
                return;
            }

            const int partSize = 8;

            int partsNeeded = (descriptionText.Length / partSize);
            if ((descriptionText.Length % partSize) > 0 && descriptionText.Length > partSize)
            {
                ++partsNeeded;
            }
            string[] dividedDescription = new string[partsNeeded];

            for (int i = 0; i < partsNeeded; ++i)
            {
                int subLength = partSize;
                if (((i * partSize) + subLength) >= descriptionText.Length)
                {
                    subLength = descriptionText.Length - (i * partSize);
                }

                dividedDescription[i] += descriptionText.Substring(i * partSize, subLength);
            }

            bool useMsgTypeSetupNew = (NewDeviceSetup.Value > 0);


            for (int i = 0; i < dividedDescription.Length; ++i)
            {
                byte[] dataBuffer = System.Text.Encoding.UTF8.GetBytes(dividedDescription[i]);
                byte[] dataToSend = new byte[dataBuffer.Length + 1];
                dataToSend[0] = (byte)i;
                System.Buffer.BlockCopy(dataBuffer, 0, dataToSend, 1, dataBuffer.Length);

                App._adaptiveNodeControl.SendMessage(
                    App.selectedDevice.NetworkId,
                    App.selectedDevice.DeviceId,
                    MessageType.MSGTYPE_SET_DESC,
                    dataToSend);

                System.Threading.Thread.Sleep(100);
            }
        }

        private void ForceDescriptionDownload_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 2; ++i)
            {
                App._adaptiveNodeControl.SendMessage(
                    App.selectedDevice.NetworkId,
                    App.selectedDevice.DeviceId,
                    MessageType.MSGTYPE_GET_DESC,
                    new byte[] { (byte)i } );

                System.Threading.Thread.Sleep(200);
            }
        }

        public enum PortType
        {
            PortB,
            PortC,
            PortD,
            PWMPorts,
            ServoPorts,
        }

        private PortMasks GetCurrentlySelectedPort(PortType portType)
        {
            PortMasks portMask = PortMasks.NONE;

            switch (portType)
            {
                case PortType.PortC:
                    switch ((int)AnalogPortSelectionSlider.Value)
                    {
                        case 0:
                            portMask |= PortMasks.MASK_PORT_C0;
                            break;
                        case 1:
                            portMask |= PortMasks.MASK_PORT_C1;
                            break;
                        case 2:
                            portMask |= PortMasks.MASK_PORT_C2;
                            break;
                        case 3:
                            portMask |= PortMasks.MASK_PORT_C4;
                            break;
                        case 4:
                            portMask |= PortMasks.MASK_PORT_C5;
                            break;
                        case 5:
                            portMask |= PortMasks.MASK_PORT_ADC6;
                            break;
                        case 6:
                            portMask |= PortMasks.MASK_PORT_ADC7;
                            break;
                    }
                    break;
                case PortType.PortB:
                case PortType.PortD:
                    switch ((int)PortSelectionSlider.Value)
                    {
                        case 0:
                            portMask |= PortMasks.MASK_PORT_B1;
                            break;
                        case 1:
                            portMask |= PortMasks.MASK_PORT_B2;
                            break;
                        case 2:
                            portMask |= PortMasks.MASK_PORT_D2;
                            break;
                        case 3:
                            portMask |= PortMasks.MASK_PORT_D3;
                            break;
                        case 4:
                            portMask |= PortMasks.MASK_PORT_D4;
                            break;
                        case 5:
                            portMask |= PortMasks.MASK_PORT_D5;
                            break;
                        case 6:
                            portMask |= PortMasks.MASK_PORT_D6;
                            break;
                    }
                    break;
                case PortType.ServoPorts:
                    switch ((int)ServoPortSelectionSlider.Value)
                    {
                        case 0:
                            portMask |= PortMasks.MASK_PORT_B1;
                            break;
                        case 1:
                            portMask |= PortMasks.MASK_PORT_B2;
                            break;
                        case 2:
                            portMask |= PortMasks.MASK_PORT_D5;
                            break;
                        case 3:
                            portMask |= PortMasks.MASK_PORT_D6;
                            break;
                    }
                    break;
                case PortType.PWMPorts:
                    switch ((int)PWMPortSelectionSlider.Value)
                    {
                        case 0:
                            portMask |= PortMasks.MASK_PORT_B1;
                            break;
                        case 1:
                            portMask |= PortMasks.MASK_PORT_B2;
                            break;
                        case 2:
                            portMask |= PortMasks.MASK_PORT_D5;
                            break;
                        case 3:
                            portMask |= PortMasks.MASK_PORT_D6;
                            break;
                    }
                    break;
            }

            return portMask;
        }

        private void ResetPortButton_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = GetCurrentlySelectedPort(PortType.PortB | PortType.PortD);

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port was selected, please check that a port was selected.");
                return;
            }

            SendPortSetupMessage(MessageSetupOptions.MSGSETUP_RESETPORT, portMask);
        }

        private void AnalogResetButton_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = GetCurrentlySelectedPort(PortType.PortC);

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port was selected, please check that a port was selected.");
                return;
            }

            SendPortSetupMessage(MessageSetupOptions.MSGSETUP_RESETPORT, portMask);
        }

        private void PWMResetPort_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = GetCurrentlySelectedPort(PortType.PWMPorts);

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port was selected, please check that a port was selected.");
                return;
            }

            SendPortSetupMessage(MessageSetupOptions.MSGSETUP_RESETPORT, portMask);
        }

        private void PWMEnableButton_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = GetCurrentlySelectedPort(PortType.PWMPorts);

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port was selected, please check that a port was selected.");
                return;
            }
            
            MessageSetupOptions msgSetupOption = MessageSetupOptions.MSGSETUP_PWM_ENABLE;

            int dutyCycle = (int)dutyCyclePercentage;
            if (dutyCycle < 0 || dutyCycle > 100)
            {
                MessageBox.Show("Invalid duty cycle, should be between 0 and 100%.");
            }

            if (msgSetupOption == MessageSetupOptions.MSGSETUP_INVALID)
            {
                MessageBox.Show("No operation detected. Please select direction and command.");
                return;
            }

            if (bConfigured == false)
            {
                UpdateFastPWMCalculation();
                bConfigured = true;
                System.Threading.Thread.Sleep(150);
            }

            SendPWMPortSetupMessage(msgSetupOption, portMask, dutyCycle);
        }

        private void SendPWMPortSetupMessage(MessageSetupOptions msgSetupOption, PortMasks portMask, double percentage, bool useMsgTypeSetupNew = false)
        {
            UInt16 topValue = (UInt16)bTopValue;

            switch (portMask)
            {
                case PortMasks.MASK_PORT_B1:
                case PortMasks.MASK_PORT_B2:
                    if (topValue <= 0 || topValue > 0xFFFF)
                    {
                        topValue = 0xFFFF;
                    }
                    break;
                case PortMasks.MASK_PORT_D5:
                case PortMasks.MASK_PORT_D6:
                default:
                    if (topValue <= 0 || topValue > 0xFF)
                    {
                        topValue = 0xFF;
                    }
                    break;
            }

            UInt16 dutyCycle = (UInt16)((double)topValue * (percentage * 0.01));
            
            byte[] payload = new byte[4];
            payload[0] = (byte)msgSetupOption;
            payload[1] = (byte)portMask;
            byte[] data = BitConverter.GetBytes(dutyCycle);

            if (data.Length != 2)
            {
                MessageBox.Show("Duty Cycle conversion failed, data was larger than 2 bytes.");
                return;
            }

            if (dutyCycle <= 0xFF)
            {
                payload[2] = 0;
                payload[3] = data[0];
            }
            else
            {
                payload[2] = data[0];
                payload[3] = data[1];
            }

            App._adaptiveNodeControl.SendMessage(
                App.selectedDevice.NetworkId,
                App.selectedDevice.DeviceId,
                useMsgTypeSetupNew ? MessageType.MSGTYPE_NEW_SETUP : MessageType.MSGTYPE_SETUP, payload);
        }

        private void UpdateFastPWMCalculation()
        {
            PortMasks portMask = GetCurrentlySelectedPort(PortType.PWMPorts);

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port was selected, please check that a port was selected.");
                return;
            }

            // TODO: select right top value if using D5/D6
            UInt16 result = (UInt16)bTopValue;
            int clockDivision = 1; // (/1, default)
            ClockDivisor clkDiv = bclockDivisor;
            switch (clkDiv)
            {
                default:
                case ClockDivisor.PWM_CLOCK_DIVISOR_1:
                    clockDivision = 1;
                    break;
                case ClockDivisor.PWM_CLOCK_DIVISOR_8:
                    clockDivision = 2;
                    break;
                case ClockDivisor.PWM_CLOCK_DIVISOR_64:
                    clockDivision = 3;
                    break;
                case ClockDivisor.PWM_CLOCK_DIVISOR_128:
                    clockDivision = 4;
                    break;
                case ClockDivisor.PWM_CLOCK_DIVISOR_1024:
                    clockDivision = 5;
                    break;
            }

            switch (portMask)
            {
                case PortMasks.MASK_PORT_B1:
                case PortMasks.MASK_PORT_B2:
                    if (result <= 0 || result > 0xFFFF)
                    {
                        MessageBox.Show("Invalid top value selected, B1/B2 max is 65535.");
                        return;
                    }
                    break;
                case PortMasks.MASK_PORT_D5:
                case PortMasks.MASK_PORT_D6:
                    if (result <= 0 || result > 0xFF)
                    {
                        MessageBox.Show("Invalid top value selected, D5/D6 max is 255.");
                        return;
                    }
                    break;
                default:
                    MessageBox.Show("Invalid port configuration");
                    return;
            }

            byte[] payload = new byte[6];
            payload[0] = (byte)MessageSetupOptions.MSGSETUP_PWM_CLOCK;
            payload[1] = (byte)portMask;
            payload[2] = (byte)clockDivision;
            byte[] data = BitConverter.GetBytes(result);

            if (data.Length != 2)
            {
                MessageBox.Show("Duty Cycle conversion failed, data was larger than 2 bytes.");
                return;
            }

            if (result < 0xFF)
            {
                payload[3] = 0;
                payload[4] = data[0];
            }
            else
            {
                payload[3] = data[0];
                payload[4] = data[1];
            }

            // TODO: Add an option (pauload[5]) for Fast PWM vs Phase correct PWM
            payload[5] = 0;

            App._adaptiveNodeControl.SendMessage(
                App.selectedDevice.NetworkId,
                App.selectedDevice.DeviceId,
                MessageType.MSGTYPE_SETUP, 
                payload, 
                2);
        }

        public enum ClockDivisor : int
        {
            PWM_CLOCK_DIVISOR_1 = 1,
            PWM_CLOCK_DIVISOR_8 = 8,
            PWM_CLOCK_DIVISOR_64 = 64,
            PWM_CLOCK_DIVISOR_128 = 128,
            PWM_CLOCK_DIVISOR_1024 = 1024
        }

        /// <summary>
        /// Calculates the top value and divisor required based on the given frequency (for fast PWM modes)
        /// </summary>
        /// <param name="frequency">1Hz - 8,000,000Hz</param>
        /// <param name="maxSize">For a 2 byte register, we use 0xFFFF/65535</param>
        /// <param name="topValue">The calculated top value</param>
        /// <param name="clockDivisor">The corresponding clock divisor</param>
        /// <returns>returns true if a possible combination exists</returns>
        public bool CalculateTopAndClockDivisorForFastPWM(int frequency, int maxSize, out uint topValue, out ClockDivisor clockDivisor)
        {
            // Default values
            topValue = 0xFFFF;
            clockDivisor = ClockDivisor.PWM_CLOCK_DIVISOR_1;

            if (frequency < 1 || frequency > 2000000)
            {
                return false;
            }

            int[] validDivisors = { 
                                        (int)ClockDivisor.PWM_CLOCK_DIVISOR_1, 
                                        (int)ClockDivisor.PWM_CLOCK_DIVISOR_8,
                                        (int)ClockDivisor.PWM_CLOCK_DIVISOR_64,
                                        (int)ClockDivisor.PWM_CLOCK_DIVISOR_128,
                                        (int)ClockDivisor.PWM_CLOCK_DIVISOR_1024
                                    };

            for(int i = 0; i < 5; i++)
            {
               int calculated = (16000000 / (frequency * validDivisors[i]) - 1);

               if (calculated <= 0)
               {
                   return false;
               }

               if(calculated <= maxSize)
               {
                   clockDivisor = (ClockDivisor)validDivisors[i];
                   topValue = (uint)calculated;
                   return true;
               }
             }
  
            return false;
        }

        bool bConfigured = false;
        const int bMaxSize = 0xFFFF;
        ClockDivisor bclockDivisor = ClockDivisor.PWM_CLOCK_DIVISOR_1;
        uint bTopValue;
        //private void PWMFrequency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    if (System.Threading.Monitor.TryEnter(mutex))
        //    {
        //        try
        //        {
        //            if (PWMFrequencySlider == null) return;
        //            double newVal = Math.Round(PWMFrequencySlider.Value, MidpointRounding.ToEven);
        //            PWMFrequencySlider.Value = newVal;

        //            // TODO: Calculate this based on the currently selected port
        //            int maxSize = bMaxSize;
        //            if (!CalculateTopAndClockDivisorForFastPWM((int)PWMFrequencySlider.Value, maxSize, out bTopValue, out bclockDivisor))
        //            {
        //                FrequencyText.Text = "Invalid frequency, please select another.";
        //                return;
        //            }

        //            bConfigured = false;
        //        }
        //        finally
        //        {
        //            System.Threading.Monitor.Exit(mutex);
        //        }
        //    }

        //    FrequencyText.Text = PWMFrequencySlider.Value + " Hz";
        //}

        private void PWMMinFrequency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    if (bPWMFrequencySliderMin == null) return;
                    double newVal = Math.Round(bPWMFrequencySliderMin.Value, MidpointRounding.ToEven);
                    bPWMFrequencySliderMin.Value = newVal;

                    // TODO: Calculate this based on the currently selected port
                    int maxSize = bMaxSize;
                    if (!CalculateTopAndClockDivisorForFastPWM((int)bPWMFrequencySliderMin.Value, maxSize, out bTopValue, out bclockDivisor))
                    {
                        FrequencyText.Text = "Invalid frequency, please select another.";
                        return;
                    }

                    bConfigured = false;
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }

            FrequencyText.Text = bPWMFrequencySliderMin.Value + " Hz";
        }

        bool pulseWidthDivisorSetup = false;
        double servoDutyCycle = 5.0;
        private void CalculatePulseWidth(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PulseWidthSlider == null)
            {
                return;
            }

            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    // TODO: Calculate this based on the currently selected port
                    int maxSize = bMaxSize;
                    if (CalculateTopAndClockDivisorForFastPWM(50, maxSize, out bTopValue, out bclockDivisor))
                    {
                        pulseWidthDivisorSetup = false;
                    }

                    servoDutyCycle = 5.0 * PulseWidthSlider.Value;
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }

            PulseWidthText.Text = PulseWidthSlider.Value + " ms";
        }

        private void ServoEnableButton_Click(object sender, RoutedEventArgs e)
        {
            PortMasks portMask = GetCurrentlySelectedPort(PortType.ServoPorts);

            if (portMask == PortMasks.NONE)
            {
                MessageBox.Show("No port was selected, please check that a port was selected.");
                return;
            }

            MessageSetupOptions msgSetupOption = MessageSetupOptions.MSGSETUP_PWM_ENABLE;

            if (msgSetupOption == MessageSetupOptions.MSGSETUP_INVALID)
            {
                MessageBox.Show("No operation detected. Please select direction and command.");
                return;
            }

            if (pulseWidthDivisorSetup == false)
            {
                UpdateFastPWMCalculation();
                pulseWidthDivisorSetup = true;
                System.Threading.Thread.Sleep(150);
            }

            SendPWMPortSetupMessage(msgSetupOption, portMask, servoDutyCycle);
        }

    }
}
