using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PinCheck.Network
{
    class CmdClient
    {
        public RegistryKey reg;

        public TcpClient cmdSocket;
        private NetworkStream cmdStream;

        Thread mThread;

        //모션SW↔핀체크SW 디폴트 IP, PORT 값
        private string socketIP = "192.168.0.10";
        private int socketPort = 8003;

        CircularBuffer<string> cmdBuffer = new CircularBuffer<string>(1024);
        private string tempBuffer;
        private bool flagMessage = false;

        public bool Open()
        {
            if (cmdSocket == null)
            {
                cmdSocket = new TcpClient();
            }

            if (!cmdSocket.Connected)
            {
                try
                {
                    cmdSocket = new TcpClient();
                    var result = cmdSocket.BeginConnect(socketIP, socketPort, null, null);

                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));

                    if (!success)
                    {
                        cmdSocket = null;
                        return false;
                    }

                    cmdSocket.EndConnect(result);

                    cmdStream = cmdSocket.GetStream();

                    mThread = new Thread(new ThreadStart(ReceiveCommand));
                    tempBuffer = "";
                    mThread.Start();
                    return true;
                }
                catch (Exception)
                {
                }
            }
            return false;
        }

        public bool Close()
        {
            if (cmdSocket != null)
            {
                if (cmdSocket.Connected)
                {
                    try
                    {
                        cmdSocket.Close();
                        cmdStream.Close();

                        mThread.Abort();

                        return true;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return false;
        }

        public bool IsConnected()
        {
            if (cmdSocket == null)
            {
                return false;
            }
            else
            {
                if (cmdSocket.Connected)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void SetIP(string value)
        {
            // 기존 IP를 레지스트리에 저장된 SERVER IP 값으로 변경한다.
            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            value = reg.GetValue("SERVERIP").ToString();
            socketIP = value;
        }

        public void SetPort(int value)
        {
            // 기존 PORT를 레지스트리에 저장된 SERVER PORT 값으로 변경한다.
            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            Int32.TryParse(reg.GetValue("SERVERPORT").ToString(), out socketPort);
            socketPort = value;
        }

        public void SendMessage(string value)
        {
            byte[] buff = Encoding.Default.GetBytes(value);
            cmdStream.Write(buff, 0, buff.Length);
            Store.getInstance().twLog.WriteLog("[핀체크→모션SW] " + value);
        }

        public void ReceiveCommand()
        {
            byte[] msg = new byte[1024];
            int msgLength = 0;

            while (cmdSocket.Connected)
            {
                try
                {
                    if (cmdStream.CanRead)
                    {
                        msgLength = cmdStream.Read(msg, 0, msg.Length);
                        // 버퍼를 String으로 형변환 하기 위한 인코딩 함수 사용.
                        Store.getInstance().twLog.WriteLog("[모션SW→핀체크] " + Encoding.Default.GetString(msg).Trim('\0'));

                        if (msgLength == 0)
                        {
                            cmdSocket.Close();
                            return;
                        }

                        for (int i = 0; i < msgLength; i++)
                        {
                            if (msg[i] == '<')
                            {
                                tempBuffer = "";
                                flagMessage = true;
                            }

                            if (flagMessage)
                            {
                                if (msg[i] != 0x10 && msg[i] != 0x13)
                                {
                                    tempBuffer += (char)msg[i];
                                }

                                if (msg[i] == '>')
                                {
                                    cmdBuffer.PushBack(tempBuffer);
                                    flagMessage = false;
                                }


                            }
                        }
                        Array.Clear(msg, 0, 1024);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public bool isMessage()
        {
            if (cmdBuffer.Size > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public string getMessage()
        {
            string temp = "";

            if (cmdBuffer.Size > 0)
            {
                temp = cmdBuffer[0];
                cmdBuffer.PopFront();
            }

            return temp;
        }
    }
}
