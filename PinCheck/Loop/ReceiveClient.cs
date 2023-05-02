using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PinCheck
{
    public partial class MainWindow : System.Windows.Window
    {
        //핀카운트 횟수
        static int num_Count;
        static string str_Count;

        static Action loopReceiveClient = () =>
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            while (Store.getInstance().runningLoops)
            {
                try
                {
                    if (!Store.getInstance().client.IsConnected())
                    {
                        if (Store.getInstance().flagClientDisconnectLogging)
                        {
                            Store.getInstance().twLog.WriteLog("[모션SW] 연결 끊김");
                            Store.getInstance().flagClientDisconnectLogging = false;
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 소켓 통신 루프의 포트를 레지스트리에 저장된 SERVER PORT 값으로 설정한다.
                            Store.getInstance().client.SetIP(((MainWindow)System.Windows.Application.Current.MainWindow).ClientIP.Text);
                            // 소켓 통신 루프의 포트를 레지스트리에 저장된 SERVER PORT 값으로 설정한다.
                            int sockPort;
                            //Int32.TryParse(((MainWindow)System.Windows.Application.Current.MainWindow).reg.GetValue("SERVERPORT").ToString(), out sockPort);
                            Int32.TryParse(reg.GetValue("SERVERPORT").ToString(), out sockPort);
                            Store.getInstance().client.SetPort(sockPort);
                        });
                        if (Store.getInstance().client.Open())
                        {
                            Store.getInstance().flagClientDisconnectLogging = true;
                            Store.getInstance().twLog.WriteLog("[모션SW] 연결성공");
                            Store.getInstance().client.SendMessage(" ");
                        }
                    }
                    else
                    {
                        if (Store.getInstance().client.isMessage())
                        {
                            //서버(모션SW)로부터 받은 메시지, 구분자는 : <>
                            string message = Store.getInstance().client.getMessage();
                            Store.getInstance().twLog.WriteLog("[모션SW→핀체크(Loop)] : " + message);

                            //모션 SW로부터 BYPASS가 포함된 프로토콜을 받았을 때
                            if (message.Contains("BYPASS"))
                            {
                                message = message.Replace("BYPASS,", "");
                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    Store.getInstance().server.Send(message);//BYPASS 제외하고 검사기 OS한테 전달
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("BYPASS Rcv Fail : Client Disconnected");
                                }
                            }
                            //모션SW로부터 PINCHECK 프로토콜을 받았을 때
                            else if (message == "<CTACT,PINCHECK>")
                            {
                                // 핀 카운트 시퀀스 시작 =================================================================================================================================================
                                CountPinCheck(str_Count);
                                // 핀 카운트 시퀀스 끝 =================================================================================================================================================

                                //검사기 OS가 연결되어 있으면
                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    //12V ON 메시지 전송
                                    Store.getInstance().server.Send("<CTACT,12V,ON>");

                                    Store.getInstance().waitReceive12VON = true;  //메시지 응답 대기
                                    Store.getInstance().flagReceive12VON = false; // 12V ON Flag

                                    Task.Run(() =>
                                    {

                                        TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                                        Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                                        maxD = TimeSpan.FromSeconds(5); //  Time out 5초 기다리겠다....  설정의 의미
                                        sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                                        //12V ON OK/NG 리시브가 5초 이상 오지 않을 때 PINCHECK NG 판정을 내리는 타임아웃
                                        while (!Store.getInstance().flagReceive12VON) // 기다리기 시작
                                        {
                                            Thread.Sleep(10);
                                            if (sw1.Elapsed > maxD)
                                            {
                                                sw1.Stop();
                                                Store.getInstance().waitReceive12VON = true;
                                                // 모션 SW가 연결되어 있으면
                                                if (Store.getInstance().client.IsConnected())
                                                {
                                                    // 타임아웃으로 인한 PINCHECK NG, 12V NG 리턴
                                                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                                    Store.getInstance().client.SendMessage("[CTACT,12V,ON,NG]");
                                                }
                                                else
                                                {
                                                    Store.getInstance().twLog.WriteLog("12V ON NG Pass Fail : Server Disconnected");
                                                }
                                                break;
                                            }
                                            if (!Store.getInstance().waitReceive12VON)
                                            {
                                                if (sw1.IsRunning)
                                                {
                                                    sw1.Stop();
                                                    sw1.Reset();
                                                }
                                                break;
                                            }
                                        }
                                        if (sw1.IsRunning)
                                        {
                                            sw1.Stop();
                                            sw1.Reset();
                                        }
                                    });
                                }
                                else
                                {
                                    // 검사기 OS가 연결되어있지 않다고 리턴.
                                    Store.getInstance().twLog.WriteLog("12V ON Rcv Fail : Client Disconnected.");

                                    //모션 SW가 연결되어있으면
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        // PINCHECK NG, 12V NG 리턴
                                        Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                        Store.getInstance().client.SendMessage("[CTACT,12V,ON,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("PINCHECK Pass Fail : Server Disconnected");
                                    }
                                }
                            }
                            // 모션 SW로부터 <PIN,ALL> (ANI 테스트용으로 쓴다고 함) 명령어를 받았을 때
                            else if (message == "<PIN,ALL>")
                            {
                                Store.getInstance().flagReceive12VON = true;

                                Store.getInstance().waitReceivePinALL = true;  //메시지 응답 대기
                                Store.getInstance().flagReceivePinOK = false;

                                //메시지를 받았을 당시에는 핀체크 데이터가 없음
                                //핀체크 데이터를 받은적이 없다고 변경
                                Store.getInstance().flagReceivePinALL = false;

                                //핀체크 수행하기 위해 상판에 메시지 전달
                                if (Store.getInstance().topSerial.IsOpen)
                                {
                                    if (Properties.Settings.Default.CheckSumModeUse == true)
                                    {
                                        string input1 = "<CONTACT1";
                                        string checksum1 = GetHexChecksum(input1);
                                        Store.getInstance().topSerial.Send("<CONTACT1^" + checksum1 + ">");

                                        string input2 = "<PIN,ALL";
                                        string checksum2 = GetHexChecksum(input2);
                                        Store.getInstance().topSerial.Send("<PIN,ALL^" + checksum2 + ">");
                                    }
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
                                            ChangeColour();
                                            MessageBox.Show("상판보드 통신 에러!\n상판보드를 확인하세요.","E R R O R !", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                        msgPinData = "[" + Store.getInstance().pinALL + "]";

                                        //상판 데이터를 검사기로 전달
                                        if (Store.getInstance().server.IsSocketConnected())
                                        {
                                            Store.getInstance().server.Send(msgPinData);
                                        }
                                        //검사기에 연결되어있지 않으면
                                        else
                                        {
                                            Store.getInstance().twLog.WriteLog("PIN CHECK DATA Rcv Fail : Client Disconnected");
                                            if (Store.getInstance().client.IsConnected())
                                            {
                                                // 타임아웃으로 인한 PINCHECK NG 리턴
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
                                //상판이 연결되어있지 않으면
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PIN CHECK Fail : UpBoard Disconnected");
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        //PINCHECK ERROR 리턴
                                        Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("PINCHECK ERROR Pass Fail : Server Disconnected");
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
                                //            if (Store.getInstance().client.IsConnected())
                                //            {
                                //                // 타임아웃으로 인한 PINCHECK NG 리턴
                                //                Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                //            }
                                //            else
                                //            {
                                //                Store.getInstance().twLog.WriteLog("PINCHECK NG Pass Fail : Server Disconnected");
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
                            // ANI 모션 SW로부터 물류 매뉴얼 동작 시 12V ON을 해주기 위해(테스트용으로 쓴다고 함) 추가.
                            else if (message == "<CTACT,12V,ON>")
                            {
                                //검사기 OS가 연결되어있으면
                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    //검사기 OS에 12V OFF 메시지 전송
                                    Store.getInstance().server.Send("<CTACT,12V,ON>");

                                    Store.getInstance().waitReceive12VON = true;  //메시지 응답 대기
                                    Store.getInstance().flagReceive12VON = false; // 12V ON Flag


                                    Task.Run(() =>
                                    {

                                        TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                                        Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                                        maxD = TimeSpan.FromSeconds(5); //  Time out 5초 기다리겠다....  설정의 의미
                                        sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                                        //12V ON OK/NG 리시브가 5초 이상 오지 않을 때 PINCHECK NG 판정을 내리는 타임아웃
                                        while (!Store.getInstance().flagReceive12VON) // 기다리기 시작
                                        {
                                            Thread.Sleep(10);
                                            if (sw1.Elapsed > maxD)
                                            {
                                                sw1.Stop();
                                                Store.getInstance().waitReceive12VON = false;
                                                //모션 SW가 연결되어있으면
                                                if (Store.getInstance().client.IsConnected())
                                                {
                                                    // 타임아웃으로 PINCHECK NG, 12V NG 리턴
                                                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                                    Store.getInstance().client.SendMessage("[CTACT,12V,ON,NG]");
                                                }
                                                else
                                                {
                                                    Store.getInstance().twLog.WriteLog("12V ON NG Pass Fail : Server Disconnected");
                                                }
                                                break;
                                            }
                                            if (!Store.getInstance().waitReceive12VON)
                                            {
                                                if (sw1.IsRunning)
                                                {
                                                    sw1.Stop();
                                                    sw1.Reset();
                                                }
                                                break;
                                            }
                                        }
                                        if (sw1.IsRunning)
                                        {
                                            sw1.Stop();
                                            sw1.Reset();
                                        }
                                    });
                                }
                                else
                                {
                                    // 검사기 OS가 연결되어있지 않다고 리턴.
                                    Store.getInstance().twLog.WriteLog("12V ON Rcv Fail : Client Disconnected.");

                                    //모션 SW가 연결되어있으면
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        // 12V NG 리턴
                                        Store.getInstance().client.SendMessage("[CTACT,12V,ON,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("12V ON NG Rcv Fail : Server Disconnected");
                                    }
                                }
                            }
                            // ANI 모션 SW로부터 물류 매뉴얼 동작 시 12V OFF를 해주기 위해(테스트용으로 쓴다고 함) 추가.
                            else if (message == "<CTACT,12V,OFF>")
                            {
                                //검사기 OS가 연결되어있으면
                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    //검사기 OS에 12V OFF 메시지 전송
                                    Store.getInstance().server.Send("<CTACT,12V,OFF>");

                                    Store.getInstance().waitReceive12VOFF = true;  //메시지 응답 대기
                                    Store.getInstance().flagReceive12VOFF = false; // 12V ON Flag


                                    Task.Run(() =>
                                    {

                                        TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                                        Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                                        maxD = TimeSpan.FromSeconds(5); //  Time out 5초 기다리겠다....  설정의 의미
                                        sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                                        //12V OFF OK/NG 리시브가 5초 이상 오지 않을 때 PINCHECK NG 판정을 내리는 타임아웃
                                        while (!Store.getInstance().flagReceive12VOFF) // 기다리기 시작
                                        {
                                            Thread.Sleep(10);
                                            if (sw1.Elapsed > maxD)
                                            {
                                                sw1.Stop();
                                                Store.getInstance().waitReceive12VOFF = false;
                                                //모션 SW가 연결되어있으면
                                                if (Store.getInstance().client.IsConnected())
                                                {
                                                    // 타임아웃으로 PINCHECK NG, 12V NG 리턴
                                                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                                                    Store.getInstance().client.SendMessage("[CTACT,12V,OFF,NG]");
                                                }
                                                else
                                                {
                                                    Store.getInstance().twLog.WriteLog("12V OFF NG Pass Fail : Server Disconnected");
                                                }
                                                break;
                                            }
                                            if (!Store.getInstance().waitReceive12VOFF)
                                            {
                                                if (sw1.IsRunning)
                                                {
                                                    sw1.Stop();
                                                    sw1.Reset();
                                                }
                                                break;
                                            }
                                        }
                                        if (sw1.IsRunning)
                                        {
                                            sw1.Stop();
                                            sw1.Reset();
                                        }
                                    });
                                }
                                else
                                {
                                    // 검사기 OS가 연결되어있지 않다고 리턴.
                                    Store.getInstance().twLog.WriteLog("12V OFF Rcv Fail : Client Disconnected.");
                                    //모션 SW가 연결되어있으면
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        // 12V NG 리턴
                                        Store.getInstance().client.SendMessage("[CTACT,12V,OFF,NG]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("12V OFF Pass Fail : Server Disconnected");
                                    }
                                }
                            }
                            // 모션 SW로부터 <CTACT,VOL> (ANI 테스트용으로 쓴다고 함) 명령어를 받았을 때
                            else if (message == "<CTACT,VOL>")
                            {
                                //메시지를 받았을 당시에는 볼트체크 데이터가 없음
                                //핀볼트 데이터를 받은적이 없다고 변경
                                Store.getInstance().flagReceivePinVOL = false;

                                if (Store.getInstance().topSerial.IsOpen)
                                {
                                    //핀볼트 수행하기 위해 상판에 메시지 전달
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
                                        Store.getInstance().topSerial.Send("<CONTACT1><PIN,DATA>");
                                    }

                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("VOL CHECK Fail : UpBoard Disconnected");

                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        //VOL ERROR 리턴
                                        Store.getInstance().client.SendMessage("[CTACT,VOL,ERROR]");
                                    }
                                    else
                                    {
                                        Store.getInstance().twLog.WriteLog("VOL ERROR Pass Fail : Server Disconnected");
                                    }
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
                                        ChangeColour();
                                        MessageBox.Show("상판보드 통신 에러!\n상판보드를 확인하세요.", "E R R O R !", MessageBoxButton.OK, MessageBoxImage.Error);
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

                                    //상판 데이터를 모션SW가 연결되어있다면 전달
                                    if (Store.getInstance().client.IsConnected())
                                    {
                                        Store.getInstance().client.SendMessage(msgPinData);
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
                        }
                    }
                    Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    Store.getInstance().twLog.WriteLog("[모션SW Loop] Execption 발생" + e.Message);
                }
            }
        };

        //CTACT PINCHECK 횟수 체크하는 함수
        static void CountPinCheck(string str_Count)
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //초기 레지스트리에 저장된 PinCheck 카운트 횟수 불러오기
            //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            //string reg_str_PincheckCount = ((MainWindow)System.Windows.Application.Current.MainWindow).reg.GetValue("PINCHECKCOUNT").ToString();
            string reg_str_PincheckCount = reg.GetValue("PINCHECKCOUNT").ToString();

            //String을 int로 변환
            Int32.TryParse(reg_str_PincheckCount, out num_Count);

            //핀카운트 횟수 int 를
            num_Count++;

            //8자리 string으로 변환
            str_Count = num_Count.ToString("D8");

            //카운트한 만큼 8자리 string으로 더한다.
            Store.getInstance().twLog.WriteLog("PINCHECK COUNT:" + str_Count);

            //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            //((MainWindow)System.Windows.Application.Current.MainWindow).reg.SetValue("PINCHECKCOUNT", str_Count);
            reg.SetValue("PINCHECKCOUNT", str_Count);

            //});
        }
    }
}
