//WinRos project by Sekou Diane (sekoudiane1990@gmail.com) and Alexey Novoselsky
//Moscow, MIREA, 2014-2015
//The aim of the project is to control a group of Ubuntu-driven robots (youBots) from Windows laptop

using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YoubotCS.YoubotHandler
{
    public class SSHWrapper
    {
        public SshClient sshClient;//SSH клиент
        public ShellStream shell_stream;
        public StreamReader shell_rd;
        public StreamWriter shell_wr;

        string endpoint, login, pass;

        public delegate void OnShellDataDel(string text);
        public OnShellDataDel OnShellData;

        public bool Connected()
        {
            return sshClient != null && sshClient.IsConnected;
        }

        public SSHWrapper(string endpoint, string login, string pass)
        {
            this.endpoint = endpoint;
            this.login = login;
            this.pass = pass;
        }

        #region SSH

        public bool ping()
        {
            var ping = new Ping();
            var repl = ping.Send(endpoint, 100);
            return (repl.Status == IPStatus.Success);
        }

        object ssh_lock = 2713687;
        public void ssh_connect()
        {
            lock (ssh_lock)
            {
                if (sshClient != null && sshClient.IsConnected) return; //если подключение занимает долго и с прошлого раза только-только успели подключиться

                if (!ping()) return; //ssh_disconnect();

                //Подключаемся по SSH
                //var connectionInfo = new PrivateKeyConnectionInfo(robot_endpoint, "root", null);//KeyboardInteractiveConnectionInfo(robot_endpoint,"root");
                var connectionInfo = new PasswordConnectionInfo(endpoint, login, pass);
                //connectionInfo.Encoding = Encoding.ASCII;

                sshClient = new SshClient(connectionInfo);

                try
                {
                    sshClient.Connect();
                }
                catch { }

                if (!sshClient.IsConnected)
                {
                    ssh_disconnect();
                    return;
                }

#warning catching error
                try
                {
                    start_shell();
                }
                catch { }
            }
        }
        public void ssh_disconnect()
        {
            if (sshClient == null) return;

            bool Ping = ping();

            //Отключаеся от SSH

            lock (sshClient)
            {
                if (Ping)
                    if (sshClient != null && sshClient.IsConnected)
                        sshClient.Disconnect();

                sshClient = null;

                if (Ping) if (shell_stream != null) shell_stream.Close();

                shell_stream = null;

                shell_rd = null;
                shell_wr = null;
            }

            //sudo pkill -u james
        }

        void start_shell()
        {
            shell_stream = sshClient.CreateShellStream("uxterm", 80, 24, 800, 600, 1024);
            var enc = new UTF8Encoding();
            shell_rd = new StreamReader(shell_stream, enc);
            shell_wr = new StreamWriter(shell_stream, enc);
            shell_wr.AutoFlush = false;

            var thread = new Thread(shell_stream_func);
            thread.Start();
        }



        void shell_stream_func()
        {


#warning use WaitOne
            while (true)
            {
                if (shell_stream == null) break;
                while (shell_stream != null && !shell_stream.DataAvailable) Thread.Sleep(1000);
                if (shell_stream == null) break;

                var text = shell_rd.ReadToEnd();

                // http://sshnet.codeplex.com/discussions/301739

                //stream.DataReceived+=on_data;

#warning empty try-catch
                if (OnShellData != null) try { OnShellData(text); }
                    catch { }

                Thread.Sleep(30);
            }
        }

        public void Send(string s)//button6_send(object sender, EventArgs e) //tb_shell_send.Text.
        {
            if (shell_stream == null) return;
            s = s.Replace("\r\n", "\n");
            if (!s.EndsWith("\n")) s += "\n";
            shell_wr.Write(s);
            shell_wr.Flush();
        }

        public void SendCtrlC()//button7_Click(object sender, EventArgs e)
        {
            if (shell_stream == null) return;
            string s = "\x03";
            shell_wr.Write(s);
            shell_wr.Flush();
        }
        #endregion
    }
}
