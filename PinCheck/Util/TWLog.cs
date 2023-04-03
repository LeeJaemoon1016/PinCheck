using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PinCheck.Util
{
    public partial class TWLog
    {
        private int MaxLine = 20;
        private TextBox textBox = null;
        private ScrollViewer scrollLog = null;

        bool bRun = false;
        FileStream fs;
        StreamWriter sw;
        String timeOld;
        String sDirPath;
        String path;

        public void InitLog(TextBox tbox , ScrollViewer scview, int LineMax)
        {
            MaxLine = LineMax;
            textBox = tbox;
            scrollLog = scview;
        }

        public bool WriteLog(string txt)
        {
            try
            {
                string message = "[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] " + txt;
                WriteFile(message);

                if ((textBox != null) && (scrollLog != null))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        textBox.Text += System.Environment.NewLine + message;

                        int lines = textBox.LineCount;
                        if (lines > MaxLine)
                        {
                            StringBuilder text = new StringBuilder();
                            for (int i = lines - MaxLine; i < lines; i++)
                            {
                                text.Append(textBox.GetLineText(i));
                            }
                            textBox.Text = text.ToString();
                        }
                        scrollLog.ScrollToBottom();
                    });
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        //파일 사용중 체크
        public bool CheckFileLocked(string filePath)
        {
            try
            {
                FileInfo file = new FileInfo(filePath);

                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        public void FileOpen()
        {
            bRun = true;
            bool fileExists = false;

            String timeNow = System.DateTime.Now.ToString("yyyy/MM/dd");
            sDirPath = AppDomain.CurrentDomain.BaseDirectory + "Log";

            DirectoryInfo di = new DirectoryInfo(sDirPath);
            if (!di.Exists)
            {
                Directory.CreateDirectory(sDirPath);
            }

            path = sDirPath + $"\\[PinCheck]{timeNow}_Log.txt";

            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Exists)
            {
                fileExists = true;
                if (CheckFileLocked(path))
                {
                    //다른 프로세스가 파일 사용 중, 파일명을 변경해야 한다.
                    for(int i=0; i<10; i++) //설마 10번 넘겠어?
                    {
                        path = sDirPath + $"\\[PinCheck]{timeNow}_Log[" + i.ToString() + "].txt";
                        FileInfo tempInfo = new FileInfo(path);

                        if (tempInfo.Exists)
                        {
                            if (!CheckFileLocked(path))
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                        
                    }
                }
            }
            else
            {
                fileExists = false;
            }

            try
            {
                fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                timeOld = timeNow;

                if (!fileExists)
                {
                    sw.Write("");
                    sw.Flush();
                }
            }
            catch (IOException)
            {
                bRun = false;
                return;
            }
            bRun = false;
        }

        public void FileClosed()
        {
            while (bRun) { System.Threading.Thread.Sleep(10); }
            sw.Close();
            fs.Close();
            sw.Dispose();
            fs.Dispose();
        }

        private void WriteFile(string txt)
        {
            bRun = true;
            String timeNow = System.DateTime.Now.ToString("yyyy/MM/dd");

            if (timeNow != timeOld)
            {
                sw.Close();
                fs.Close();
                sw.Dispose();
                fs.Dispose();

                path = sDirPath + $"\\[PinCheck]{timeNow}_Log.txt";
                //FileOpen때처럼 존재여부, 쓰기권한 체크를 하지 않는 이유는 프로그램이 제일먼저 생성해 사용하기 때문
                //물론 추가해주면 좋음

                try
                {
                    fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                    timeOld = timeNow;
                }
                catch (IOException)
                {
                    return;
                }
            }
            sw.WriteLine(txt);
            sw.Flush();

            bRun = false;
        }
    }
}
