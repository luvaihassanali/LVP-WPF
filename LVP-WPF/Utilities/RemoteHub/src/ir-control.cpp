#include <ir-control.h>

// https://github.com/Arduino-IRremote/Arduino-IRremote/tree/master/examples/SendRawDemo
void sendRaw(const MICROSECONDS_T intro[], size_t lengthIntro, const MICROSECONDS_T repeat[], size_t lengthRepeat, FREQUENCY_T frequency, unsigned times)
{
    if (lengthIntro > 0U)
    {
        IrSender.sendRaw_P(intro, lengthIntro, HZ_2_KHZ(frequency));
    }
    if (lengthRepeat > 0U)
    {
        for (unsigned i = 0U; i < times - (lengthIntro > 0U); i++)
        {
            IrSender.sendRaw_P(repeat, lengthRepeat, HZ_2_KHZ(frequency));
        }
    }
}

void PowerSoundBar()
{
    if (soundBarPowerSwitch)
    {
        Log("Power off sound bar");
        sendRaw(I_POWER, 68U, R_POWER, 4U, 38400U, 12);
        soundBarPowerSwitch = false;
    }
    else
    {
        Log("Power on sound bar");
        sendRaw(I_POWER, 68U, R_POWER, 4U, 38400U, 1);
        soundBarPowerSwitch = true;
    }
}

void SoundBarControl()
{
    if (button4State == LOW)
    {
        Log("Soundbar reset");
        sendRaw(I_RESET, 68U, R_RESET, 4U, 38400U, 1);
    }

    if (button5State == LOW || joystickButtonState == LOW)
    {
        PowerSoundBar();
    }
    else if (button3State == LOW)
    {
        SoundBarInput();
    }
    else if (button2State == LOW)
    {
        Log("Mute");
        sendRaw(I_MUTE, 68U, R_MUTE, 4U, 38400U, 1);
    }
    else if (joystickMapY > JS_THRESHOLD)
    {
        Log("Soundbar vol up");
        sendRaw(I_UP, 68U, R_UP, 4U, 38400U, 1);
    }
    else if (joystickMapY < -JS_THRESHOLD)
    {
        Log("Soundbar vol down");
        sendRaw(I_DOWN, 68U, R_DOWN, 4U, 38400U, 1);
    }
    FlashGreenLed();
}

void SoundBarInput()
{
    if (opticalBluetoothSwitch)
    {
        Log("Bluetooth");
        sendRaw(I_BT, 68U, R_BT, 4U, 38400U, 1);
        opticalBluetoothSwitch = false;
    }
    else
    {
        Log("Optical");
        sendRaw(I_OPTICAL, 68U, R_OPTICAL, 4U, 38400U, 1);
        opticalBluetoothSwitch = true;
    }
}

// send remote signal pattern to navigate and click on sound input settings option
void TvSoundInput()
{
    IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_HOME, 0);
    FlashRedLed();
    Log("home");
    for (int i = 0; i < 20; i++)
    {
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_LEFT, 0);
        FlashRedLed();
        Log(String(i) + " left");
    }
    IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_RIGHT, 0);
    FlashRedLed();
    Log("right");
    IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_UP, 0);
    FlashRedLed();
    Log("up");
    for (int i = 0; i < 3; i++)
    {
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_RIGHT, 0);
        FlashRedLed();
        Log(String(i) + " right");
    }
    IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_ENTER, 0);
    FlashRedLed();
    Log("enter");
    IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_HOME, 0);
    FlashRedLed();
    Log("home");
}

void TvControl()
{
    if (button4State == LOW)
    {
        Log("tv sound input script");
        TvSoundInput();
    }
    else if (button5State == LOW)
    {
        Log("Power tv");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_POWER, 0);
    }
    else if (button3State == LOW)
    {
        Log("tv home");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_HOME, 0);
    }
    else if (button2State == LOW)
    {
        if (joystickMapY > JS_THRESHOLD)
        {
            Log("tv vol up");
            IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_VOL_UP, 0);
        }
        else if (joystickMapY < -JS_THRESHOLD)
        {
            Log("tv vol down");
            IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_VOL_DOWN, 0);
        }
        else if (joystickMapX > JS_THRESHOLD) {
            Log("tv play/pause");
            IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_STOP, 0);
        }
        else if (joystickMapX < -JS_THRESHOLD) {
            Log("tv back");
            IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_BACK, 0);
        }
        else if (!(joystickMapY > JS_THRESHOLD || joystickMapY < -JS_THRESHOLD))
        {
            Log("tv enter");
            IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_ENTER, 0);
        }
    }
    else if (joystickButtonState == LOW)
    {
        Log("tv enter");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_ENTER, 0);
    }
    else if (joystickMapX > JS_THRESHOLD && button5State == HIGH)
    {
        Log("tv right");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_RIGHT, 0);
    }
    else if (joystickMapX < -JS_THRESHOLD && button5State == HIGH)
    {
        Log("tv left");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_LEFT, 0);
    }
    else if (joystickMapY > JS_THRESHOLD)
    {
        Log("tv up");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_UP, 0);
    }
    else if (joystickMapY < -JS_THRESHOLD)
    {
        Log("tv down");
        IrSender.sendSamsung(SAMSUNG_ADDR, SAMSUNG_DOWN, 0);
    }
    FlashRedLed();
}