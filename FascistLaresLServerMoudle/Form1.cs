using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FascistLaresLServerMoudle
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.btn_start.Enabled = true;
            this.btn_stop.Enabled = false;
        }

        DateTime lastReportTime;//客户端心跳检查最后响应时间
        int heart_beat = 0;
        List<string> mail_Copy = new List<string>();
        string disconnect_reason = "";

        System.Threading.Timer timer;

        private void Form1_Load(object sender, EventArgs e)
        {
            timer = new System.Threading.Timer(new TimerCallback(progress), null, 0, -1);
            mail_Copy.Add("42434031@qq.com");
        }

        public List<Socket> clientList = new List<Socket>();

        private void acceptClient(object server)
        {
            Socket socket = server as Socket;
            while (true)
            {
                Socket clientSocket = socket.Accept();//接收客户端连接
                clientList.Add(clientSocket);
                //timer1.Start();
                //timer1.Enabled = true;
                timer.Change(0,1000);
                this.AppendTxtToShowLog(string.Format("客户端{0}连接成功", clientSocket.RemoteEndPoint.ToString()));
                //不断接收客户端发来的消息
                ThreadPool.QueueUserWorkItem(new WaitCallback(receiveClient), clientSocket);
            }
        }

        private void receiveClient(object client)
        {
            Socket clientSocket = client as Socket;
            byte[] data = new byte[1024 * 1024];
            while (true)
            {
                int len = 0;
                try
                {
                    len = clientSocket.Receive(data, 0, data.Length, SocketFlags.None);//接收字节数组，并获取长度
                }
                catch (Exception ex)//客户端异常退出
                {

                    try
                    {
                        this.AppendTxtToShowLog(string.Format("客户端{0}异常退出", clientSocket.RemoteEndPoint.ToString()));
                        richTextBox2.AppendText(Environment.NewLine + $"[{DateTime.Now.ToLocalTime()}]客户端{clientSocket.RemoteEndPoint}异常退出!{ex.Message}");
                        //timer1.Stop();
                        timer.Change(0, -1);

                        //timer1.Enabled = false;
                        disconnect_reason = ex.Message;
                    }
                    catch (Exception)
                    {

                    }
                    clientList.Remove(clientSocket);
                    stopConnect(clientSocket);//关闭连接
                    string result = showLog.Text.Trim(); //输入文本
                    StreamWriter sw = File.AppendText($"{DateTime.Now.ToString("yyyy - MM - dd - HH - mm - ss")}_log.txt"); //保存到指定路径
                    sw.Write(result);
                    sw.Flush();
                    sw.Close();
                    return;
                }

                if (len <= 0)//客户端正常退出
                {
                    try
                    {
                        this.AppendTxtToShowLog(string.Format("客户端{0}正常退出", clientSocket.RemoteEndPoint.ToString()));
                        heart_beat = 0;
                        //timer1.Stop();
                        timer.Change(0, -1);
                        string result = showLog.Text.Trim(); //输入文本
                        StreamWriter sw = File.AppendText($"{DateTime.Now.ToString("yyyy - MM - dd - HH - mm - ss")}_log.txt"); //保存到指定路径
                        sw.Write(result);
                        sw.Flush();
                        sw.Close();

                        //timer1.Enabled = false;

                    }
                    catch (Exception)
                    {

                    }
                    clientList.Remove(clientSocket);
                    stopConnect(clientSocket);//关闭连接
                    return;
                }

                string str = Encoding.Default.GetString(data, 0, len);//将字节数组转换成字符串
                if (str == "[alive]")
                {
                    heart_beat=0;
                }
                else
                {
                    this.AppendTxtToShowLog(string.Format("客户端{0}发来信息：{1}", clientSocket.RemoteEndPoint.ToString(), str));
                }

            }
        }

        //关闭连接
        private void stopConnect(Socket clientSocket)
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

        private void AppendTxtToShowLog(string str)//添加日志记录
        {
            if (showLog.InvokeRequired)
            {
                showLog.BeginInvoke(new Action<string>(s =>
                {
                    showLog.AppendText(Environment.NewLine + DateTime.Now.ToLongTimeString() + str);
                }), str);
            }
            else
            {
                showLog.AppendText(Environment.NewLine + DateTime.Now.ToLongTimeString() + str);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //1、创建socket对象
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //2、绑定ip和端口
            socket.Bind(new IPEndPoint(IPAddress.Any, 12589));
            //3、开启监听
            socket.Listen(10);
            //4、开始接收客户端消息

            ThreadPool.QueueUserWorkItem(new WaitCallback(acceptClient), socket);
            this.AppendTxtToShowLog(string.Format("服务器启动成功！"));
            this.btn_start.Enabled = false;
            this.btn_stop.Enabled = true;
            label5.Text = "服务运行中";
            label5.BackColor = Color.Green;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ShutdownService();
            label5.BackColor = Color.Salmon;
            label5.Text = "服务未运行";
        }

        private void button3_Click(object sender, EventArgs e)
        {
            foreach (var clientSocket in clientList)
            {
                if (clientSocket.Connected)
                {
                    byte[] data = Encoding.Default.GetBytes(this.sendContent.Text);
                    clientSocket.Send(data, 0, data.Length, SocketFlags.None);
                }
            }
        }
        private void ShutdownService()
        {
            this.btn_start.Enabled = true;
            this.btn_stop.Enabled = false;
            foreach (var clientSocket in clientList)
            {
                if (clientSocket.Connected)
                {
                    try
                    {
                        clientSocket.Shutdown(SocketShutdown.Both);
                        clientSocket.Close();
                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ShutdownService();
        }

        public void progress(object b)
        {
            label7.Text = heart_beat.ToString();
            //一分钟没有相应
            if (--heart_beat < -12)
            {
                lastReportTime = DateTime.Now;
                this.BackColor = Color.PaleVioletRed;
                label6.Text = $"最后心跳时间:{lastReportTime.ToLocalTime()}";
                richTextBox2.AppendText(Environment.NewLine + $"最后心跳时间:{lastReportTime.ToLocalTime()}");
                disconnect_reason = "心跳检查失败：客户端程序无响应或网络连接已丢失";
                OnClientOfflineInlegally();
                timer.Change(0, -1);

                //timer1.Stop();
                //timer1.Enabled = false;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
           
        }

        /// <summary>
        /// 非正常的退出
        /// </summary>
        private void OnClientOfflineInlegally()
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
            myMail.Subject = "紧急邮件 - 高三三班计算机管理系统";
            myMail.SubjectEncoding = System.Text.Encoding.UTF8;

            //如果要添加抄送 
            foreach (var item in mail_Copy)
            {
                MailAddress copy = new MailAddress(item);
                myMail.Bcc.Add(copy);
            }

            DateTime dt = DateTime.Now;
            myMail.Body = $"<h1>高三三班计算机管理系统-紧急报告</h1>" +
                $"<p>收到该邮件时，意味着班级计算机上运行的\"高三三班计算机管理软件\"已经遭到不正常的关闭。</p>" +
                $"<p>关闭信息：{disconnect_reason}</p>" +
                $"<p><strong>注意：这可能是网络中断引起的误报，并不能完全代表真实情况。</strong></p>" +
                $"<br />" +
                $"<p align=\"right\">高三三班计算机管理系统</p>";
            

            myMail.BodyEncoding = System.Text.Encoding.UTF8;
            myMail.IsBodyHtml = true;
            try
            {
                client.Send(myMail);
            }
            catch(Exception ex)
            {
                richTextBox2.AppendText(Environment.NewLine + ex.Message);
            }
        }
    }
}
