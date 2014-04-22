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
                                    finalStatus += port.ToString() + " - " + (parsedDigitalPortValues[port]?"HIGH":"LOW") + "\t";
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

        private void RedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
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
                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
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
                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
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

                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
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
            else if (sender.Equals(AnalogDirectionSlider))
            {
                currentSlider = AnalogDirectionSlider;
            } 
            else if (sender.Equals(AnalogLevelSlider))
            {
                currentSlider = AnalogLevelSlider;
            }
            
            double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
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

            SendMessageSetup(msgSetupOption, portMask);
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

                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
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

            SendMessageSetup(msgSetupOption, portMask);
        }

        private void PWMSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
                    if (PWMLevelSlider == null) return;
                    PWMLevelSlider.Value = newVal;
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }

        private void PWMClockSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (System.Threading.Monitor.TryEnter(mutex))
            {
                try
                {
                    double newVal = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
                    if (PWMClockSlider == null) return;
                    PWMClockSlider.Value = newVal;
                }
                finally
                {
                    System.Threading.Monitor.Exit(mutex);
                }
            }
        }
    }
}
