using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Remote
{
    public class WolUtilities
    {
        internal static void SendTenMagicPackets()
        {
            for (int i = 0; i < 10; i++)
            {
                Debug.WriteLine($"Sending magic packet {i}");
                SendMagicPacket();
            }
        }

        private static void SendMagicPacket()
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                string text = "192.168.0.126";
                string validMacAddress = GetValidMacAddress("00-C3-F4-70-CA-AD");
                string input = "255.255.255.255";
                if (text.Length > 0)
                {
                    if (validMacAddress != null)
                    {
                        IPAddress address;
                        int num;
                        if (input.Length <= 0)
                        {
                            address = new IPAddress(0xffffffffL);
                        }
                        else
                        {
                            try
                            {
                                Match match = new Regex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}").Match(input);
                                if (!match.Success)
                                {
                                    Debug.WriteLine("The subnet mask is not a valid IP address (example: 255.255.255.0).", "WOL - Magic Packet Sender");
                                    return;
                                }
                                else
                                {
                                    address = IPAddress.Parse(match.Captures[0].Value);
                                }
                            }
                            catch (FormatException)
                            {
                                Debug.WriteLine("The subnet mask is not a valid IP address (example: 255.255.255.0).", "WOL - Magic Packet Sender");
                                return;
                            }
                        }
                        try
                        {
                            num = StringToPort("9");
                        }
                        catch (Exception exception)
                        {
                            Debug.WriteLine(exception.Message);
                            return;
                        }
                        WolUtilities.SendMagic(text, address, validMacAddress, num, ProtocolType.Udp); // ProtocolType.Tcp
                    }
                    else
                    {
                        Debug.WriteLine("The MAC Address must be 12 characters (example: 00-38-B7-28-60-D1)");
                    }
                }
                else
                {
                    Debug.WriteLine("Please enter a host name.");
                }
            }
            catch (Exception exception2)
            {
                Debug.WriteLine(exception2.Message);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private static string GetValidMacAddress(string mac)
        {
            if (mac != null)
            {
                mac = mac.Trim();
                mac = mac.Replace("-", "");
                if (mac.Length != 12)
                {
                    mac = null;
                }
            }
            return mac;
        }

        private static int StringToPort(string portNumber)
        {
            int num = -1;
            try
            {
                num = Convert.ToInt32(portNumber, 10);
            }
            catch
            {
                throw new ArgumentException("Port must be a number");
            }
            if ((num < 0) || (num > 0xffff))
            {
                throw new ArgumentOutOfRangeException("Port Number out of range.");
            }
            return num;
        }

        private static byte[] CreateMagicPacket(byte[] macAddress)
        {
            byte[] array = new byte[0x66];
            for (int i = 0; i < 6; i++)
            {
                array[i] = 0xff;
            }
            for (int j = 1; j <= 0x10; j++)
            {
                macAddress.CopyTo(array, (int)(j * 6));
            }
            return array;
        }

        private static bool IsMagicPacket(byte[] bytes, out string macAddress)
        {
            macAddress = null;
            if (bytes.Length != 0x66)
            {
                return false;
            }
            for (int i = 0; i < 6; i++)
            {
                if (bytes[i] != 0xff)
                {
                    return false;
                }
            }
            byte[] buffer = new byte[6];
            for (int j = 0; j < 6; j++)
            {
                buffer[j] = bytes[6 + j];
            }
            int num3 = 2;
            while (num3 <= 0x10)
            {
                int index = 0;
                while (true)
                {
                    if (index >= 6)
                    {
                        num3++;
                        break;
                    }
                    if (bytes[(num3 * 6) + index] != buffer[index])
                    {
                        return false;
                    }
                    index++;
                }
            }
            macAddress = $"{buffer[0]:X}{buffer[1]:X}{buffer[2]:X}{buffer[3]:X}{buffer[4]:X}{buffer[5]:X}";
            return true;
        }

        private static void SendMagic(string hostName, IPAddress mask, string macAddress, int portNumber, ProtocolType protocol)
        {
            IPAddress[] hostAddresses;
            if (macAddress.Length != 12)
            {
                throw new ArgumentException("The MAC Address must be 12 characters", "macAddress");
            }
            try
            {
                hostAddresses = Dns.GetHostAddresses(hostName);
            }
            catch
            {
                throw new ApplicationException($"Could not resolve address: {hostName}");
            }
            if (hostAddresses.Length == 0)
            {
                throw new ApplicationException($"Could not resolve address: {hostName}");
            }
            byte[] addressBytes = mask.GetAddressBytes();
            byte[] buffer2 = hostAddresses[0].GetAddressBytes();
            for (int i = 0; i < buffer2.Length; i++)
            {
                addressBytes[i] = (byte)(buffer2[i] | (addressBytes[i] ^ 0xff));
            }
            IPEndPoint ipEndPoint = new IPEndPoint(new IPAddress(addressBytes), portNumber);
            byte[] buffer3 = new byte[6];
            for (int j = 0; j < 6; j++)
            {
                try
                {
                    macAddress.Substring(2 * j, 2);
                    buffer3[j] = Convert.ToByte(macAddress.Substring(2 * j, 2), 0x10);
                }
                catch (Exception)
                {
                    throw new ApplicationException("Error : Bad Mac Address");
                }
            }
            ProtocolType type = protocol;
            if (type == ProtocolType.Tcp)
            {
                SendMagicTcp(ipEndPoint, buffer3);
            }
            else
            {
                if (type != ProtocolType.Udp)
                {
                    throw new ApplicationException("Unknown Protocol");
                }
                SendMagicUdp(ipEndPoint, buffer3);
            }
        }

        private static void SendMagicTcp(IPEndPoint ipEndPoint, byte[] macAddress)
        {
            TcpClient client = new TcpClient();
            try
            {
                client.Connect(ipEndPoint);
                NetworkStream stream = client.GetStream();
                byte[] buffer = CreateMagicPacket(macAddress);
                if (stream.CanWrite)
                {
                    stream.Write(buffer, 0, buffer.Length);
                }
                client.Close();
            }
            catch (Exception exception1)
            {
                throw exception1;
            }
            finally
            {
                if (client != null)
                {
                    ((IDisposable)client).Dispose();
                }
            }
        }

        private static void SendMagicUdp(IPEndPoint ipEndPoint, byte[] macAddress)
        {
            UdpClient client = new UdpClient();
            try
            {
                byte[] dgram = CreateMagicPacket(macAddress);
                client.Send(dgram, dgram.Length, ipEndPoint);
                client.Close();
            }
            catch (Exception exception1)
            {
                throw exception1;
            }
            finally
            {
                if (client != null)
                {
                    ((IDisposable)client).Dispose();
                }
            }
        }
    }
}

