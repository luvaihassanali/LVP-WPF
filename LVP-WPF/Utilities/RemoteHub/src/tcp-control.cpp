#include <tcp-control.h>

void InitializeEsp8266()
{
    if (!esp8266Init)
    {
        esp8266.begin(9600);
        Log("Starting esp8266...");         // reset module
        TcpDataOut("AT+CWMODE=1\r\n", 201); // wifi mode: 1 station 2 soft access point 3 both
        TurnLedOff();
        dataOutResult = TcpDataOut("AT+CIFSR\r\n", 201); // get assigned IP address
        BlueLedOn();
        if (dataOutResult.indexOf("0.0") > 0)
        {
            Log("Invalid IP");
            dataOutResult = TcpDataOut(CONNECTION_STRING, 2100); // join access point
            TurnLedOff();
            while (!esp8266.find("OK"))
            {
            }
            if (dataOutResult.indexOf("0.0") > 0)
            {
                Log("Invalid IP after AT+CWJAP");
                ResetEsp8266();
                return;
            }
            Log("Connected to wifi");
            TcpDataOut("AT+CIFSR\r\n", 201);
            BlueLedOn();
        }

        TcpDataOut("AT+CIPMUX=1\r\n", 201); // enable multiple connections
        TurnLedOff();
        TcpDataOut(SERVER_STRING, 201); // 1 for create (at port 3000), 0 for delete
        BlueLedOn();
        esp8266Init = true;
        Log("Esp8266 ready");
    }
}

void ResetEsp8266()
{
    BlinkBlueLed();
    esp8266Init = false;
    TcpDataOut("AT+RST\r\n", 2100);
    InitializeEsp8266();
    clientConnected = false;
}

void TcpDataIn(const int timeout)
{
    dataInResponse = "";
    long int time = millis();
    while ((time + timeout) > millis())
    {
        while (esp8266.available())
        {
            char c = esp8266.read();
            dataInResponse += c;
        }
    }

    if (dataInResponse.length() == 0)
    {
        if (!clientConnected && currentState == 1)
        {
            FlashBlueLed();
        }
        return;
    }

    Log("Received: " + dataInResponse);

    if (dataInResponse.indexOf("zzzz") > 0 || dataInResponse.indexOf("zzz") > 0 || dataInResponse.indexOf("zz") > 0 || dataInResponse.indexOf("z") > 0)
    {
        clientConnected = true;
        Log("Client connected");
        tcpDataInOutput = "initack\r\n";
        tcpDataInSendLength = "AT+CIPSEND=0," + String(tcpDataInOutput.length()) + "\r\n";
        TcpDataOut(tcpDataInSendLength, 10);
        TcpDataOut(tcpDataInOutput, 100);
        clientConnected = true;
        return;
    }

    if (dataInResponse.indexOf("CONNECT FAIL") > 0 || dataInResponse.indexOf("FAIL") > 0 || dataInResponse.indexOf("CLOSED")) 
    {
        Log("Unlink detected");
        clientConnected = false;
        return;
    }
}

String TcpDataOut(String command, const int timeout)
{
    dataOutResponse = "";
    esp8266.print(command);
    long int time = millis();
    while ((time + timeout) > millis())
    {
        while (esp8266.available())
        {
            char c = esp8266.read();
            dataOutResponse += c;
        }
    }

    if (dataOutResponse.indexOf("link is not") > 0)
    {
        Log("Client disconnect tcp data out");
        clientConnected = false;
        return;
    }

    if (dataOutResponse.indexOf("Error") > 0)
    {
        Log("Error in TcpDataOut");
        ResetEsp8266();
    }

    Log(dataOutResponse);
    return dataOutResponse;
}

void MouseControl()
{
    // Data sent over tcp contains joystick x/y positions, joystick buttin pin state, and scroll button pin state
    joystickSendLength = "AT+CIPSEND=0," + String(joystickOutput.length()) + "\r\n";
    TcpDataOut(joystickSendLength, 10);
    TcpDataOut(joystickOutput, 100);
    Serial.print(joystickOutput);
}

void SendTcpKeepAlive()
{
    // Setup data transmission circulation every 5s to avoid esp8266 timeout mechanism (page 9/14)
    // https://www.espressif.com/sites/default/files/documentation/4b-esp8266_at_command_examples_en.pdf
    keepAliveOutput = "ka\r\n";
    keepAliveSendLength = "AT+CIPSEND=0," + String(keepAliveOutput.length()) + "\r\n";
    TcpDataOut(keepAliveSendLength, 10);
    TcpDataOut(keepAliveOutput, 100);
}