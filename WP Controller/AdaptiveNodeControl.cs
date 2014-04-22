using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace WP_Controller
{
    public class AdaptiveNodeControl
    {
        private StreamSocket _socket;
        private DataWriter _dataWriter;
        private DataReader _dataReader;
        private BackgroundWorker dataReadWorker;

        public delegate void MessageReceivedHandler(Message message);
        public event MessageReceivedHandler MessageReceived;        

        public delegate void DebugDataReceivedHandler(byte[] debugData);
        public event DebugDataReceivedHandler DebugMessageReceived;        


        public StreamSocket Socket
        {
            get { return _socket; }
            set
            {
                if (value != null)
                {
                    _socket = value;
                    _dataWriter = new DataWriter(_socket.OutputStream);
                    _dataReader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

                    //_dataWriter.ByteOrder = ByteOrder.LittleEndian;

                    dataReadWorker.RunWorkerAsync();
                }
                else
                {
                    dataReadWorker.CancelAsync();
                }
            }
        }

        public AdaptiveNodeControl()
        {
            dataReadWorker = new BackgroundWorker();
            dataReadWorker.WorkerSupportsCancellation = true;
            dataReadWorker.DoWork += new DoWorkEventHandler(ReceiveMessages);
        }

        private async void ReceiveMessages(object sender, DoWorkEventArgs e)
        {
            try
            {
                byte[] buffer;
                int bufferIdx = 0;
                while (true)
                {
                    buffer = new byte[256];
                    bufferIdx = 0;

                    // loop until we hit a newline
                    while (true)
                    {
                        uint numStrBytes = await _dataReader.LoadAsync(1);
                        if (numStrBytes != 0)
                        {
                            byte receivedByte = _dataReader.ReadByte();
                            if (receivedByte == '\n')
                            {
                                //string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bufferIdx);
                                Message message = DataParser.ParseByteArrayAsMessage(buffer, bufferIdx);

                                //int validMessageHeader = message.IndexOf(">M>");
                                //if (validMessageHeader >= 0)
                                //{
                                //    if ((bufferIdx - validMessageHeader) > 12)
                                //    {
                                //        string parsedMessage = message.Substring(validMessageHeader, bufferIdx - validMessageHeader - 1);
                                //        MessageReceived(parsedMessage);
                                //    }
                                //}
                                if (message != null)
                                {
                                    MessageReceived(message);
                                }
                                
                                // Clear the buffer, start over
                                break; // inner loop
                            }

                            // Reset/start over
                            if (bufferIdx >= buffer.Length)
                            {
                                break; // inner loop
                            }

                            buffer[bufferIdx++] = receivedByte;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsConnected)
                {
                    // PivotTitle.Title = DefaultAppTitle + "- Connected (EX)";
                }
                else
                {
                    // PivotTitle.Title = DefaultAppTitle + "- Not Connected";
                }
            }

        }


        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        // Expected Message Format: M:0D:C3:FF:FF:427F::��������  (':' are optional)
        public bool SendMessage(string networkId, string recipientId, MessageType messageType, byte[] payloadData)
        {
            //_dataWriter.FlushAsync();

            byte[] rawMessage = new byte[64];
            int index = 0;
            rawMessage[index++] = (byte)'M';

            // Sender - re-written on the device anyway
            rawMessage[index++] = 0xFF;
            rawMessage[index++] = 0xFF;

            // Recipient
            byte[] networkByte = StringToByteArray(networkId);
            rawMessage[index++] = networkByte[0];
            byte[] deviceByte = StringToByteArray(recipientId);
            rawMessage[index++] = deviceByte[0];
            byte[] messageId = new byte[1];
            new Random().NextBytes(messageId);
            // MessageId - the TTL is decremented before resending and a new header is generated
            rawMessage[index++] = messageId[0]; 
            rawMessage[index++] = 0x4F;// TODO: this is a hardcoded TTL of 4, need to update

            // MessageType
            rawMessage[index++] = (byte)messageType;

            // Data 9 - 26 bytes when using P header
            for (int i = 0; i < 9 && i < payloadData.Length; ++i)
            {
               rawMessage[index++] = payloadData[i];
            }

            // Must end in new line
            rawMessage[index++] = 0x0A;

            if (!IsConnected)
            {
                return false;
            }

            //_dataWriter.WriteBytes(rawMessage);
            foreach (byte rawb in rawMessage)
            {
                _dataWriter.WriteByte(rawb);
            }

            DataWriterStoreOperation result = _dataWriter.StoreAsync();

            if (result.ErrorCode == null)
            {
                return false;
            }

            return true;
        }

        public DataWriterStoreOperation SendCommand(string command)
        {
            _dataWriter.WriteString(String.Format("{0}", command));

            return _dataWriter.StoreAsync();
        }

        public void CloseSocket(Dispatcher dispatcher)
        {
            try
            {
                if (_socket != null)
                {
                    _socket.Dispose();
                    _socket = null;
                }
            }
            catch (Exception f)
            {
                //   dispatcher.BeginInvoke(() => MessageBox.Show(String.Format(AppResources.ErrorClosingSocket, f.Message)));
            }
        }

        public bool IsConnected
        {
            get
            {
                return (_socket != null && _dataWriter != null);
            }
        }
    }
}
