#include <utils.h>

void RGB_color(int redLightValue, int greenLightValue, int blueLightValue)
{
  analogWrite(RED_LED_PIN, redLightValue);
  analogWrite(GREEN_LED_PIN, greenLightValue);
  analogWrite(BLUE_LED_PIN, blueLightValue);
}

void RedLedOn()
{
  delay(100);
  RGB_color(255, 0, 0);
}

void GreenLedOn()
{
  delay(100);
  RGB_color(0, 255, 0);
}

void BlueLedOn()
{
  delay(100);
  RGB_color(0, 0, 255);
}

void TurnLedOff()
{
  RGB_color(0, 0, 0);
}

void FlashRedLed()
{
  TurnLedOff();
  delay(100);
  RedLedOn();
}

void FlashGreenLed()
{
  TurnLedOff();
  delay(100);
  GreenLedOn();
}

void FlashBlueLed()
{
  TurnLedOff();
  delay(100);
  BlueLedOn();
}

void BlinkRedLed()
{
  for (int i = 0; i < 5; i++)
  {
    TurnLedOff();
    delay(100);
    RedLedOn();
    delay(100);
  }
}

void BlinkGreenLed()
{
  for (int i = 0; i < 5; i++)
  {
    TurnLedOff();
    delay(100);
    GreenLedOn();
    delay(100);
  }
}

void BlinkBlueLed()
{
  for (int i = 0; i < 5; i++)
  {
    TurnLedOff();
    delay(100);
    BlueLedOn();
    delay(100);
  }
}

void Log(int msg)
{
  if (DEBUG)
  {
    Serial.println(String(msg));
  }
}

void Log(String msg)
{
  if (DEBUG)
  {
    Serial.println(msg);
  }
}