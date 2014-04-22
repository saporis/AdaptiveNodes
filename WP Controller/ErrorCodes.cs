using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WP_Controller
{
    public enum ErrorCodes : int
    {
        INITIALIZING				= 0x01,
        NRF24L_SETUP				= 0x02,
        NRF24L_SETUP_FAILURE		= 0x03,
        AUTO_SOFT_REBOOT			= 0x04,
        ERROR_SOFT_REBOOT			= 0x05,
        AUTO_GENERATED_NETWORK_ID	= 0x06,
        AUTO_GENERATED_DEVICE_ID	= 0x07,
        CLEARING_MESSAGE_BUFFER		= 0x08,
        CLEARING_HEADER_CACHE		= 0x09,
        HC05_SETUP					= 0x0A,
        HC05_SETUP_FAILED			= 0x0B,
        NO_NETWORK_ID_CONFIGURED	= 0x0C,
        NO_DEVICE_ID_CONFIGURED		= 0x0D,
        DISCOVERED_NETWORK_ID		= 0x0E,
        EEPROM_RESET				= 0x0F,
        SERIAL_DISABLED				= 0x10,
        INCOMING_SERIAL_ERROR		= 0x11,
        FAILED_MESSAGE_HEADER		= 0x12,
        MESSAGE_IN_CACHE			= 0x13,
        GENERATING_RND_SEED			= 0x14,
        DO_WORK_ERROR				= 0x15,
        SERIAL_MESSAGE_FORWARDED	= 0x16,
        PORT_CONFLICT_WITH_PWM_B	= 0x17,
        PORT_CONFLICT_WITH_PWM_D	= 0x18,
        DEVICE_GOING_TO_SLEEP		= 0x19,

        DO_WORK_MISSING_PARAMETER		= 0x5D,  // A parameter was missing (eg: missing port?)
        DO_WORK_MISSING_DATA			= 0x5E, 
        DO_WORK_INVALID_PORT			= 0x5F,
        DO_WORK_ERROR_PORTCONFLICT		= 0x60, // Ports already in use and conflicts (unknown conflict)
        DO_WORK_ERROR_PORTCONFLICTB1	= 0x61, // B1 Port already in use by PWM
        DO_WORK_ERROR_PORTCONFLICTB2	= 0x62,
        DO_WORK_ERROR_PORTCONFLICTD2	= 0x63,
        DO_WORK_ERROR_PORTCONFLICTD3	= 0x64,
        DO_WORK_ERROR_PORTCONFLICTD4	= 0x65,
        DO_WORK_ERROR_PORTCONFLICTD5	= 0x66,
        DO_WORK_ERROR_PORTCONFLICTD6	= 0x67,
        DO_WORK_ERROR_PORTCONFLICTC0	= 0x68,
        DO_WORK_ERROR_PORTCONFLICTC1	= 0x69,
        DO_WORK_ERROR_PORTCONFLICTC2	= 0x6A,
        DO_WORK_ERROR_PORTCONFLICTC3	= 0x6B,
        DO_WORK_ERROR_PORTCONFLICTC4	= 0x6C,
        DO_WORK_ERROR_PORTCONFLICTC5	= 0x6C,
        DO_WORK_ERROR_PORTCONFLICTC6	= 0x6D,
        DO_WORK_ERROR_PORTCONFLICTC7	= 0x6E,
        DO_WORK_ERROR_PORTCONFLICT_IN	= 0x70,
        DO_WORK_ERROR_PORTCONFLICT_OUT	= 0x71,
        DO_WORK_ERROR_PORTCONFLICT_TRIG	= 0x72,
        DO_WORK_ERROR_UNCHANGED			= 0x7A, // Nothing happened, change was a duplicate
        DO_WORK_ERROR_NOT_IMPLEMENTED	= 0x7B,
        DO_WORK_ERROR_UNKNOWN			= 0x7C
    }
}
