#include <utils.h>

/*
+-----------+--------------+-----------+-------------+-------------------+
|  Control  |     1 TV     |   2 LVP   |  3 Laptop   |      4 Audio      |
+-----------+--------------+-----------+-------------+-------------------+
| Button 1  | -            | -         | -           | -                 |
| Button 2  | Enter        | Click     | D. Click    | Mute              |
| Button 3  | Home         | Scroll    | R. Click    | Input             |
| Button 4  | Sound script | Reset     | x           | Reset             |
| Button 5  | Power        | x         | x           | Power             |
| Button 6  | -            | -         | -           | -                 |
| J. Button | Enter        | Click     | D. Click    | Power             |
| J. Stick  | Direction    | Direction | Direction   | Up(+)/down(-) vol |
+-----------+--------------+-----------+-------------+-------------------+
- J. Stick Up/Down + Button 2 = TV Volume
- J. Stick Left + Button 2 = TV Back
- J. Stick Right + Button 2 = TV Play/Pause
- Button 2 + Button 3 = Laptop Taskmgr
*/

void ControlHandler();
void InitializeEsp8266();
void InnerLoop();
void MouseControl();
void RemoteState();
void ResetEsp8266();
void SoundBarControl();
void TcpDataIn(const int timeout);
String TcpDataOut(String command, const int timeout);
void SendTcpKeepAlive();
void TvControl();

int currentState = 0; // 0 = TV, 1 = LVP, 2 = Audio
int button1State = 0;
int button2State = 0;
int button3State = 0;
int button4State = 0;
int button5State = 0;
int button6State = 0;
int joystickButtonState = 0;
int joystickMapX = 0;
int joystickMapY = 0;
int joystickXPos = 0;
int joystickYPos = 0;
unsigned long currentMillis = 0;

void setup()
{
    Serial.begin(9600);
    pinMode(JS_RX_PIN, INPUT);
    pinMode(JS_RY_PIN, INPUT);
    pinMode(JS_BTN_PIN, INPUT_PULLUP);
    pinMode(BTN1_PIN, INPUT_PULLUP);
    pinMode(BTN2_PIN, INPUT_PULLUP);
    pinMode(BTN3_PIN, INPUT_PULLUP);
    pinMode(BTN4_PIN, INPUT_PULLUP);
    pinMode(BTN5_PIN, INPUT_PULLUP);
    pinMode(BTN6_PIN, INPUT_PULLUP);
    pinMode(RED_LED_PIN, OUTPUT);
    pinMode(GREEN_LED_PIN, OUTPUT);
    pinMode(BLUE_LED_PIN, OUTPUT);
    BlueLedOn();
    GreenLedOn();
    RedLedOn();
    Log("Ready");
}

void loop()
{
    currentMillis = millis();
    // Separate main loop to implement simple timer
    while ((currentMillis + KA_TIMEOUT) > millis())
    {
        InnerLoop();
    }
    // If TCP client has connected, send keep alive every 5s
    if (clientConnected)
    {
        SendTcpKeepAlive();
    }
}

void InnerLoop()
{
    button1State = digitalRead(BTN1_PIN);
    button2State = digitalRead(BTN2_PIN);
    button3State = digitalRead(BTN3_PIN);
    button4State = digitalRead(BTN4_PIN);
    button5State = digitalRead(BTN5_PIN);
    button6State = digitalRead(BTN6_PIN);

    joystickXPos = analogRead(JS_RX_PIN);
    joystickYPos = analogRead(JS_RY_PIN);
    joystickButtonState = digitalRead(JS_BTN_PIN);
    joystickMapX = map(joystickXPos, 0, 1023, -512, 512);
    joystickMapY = map(joystickYPos, 0, 1023, -512, 512);
    joystickOutput = String(joystickMapX) + "," + String(joystickMapY) + "," + String(joystickButtonState) + "," + String(button2State) + "," + String(button3State) + "\r\n";
    
    /*String buttonOutput = String(button1State) + " " + String(button2State) + " " + String(button3State) + " " + String(button4State) + " " + String(button5State) + " " + String(button6State);
    Serial.println(joystickOutput);
    Serial.println(buttonOutput);
    delay(1000);*/

    RemoteState();
    ControlHandler();

    // Check for incoming data if esp8266 is active
    if (esp8266Init)
    {
        TcpDataIn(50);
    }
    else
    {
        delay(50);
    }
}

void RemoteState()
{
    if (button1State == 0)
    {
        Log("b1");
        if (currentState == 0)
        {
            currentState = 2;
            GreenLedOn();
        }
        else if (currentState == 2)
        {
            currentState = 0;
            RedLedOn();
        }
        else if (currentState == 1)
        {
            currentState = 0;
            RedLedOn();
        }
    }

    if (button6State == 0)
    {
        Log("b6");
        if (currentState == 0)
        {
            currentState = 1;
            BlueLedOn();
            if (!esp8266Init)
            {
                InitializeEsp8266();
            }
        }
        else if (currentState == 2)
        {
            currentState = 1;
            BlueLedOn();
            if (!esp8266Init)
            {
                InitializeEsp8266();
            }
        }
        else if (currentState == 1)
        {
            currentState = 2;
            GreenLedOn();
        }
    }
}

void ControlHandler()
{
    // If esp8266 not initialized joystick control sends infared tv remote signals
    if (currentState == 0 && (button5State == LOW || button4State == LOW || button2State == LOW || button3State == LOW || joystickButtonState == LOW ||
      joystickMapX > JS_THRESHOLD || joystickMapX < -JS_THRESHOLD || joystickMapY > JS_THRESHOLD || joystickMapY < -JS_THRESHOLD))
    {
        TvControl();
    }

    // Reset esp8266
    if (currentState == 1 && button4State == LOW) {
        if (button4State == LOW) {
            Log("Reset esp8266");
            ResetEsp8266();
        }
    }

    if (clientConnected && currentState == 1 && (button2State == LOW || button3State == LOW  || joystickButtonState == LOW ||
      joystickMapX > JS_THRESHOLD || joystickMapX < -JS_THRESHOLD || joystickMapY > JS_THRESHOLD || joystickMapY < -JS_THRESHOLD))
    {
        MouseControl();
    }
    
    if (currentState == 2 && (button4State == LOW || button5State == LOW || button2State == LOW || button3State == LOW || joystickButtonState == LOW ||
      joystickMapY > JS_THRESHOLD || joystickMapY < -JS_THRESHOLD))
    {
        SoundBarControl();
    }
}
