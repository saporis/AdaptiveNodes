namespace WP_Controller
{
    public enum MessageType : byte
    {
        MSGTYPE_IGNORED =       0x00,
        MSGTYPE_ACK =           0x01,
        MSGTYPE_HEARTBEAT =     0x03,
        MSGTYPE_BOOT =          0x04,
        MSGTYPE_LEDUPDATE   =   0x05,
        MSGTYPE_UPDATE =        0x08,
        MSGTYPE_TRIGGER =       0x09,
        MSGTYPE_SETUP =         0x10,

        // Identical to MSGTYPE_SETUP, however, this instructs the device that it should 
        // only accept the command *IF* and only IF it is the devices first boot (unconfigured). 
        // This allows one to program devices that have a conflicting device ID.
        MSGTYPE_NEW_SETUP =     0x11,

        MSGTYPE_TIME =          0x20,
        MSGTYPE_DEVICE_ARP =    0x0F,
        MSGTYPE_NETWORK_ARP =   0x33,
    }

    public enum MessageSetupOptions : byte
    {
        MSGSETUP_INVALID    = 0x00,	
        MSGSETUP_WORK_TIMER = 0x01,	// Valid data is in seconds, valid up to 4294966 seconds (49 days!)
        MSGSETUP_ANALOGPORT = 0x05,	// Analog port to listen to, valid values C0-C7, excluding C3
        MSGSETUP_DIGITALPORT = 0x06,	// Digital port to read, valid values B1, B2, D2-D6.
        MSGSETUP_TRIGGERPORT = 0x07,	// Digital port to set as a trigger, valid values B1, B2, D2-D6.
        MSGSETUP_ANALOGHIGH = 0x08,	// Analog port to set high, valid values C0-C7, excluding C3
        MSGSETUP_ANALOGLOW = 0x09,	// Analog port to set low, valid values C0-C7, excluding C3
        MSGSETUP_DIGITALHIGH = 0x0A,	// Digital port to set high, valid values B1, B2, D2-D6.
        MSGSETUP_DIGITALLOW = 0x0B,	// Digital port to set low, valid values B1, B2, D2-D6.
    }


    public enum PortMasks : byte
    {
        NONE=                   0x00,
        // MSB set LOW, eg: D3 == 0x08
        MASK_PORT_B1=			0x01,
        MASK_PORT_B2=			0x02,
        MASK_PORT_D2=			0x04,
        MASK_PORT_D3=			0x08,
        MASK_PORT_D4=			0x10,
        MASK_PORT_D5=			0x20,
        MASK_PORT_D6=			0x40,
        // MSB set HIGH, eg: C3 == 0x88, C7 == 0x80.
        MASK_PORT_C0=			0x01,
        MASK_PORT_C1=			0x02,
        MASK_PORT_C2=			0x04,
        MASK_PORT_C4=			0x10,
        MASK_PORT_C5=			0x20,
        MASK_PORT_C3=			0x08,  // Port C3 is used for LED status - either green LED or ws2812
        MASK_PORT_C6=			0x40,  // C6 and C7 are used for accessory reading
        MASK_PORT_C7=			0x80,  // C7 is used for accessory ID and 3v3 power supply (keep high)
    }
}
