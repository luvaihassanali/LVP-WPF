#ifndef TCP_CONTROL_H
#define TCP_CONTROL_H

#include <utils.h>
#include <SoftwareSerial.h>

// Replace values for TCP server
const String WIFI_SSID = "ssid";
const String WIFI_PASS = "password";
const String WIFI_PORT = "3000";
const String CONNECTION_STRING = "AT+CWJAP=\"" + WIFI_SSID + "\",\"" + WIFI_PASS + "\"\r\n";
const String SERVER_STRING = "AT+CIPSERVER=1," + WIFI_PORT + "\r\n";

void InitializeEsp8266();
void MouseControl();
void ResetEsp8266();
void TcpDataIn(const int timeout);
String TcpDataOut(String command, const int timeout);
void SendTcpKeepAlive();

bool clientConnected = false;
bool esp8266Init = false;

SoftwareSerial esp8266(8, 7);
String dataOutResult = "";
String dataInResponse = "";
String dataOutResponse = "";
String joystickOutput = "";
String joystickSendLength = "";
String keepAliveOutput = "";
String keepAliveSendLength = "";
String tcpDataInOutput = "";
String tcpDataInSendLength = "";

#endif