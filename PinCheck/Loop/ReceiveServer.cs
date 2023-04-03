using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

//
namespace PinCheck
{
    public partial class MainWindow : System.Windows.Window
    {

        static Action loopReceiveServer = () =>
        {
            while (Store.getInstance().runningLoops)
            {
                try
                {
                    if (!Store.getInstance().server.IsSocketConnected() && Store.getInstance().ClientDisconnectFlag)
                    {
                        //연결 끊김 감지
                        //UI변경
                        Store.getInstance().twLog.WriteLog("[검사기OS] : 연결 끊김");
                        Store.getInstance().ClientDisconnectFlag = false;
                    }
                    if (Store.getInstance().server.IsMessage())
                    {
                        //씽크윈텍 검사기 OS로 부터 받은 메시지 구분자:<>[]
                        string message = Store.getInstance().server.GetMessage();
                        Store.getInstance().twLog.WriteLog("[검사기OS→핀체크(Loop)] : " + message);

                        //============================================================================
                        // BYPASS 추가될 수 있으니 미리 처리해두자
                        // 검사기 OS로부터 BYPASS가 포함된 프로토콜을 받았을 때
                        if (message.Contains("BYPASS"))
                        {
                            message = message.Replace("BYPASS,", "");

                            if (Store.getInstance().client.IsConnected())
                            {
                                Store.getInstance().client.SendMessage(message);//BYPASS 제외하고 모션 SW한테 전달
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("BYPASS Fail : Server Disconnected");
                            }
                        }
                        //============================================================================
                        //검사기OS로부터 PINCHECK 프로토콜을 받았을 때
                        else if (message == "<CTACT,PINCHECK>")
                        {
                            // 핀 카운트 시퀀스 시작 =================================================================================================================================================
                            CountPinCheck(str_Count);
                            // 핀 카운트 시퀀스 끝 =================================================================================================================================================

                            Store.getInstance().waitReceivePinALL = true;  //메시지 응답 대기
                            Store.getInstance().flagReceivePinOK = false;

                            //메시지를 받았을 당시에는 핀체크 데이터가 없음
                            //핀체크 데이터를 받은적이 없다고 변경
                            Store.getInstance().flagReceivePinALL = false;

                            //핀체크 수행하기 위해 상판에 메시지 전달
                            if (Store.getInstance().topSerial.IsOpen)
                            {
                                //체크섬 사용
                                if (Properties.Settings.Default.CheckSumModeUse == true)
                                {
                                    string input1 = "<CONTACT1";
                                    string checksum1 = GetHexChecksum(input1);
                                    Store.getInstance().topSerial.Send("<CONTACT1^" + checksum1 + ">");

                                    string input2 = "<PIN,ALL";
                                    string checksum2 = GetHexChecksum(input2);
                                    Store.getInstance().topSerial.Send("<PIN,ALL^" + checksum2 + ">");
                                }
                                //체크섬 미사용
                                else
                                {
                                    Store.getInstance().topSerial.Send("<CONTACT1><PIN,ALL>");
                                }

                                //상판에서 핀체크 데이터 전달될때까지 기다리는 타이머
                                TimeSpan maxDuration = TimeSpan.FromMilliseconds(3000);
                                Stopwatch sw = Stopwatch.StartNew();
                                //타임아웃 여부 판정 하는 변수
                                bool bTimeout = false;

                                while (!bTimeout)
                                {
                                    Thread.Sleep(10);

                                    if (sw.Elapsed > maxDuration)
                                    {
                                        sw.Stop();
                                        bTimeout = true;
                                        Store.getInstance().twLog.WriteLog("[상판보드] 핀체크 타임아웃");
                                    }

                                    if (Store.getInstance().flagReceivePinALL)
                                    {
                                        bTimeout = true;
                                    }
                                }
                                if (sw.IsRunning)
                                {
                                    sw.Stop();
                                    sw.Reset();
                                }

                                //프로토콜 맞추기 위해 선언
                                String msgPinData;
                                msgPinData = "[CTACT,PINCHECK," + Store.getInstance().pinALL + "]";

                                //상판 데이터를 검사기로 전달
                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    Store.getInstance().server.Send(msgPinData);
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PIN CHECK DATA Rcv Fail : Client Disconnected");

                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        //검사기 OS 연결 끓김으로 인해 실패했다고 모션 SW에 NG 전달
                                        Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("PINCHECK NG Pass Fail : Server Disconnected");
                                    }
                                }
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("PIN CHECK Fail : UpBoard Disconnected");

                                //모션 SW가 연결되어있으면
                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    //상판 연결 실패로 인한 PINCHECK NG 리턴
                                    //Store.getInstance().server.Send("[CTACT,PINCHECK,ERROR]");
                                    Store.getInstance().server.Send("[CTACT,PINCHECK,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PINCHECK ERROR/NG Pass Fail : Server Disconnected");
                                }
                            }

                            //Task.Run(() =>
                            //{
                            //    TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                            //    Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                            //    maxD = TimeSpan.FromSeconds(5); //  Time out 5초 기다리겠다....  설정의 의미
                            //    sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                            //    while (!Store.getInstance().flagReceivePinOK) // 기다리기 시작
                            //    {
                            //        Thread.Sleep(10);
                            //        if (sw1.Elapsed > maxD)
                            //        {
                            //            sw1.Stop();
                            //            Store.getInstance().waitReceivePinALL = false;

                            //            if (Store.getInstance().server.IsSocketConnected())
                            //            {
                            //                //상판으로부터 전달되는 메시지 없음으로 인한 NG
                            //                //Store.getInstance().server.Send("[CTACT,PINCHECK,ERROR]");
                            //                Store.getInstance().server.Send("[CTACT,PINCHECK,NG]");
                            //            }
                            //            else
                            //            {
                            //                Store.getInstance().twLog.WriteLog("PINCHECK ERROR/NG Pass Fail : Server Disconnected");
                            //            }
                            //            break;
                            //        }
                            //        if (!Store.getInstance().waitReceivePinALL)
                            //        {
                            //            if (sw1.IsRunning)
                            //            {
                            //                sw1.Stop();
                            //                sw1.Reset();
                            //            }
                            //            break;
                            //        }
                            //    }
                            //    if (sw1.IsRunning)
                            //    {
                            //        sw1.Stop();
                            //        sw1.Reset();
                            //    }
                            //});
                        }
                        // 검사기 OS로부터 12V ON OK나 12V OK를 받았을 때
                        else if (message == "[CTACT,12V,OK]" || message == "[CTACT,12V,ON,OK]")
                        {
                            Store.getInstance().flagReceive12VON = true;

                            //ANI Mode가 체크되어있으면
                            if (Properties.Settings.Default.ANIModeUse == true)
                            {
                                //모션 SW가 연결되어있으면
                                if (Store.getInstance().client.IsConnected())
                                {
                                    // 12V ON OK 리턴
                                    Store.getInstance().client.SendMessage("[CTACT,12V,ON,OK]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("12V ON OK Pass Fail : Server Disconnected");
                                }
                            }

                            Store.getInstance().waitReceivePinALL = true;  //메시지 응답 대기
                            Store.getInstance().flagReceivePinOK = false;

                            //메시지를 받았을 당시에는 핀체크 데이터가 없음
                            //핀체크 데이터를 받은적이 없다고 변경
                            Store.getInstance().flagReceivePinALL = false;

                            //핀체크 수행하기 위해 상판에 메시지 전달
                            //체크섬 사용
                            if (Store.getInstance().topSerial.IsOpen)
                            {
                                //체크섬 사용
                                if (Properties.Settings.Default.CheckSumModeUse == true)
                                {
                                    string input1 = "<CONTACT1";
                                    string checksum1 = GetHexChecksum(input1);
                                    Store.getInstance().topSerial.Send("<CONTACT1^" + checksum1 + ">");

                                    string input2 = "<PIN,ALL";
                                    string checksum2 = GetHexChecksum(input2);
                                    Store.getInstance().topSerial.Send("<PIN,ALL^" + checksum2 + ">");
                                }
                                //체크섬 미사용
                                else
                                {
                                    Store.getInstance().topSerial.Send("<CONTACT1><PIN,ALL>");
                                }

                                //상판에서 핀체크 데이터 전달될때까지 기다리는 타이머
                                TimeSpan maxDuration = TimeSpan.FromMilliseconds(3000);
                                Stopwatch sw = Stopwatch.StartNew();

                                //타임아웃 여부 판정 하는 변수
                                bool bTimeout = false;

                                while (!bTimeout)
                                {
                                    Thread.Sleep(10);

                                    if (sw.Elapsed > maxDuration)
                                    {
                                        sw.Stop();
                                        bTimeout = true;
                                        Store.getInstance().twLog.WriteLog("[상판보드] 핀체크 타임아웃");
                                    }

                                    if (Store.getInstance().flagReceivePinALL)
                                    {
                                        bTimeout = true;
                                    }
                                }
                                if (sw.IsRunning)
                                {
                                    sw.Stop();
                                    sw.Reset();
                                }

                                if (Store.getInstance().flagReceivePinALL)
                                {
                                    //프로토콜 맞추기 위해 선언
                                    String msgPinData;
                                    msgPinData = "<CTACT,PINCHECK," + Store.getInstance().pinALL + ">";

                                    //상판 데이터를 검사기로 전달
                                    if (Store.getInstance().server.IsSocketConnected())
                                    {
                                        Store.getInstance().server.Send(msgPinData);
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("PIN CHECK DATA Rcv Fail : Client Disconnected");

                                        if (Store.getInstance().client.IsConnected())
                                        {
                                            //검사기 OS 연결 끓김으로 인해 실패했다고 모션 SW에 NG 전달
                                            Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                        }
                                        else
                                        {
                                            Store.getInstance().twLog.WriteLog("PINCHECK NG Pass Fail : Server Disconnected");
                                        }
                                    }
                                }
                                else
                                {
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        //타임아웃으로 인해 실패했다고 모션 SW에 NG 전달
                                        Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("PINCHECK NG Pass Fail : Server Disconnected");
                                    }
                                }
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("PIN CHECK Fail : UpBoard Disconnected");

                                //모션 SW가 연결되어있으면
                                if (Store.getInstance().client.IsConnected())
                                {
                                    //상판 연결 실패로 인한 PINCHECK NG 리턴
                                    //Store.getInstance().client.SendMessage("[CTACT,PINCHECK,ERROR]");
                                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PINCHECK ERROR/NG Pass Fail : Server Disconnected");
                                }
                            }

                            //Task.Run(() =>
                            //{
                            //    TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                            //    Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                            //    maxD = TimeSpan.FromSeconds(3); //  Time out 5초 기다리겠다....  설정의 의미
                            //    sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                            //    while (!Store.getInstance().flagReceivePinOK) // 기다리기 시작
                            //    {
                            //        Thread.Sleep(10);
                            //        if (sw1.Elapsed > maxD)
                            //        {
                            //            sw1.Stop();
                            //            Store.getInstance().waitReceivePinALL = false;
                            //            if (Store.getInstance().client.IsConnected())
                            //            {
                            //                //상판으로부터 전달되는 메시지 없음으로 인한 NG
                            //                //Store.getInstance().client.SendMessage("[CTACT,PINCHECK,ERROR]");
                            //                Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                            //            }
                            //            else
                            //            {
                            //                Store.getInstance().twLog.WriteLog("PINCHECK ERROR/NG Pass Fail : Server Disconnected");
                            //            }
                            //            break;
                            //        }
                            //        if (!Store.getInstance().waitReceivePinALL)
                            //        {
                            //            if (sw1.IsRunning)
                            //            {
                            //                sw1.Stop();
                            //                sw1.Reset();
                            //            }
                            //            break;
                            //        }
                            //    }
                            //    if (sw1.IsRunning)
                            //    {
                            //        sw1.Stop();
                            //        sw1.Reset();
                            //    }
                            //});
                        }
                        // 검사기 OS로부터 12V ON NG나 12V NG를 받았을 때
                        else if (message == "[CTACT,12V,NG]" || message == "[CTACT,12V,ON,NG]")
                        {
                            Store.getInstance().flagReceive12VON = true;

                            if (Store.getInstance().client.IsConnected())
                            {
                                //12V OK NG를 모션SW로 전달
                                Store.getInstance().client.SendMessage("[CTACT,12V,ON,NG]");
                                Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("12V ON OK Pass Fail : Server Disconnected");
                            }
                        }
                        // 검사기 OS로부터 12V OFF OK를 받았을 때
                        else if (message == "[CTACT,12V,OFF,OK]")
                        {
                            Store.getInstance().flagReceive12VOFF = true;

                            if (Store.getInstance().client.IsConnected())
                            {
                                //12V OFF OK를 모션SW로 전달
                                Store.getInstance().client.SendMessage("[CTACT,12V,OFF,OK]");
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("12V ON OK Pass Fail : Server Disconnected");
                            }
                        }
                        // 검사기 OS로부터 12V OFF NG를 받았을 때
                        else if (message == "[CTACT,12V,OFF,NG]")
                        {
                            Store.getInstance().flagReceive12VOFF = true;

                            if (Store.getInstance().client.IsConnected())
                            {
                                //12V OFF NG를 모션SW로 전달
                                Store.getInstance().client.SendMessage("[CTACT,12V,OFF,NG]");
                                Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("12V ON OK Pass Fail : Server Disconnected");
                            }
                        }
                        // 검사기 OS로부터 PINCHECK OK를 받았을 때
                        else if (message == "[CTACT,PINCHECK,OK]")
                        {
                            Store.getInstance().flagReceivePinOK = true;

                            ////메시지를 받았을 당시에는 핀 데이터가 없음
                            ////핀체크 데이터를 받은적이 없다고 변경
                            ////핀볼트 데이터를 받은적이 없다고 변경
                            //Store.getInstance().flagReceivePinALL = false;
                            //Store.getInstance().flagReceivePinVOL = false;

                            ////핀체크 수행하기 위해 상판에 메시지 전달
                            //if (Store.getInstance().topSerial.IsOpen)
                            //{
                            //    Store.getInstance().topSerial.Send("<PIN,ALL>");
                            //}
                            //else
                            //{
                            //    Store.getInstance().twLog.WriteLog("PIN CHECK Fail : UpBoard Disconnected");
                            //}

                            ////상판에서 데이터 전달될때까지 기다림
                            //while (!Store.getInstance().flagReceivePinALL)
                            //{
                            //    Thread.Sleep(10);
                            //}

                            //if (Store.getInstance().topSerial.IsOpen)
                            //{
                            ////    //핀볼트 수행하기 위해 상판에 메시지 전달
                            //    Store.getInstance().topSerial.Send("<CONTACT1><PIN,DATA>");
                            //}
                            //else
                            //{
                            //    Store.getInstance().twLog.WriteLog("VOL CHECK Fail : UpBoard Disconnected");
                            //}

                            ////상판에서 데이터 전달될때까지 기다림
                            //while (!Store.getInstance().flagReceivePinVOL)
                            //{
                            //    Thread.Sleep(10);
                            //}

                            //프로토콜 맞추기 위해 선언
                            String msgPinData;
                            msgPinData = "[CTACT,PINCHECK," + Store.getInstance().pinALL + "]";
                            //향후 PinDATA 넘겨달라할 때 이걸로 사용. (OK 쉼표 뒤에 핀데이터 넣은거임.)
                            //msgPinData = message.Trim(']') + "," + Store.getInstance().pinALL + "]";
                            String msgPinData2;
                            msgPinData2 = "[CTACT," + Store.getInstance().pinVOL + "]";

                            if (Store.getInstance().client.IsConnected())
                            {
                                //PINCHECK OK를 모션SW로 전달
                                Store.getInstance().client.SendMessage(message);
                                //Thread.Sleep(10);
                                //Store.getInstance().client.SendMessage(msgPinData);
                                //Thread.Sleep(10);
                                //Store.getInstance().client.SendMessage(msgPinData2);
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("PINCHECK OK Pass Fail : Server Disconnected");
                            }
                        }
                        // 검사기 OS로부터 PINCHECK NG를 받았을 때
                        else if (message == "[CTACT,PINCHECK,NG]")
                        {
                            Store.getInstance().flagReceivePinOK = true;

                            ////메시지를 받았을 당시에는 핀 데이터가 없음
                            ////핀체크 데이터를 받은적이 없다고 변경
                            ////핀볼트 데이터를 받은적이 없다고 변경
                            //Store.getInstance().flagReceivePinALL = false;
                            //Store.getInstance().flagReceivePinVOL = false;

                            ////핀체크 수행하기 위해 상판에 메시지 전달
                            //if (Store.getInstance().topSerial.IsOpen)
                            //{
                            //    Store.getInstance().topSerial.Send("<PIN,ALL>");
                            //}
                            //else
                            //{
                            //    Store.getInstance().twLog.WriteLog("PIN CHECK Fail : UpBoard Disconnected");
                            //}

                            ////상판에서 데이터 전달될때까지 기다림
                            //while (!Store.getInstance().flagReceivePinALL)
                            //{
                            //    Thread.Sleep(10);
                            //}

                            //if (Store.getInstance().topSerial.IsOpen)
                            //{
                            //    //핀볼트 수행하기 위해 상판에 메시지 전달
                            //    Store.getInstance().topSerial.Send("<CONTACT1><PIN,DATA>");
                            //}
                            //else
                            //{
                            //    Store.getInstance().twLog.WriteLog("VOL CHECK Fail : UpBoard Disconnected");
                            //}

                            ////상판에서 데이터 전달될때까지 기다림
                            //while (!Store.getInstance().flagReceivePinVOL)
                            //{
                            //    Thread.Sleep(10);
                            //}

                            //프로토콜 맞추기 위해 선언
                            String msgPinData;
                            msgPinData = "[CTACT,PINCHECK," + Store.getInstance().pinALL + "]";
                            //향후 PinDATA 넘겨달라할 때 이걸로 사용. (NG 쉼표 뒤에 핀데이터 넣은거임.)
                            //msgPinData = message.Trim(']') + "," + Store.getInstance().pinALL + "]";
                            String msgPinData2;
                            msgPinData2 = "[CTACT," + Store.getInstance().pinVOL + "]";

                            if (Store.getInstance().client.IsConnected())
                            {
                                //PINCHECK OK를 모션SW로 전달
                                Store.getInstance().client.SendMessage(message);
                                //Thread.Sleep(10);
                                //Store.getInstance().client.SendMessage(msgPinData);
                                //Thread.Sleep(10);
                                //Store.getInstance().client.SendMessage(msgPinData2);
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("PINCHECK OK Pass Fail : Server Disconnected");
                            }
                        }
                        // 검사기 OS로부터 핀 볼트 체크 요청을 받았을 때
                        else if (message == "<CTACT,VOL>")
                        {
                            //메시지를 받았을 당시에는 볼트체크 데이터가 없음
                            //핀볼트 데이터를 받은적이 없다고 변경
                            Store.getInstance().flagReceivePinVOL = false;

                            if (Store.getInstance().topSerial.IsOpen)
                            {
                                //체크섬 사용
                                if (Properties.Settings.Default.CheckSumModeUse == true)
                                {
                                    string input1 = "<CONTACT1";
                                    string checksum1 = GetHexChecksum(input1);
                                    Store.getInstance().topSerial.Send("<CONTACT1^" + checksum1 + ">");

                                    string input2 = "<PIN,DATA";
                                    string checksum2 = GetHexChecksum(input2);
                                    Store.getInstance().topSerial.Send("<PIN,DATA^" + checksum2 + ">");
                                }
                                //체크섬 미사용
                                else
                                {
                                    //핀볼트 수행하기 위해 상판에 메시지 전달
                                    Store.getInstance().topSerial.Send("<CONTACT1><PIN,DATA>");
                                }

                                //상판에서 핀볼트 데이터 전달될때까지 기다리는 타이머
                                TimeSpan maxDuration = TimeSpan.FromMilliseconds(3000);
                                Stopwatch sw = Stopwatch.StartNew();
                                //타임아웃 여부 판정 하는 변수
                                bool bTimeout = false;

                                while (!bTimeout)
                                {
                                    Thread.Sleep(10);

                                    if (sw.Elapsed > maxDuration)
                                    {
                                        sw.Stop();
                                        bTimeout = true;
                                        Store.getInstance().twLog.WriteLog("[상판보드] 핀체크 타임아웃");
                                    }

                                    if (Store.getInstance().flagReceivePinVOL)
                                    {
                                        bTimeout = true;
                                    }
                                }
                                if (sw.IsRunning)
                                {
                                    sw.Stop();
                                    sw.Reset();
                                }

                                if (Store.getInstance().flagReceivePinVOL)
                                {
                                    //프로토콜 맞추기 위해 선언
                                    String msgPinData;
                                    msgPinData = "[CTACT," + Store.getInstance().pinVOL + "]";

                                    //상판 데이터를 검사기로 전달
                                    if (Store.getInstance().server.IsSocketConnected())
                                    {
                                        Store.getInstance().server.Send(msgPinData);
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("VOL CHECK DATA Rcv Fail : Client Disconnected");
                                    }
                                }
                                else
                                {
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        //타임아웃으로 인해 실패했다고 모션 SW에 NG 전달
                                        Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("PINCHECK NG Pass Fail : Server Disconnected");
                                    }
                                }
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("VOL CHECK Fail : UpBoard Disconnected");

                                if (Store.getInstance().client.IsConnected())
                                {
                                    //VOL ERROR 리턴
                                    Store.getInstance().client.SendMessage("[CTACT,VOL,ERROR]");
                                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("VOL ERROR Pass Fail : Server Disconnected");
                                }
                            }
                        }
                        // 검사기 OS로부터 핀 카운트 요청을 받았을 때
                        else if (message == "<CTACT,PINCOUNT>")
                        {
                            //핀카운트 플래그 아직 받지 않음
                            Store.getInstance().flagReceivePinCount = false;

                            if (Store.getInstance().server.IsSocketConnected())
                            {
                                PinCount();
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("Pin Count Pass Fail : Client Disconnected");
                            }
                        }
                        //검사기 OS로부터 상판 버전 요청을 받았을 때
                        else if (message == "<WHO>")
                        {
                            Store.getInstance().flagReceivePinVer = false;

                            if (Store.getInstance().server.IsSocketConnected())
                            {
                                if (Store.getInstance().topSerial.IsOpen)
                                {
                                    if (Properties.Settings.Default.CheckSumModeUse == true)
                                    {
                                        //체크섬 사용
                                        string input1 = "<WHO";
                                        string checksum1 = GetHexChecksum(input1);
                                        Store.getInstance().topSerial.Send("<WHO^" + checksum1 + ">");
                                    }
                                    else
                                    {
                                        //체크섬 미사용
                                        Store.getInstance().topSerial.Send("<WHO>");
                                    }
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("Upboard Ver Pass Fail : Upboard Disconnected");

                                    Store.getInstance().topSerial.Send("[WHO,NG]");
                                }

                                //상판에서 핀볼트 데이터 전달될때까지 기다리는 타이머
                                TimeSpan maxDuration = TimeSpan.FromMilliseconds(3000);
                                Stopwatch sw = Stopwatch.StartNew();
                                //타임아웃 여부 판정 하는 변수
                                bool bTimeout = false;

                                while (!bTimeout)
                                {
                                    Thread.Sleep(10);

                                    if (sw.Elapsed > maxDuration)
                                    {
                                        sw.Stop();
                                        bTimeout = true;
                                        Store.getInstance().twLog.WriteLog("[상판보드] 상판보드 버전 체크 타임아웃");
                                        Store.getInstance().server.Send("[WHO,NG]");
                                    }

                                    if (Store.getInstance().flagReceivePinVer)
                                    {
                                        bTimeout = true;
                                    }
                                }
                                if (sw.IsRunning)
                                {
                                    sw.Stop();
                                    sw.Reset();
                                }
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("Upboard Ver Pass Fail : Client Disconnected");
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    Store.getInstance().twLog.WriteLog("[검사기OS Loop] Execption 발생" + e.Message);
                }
            }
        };

        //핀카운트 더하는 함수
        static void PinCount()
        {
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            if ((reg.GetValue("PINID") != null)
            && (reg.GetValue("PINCOUNT") != null)
            && (reg.GetValue("PINCHECKCOUNT") != null))
            {
                //레지에 저장된 PinID 불러오기
                //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                //string reg_Pin_ID = ((MainWindow)System.Windows.Application.Current.MainWindow).reg.GetValue("PINID").ToString();
                string reg_Pin_ID = reg.GetValue("PINID").ToString();

                //레지에 저장된 PinCount 횟수 불러오기
                //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                //string reg_str_Count = ((MainWindow)System.Windows.Application.Current.MainWindow).reg.GetValue("PINCOUNT").ToString();
                string reg_str_Count = reg.GetValue("PINCOUNT").ToString();

                //String을 int로 변환
                int reg_num_Count;
                Int32.TryParse(reg_str_Count, out reg_num_Count);

                //CTACT PINCHECK에서 카운트한 str 불러오기
                //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                //string reg_str_PincheckCount = ((MainWindow)System.Windows.Application.Current.MainWindow).reg.GetValue("PINCHECKCOUNT").ToString();
                string reg_str_PincheckCount = reg.GetValue("PINCHECKCOUNT").ToString();

                //String을 int로 변환
                int reg_num_PincheckCount;
                Int32.TryParse(reg_str_PincheckCount, out reg_num_PincheckCount);

                //두 int 더하기
                int sum = reg_num_Count + reg_num_PincheckCount;

                //더한 값 String으로 변환
                string str_Merge = sum.ToString("D8");

                if (Store.getInstance().topSerial.IsOpen)
                {
                    //프로토콜 메시지 조합하기 <EEP,WRITE,UP,XXXXXXXX>
                    string message = "<EEP,WRITE,UP," + str_Merge + ">";

                    //체크섬 사용
                    if (Properties.Settings.Default.CheckSumModeUse == true)
                    {
                        string input1 = "<CONTACT0";
                        string checksum1 = GetHexChecksum(input1);
                        Store.getInstance().topSerial.Send("<CONTACT0^" + checksum1 + ">");

                        string input2 = "<EEP,READ,UP,ID";
                        string checksum2 = GetHexChecksum(input2);
                        Store.getInstance().topSerial.Send("<EEP,READ,UP,ID^" + checksum2 + ">");

                        string input3 = "<EEP,WRITE,UP," + str_Merge;
                        string checksum3 = GetHexChecksum(input3);
                        Store.getInstance().topSerial.Send("<EEP,WRITE,UP," + str_Merge + "^" + checksum3 + ">");
                    }
                    //체크섬 미사용
                    else
                    {
                        Store.getInstance().topSerial.Send("<CONTACT0><EEP,READ,UP,ID>" + message);
                    }

                    //상판에서 핀카운트 데이터 전달될때까지 기다리는 타이머
                    TimeSpan maxDuration = TimeSpan.FromMilliseconds(1000);
                    Stopwatch sw = Stopwatch.StartNew();
                    //타임아웃 여부 판정 하는 변수
                    bool bTimeout = false;

                    while (!bTimeout)
                    {
                        Thread.Sleep(10);

                        if (sw.Elapsed > maxDuration)
                        {
                            sw.Stop();
                            bTimeout = true;
                            Store.getInstance().twLog.WriteLog("[상판보드] 핀카운트 타임아웃");
                        }

                        if (Store.getInstance().flagReceivePinCount)
                        {
                            bTimeout = true;
                        }
                    }
                    if (sw.IsRunning)
                    {
                        sw.Stop();
                        sw.Reset();
                    }

                    if (Store.getInstance().flagReceivePinCount)
                    {
                        if (Store.getInstance().server.IsSocketConnected())
                        {
                            // 레지스트리에 저장된 Pin Count 횟수를 검사기에 리턴
                            Store.getInstance().server.Send("[CTACT,PINCOUNT," + reg_Pin_ID + str_Merge + "]");
                        }
                        else
                        {
                            Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                        }

                        //CTACT PINCHECK 카운트한 int 초기화
                        //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                        //((MainWindow)System.Windows.Application.Current.MainWindow).reg.SetValue("PINCHECKCOUNT", "00000000");
                        reg.SetValue("PINCHECKCOUNT", "00000000");

                        //체크섬 사용
                        if (Properties.Settings.Default.CheckSumModeUse == true)
                        {
                            string input1 = "<CONTACT0";
                            string checksum1 = GetHexChecksum(input1);
                            Store.getInstance().topSerial.Send("<CONTACT0^" + checksum1 + ">");

                            string input2 = "<EEP,READ,UP";
                            string checksum2 = GetHexChecksum(input2);
                            Store.getInstance().topSerial.Send("<EEP,READ,UP^" + checksum2 + ">");
                        }
                        //체크섬 미사용
                        else
                        {
                            Store.getInstance().topSerial.Send("<CONTACT0><EEP,READ,UP>");
                        }
                    }
                    else
                    {
                        Store.getInstance().twLog.WriteLog("PIN COUNT Fail : UpBoard 통신 이상");

                        if (Store.getInstance().server.IsSocketConnected())
                        {
                            Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                        }
                        else
                        {
                            Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                        }
                    }
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("PIN COUNT Fail : UpBoard Disconnected");

                    if (Store.getInstance().server.IsSocketConnected())
                    {
                        Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                    }
                    else
                    {
                        Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                    }
                }
            }
            else
            {
                Store.getInstance().twLog.WriteLog("PIN COUNT Fail : PIN ID & COUNT Load Fail!!");

                if (Store.getInstance().server.IsSocketConnected())
                {
                    Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                }
            }
            //});
        }
    }
}
