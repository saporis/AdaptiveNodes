using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WP_Controller
{
    public class Message
    {
        // 1 byte
        public int NetworkId;
        // 1 byte
        public int DeviceId;
        // 1 byte
        public int RecipientNetworkId;
        // 1 byte
        public int RecipientDeviceId;
        // 2 bytes
        public int MessageTTlAndId;
        // 1 byte
        public MessageType MessageType;

        // 8 to 26 bytes
        public byte[] MessageData;

        // Included as part of a serial message, usually 'B'roadcast or 'I'ncoming.
        public char Direction;
        // Included as part of a serial message, actual data pipe used
        public int Pipe;
        // Debug status 
        public string Debug;
        // Error status 
        public string Error;
    }
}
