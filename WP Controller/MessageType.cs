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

        // Sets the friendly description of the device, the first byte being the text
        // index, giving a maximum description length of 128 bytes.
        MSGTYPE_SET_DESC    =   0x20,
        MSGTYPE_GET_DESC    =   0x21,

        // When a node replies to a name request
        MSGTYPE_DESC_REPLY	=   0x22,


        MSGTYPE_TIME =          0x30,
        MSGTYPE_DEVICE_ARP =    0x32,
        MSGTYPE_NETWORK_ARP =   0x33,
    }

    public enum MessageSetupOptions : byte
    {
        MSGSETUP_INVALID    = 0x00,	
        MSGSETUP_WORK_TIMER = 0x01,	// Valid data is in seconds, valid up to 4294966 seconds (49 days!)
        MSGSETUP_ANALOGPORT = 0x05,	// Analog port to listen to, valid values C0-C7, excluding C3
        MSGSETUP_DIGITALPORT = 0x06,	// Digital port to read, valid values B1, B2, D2-D6.
        MSGSETUP_TRIGGERPORT = 0x07,	// Port set as a trigger, valid values B1, B2, D2-D6, C0-C2, C3-C5, ADC6, ADC7.
        MSGSETUP_ANALOGHIGH = 0x08,	// Analog port to set high, valid values C0-C7, excluding C3
        MSGSETUP_ANALOGLOW = 0x09,	// Analog port to set low, valid values C0-C7, excluding C3
        MSGSETUP_DIGITALHIGH = 0x1A,	// Digital port to set high, valid values B1, B2, D2-D6.
        MSGSETUP_DIGITALLOW = 0x1B,	// Digital port to set low, valid values B1, B2, D2-D6.
        MSGSETUP_RESETPORT	= 0x1C,	// Resets a port - same as digital/analog low.

        MSGSETUP_PWM_ENABLE		= 0x31,
        MSGSETUP_PWM_CLOCK		= 0x32,

        PWM_CLOCK_DIVISOR_1     = 0x1,
        PWM_CLOCK_DIVISOR_8		= 0x2,
        PWM_CLOCK_DIVISOR_64	= 0x3,
        PWM_CLOCK_DIVISOR_128	= 0x4,
        PWM_CLOCK_DIVISOR_1024	= 0x5,

        MSGSETUP_DISABLE_ANALOG_PULLUP	= 0x15,	// Analog port to set as input and disable pull up resistor, valid values C0-C7, excluding C3
        MSGSETUP_DISABLE_DIGITAL_PULLUP	= 0x16,	// Digital port to set as input and disable pull up resistor, valid values B1, B2, D2-D6.
    }

    public enum ClockDivisor : byte
    {
        PWM_CLOCK_DIVISOR_1 = 0x1,
        PWM_CLOCK_DIVISOR_8 = 0x2,
        PWM_CLOCK_DIVISOR_64 = 0x3,
        PWM_CLOCK_DIVISOR_128 = 0x4,
        PWM_CLOCK_DIVISOR_1024 = 0x5
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
        MASK_PORT_C0=			0x81,
        MASK_PORT_C1=			0x82,
        MASK_PORT_C2=			0x84,
        MASK_PORT_C4=			0x90,
        MASK_PORT_C5=			0xA0,
        MASK_PORT_C3=			0x88,  // Port C3 is used for LED status - either green LED or ws2812
        MASK_PORT_ADC6=			0xC0,  // ADC6 and ADC7 are used for accessory reading 
        MASK_PORT_ADC7=			0x80,  // C7 is used for accessory ID and 3v3 power supply (keep high)
    }
}
