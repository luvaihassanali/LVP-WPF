#include "PinDefinitionsAndMore.h"
// Defines must appear before IRremote include
#define DECODE_NEC
#define DECODE_SAMSUNG
#define DECODE_SONY
#define MARK_EXCESS_MICROS 10

#include <IRremote.hpp> 

const uint32_t SAMSUNG_POWER = 0x19E60707; 
const uint32_t SAMSUNG_A = 0x936C0707; 
const uint32_t SAMSUNG_B = 0xEB140707; 
const uint32_t SAMSUNG_C = 0xEA150707; 
const uint32_t SAMSUNG_D = 0xE9160707; 
const uint32_t SAMSUNG_LEFT = 0x9A650707; 
const uint32_t SAMSUNG_RIGHT = 0x9D620707; 
const uint32_t SAMSUNG_UP = 0x9F600707; 
const uint32_t SAMSUNG_DOWN = 0x9E610707; 
const uint32_t SAMSUNG_ENTER = 0x97680707; 
const uint32_t SAMSUNG_RETURN = 0xA7580707; 
const uint32_t SAMSUNG_PLAY = 0xB8470707; 
const uint32_t SAMSUNG_PAUSE = 0xB54A0707; 
const uint32_t SAMSUNG_STOP = 0xB9460707; 
const uint32_t SAMSUNG_FAST_FORWARD = 0xB7480707; 
const uint32_t SAMSUNG_REWIND = 0xBA450707; 
//const uint32_t SAMSUNG_EXIT = 0xD22D0707; 
const uint32_t SAMSUNG_MINUS = 0xDC230707;
const uint32_t SAMSUNG_PRECH = 0xEC130707;
const uint32_t SAMSUNG_num_1 = 0xBE415343;
const uint32_t SAMSUNG_num_2 = 0xBD425343;
const uint32_t SAMSUNG_num_3 = 0xBC435343;
const uint32_t SAMSUNG_num_4 = 0xBB445343;
const uint32_t SAMSUNG_num_5 = 0xBA455343;
const uint32_t SAMSUNG_num_6 = 0xB9465343;
const uint32_t SAMSUNG_num_7 = 0xB8475343;
const uint32_t SAMSUNG_num_8 = 0xC03F5343;
const uint32_t SAMSUNG_num_9 = 0xC8375343;
const uint32_t SAMSUNG_num_0 = 0xD02F5343;

const uint32_t num_1 = 0xFB040707;
const uint32_t num_2 = 0xFA050707;
const uint32_t num_3 = 0xF9060707;
const uint32_t num_4 = 0xF7080707;
const uint32_t num_5 = 0xF6090707;
const uint32_t num_6 = 0xF50A0707;
const uint32_t num_7 = 0xF30C0707;
const uint32_t num_8 = 0xF20D0707;
const uint32_t num_9 = 0xF10E0707;
const uint32_t num_0 = 0xEE110707;
const uint32_t TV_POWER = 0xFD020707; 
const uint32_t SOUNDBAR_POWER = 0xFE015343; 
const uint32_t SOUNDBAR_VOL_UP = 0xCC335343; 
const uint32_t SOUNDBAR_VOL_DOWN = 0xC43B5343; 
const uint32_t APP_POWER = 0xD7285343; //"TV/VIDEO" button
const uint32_t LEFT = 0xDA255343; 
const uint32_t RIGHT = 0xE21D5343; 
const uint32_t UP = 0xF20D5343; 
const uint32_t DOWN = 0xEA155343; 
const uint32_t ENTER = 0xFA055343; 
const uint32_t RETURN = 0xE31C5343; 
const uint32_t PLAY = 0xFC035343; 
const uint32_t PAUSE = 0x827D5343; 
const uint32_t STOP = 0xF40B5343; 
const uint32_t FORWARD = 0xEC135343; 
const uint32_t BACKWARD = 0xE41B5343; 
const uint32_t FAST_FORWARD = 0x8A755343; 
const uint32_t REWIND = 0x8B745343; 
const uint32_t SOUNDBAR_INPUT = 0xF9065343;  //"AUDIO" button
const uint32_t SOUNDBAR_MUTE = 0xC6395343; 
const uint32_t SOUNDBAR_RESET = 0xF10E5343; // "SUBTITLES"
const uint32_t MODE = 0xFB045343; //"MODE"
const uint32_t EFFECT = 0xF30C5343; // "EFFECT"

// The following variables are automatically generated using IrScrutinizer 2.3.0 and Bomaker Ondine 1 Soundbar.rmdu
// http://www.hifi-remote.com/forums/dload.php?action=file&file_id=25809

typedef uint16_t microseconds_t;
typedef uint16_t frequency_t;

static inline unsigned hz2khz(frequency_t f)
{
    return f / 1000U;
}

const microseconds_t intro_Power[] PROGMEM = {9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 39756};
const microseconds_t repeat_Power[] PROGMEM = {9024U, 2256U, 564U, 65535U};
const microseconds_t intro_BT[] PROGMEM = {9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 39756};
const microseconds_t repeat_BT[] PROGMEM = {9024U, 2256U, 564U, 65535U};
const microseconds_t intro_Optical[] PROGMEM = {9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 39756};
const microseconds_t repeat_Optical[] PROGMEM = {9024U, 2256U, 564U, 65535U};
const microseconds_t intro_up_arrow[] PROGMEM = {9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 39756};
const microseconds_t repeat_up_arrow[] PROGMEM = {9024U, 2256U, 564U, 65535U};
const microseconds_t intro_down_arrow[] PROGMEM = {9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 39756};
const microseconds_t repeat_down_arrow[] PROGMEM = {9024U, 2256U, 564U, 65535U};
const microseconds_t intro_Mute[] PROGMEM = { 9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 39756 };
const microseconds_t repeat_Mute[] PROGMEM = { 9024U, 2256U, 564U, 65535U };
const microseconds_t intro_Reset[] PROGMEM = { 9024U, 4512U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 564U, 564U, 1692U, 564U, 1692U, 564U, 1692U, 564U, 39756 };
const microseconds_t repeat_Reset[] PROGMEM = { 9024U, 2256U, 564U, 65535U };

void checkReceive(uint16_t aSentAddress, uint16_t aSentCommand);
void ParseIrValue(uint32_t rawData);
void sendRaw(const microseconds_t intro[], size_t lengthIntro, const microseconds_t repeat[], size_t lengthRepeat, frequency_t frequency, unsigned times);
void ChangeInputSoundBar();
void PowerSoundBar();
void PowerSoundBarLong();
void Led13Blink();
void Log(String m);
void SendSerialData(String d);

// Sent address and command placeholder for checkReceive
uint16_t sAddress = 0xFFF1;
uint8_t sCommand = 0x76;

bool opticalBluetoothSwitch = false;
bool powerPressed = false;