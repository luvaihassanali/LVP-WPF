#ifndef UTILS_H
#define UTILS_H

#include <Arduino.h>

const bool DEBUG = true;

extern bool clientConnected;
extern bool esp8266Init;
extern bool soundBarPowerSwitch;
extern bool opticalBluetoothSwitch;

extern int currentState;
extern int button1State;
extern int button2State;
extern int button3State;
extern int button4State;
extern int button5State;
extern int button6State;
extern int joystickButtonState;
extern int joystickMapX;
extern int joystickMapY;
extern int joystickXPos;
extern int joystickYPos;
extern unsigned long currentMillis;
extern String joystickOutput;
const unsigned long KA_TIMEOUT = 4999;

const int RED_LED_PIN = 10;
const int GREEN_LED_PIN = 11;
const int BLUE_LED_PIN = 12;
const int BTN1_PIN = 9;
const int BTN2_PIN = 4;
const int BTN3_PIN = 5;
const int BTN4_PIN = 6;
const int BTN5_PIN = A2;
const int BTN6_PIN = A3;
const int JS_BTN_PIN = 2;
const int JS_RX_PIN = A1;
const int JS_RY_PIN = A0;
const int JS_THRESHOLD = 10;

void RGB_color(int redLightValue, int greenLightValue, int blueLightValue);
void RedLedOn();
void GreenLedOn();
void BlueLedOn();
void TurnLedOff();
void FlashRedLed();
void FlashGreenLed();
void FlashBlueLed();
void BlinkRedLed();
void BlinkGreenLed();
void BlinkBlueLed();
void Log(int msg);
void Log(String msg);

#endif