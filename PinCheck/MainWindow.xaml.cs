using PinCheck.Network;
using PinCheck.Util;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PinCheck
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        static CancellationTokenSource cancellationTasks = new CancellationTokenSource();
        static TaskCreationOptions options = TaskCreationOptions.LongRunning;

        public RegistryKey reg;
        private DispatcherTimer timer = new DispatcherTimer();

        //메인 윈도우 구성
        public MainWindow()
        {
            InitializeComponent();

            // 설정한 레지스트리 값들 가져오기
            GetRegistryData();

            windowMain.Title = "PinCheck(" + Store.getInstance().VER + ")";

            Task.Run(() =>
            {
                Init_Log();

                Store.getInstance().twLog.WriteLog("[핀체크] 프로그램 시작(" + Store.getInstance().VER + ")");
                Init_Comm();

                Task taskTop = new Task(loopReceiveTop, cancellationTasks.Token, options);
                taskTop.Start();
                Store.getInstance().twLog.WriteLog("[상판보드] 상판 루프 시작");

                Task taskServer = new Task(loopReceiveServer, cancellationTasks.Token, options);
                taskServer.Start();
                Store.getInstance().twLog.WriteLog("[모션SW] Waiting for a connection...");

                Task taskClient = new Task(loopReceiveClient, cancellationTasks.Token, options);
                taskClient.Start();
                Store.getInstance().twLog.WriteLog("[검사기OS] Waiting for a connection...");


            });

            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            reg.SetValue("SERVERIP", Properties.Settings.Default.serverIP);

            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            reg.SetValue("SERVERPORT", Properties.Settings.Default.serverPORT);

            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            reg.SetValue("CLIENTPORT", Properties.Settings.Default.clientPORT);

            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            reg.SetValue("PINCHECKCOUNT", Properties.Settings.Default.pinCheckCount);

            if (Properties.Settings.Default.CheckSumModeUse)
            {
                cb_CheckSumMode.IsChecked = true;
            }
            else
            {
                cb_CheckSumMode.IsChecked = false;
            }

            if (Properties.Settings.Default.ANIModeUse)
            {
                cb_ANIMode.IsChecked = true;
            }
            else
            {
                cb_ANIMode.IsChecked = false;
            }

            //프로그램 시작 시 자동으로 상판 연결되도록. 단, 기본 연결 포트는 가장 최근 연결 성공한 포트로 설정한다.
            if (Store.getInstance().topSerial.OpenComm(Properties.Settings.Default.rsCom, 115200, 8, StopBits.One, Parity.None, Handshake.None))
            {
                Store.getInstance().twLog.WriteLog("[상판보드] 상판 초기 연결 성공");

                if (cb_CheckSumMode.IsChecked == true)
                {
                    //체크섬 사용
                    string input1 = "<CONTACT0";
                    string checksum1 = GetHexChecksum(input1);
                    Store.getInstance().topSerial.Send("<CONTACT0^" + checksum1 + ">");

                    string input2 = "<EEP,READ,UP,ID";
                    string checksum2 = GetHexChecksum(input2);
                    Store.getInstance().topSerial.Send("<EEP,READ,UP,ID^" + checksum2 + ">");

                    string input3 = "<EEP,READ,UP";
                    string checksum3 = GetHexChecksum(input3);
                    Store.getInstance().topSerial.Send("<EEP,READ,UP^" + checksum3 + ">");
                }
                else
                {
                    //체크섬 미사용
                    Store.getInstance().topSerial.Send("<CONTACT0><EEP,READ,UP,ID><EEP,READ,UP>");
                }

                //연결 성공한 포트를 레지스트리에 저장함.
                reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                reg.SetValue("UPBOARDPORT", Properties.Settings.Default.rsCom);
                reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                reg.SetValue("PINID", textTopBoard_PinID.Content);
                reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);

                // UI 통신 연결 상태 버튼 색상 변경
                ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 200, 43));
                textTopBoard_PinID.Background = new SolidColorBrush(Color.FromRgb(43, 200, 43));
            }
            else
            {
                Store.getInstance().twLog.WriteLog("[상판보드] 상판 초기 연결 실패");

                reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                reg.SetValue("UPBOARDPORT", Properties.Settings.Default.rsCom);
                reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                reg.SetValue("PINID", textTopBoard_PinID.Content);
                reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);

                // UI 통신 연결 상태 버튼 색상 변경
                ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));
                textTopBoard_PinID.Background = new SolidColorBrush(Color.FromRgb(200, 43, 43));
            }

            timer.Interval = TimeSpan.FromMilliseconds(1000);    //시간간격 설정
            timer.Tick += new EventHandler(Timer_Tick);          //이벤트 추가
            timer.Start();  ///timer_Tick 에서 수행되고 , ONTimer 개념으로 1초 마다 이벤트로 들어옴...

        }

        //로그 스크롤 창 초기화
        public void Init_Log()
        {
            Store.getInstance().twLog.InitLog(logViewer, logScroll, 20);
            Store.getInstance().twLog.FileOpen();
        }

        //설정한 레지스트리 값들 가져오는 함수
        public void GetRegistryData()
        {
            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");

            if (reg.GetValue("UPBOARDPORT") != null)
            {
                Properties.Settings.Default.rsCom = reg.GetValue("UPBOARDPORT").ToString();
                comboTop.SelectedItem = Properties.Settings.Default.rsCom;
            }
            if (reg.GetValue("PINID") != null)
            {
                Properties.Settings.Default.pinId = reg.GetValue("PINID").ToString();
                textTopBoard_PinID.Content = Properties.Settings.Default.pinId;
            }
            if (reg.GetValue("PINCOUNT") != null)
            {
                Properties.Settings.Default.pinCount = reg.GetValue("PINCOUNT").ToString();
                textTopBoard_PinCount.Content = Properties.Settings.Default.pinCount;
            }
            if (reg.GetValue("PINCHECKCOUNT") != null)
            {
                Properties.Settings.Default.pinCheckCount = reg.GetValue("PINCHECKCOUNT").ToString();
            }
            if (reg.GetValue("SERVERIP") != null)
            {
                Properties.Settings.Default.serverIP = reg.GetValue("SERVERIP").ToString();
                ServerIP.Text = Properties.Settings.Default.serverIP;
            }
            if (reg.GetValue("SERVERPORT") != null)
            {
                Properties.Settings.Default.serverPORT = reg.GetValue("SERVERPORT").ToString();
                ServerPORT.Text = Properties.Settings.Default.serverPORT;
            }
            if (reg.GetValue("CLIENTPORT") != null)
            {
                Properties.Settings.Default.clientPORT = reg.GetValue("CLIENTPORT").ToString();
                ClientPORT.Text = Properties.Settings.Default.clientPORT;
            }
            if (reg.GetValue("CHECKSUMMODEUSE") != null)
            {
                if (reg.GetValue("CHECKSUMMODEUSE").ToString().CompareTo("1") >= 0)
                {
                    Properties.Settings.Default.CheckSumModeUse = true;
                }
                else if (reg.GetValue("CHECKSUMMODEUSE").ToString().CompareTo("0") >= 0)
                {
                    Properties.Settings.Default.CheckSumModeUse = false;
                }
            }
            if (reg.GetValue("ANIMODEUSE") != null)
            {
                if (reg.GetValue("ANIMODEUSE").ToString().CompareTo("1") >= 0)
                {
                    Properties.Settings.Default.ANIModeUse = true;
                }
                else if (reg.GetValue("ANIMODEUSE").ToString().CompareTo("0") >= 0)
                {
                    Properties.Settings.Default.ANIModeUse = false;
                }
            }
        }

        //프로그램 시작 시 상판 컴포트, SERVER 시작 포트 찾아주는 함수
        public void Init_Comm()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Store.getInstance().twLog.WriteLog("[핀체크] 컴포트 찾기 시작");
                comboTop.Items.Clear();
                Serial tempSerial = new Serial();
                foreach (string com in tempSerial.SearchPorts())
                {
                    comboTop.Items.Add(com);
                }
                Store.getInstance().twLog.WriteLog("[핀체크] 컴포트 찾기 끝");

                Store.getInstance().server.StartListening();
                Store.getInstance().twLog.WriteLog("[핀체크↔검사기OS] 통신 시작 - " + "[IP : 127.0.0.1] " + "[포트:" + reg.GetValue("CLIENTPORT").ToString() + "]");
                Store.getInstance().twLog.WriteLog("[SW모션↔핀체크] 통신 시작 - " + "[IP: " + reg.GetValue("SERVERIP").ToString() + "] " + "[포트: " + reg.GetValue("SERVERPORT").ToString() + "]");
            });
        }

        //프로그램 끌 때 루프들 종료시켜줌
        void MainWindowClosing(object sender, CancelEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[핀체크] 프로그램 종료");

            //프로그램 종료시 호출
            Store.getInstance().runningLoops = false;
            Store.getInstance().twLog.FileClosed();

            Store.getInstance().topSerial.CloseComm();
            Store.getInstance().client.Close();

            reg.SetValue("SERVERIP", ServerIP.Text);
            Properties.Settings.Default.serverIP = ServerIP.Text;
            reg.SetValue("SERVERPORT", ServerPORT.Text);
            Properties.Settings.Default.serverPORT = ServerPORT.Text;
            reg.SetValue("CLIENTPORT", ClientPORT.Text);
            Properties.Settings.Default.clientPORT = ClientPORT.Text;

            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            reg.SetValue("PINID", textTopBoard_PinID.Content);
            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
            reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);

            if (cb_CheckSumMode.IsChecked == true)
            {
                Properties.Settings.Default.CheckSumModeUse = true;
                reg.SetValue("CHECKSUMMODEUSE", "1");
            }
            else
            {
                Properties.Settings.Default.CheckSumModeUse = true;
                reg.SetValue("CHECKSUMMODEUSE", "0");
            }

            if (cb_ANIMode.IsChecked == true)
            {
                Properties.Settings.Default.ANIModeUse = true;
                reg.SetValue("ANIMODEUSE", "1");
            }
            else
            {
                Properties.Settings.Default.ANIModeUse = true;
                reg.SetValue("ANIMODEUSE", "0");
            }

            //Environment.Exit(0);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            this.Close();
        }

        //상판보드 연결하는 버튼
        void ConnectTopBoard(object sender, RoutedEventArgs e)
        {
            if (!Store.getInstance().topSerial.IsOpen)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (comboTop.SelectedIndex != -1)
                    {
                        string portName = comboTop.SelectedItem.ToString();

                        if (Store.getInstance().topSerial.OpenComm(portName, 115200, 8, StopBits.One, Parity.None, Handshake.None))
                        {
                            Store.getInstance().twLog.WriteLog("[상판보드] 상판 수동 연결 성공");

                            if (cb_CheckSumMode.IsChecked == true)
                            {
                                //체크섬 사용
                                string input1 = "<CONTACT0";
                                string checksum1 = GetHexChecksum(input1);
                                Store.getInstance().topSerial.Send("<CONTACT0^" + checksum1 + ">");

                                string input2 = "<EEP,READ,UP,ID";
                                string checksum2 = GetHexChecksum(input2);
                                Store.getInstance().topSerial.Send("<EEP,READ,UP,ID^" + checksum2 + ">");

                                string input3 = "<EEP,READ,UP";
                                string checksum3 = GetHexChecksum(input3);
                                Store.getInstance().topSerial.Send("<EEP,READ,UP^" + checksum3 + ">");
                            }
                            else
                            {
                                //체크섬 미사용
                                Store.getInstance().topSerial.Send("<CONTACT0><EEP,READ,UP,ID><EEP,READ,UP>");
                            }

                            //연결 성공한 포트를 레지스트리에 저장함.
                            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                            reg.SetValue("UPBOARDPORT", portName);
                            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                            reg.SetValue("PINID", textTopBoard_PinID.Content);
                            reg = Registry.CurrentUser.CreateSubKey("Software").CreateSubKey("PINCHECKSW").CreateSubKey("SETTING");
                            reg.SetValue("PINCOUNT", textTopBoard_PinCount.Content);

                            ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 200, 43));
                            textTopBoard_PinID.Background = new SolidColorBrush(Color.FromRgb(43, 200, 43));
                        }
                        else
                        {
                            Store.getInstance().twLog.WriteLog("[상판보드] 상판 수동 연결 실패");
                            ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));
                            textTopBoard_PinID.Background = new SolidColorBrush(Color.FromRgb(200, 43, 43));
                        }
                    }
                });
            }
        }

        //상판보드 연결 해제하는 버튼
        private void DisConnectTopBoard(object sender, RoutedEventArgs e)
        {
            Store.getInstance().topSerial.CloseComm();

            Store.getInstance().twLog.WriteLog("[상판보드] 상판 수동 연결 해제");

            ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));
            textTopBoard_PinID.Background = new SolidColorBrush(Color.FromRgb(200, 43, 43));
        }

        //상판에 핀체크 명령 날리는 버튼
        void GetPinData(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[상판보드] 상판 수동 핀체크 버튼 클릭");

            if (Store.getInstance().topSerial.IsOpen)
            {
                if (cb_CheckSumMode.IsChecked == true)
                {
                    //체크섬 사용
                    string input1 = "<CONTACT1";
                    string checksum1 = GetHexChecksum(input1);
                    Store.getInstance().topSerial.Send("<CONTACT1^" + checksum1 + ">");

                    string input2 = "<PIN,ALL";
                    string checksum2 = GetHexChecksum(input2);
                    Store.getInstance().topSerial.Send("<PIN,ALL^" + checksum2 + ">");
                }
                else
                {
                    //체크섬 미사용
                    Store.getInstance().topSerial.Send("<CONTACT1><PIN,ALL>");
                }
            }
            else
            {
                Store.getInstance().twLog.WriteLog("PINCHECK Fail : UpBoard Disconnected.");
            }
        }

        //상판에 볼트체크 명령 날리는 버튼
        private void GetVolData(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[상판보드] 상판 수동 볼트체크 버튼 클릭");

            if (Store.getInstance().topSerial.IsOpen)
            {
                if (cb_CheckSumMode.IsChecked == true)
                {
                    //체크섬 사용
                    string input1 = "<CONTACT1";
                    string checksum1 = GetHexChecksum(input1);
                    Store.getInstance().topSerial.Send("<CONTACT1^" + checksum1 + ">");

                    string input2 = "<PIN,DATA";
                    string checksum2 = GetHexChecksum(input2);
                    Store.getInstance().topSerial.Send("<PIN,DATA^" + checksum2 + ">");
                }
                else
                {
                    //체크섬 미사용
                    Store.getInstance().topSerial.Send("<CONTACT1><PIN,DATA>");
                }
            }
            else
            {
                Store.getInstance().twLog.WriteLog("PINCHECK Fail : UpBoard Disconnected.");
            }
        }

        //상판에 메시지 전송하는 버튼
        private void SendUpboardMessage(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[상판보드] 상판 수동 메시지 전송 버튼 클릭");

            if (Store.getInstance().topSerial.IsOpen)
            {
                if (textBoxTopBoard_Message.Text.Contains("<"))
                {
                    if (cb_CheckSumMode.IsChecked == true)
                    {
                        //체크섬 사용
                        //에디트로 받은 메시지
                        string message;
                        message = textBoxTopBoard_Message.Text;

                        //< > 제거한 순수 메시지
                        string replacemessage;
                        replacemessage = message.Replace("<", "").Replace(">", "");

                        //체크섬용 메시지 ex)<CTACT1> → <CTACT1
                        string checkSummessage;
                        checkSummessage = message.Replace(">", "");

                        //^ 기준으로 CheckSum 구하기
                        string input1 = checkSummessage;
                        string checksum1 = GetHexChecksum(input1);

                        // "<" + str + "^" + CheckSum + ">" 로 보낼 문자 재조합
                        Store.getInstance().topSerial.Send("<" + replacemessage + "^" + checksum1 + ">");
                    }
                    else
                    {
                        //체크섬 미사용
                        string message;
                        message = textBoxTopBoard_Message.Text;
                        Store.getInstance().topSerial.Send(message);
                    }
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("Message Send Fail : 상판 메시지는 프로토콜 < >만 전송할 수 있습니다..");
                }
            }
            else
            {
                Store.getInstance().twLog.WriteLog("PINCHECK Fail : UpBoard Disconnected.");
            }
        }

        //모션SW에 핀체크 OK 메시지 날리는 버튼
        private void ServerPINCHECKOKTest(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[핀체크→모션SW] 수동 PINCHECK OK 버튼 클릭");

            if (Store.getInstance().client.IsConnected())
            {
                //PINCHECK OK를 모션SW로 전달
                Store.getInstance().client.SendMessage("[CTACT,PINCHECK,OK]");
            }
            else
            {
                Store.getInstance().twLog.WriteLog("PINCHECK OK Rcv Fail : Server Disconnected.");
            }
        }

        //모션SW에 핀체크 NG 메시지 날리는 버튼
        private void ServerPINCHENGTest(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[핀체크→모션SW] 수동 PINCHECK NG 버튼 클릭");

            if (Store.getInstance().client.IsConnected())
            {
                Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
            }
            else
            {
                Store.getInstance().twLog.WriteLog("PINCHECK NG Rcv Fail : Server Disconnected.");
            }
        }

        //검사기에 12V ON 명령 날리는 버튼
        private void Client12VONTest(object sender, RoutedEventArgs e)
        {
            bool bRet;
            bRet = Store.getInstance().server.IsSocketConnected();

            //Store.getInstance().twLog.WriteLog("Client Connect Boolean : " + bRet);

            Store.getInstance().twLog.WriteLog("[핀체크→검사기OS] 수동 12V ON 버튼 클릭");

            if (Store.getInstance().server.IsSocketConnected())
            {
                Store.getInstance().server.Send("<CTACT,12V,ON>");

                Store.getInstance().waitReceive12VON = true;  //메시지 응답 대기
                Store.getInstance().flagReceive12VON = false; // 12V ON Flag

                Task.Run(() =>
                {

                    TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                    Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                    maxD = TimeSpan.FromSeconds(5); //  Time out 5초 기다리겠다....  설정의 의미
                    sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                    while (!Store.getInstance().flagReceive12VON) // 기다리기 시작
                    {
                        Thread.Sleep(10);
                        if (sw1.Elapsed > maxD)
                        {
                            sw1.Stop();
                            Store.getInstance().waitReceive12VON = false;
                            if (Store.getInstance().client.IsConnected())
                            {
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
                Store.getInstance().twLog.WriteLog("TEST 12V ON Send Fail : Client Diconnected");

                if (Store.getInstance().client.IsConnected())
                {
                    Store.getInstance().client.SendMessage("[CTACT,12V,ON,NG]");
                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("12V ON NG Pass Fail : Server Disconnected");
                }
            }
        }

        //검사기에 12V OFF 명령 날리는 버튼
        private void Client12VOFFTest(object sender, RoutedEventArgs e)
        {
            bool bRet;
            bRet = Store.getInstance().server.IsSocketConnected();

            //Store.getInstance().twLog.WriteLog("Client Connect Boolean : " + bRet);

            Store.getInstance().twLog.WriteLog("[핀체크→검사기OS] 수동 12V OFF 버튼 클릭");

            if (Store.getInstance().server.IsSocketConnected())
            {
                Store.getInstance().server.Send("<CTACT,12V,OFF>");

                Store.getInstance().waitReceive12VOFF = true;  //메시지 응답 대기
                Store.getInstance().flagReceive12VOFF = false; // 12V OFF Flag

                Task.Run(() =>
                {

                    TimeSpan maxD; // 몇 초 기다릴 것인지 클래스 변수 선언
                    Stopwatch sw1;  //응답 대기 만들려고 클래서 변수 선언

                    maxD = TimeSpan.FromSeconds(5); //  Time out 5초 기다리겠다....  설정의 의미
                    sw1 = Stopwatch.StartNew(); //시간 재기... 타임아웃 측정위해서 시간을 실제 시작해라

                    while (!Store.getInstance().flagReceive12VOFF) // 기다리기 시작
                    {
                        Thread.Sleep(10);
                        if (sw1.Elapsed > maxD)
                        {
                            sw1.Stop();
                            Store.getInstance().waitReceive12VOFF = false;
                            if (Store.getInstance().client.IsConnected())
                            {
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
                Store.getInstance().twLog.WriteLog("TEST 12V OFF Send Fail : Client Diconnected");

                if (Store.getInstance().client.IsConnected())
                {
                    Store.getInstance().client.SendMessage("[CTACT,12V,OFF,NG]");
                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("12V OFF NG Pass Fail : Server Disconnected");
                }
            }
        }

        //검사기에 핀데이터 날리는 버튼
        private void ClientPINCHECKTest(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[핀체크→검사기OS] 수동 핀체크 데이터 전송 버튼 클릭");

            if (Store.getInstance().server.IsSocketConnected())
            {
                Store.getInstance().server.Send("<CTACT,PINCHECK,1111111111111111111111111111111111111111111111111111111111111111111111111111111GGGG11NNG111NN1111111111GGGGNNN1111111111111GGGGG1N1111>");
            }
            else
            {
                Store.getInstance().twLog.WriteLog("TEST PIN CHECK DATA Send Fail : Client Disconnected");

                if (Store.getInstance().client.IsConnected())
                {
                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("PINCHECK NG Pass Fail : Server Disconnected");
                }
            }
        }

        //검사기에 핀볼트값 날리는 버튼
        private void ClientVOLCHECKTest(object sender, RoutedEventArgs e)
        {
            Store.getInstance().twLog.WriteLog("[핀체크→검사기OS] 수동 핀볼트 데이터 전송 버튼 클릭");

            if (Store.getInstance().server.IsSocketConnected())
            {
                Store.getInstance().server.Send("[CTACT,VOL,0.12,0.11,0.11,0.12,N,N,N,N,N,N,N,N,N,N,N,N,0.11,0.12,0.12,0.11,N,N,N,N,N,N,N,N,N,N,N,N,0.12,0.11,0.12,0.12,N,N,N,N,N,N,N,N,N,N,N,N,0.12,0.12,0.11,0.12,N,N,N,N,N,N,N,N,N,N,N,N,0.16,0.18,0.19,0.17,3.30,N,N,N,N,N,N,N,N,N,N,G,G,G,G,N,N,N,N,G,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,G,G,G,G,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,G,G,G,G,G,3.30,N,N,N,N,0.08]");
            }
            else
            {
                Store.getInstance().twLog.WriteLog("TEST VOL CHECK DATA Send Fail : Client Disconnected");

                if (Store.getInstance().client.IsConnected())
                {
                    Store.getInstance().client.SendMessage("[CTACT,PINCHECK,NG]");
                }
                else
                {
                    Store.getInstance().twLog.WriteLog("VOL NG Pass Fail : Server Disconnected");
                }
            }
        }

        //현재 메인 인터페이스의 상태들 색깔로 표시해주는 기능
        private void Timer_Tick(object sender, EventArgs e) //GUI 메인 창에 상태 상부 / 하부 타이틀 및 상우 아이콘  그림 처리....
        {
            try
            {
                //상판보드 통신상태 표시
                if (Store.getInstance().topSerial != null)
                {
                    if (Store.getInstance().topSerial.IsOpen)
                    {
                        ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 200, 43));
                        textTopBoard_PinID.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 200, 43));
                    }
                    else
                    {
                        ConnectUpBoard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));
                        textTopBoard_PinID.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));
                    }
                }
                //검사기OS 통신상태 표시
                if (Store.getInstance().server != null)
                {
                    if (Store.getInstance().server.isClientConnect && Store.getInstance().server.IsSocketConnected())
                    {
                        ConnectMTSW.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 200, 43));
                    }
                    else
                    {
                        ConnectMTSW.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));
                    }
                }
                //모션SW와의 통신상태 표시
                if (Store.getInstance().client != null)
                {
                    if (Store.getInstance().client.IsConnected())
                    {
                        ConnectMotionSW.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 200, 43));
                    }
                    else
                    {
                        ConnectMotionSW.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 43, 43));

                    }
                }
            }
            catch (Exception)
            {
            }
        }

        //창 최소화
        //private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        //{
        //    this.WindowState = WindowState.Minimized;
        //}
    }
}
