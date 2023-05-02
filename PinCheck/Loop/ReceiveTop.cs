using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO.Ports;

namespace PinCheck
{
    public partial class MainWindow : System.Windows.Window
    {
        static Action loopReceiveTop = () =>
        {
            while (Store.getInstance().runningLoops)
            {
                try
                {
                    if (!Store.getInstance().topSerial.IsOpen && Store.getInstance().TopboardDisconnectFlag)
                    {
                        Store.getInstance().twLog.WriteLog("[상판보드] : 연결 끊김");
                        Store.getInstance().TopboardDisconnectFlag = false;
                    }
                    if (!Store.getInstance().topSerial.IsOpen && !Store.getInstance().TopboardDisconnectFlag)
                    {
                        //Store.getInstance().twLog.WriteLog("[상판보드] 연결성공(테스트)");
                        if (Store.getInstance().topSerial.OpenComm(Properties.Settings.Default.rsCom, 115200, 8, StopBits.One, Parity.None, Handshake.None))
                        {
                            Store.getInstance().twLog.WriteLog("[상판보드] : 재연결 성공");
                            Store.getInstance().TopboardDisconnectFlag = true;
                        }
                    }
                    if (Store.getInstance().topSerial.isMessage())
                    {
                        string origin_message = Store.getInstance().topSerial.getMessage();
                        Store.getInstance().twLog.WriteLog("[상판보드→핀체크(Loop)] " + origin_message);

                        // 상판으로부터 오는 메시지, <> [] 기준으로 재처리
                        string replace_message = origin_message.Replace("[", "").Replace("]", "").Replace("<", "").Replace(">", "");

                        string[] msgData = replace_message.Split(new char[] { ',' });
                        int msgCount = msgData.Length;

                        string checksum_msg;    // 수신 메시지 중에 checksum ^ 뒤에 2자리만 추출한 변수
                        string checksum_msg2;   // 새로 계산한 체크섬
                        string[] replace_message_Split; // ^를 기준으로 자른 앞쪽 문자열
                        string checksum_Replace_message;    // 체크섬 메시지 변환용 임시 변수([CONTACT0 같이 뒤 >나 ]를 제거한 변수)

                        //체크섬 모드 사용시....
                        if (Properties.Settings.Default.CheckSumModeUse == true)
                        {
                            //수신 메시지의 checksum message 새로 계산하기 (<XXXXX 까지)
                            checksum_msg = replace_message.Substring(replace_message.IndexOf('^') + 1).Trim();
                            checksum_msg.Trim();

                            replace_message_Split = origin_message.Split('^');  // ^를 기준으로 문자를 자름
                            checksum_Replace_message = replace_message_Split[0];    //replace message Split의 0번째 들어간 메시지를 checksum Replace message로
                            checksum_msg2 = GetHexChecksum(checksum_Replace_message);
                            checksum_msg2.Trim();

                            //위 두 메시지 비교하기
                            if (checksum_msg == checksum_msg2)
                            {
                                //같으면 넘어가기
                                Store.getInstance().twLog.WriteLog("Checksum Compare Success : Checksum Data Same." + checksum_msg + checksum_msg2);
                            }
                            else
                            {
                                //다르면 체크섬 메시지 다르다는 메시지 출력하기
                                Store.getInstance().twLog.WriteLog("Checksum Compare Fail : Checksum Data Different." + checksum_msg + checksum_msg2);
                            }
                        }
                        //메시지가 100자 넘어가면, 상판 핀체크 데이터이다.
                        if (replace_message.Length > 100)
                        {
                            if (msgData[0] == "VOL")
                            {
                                Store.getInstance().pinVOL = replace_message;
                                Store.getInstance().flagReceivePinVOL = true;

                                //체크섬 사용
                                if (Properties.Settings.Default.CheckSumModeUse == true)
                                {
                                    Store.getInstance().pinVOL = replace_message.Substring(0, replace_message.Length - 3);

                                    string input1 = "<CONTACT0";
                                    string checksum1 = GetHexChecksum(input1);
                                    Store.getInstance().topSerial.Send("<CONTACT0^" + checksum1 + ">");
                                }
                                //체크섬 미사용
                                else
                                {
                                    Store.getInstance().topSerial.Send("<CONTACT0>");
                                }
                            }
                            else
                            {
                                //<PIN,ALL> 회신
                                if (replace_message.Length >= 134)
                                {
                                    Store.getInstance().pinALL = replace_message;
                                    Store.getInstance().flagReceivePinALL = true;
                                    SetPINUI(replace_message);

                                    //체크섬 사용
                                    if (Properties.Settings.Default.CheckSumModeUse == true)
                                    {
                                        Store.getInstance().pinALL = replace_message.Substring(0, replace_message.Length - 3);
                                        string input1 = "<CONTACT0";
                                        string checksum1 = GetHexChecksum(input1);
                                        Store.getInstance().topSerial.Send("<CONTACT0^" + checksum1 + ">");
                                    }
                                    //체크섬 미사용
                                    else
                                    {
                                        Store.getInstance().topSerial.Send("<CONTACT0>");
                                    }
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("[에러] 수신 핀체크 데이터 이상");
                                }
                            }
                        }
                        // 상판 Recv 메시지에 EEP,WRITE,UP 이 포함되어 있으면
                        else if (replace_message.Contains("EEP,WRITE,UP"))
                        {
                            if (replace_message.Contains("ERROR"))
                            {
                                Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP WRITE FAIL!");

                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                                }
                            }
                            else
                            {
                                Store.getInstance().flagReceivePinCount = true;
                                DisplayPincount1(replace_message);
                                //핀카운트 플래그를 참으로
                            }
                        }
                        else if (replace_message.Contains("EEP,WRTIE,UP"))
                        {
                            if (replace_message.Contains("ERROR"))
                            {
                                Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP WRITE FAIL!");

                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                                }
                            }
                            else
                            {
                                Store.getInstance().flagReceivePinCount = true;
                                DisplayPincount2(replace_message);
                                //핀카운트 플래그를 참으로
                            }
                        }
                        // 상판 Recv 메시지에 EEP,READ,UP,ID 가 포함되어 있으면
                        else if (replace_message.Contains("EEP,READ,UP,ID"))
                        {
                            if (replace_message.Contains("ERROR"))
                            {
                                Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP ID READ FAIL!");

                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                                }
                            }
                            else
                            {
                                DisplayPinID(replace_message);
                            }
                        }
                        // 상판 Recv 메시지에 EEP,READ,UP,0 가 포함되어 있으면
                        else if (replace_message.Contains("EEP,READ,UP,"))
                        {
                            if (replace_message.Contains("ERROR"))
                            {
                                Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP READ FAIL!");

                                if (Store.getInstance().server.IsSocketConnected())
                                {
                                    Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                                }
                                else
                                {
                                    Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                                }
                            }
                            else
                            {
                                DisplayPinStatus(replace_message);
                            }
                        }
                        // 상판 Recv 메시지에 QD CONTACT VER 가 포함되어 있으면
                        else if (replace_message.Contains("QD"))
                        {
                            Store.getInstance().flagReceivePinVer = true;

                            string upboardVer = ExtractVersion(replace_message);
                            if (Store.getInstance().server.IsSocketConnected())
                            {
                                Store.getInstance().server.Send("[WHO," + upboardVer + "]");
                            }
                            else
                            {
                                Store.getInstance().twLog.WriteLog("Upboard Verion Pass Fail : Client Disconnected");
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    // 프로그램 종료시 상판 COM PORT 닫히지 않는 현상 있음.
                    // 이때 Store에 익셉션 로그 남겨보기
                    Store.getInstance().twLog.WriteLog("[상판보드 Loop] Execption 발생" + e.Message);
                }
            }
        };

        //메인 인터페이스 핀 컬러 채우는 함수
        static void SetPINUI(string data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                char temp;
                if (data.Length == 134 || data.Length == 137)
                {
                    for (int i = 0; i < 134; i++)
                    {
                        int number = i + 1;
                        temp = data[i];
                        string nameBtn = "btn" + number.ToString("000");
                        Button button = (Button)((MainWindow)System.Windows.Application.Current.MainWindow).pinGrid.FindName(nameBtn);

                        if (button != null)
                        {
                            if (temp == '0')
                            {
                                button.Background = new SolidColorBrush(Color.FromRgb(150, 0, 0));
                                button.BorderBrush = new SolidColorBrush(Color.FromRgb(150, 0, 0));
                            }
                            else if (temp == '1')
                            {
                                button.Background = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                                button.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                            }
                            else if (temp == 'N')
                            {
                                button.Background = new SolidColorBrush(Color.FromRgb(120, 120, 120));
                                button.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 120, 120));
                            }
                            else if (temp == 'G')
                            {
                                button.Background = new SolidColorBrush(Color.FromArgb(255, 112, 48, 160));
                                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 112, 48, 160));
                            }
                            else
                            {
                                button.Background = Brushes.Black;
                                button.BorderBrush = Brushes.Black;
                            }
                        }
                    }
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("상판 데이터 이상 = " + data.Length);
                }
            });
        }

        // 핀 수명 Count 출력 기능 추가
        static void DisplayPincount1(string message)
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            Application.Current.Dispatcher.Invoke(() =>
            {
                Label textTopBoard_PinCount = (Label)((MainWindow)System.Windows.Application.Current.MainWindow).textTopBoard_PinCount;

                // EEP,WRITE,UP,를 Replace한다.
                message = message.Replace("EEP,WRITE,UP,", "");

                string message1 = message.Substring(0, 8);
                string message2 = message.Substring(8, 8);
                string message3 = message.Substring(16, 8);
                string message4 = message.Substring(24, 8);

                if (message1.Equals(message2) && message1.Equals(message3) && message1.Equals(message4))
                {
                    // 나머지(ex)00000001)를 메인 UI의 핀수명에 반영한다.
                    textTopBoard_PinCount.Content = message.Substring(0, 8);

                    Store.getInstance().twLog.WriteLog("[상판→핀체크] 핀 수명 Count 완료: " + message.Substring(0, 8));
                    //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                    //((MainWindow)System.Windows.Application.Current.MainWindow).reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);
                    reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP WRITE FAIL!");

                    if (Store.getInstance().server.IsSocketConnected())
                    {
                        Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                    }
                    else
                    {
                        Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                    }
                }
            });
        }
        static void DisplayPincount2(string message)
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            Application.Current.Dispatcher.Invoke(() =>
            {
                Label textTopBoard_PinCount = (Label)((MainWindow)System.Windows.Application.Current.MainWindow).textTopBoard_PinCount;

                // EEP,WRITE,UP,를 Replace한다.
                message = message.Replace("EEP,WRTIE,UP,", "");

                string message1 = message.Substring(0, 8);
                string message2 = message.Substring(8, 8);
                string message3 = message.Substring(16, 8);
                string message4 = message.Substring(24, 8);

                if (message1.Equals(message2) && message1.Equals(message3) && message1.Equals(message4))
                {
                    // 나머지(ex)00000001)를 메인 UI의 핀수명에 반영한다.
                    textTopBoard_PinCount.Content = message.Substring(0, 8);

                    Store.getInstance().twLog.WriteLog("[상판→핀체크] 핀 수명 Count 완료: " + message.Substring(0, 8));
                    //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                    //((MainWindow)System.Windows.Application.Current.MainWindow).reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);
                    reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP WRITE FAIL!");

                    if (Store.getInstance().server.IsSocketConnected())
                    {
                        Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                    }
                    else
                    {
                        Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                    }
                }
            });
        }

        // 핀 ID 출력 기능 추가
        static void DisplayPinID(string message)
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            Application.Current.Dispatcher.Invoke(() =>
            {
                Label textTopBoard_PinID = (Label)((MainWindow)System.Windows.Application.Current.MainWindow).textTopBoard_PinID;

                // EEP,READ,UP,ID를 Replace한다.
                message = message.Replace("EEP,READ,UP,ID,", "");
                message = message.Substring(0, 9);

                // 나머지(ex)00000001)를 메인 UI의 핀 ID 인터페이스에 반영한다.
                textTopBoard_PinID.Content = message;

                Store.getInstance().twLog.WriteLog("[상판→핀체크] 핀 ID READ 완료: " + message);

                //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                //((MainWindow)System.Windows.Application.Current.MainWindow).reg.SetValue("PINID", textTopBoard_PinID.Content);
                reg.SetValue("PINID", textTopBoard_PinID.Content);
            });
        }

        // 현재 핀 Count 횟수 출력 기능 추가
        static void DisplayPinStatus(string message)
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            Application.Current.Dispatcher.Invoke(() =>
            {
                Label textTopBoard_PinCount = (Label)((MainWindow)System.Windows.Application.Current.MainWindow).textTopBoard_PinCount;

                // EEP,READ,UP,를 Replace한다.
                message = message.Replace("EEP,READ,UP,", "");

                string message1 = message.Substring(0, 8);
                string message2 = message.Substring(8, 8);
                string message3 = message.Substring(16, 8);
                string message4 = message.Substring(24, 8);

                if (message1.Equals(message2) && message1.Equals(message3) && message1.Equals(message4))
                {
                    // 나머지(ex)00000001)를 메인 UI의 핀 ID 인터페이스에 반영한다.
                    textTopBoard_PinCount.Content = message.Substring(0, 8);

                    Store.getInstance().twLog.WriteLog("[상판→핀체크] 핀 Count READ 완료: " + message.Substring(0, 8));
                    //((MainWindow)System.Windows.Application.Current.MainWindow).reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                    //((MainWindow)System.Windows.Application.Current.MainWindow).reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);
                    reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("[상판→핀체크] EEP READ FAIL!");

                    if (Store.getInstance().server.IsSocketConnected())
                    {
                        Store.getInstance().server.Send("[CTACT,PINCOUNT,NG]");
                    }
                    else
                    {
                        Store.getInstance().twLog.WriteLog("PIN COUNT Fail : Client Disconnected");
                    }
                }
            });
        }

        //체크섬 계산하는 함수
        static string GetHexChecksum(string input)
        {
            int sum = 0;
            foreach (char c in input)
            {
                sum += (int)c;
            }
            string hexValue = sum.ToString("X");
            return hexValue.Substring(hexValue.Length - 2);
        }

        //문자열에서 . 이후 문자만 추출하는 함수
        public static string ExtractVersion(string fullVersion)
        {
            // 버전 문자열에서 "VER " 다음의 숫자를 추출합니다.
            string verKeyword = "VER ";
            int startIndex = fullVersion.IndexOf(verKeyword) + verKeyword.Length;
            if (startIndex < 0 || startIndex >= fullVersion.Length)
            {
                // "VER "가 없거나 문자열의 끝에 있는 경우 예외를 발생시킵니다.
                throw new ArgumentException("Invalid version string.", nameof(fullVersion));
            }

            // 버전 문자열에서 "VER " 다음의 첫 번째 ' ' 이전의 문자열만 추출합니다.
            int endIndex = fullVersion.IndexOf(' ', startIndex);
            if (endIndex < 0)
            {
                // 문자열의 끝까지 공백이 없는 경우, 문자열 끝까지 추출합니다.
                endIndex = fullVersion.Length;
            }

            return fullVersion.Substring(startIndex, endIndex - startIndex);
        }
    }
}
