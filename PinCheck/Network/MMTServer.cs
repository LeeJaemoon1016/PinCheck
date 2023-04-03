using PinCheck.Util;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace PinCheck.Network
{
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    //MMT와 연동하는 서버
    class mmtServer   //검사기와 통신 수행. IP는 127.0.0.1 고정.
    {
        public RegistryKey reg;

        public static ManualResetEvent allDone = new ManualResetEvent(false);
        //핀체크SW↔검사기OS 통신 포트 기본값
        public static int port = 8010;
        public byte[] recevbyte = new byte[1024];
        public int count = 0; //Home Cycle용
        private bool procEnd = false;

        private Socket Handler;

        public bool isClientConnect = false;

        //YS추가 2021-07-01
        static CircularBuffer<string> mmtBuffer = new CircularBuffer<string>(1024);
        static private string tempBuffer;
        static private bool flagMessage = false;

        public mmtServer()
        {
        }

        public void StartListening()  // paper 1 번
        {
            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  
            Task.Run(async () =>
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = IPAddress.Parse("0.0.0.0");//ipHostInfo.AddressList[5];
                port = GetPort();
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

                // Create a TCP/IP socket.  
                Socket listener = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Bind the socket to the local endpoint and listen for incoming connections.  
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(100);
                    while (true)
                    {
                        // Set the event to nonsignaled state.  
                        allDone.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        listener.BeginAccept(
                            new AsyncCallback(AcceptCallback),
                            listener);

                        // Wait until a connection is made before continuing.  
                        allDone.WaitOne();
                        Store.getInstance().ClientDisconnectFlag = true;
                        isClientConnect = true;
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        public bool IsSocketConnected()
        {
            try
            {
                return !(Handler.Poll(1, SelectMode.SelectRead) && Handler.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

            Store.getInstance().twLog.WriteLog("[검사기OS] : 검사기 연결");
            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
            Handler = handler;
        }

        public void ReadCallback(IAsyncResult ar)
        {
            try      // jyt 20200801   문제점-MS SocketTest 프로그램  연결중  Test 프로그램 강제종료시 프로그램 프리징  try,catch로 예외 처리
            {
                String content = String.Empty;

                // Retrieve the state object and the handler socket  
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;
                Handler = handler;
                // Read data from the client socket.^&
                int bytesRead = handler.EndReceive(ar);
                var msg = Encoding.UTF8.GetString(state.buffer, 0, bytesRead);

                if (bytesRead < 1)
                {
                    isClientConnect = false;
                    Store.getInstance().ClientDisconnectFlag = false;
                    Store.getInstance().twLog.WriteLog("[검사기OS] : 검사기 끊김");

                    return;
                }

                msg = msg.Replace("\n", "");

                Store.getInstance().twLog.WriteLog("[검사기OS→핀체크] : " + msg);

                int msgLength = msg.Length;

                //들어온 프로토콜 메시지를 <> [] 기준으로 버퍼에 담음
                for (int i = 0; i < msgLength; i++)
                {
                    if (msg[i] == '<' || msg[i] == '[')
                    {
                        tempBuffer = "";
                        flagMessage = true;
                    }

                    if (flagMessage)
                    {
                        if (msg[i] != 0x10 && msg[i] != 0x13)
                        {
                            tempBuffer += msg[i];
                        }

                        if (msg[i] == '>' || msg[i] == ']')
                        {
                            mmtBuffer.PushBack(tempBuffer);
                            flagMessage = false;
                        }
                    }
                }
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
            catch (Exception)
            {
            }
        }

        public void Send(String data)  //소켓 통신: 프로그램에 메시지 보내는 부분
        {
            try
            {
                // Convert the string data to byte data using ASCII encoding.  
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data to the remote device.  
                //if (Handler != null)
                //{ 
                Handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), Handler);
                Store.getInstance().twLog.WriteLog("[핀체크→검사기OS] : " + data);
                //}
                //else
                //{
                //}
            }
            catch (Exception)
            {
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                //handler.Shutdown(SocketShutdown.Both);
                //handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public bool IsProcEnd()
        {
            return procEnd;
        }

        public void SetProcEnd(bool proc)
        {
            procEnd = proc;
        }

        private void OnClose()
        {

        }

        public bool IsMessage()
        {
            return mmtBuffer.isMessage();
        }

        public string GetMessage()
        {
            string temp = "";

            if (mmtBuffer.isMessage())
            {
                temp = mmtBuffer[0];
                mmtBuffer.PopFront();
            }

            return temp;
        }

        public void PutMessage(string msg)
        {
            mmtBuffer.PushBack(msg);
        }

        public int GetPort()
        {
            //핀체크SW↔검사기OS 소켓통신 PORT를 레지스트리 값으로 변경한다.
            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            Int32.TryParse(reg.GetValue("CLIENTPORT").ToString(), out port);
            return port;
        }
    }
}
