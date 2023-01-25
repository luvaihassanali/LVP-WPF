#include <Arduino.h>
#include "constants.h"

const bool DEBUG = false;

void setup()
{
    Serial.begin(9600);
    IrReceiver.begin(IR_RECEIVE_PIN, DISABLE_LED_FEEDBACK);
    IrSender.begin(IR_SEND_PIN, ENABLE_LED_FEEDBACK);
    Log("Ready");
}

void loop()
{
    checkReceive(sAddress & 0xFF, sCommand);
}

// https://github.com/Arduino-IRremote/Arduino-IRremote/tree/master/examples/UnitTest
void checkReceive(uint16_t aSentAddress, uint16_t aSentCommand)
{
    // Wait until signal has received
    delay((RECORD_GAP_MICROS / 1000) + 1);
    if (IrReceiver.decode())
    {
        if (DEBUG)
        {
            IrReceiver.printIRResultShort(&Serial);
        }
        uint32_t rawData = IrReceiver.decodedIRData.decodedRawData;
        ParseIrValue(rawData);
        IrReceiver.resume();
    }
}

void ParseIrValue(uint32_t rawData)
{
    switch (rawData)
    {
    case SAMSUNG_POWER:
    case TV_POWER:
        Log("tv power");
        PowerSoundBarLong();
        break;
    case SAMSUNG_A:
    case SOUNDBAR_POWER:
        Log("sound bar power");
        PowerSoundBar();
        break;
    case SAMSUNG_B:
    case SOUNDBAR_VOL_UP:
        Log("vol up");
        sendRaw(intro_up_arrow, 68U, repeat_up_arrow, 4U, 38400U, 1);
        delay(50);
        break;
    case SAMSUNG_C:
    case SOUNDBAR_VOL_DOWN:
        Log("vol down");
        sendRaw(intro_down_arrow, 68U, repeat_down_arrow, 4U, 38400U, 1);
        delay(50);
        break;
    case APP_POWER:
    case SAMSUNG_D:
        SendSerialData("power");
        break;
    case LEFT:
    case SAMSUNG_LEFT:
        SendSerialData("left");
        break;
    case RIGHT:
    case SAMSUNG_RIGHT:
        SendSerialData("right");
        break;
    case UP:
    case SAMSUNG_UP:
        SendSerialData("up");
        break;
    case DOWN:
    case SAMSUNG_DOWN:
        SendSerialData("down");
        break;
    case ENTER:
    case SAMSUNG_ENTER:
        SendSerialData("enter");
        break;
    case RETURN:
    case SAMSUNG_RETURN:
        SendSerialData("return");
        break;
    case PLAY:
    case SAMSUNG_PLAY:
        SendSerialData("play");
        break;
    case PAUSE:
    case SAMSUNG_PAUSE:
        SendSerialData("pause");
        break;
    case STOP:
    case SAMSUNG_STOP:
        SendSerialData("stop");
        break;
    case FAST_FORWARD:
    case SAMSUNG_FAST_FORWARD:
        SendSerialData("fastforward");
        break;
    case REWIND:
    case SAMSUNG_REWIND:
        SendSerialData("rewind");
        break;
    case FORWARD:
        SendSerialData("forward");
        break;
    case BACKWARD:
        SendSerialData("backward");
        break;
    case SOUNDBAR_INPUT:
        Log("sound bar input");
        ChangeInputSoundBar();
        break;
    case SOUNDBAR_MUTE:
        Log("mute");
        sendRaw(intro_Mute, 68U, repeat_Mute, 4U, 38400U, 1);
        delay(50);
        break;
    case SOUNDBAR_RESET:
        Log("reset");
        sendRaw(intro_Reset, 68U, repeat_Reset, 4U, 38400U, 1);
        delay(50);
        break;
    default:
        break;
    }
}

void SendSerialData(String msg)
{
    // Transfer string to serial USB connection
    Serial.println(msg);
    // Wait for the transmission of outgoing serial data to complete
    Serial.flush();
    Led13Blink();
}

// https://github.com/Arduino-IRremote/Arduino-IRremote/tree/master/examples/SendRawDemo
void sendRaw(const microseconds_t intro[], size_t lengthIntro, const microseconds_t repeat[], size_t lengthRepeat, frequency_t frequency, unsigned times)
{
    if (lengthIntro > 0U)
    {
        IrSender.sendRaw_P(intro, lengthIntro, hz2khz(frequency));
    }
    if (lengthRepeat > 0U)
    {
        for (unsigned i = 0U; i < times - (lengthIntro > 0U); i++)
        {
            IrSender.sendRaw_P(repeat, lengthRepeat, hz2khz(frequency));
        }
    }
}

void ChangeInputSoundBar()
{
    if (opticalBluetoothSwitch)
    {
        Log("Optical");
        sendRaw(intro_BT, 68U, repeat_BT, 4U, 38400U, 1);
        opticalBluetoothSwitch = false;
    }
    else
    {
        Log("Bluetooth");
        sendRaw(intro_Optical, 68U, repeat_Optical, 4U, 38400U, 1);
        opticalBluetoothSwitch = true;
    }
    delay(50);
}

void PowerSoundBar()
{
    if (powerPressed)
    {
        Log("Power off");
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 12);
        powerPressed = false;
    }
    else
    {
        Log("Power on");
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 1);
        powerPressed = true;
    }
    delay(50);
}

void PowerSoundBarLong()
{
    if (powerPressed)
    {
        Log("Power off");
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 12);
        delay(50);
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 12);
        powerPressed = false;
    }
    else
    {
        Log("Power on");
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 1);
        delay(50);
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 1);
        delay(50);
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 1);
        delay(50);
        sendRaw(intro_Power, 68U, repeat_Power, 4U, 38400U, 1);
        powerPressed = true;
    }
    delay(50);
}

void Led13Blink()
{
    digitalWrite(LED_BUILTIN, HIGH);
    delay(50);
    digitalWrite(LED_BUILTIN, LOW);
}

void Log(String msg)
{
    if (DEBUG)
    {
        Serial.println(msg);
    }
}