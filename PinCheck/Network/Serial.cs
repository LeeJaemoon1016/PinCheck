using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinCheck.Network
{
    class Serial
    {
        private SerialPort serialPort;

        public CircularBuffer<string> msgBuffer = new CircularBuffer<string>(1024);
        private string tempBuffer;   //링버퍼용
        private bool flagMessage = false;

        public bool IsOpen
        {
            get
            {
                if (serialPort != null) return serialPort.IsOpen;
                return false;
            }
        }

        public Serial()
        {

        }

        public string[] SearchPorts()
        {
            string[] comlist = System.IO.Ports.SerialPort.GetPortNames();

            return comlist;
        }

        public bool OpenComm(string portName, int baudrate, int databits, StopBits stopbits, Parity parity, Handshake handshake) //끝 인자에 
        {
            try
            {
                serialPort = new SerialPort();

                serialPort.PortName = portName;
                serialPort.BaudRate = baudrate;
                serialPort.DataBits = databits;
                serialPort.StopBits = stopbits;
                serialPort.Parity = parity;
                serialPort.Handshake = handshake;
                serialPort.DataReceived += serialPort_DataReceived;
                serialPort.Open();
                Store.getInstance().TopboardDisconnectFlag = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool CloseComm()
        {
            try
            {
                if (serialPort != null)
                {
                    if (serialPort.IsOpen)
                    {
                        serialPort.DtrEnable = false;
                        serialPort.RtsEnable = false;
                        serialPort.DiscardInBuffer();
                        serialPort.DiscardOutBuffer();
                        serialPort.Close();
                    }
                    serialPort = null;
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Send(string sendData)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    Store.getInstance().twLog.WriteLog("[핀체크->상판] " + sendData);
                    serialPort.Write(sendData);
                    return true;
                }
            }
            catch (Exception e)
            {
                Store.getInstance().twLog.WriteLog("[" + serialPort.PortName + "Send에러]" + e.Message);
            }
            return false;
        }

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e) // 핀 체크... 함수   
        {
            string strBuffer = serialPort.ReadExisting();

            if (strBuffer.Length > 0)
            {
                Store.getInstance().twLog.WriteLog(strBuffer);

                #region 메시지 분리 및 링버퍼 저장
                int msgLength = strBuffer.Length;

                for (int i = 0; i < msgLength; i++)
                {
                    if (strBuffer[i] == '[')
                    {
                        tempBuffer = "";
                        flagMessage = true;
                    }

                    if (flagMessage)
                    {
                        if (strBuffer[i] != 0x10 && strBuffer[i] != 0x13)
                        {
                            tempBuffer += strBuffer[i];
                        }

                        if (strBuffer[i] == ']')
                        {
                            msgBuffer.PushBack(tempBuffer);
                            flagMessage = false;
                        }
                    }
                }
                #endregion
            }
        }

        public bool isMessage()
        {
            return msgBuffer.isMessage();
        }

        public string getMessage()
        {
            string temp = "";

            if (msgBuffer.isMessage())
            {
                temp = msgBuffer[0];
                msgBuffer.PopFront();
            }

            return temp;
        }

        public void putMessage(string msg)
        {
            msgBuffer.PushBack(msg);
        }
    }
}
