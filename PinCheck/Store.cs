using PinCheck.Network;
using PinCheck.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinCheck
{
    class Store
    {
        private static Store instance;

        public static Store getInstance()
        {
            if (instance == null) instance = new Store();
            return instance;
        }

        private Store()
        {
        }

        public string VER = "1.2.6 - 20230502";
        //20220919 1.0.1 lys 프로그램 개발 시작 
        //20220919 1.0.2 ljm 메인 인터페이스 내 UI 개선, Test 버튼 및 기능 구현, 베타2 컨택 시나리오에 맞게 소켓 통신 메시지 프로토콜 추가 등등...
        //20220923 1.0.2 ljm 메인 인터페이스 UI 개선 마무리, Test 버튼 기능 보강, 프로토콜 보강
        //20220926 1.0.3 ljm 각 서버, 클라이언드 데모 프로그램 연동을 통한 테스트, 한주측 PINCHECK OK시 PIN DATA 넘겨주도록 추가, 핀체크↔서버 간 System.byte[]로 메시지 Send, Recv 되던 현상 수정
        //20220929 1.0.4 ljm 프로토콜간 타임아웃, NG 상황 등에 대비한 예외처리 보강
        //20221013 1.0.5 ljm 각 IP, PORT 레지스트리를 통해 변경 가능하게 바꿈.
        //20221014 1.0.5 ljm 상판 연결 안됐을 때 OK, NG 시 PIN DATA 받으려는 경우 프로그램 무언정지 발생 가능성이 있어 전체 주석 처리함. 일단 OK나 NG만 던져주는걸로...
        //20221018 1.0.6 ljm EXCO 테스트중
        //20221024 1.0.6 ljm VOL 잘 못 날아가는 현상 수정
        //20221025 1.0.7 ljm 상판에 메시지 날릴 수 있는 박스 추가, 아이콘 변경
        //20221025 1.0.8 ljm 프로그램 종료 시 Comm, Client, Server 통신 모두 끊기도록 (정상 종료) 수정.
        //20221025 1.0.9 ljm 한주에서 재컴파일.
        //20221025 1.1.0 ljm 서버 클라이언트 핸들러 자동으로 잡도록 수정, 클라이언트 갑작스러운 종료시에도 감지하도록 수정.
        //20221208 1.1.1 ljm 핀 ID, 수명 Count 출력 대응 함수 추가, UI 수정(상판 핀 Count, ID 출력 등...) + 체크섬 테스트중...
        //20221209 1.1.2 ljm 체크섬 함수 구현, UI 상판 체크섬 테스트 기능 추가, Message Send시 반영하도록 수정, CheckSumMode 체크박스 추가.
        //20221219 1.1.3 ljm ANI 모션SW랑 프로토콜 수정 협의한 점 반영...
        //20221223 1.1.4 ljm ANI, 검사기 12V ON, OFF 프로토콜 협의점 수정...
        //20221226 1.1.5 ljm 상판 Pin Count 수명 관리 기능 변경ing...
        //20221226 1.1.6 ljm 상판 Pin Count 수명 관리 기능 변경 완...
        //20230127 1.1.7 ljm Pin Count 시퀀스 중 Error Case 예외처리 보강...
        //20230208 1.1.8 ljm 검사기OS 통해서 <CTACT,PINCHECK> 받는거 추가...
        //20230209 1.1.9 ljm 컨택 핀체크 시퀀스 간 상판 통신에 CheckSum 기능 반영하도록 수정...
        //20230217 1.2.0 ljm 모션 SW 통신 때, 서버가 핸들러 잡지 못하는거 방지... + PINCOUNT 예외처리 보강
        //20230227 1.2.1 ljm 상판보드 Disconnectde 감지, 자동 재연결 기능 구현, 로그 항목 세부적으로 분류
        //20230228 1.2.2 ljm 상판보드 체크섬 옵션 비활성화시 시리얼 메시지 잘리는 현상 수정
        //20230315 1.2.3 ljm 상판보드 중간에 통신 끊겼을 때 While 무한루프 빠지는 현상 수정
        //20230316 1.2.4 ljm 상판보드 PinCount 기능 보강
        //20230322 1.2.5 ljm ANI 2차 투자분 <ETC,VERSION>에 대응하는 상판보드 버전 체크 프로토콜 추가
        //20230502 1.2.6 ljm 상판보드 타임아웃시 메시지 박스 출력하도록 수정

        //모든 루프 종료변수, 프로그램 종료 시 false로 바꿔야 함
        public bool runningLoops = true;

        public TWLog twLog = new TWLog();
        public Serial topSerial = new Serial();
        public mmtServer server = new mmtServer();
        public CmdClient client = new CmdClient();

        public bool flagReceivePinALL = false;
        public bool flagReceivePinOK = false;
        public bool waitReceivePinALL = false;
        public bool flagReceivePinVOL = false;
        public bool flagReceive12VON  = false;
        public bool waitReceive12VON = false;
        public bool flagReceive12VOFF = false;
        public bool waitReceive12VOFF = false;
        public bool flagReceivePinCount = false;
        public bool flagReceivePinVer = false;

        public bool flagClientDisconnectLogging = false;
        public bool ClientDisconnectFlag = false;
        public bool TopboardDisconnectFlag = false;

        public string pinALL = "";
        public string pinVOL = "";
    }
}
