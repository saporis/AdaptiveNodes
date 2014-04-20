using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace WP_Controller
{
    public static class DataParser
    {
        public static Message ParseByteArrayAsMessage(byte[] data, int loadedBytes)
        {
            Message parsedMessage = new Message();

            // try to parse the buffered message
            int parsedCounter = 0;
            while (parsedCounter < loadedBytes && data[parsedCounter] != '>')
            {
                ++parsedCounter;
                continue;
            }

            // Failed to find header, bailing
            if (parsedCounter >= loadedBytes - 1)
            {
                return null;
            }

            switch (data[++parsedCounter])
            {
                case (byte)'M':
                    // skip '>'
                    parsedCounter += 2;

                    // if we got a full message
                    if (loadedBytes > 20)
                    {
                        //>M>D:Pipe:DevID:NetID:RDevID:RNetID:Header:Data
                        parsedMessage.Direction = (char)data[parsedCounter++];
                        ++parsedCounter; // :
                        parsedMessage.Pipe = (ushort)data[parsedCounter++];
                        ++parsedCounter; // :
                        parsedMessage.NetworkId = data[parsedCounter++];
                        ++parsedCounter; // :
                        parsedMessage.DeviceId = data[parsedCounter++];
                        ++parsedCounter; // :
                        parsedMessage.RecipientNetworkId = data[parsedCounter++];
                        ++parsedCounter; // :
                        parsedMessage.RecipientDeviceId = data[parsedCounter++];
                        ++parsedCounter; // :
                        parsedMessage.MessageTTlAndId = BitConverter.ToUInt16(data, parsedCounter);
                        parsedCounter += 2; // Message Header
                        ++parsedCounter; // :
                        parsedMessage.MessageType = (MessageType)data[parsedCounter++];
                        ++parsedCounter; // :

                        int dataSize = loadedBytes - parsedCounter;
                        if (dataSize > 26)
                        {
                            parsedMessage.MessageData = new byte[26];
                        }
                        else if (dataSize > 9)
                        {
                            parsedMessage.MessageData = new byte[9];
                        }
                        else if (dataSize > 0)
                        {
                            parsedMessage.MessageData = new byte[dataSize];
                        }

                        for (int i = 0; i < parsedMessage.MessageData.Length - 1; ++i)
                        {
                            parsedMessage.MessageData[i] = data[parsedCounter++];
                        }
                    }

                    break;
                // Debug
                case (byte)'D':
                    // skip '>'
                    parsedCounter += 2;

                    if (parsedCounter < loadedBytes)
                    {
                        if (data[parsedCounter] != 0)
                        {
                            string parsedByte = ByteToMessage(data[parsedCounter]);
                            if (data[parsedCounter + 1] >= 40)
                            {
                                parsedByte = String.Format("{0} - 0x{1}", parsedByte, data[parsedCounter + 1]);
                            }
                            parsedMessage.Debug = parsedByte;
                        }
                    }

                    break;
                default:
                    break;
            }

            return parsedMessage;
        }

#if PC_VERSION
        public static Message TryParseSerialMessage(SerialPort activeSerialPort)
        {
            Message parsedMessage = null;
            try
            {
                byte[] buffer = new byte[256];
                int bufferIdx = 0;

                // loop until we hit a newline or max buffer size
                while (true && bufferIdx < 256)
                {
                    buffer[bufferIdx] = (byte)activeSerialPort.ReadByte();
                    if (buffer[bufferIdx] == '\n')
                    {
                        break;
                    }

                    ++bufferIdx;
                }

                parsedMessage = ParseByteArrayAsMessage(buffer, bufferIdx);
            }
            catch (Exception e)
            {

            }

            return parsedMessage;
        }
#endif

        public static string ByteToMessage(byte value)
        {
            switch (value)
            {
                case 0x01:
                    return "Initializing...";
                case 0x02:
                    return "Setting up NRF24L Radio...";
                case 0x03:
                    return "NRF24L Setup failure...";
                case 0x04:
                    return "Preparing to soft reboot...";
                case 0x05:
                    return "Error hit - going to soft reset...";
                case 0x06:
                    return "Network ID AutoGenerated";
                case 0x07:
                    return "Device ID AutoGenerated";
                case 0x08:
                    return "Zeroing out message buffer";
                case 0x09:
                    return "Zeroing out header cache";
                case 0x0A:
                    return "Setting up NRG24L Radio...";
                case 0x0B:
                    return "HC05 Setup Failed";
                case 0x0C:
                    return "No Network ID Setup - Going to discovery mode";
                case 0x0D:
                    return "No Device ID Setup - Going to discovery mode";
                case 0x0E:
                    return "Discovered Network ID";
                case 0x0F:
                    return "EEPROM Reset";
                case 0x10:
                    return "************************ Serial Disabled ***********************";
                case 0x11:
                    return "Serial data error";
                case 0x12:
                    return "Invalid message header detected";
                case 0x13:
                    return "Message found in cache";
                case 0x14:
                    return "Generating random seed";
                case 0x15:
                    return "Do work error";
                case 0x16:
                    return "Serial message forwarded";
                case 0x17:
                    return "Port Conflict with PWM B";
                case 0x18:
                    return "Port Conflict with PWM D";
                case 0x19:
                    return "Device going to sleep";
                case 0xF0:
                    return "All is ready - message pump starting";
            }

            return "unknown code: " + value;
        }

        public static string ByteToMessageType(byte value)
        {
            switch (value)
            {
                case 0x01:
                    return "MSGTYPE_ACK";
                case 0x03:
                    return "MSGTYPE_HEARTBEAT";
                case 0x04:
                    return "MSGTYPE_BOOT";
                case 0x08:
                    return "MSGTYPE_UPDATE";
                case 0x10:
                    return "MSGTYPE_SETUP";
                case 0x20:
                    return "MSGTYPE_TIME";
                case 0x0F:
                    return "MSGTYPE_DEVICE_ARP";
                case 0x33:
                    return "MSGTYPE_NETWORK_ARP";

            }

            return "unknown message type: " + value;
        }
    }
}
