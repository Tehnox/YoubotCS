//WinRos project by Sekou Diane (sekoudiane1990@gmail.com) and Alexey Novoselsky
//Moscow, MIREA, 2014-2015
//The aim of the project is to control a group of Ubuntu-driven robots (youBots) from Windows laptop

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using YoubotCS.YoubotHandler;

namespace YouBotConnect

{
    public class TcpConnection //TODO: repair error output
    {
        public TcpClient TcpClient;

        private StreamReader _reader;
        private StreamWriter _writer;

        public Thread TcpThread;

        private const string Confirmation = "#ok#";
        private const string TimeCheck = "#TimeCheck#";

        public delegate void EventDel(string info);

        private EventDel onConnected, onDataReceived, onDisconnect;

        private System.Windows.Forms.Timer _timerKeepAlive;

		private int _timeCheckSkipper = 5, _tcs = 0;
		private int _id;

		public bool IsConnected()
        {
            if (TcpClient == null || TcpThread == null) return false;

            var res = (DateTime.Now - t_last_msg_from_server).Seconds < 60;

            return res;
        }

        public TcpConnection(int ID, EventDel onConnected, EventDel onDataReceived, EventDel onDisconnect)
        {
            _id = ID;
            this.onConnected = onConnected;
            this.onDataReceived = onDataReceived;
            this.onDisconnect = onDisconnect;
        }

        public void Dispose()
        {

            if (TcpThread != null)
            {
                TcpThread.Abort();
                TcpThread = null;
            }
            if (_reader != null) { _reader.Dispose(); _reader = null; }
            if (_writer != null) { _writer.Dispose(); _writer = null; }
        }

        public bool IsDisposed => TcpThread==null;

	    public void Connect(string ip, string port, bool linux)
        {
            this._linux = linux;
            TcpClient = new TcpClient();

            var serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port));

            if (TcpClient.Client.RemoteEndPoint == null || TcpClient.Client.RemoteEndPoint.ToString() != serverEndPoint.ToString())
            {
                try
                {
                    TcpClient.Connect(serverEndPoint);
                }
                catch (Exception)
                {
                    //Form1.ShowMsg("Bad endpoint: _id = "+(_id+1));
                    TcpClient = null;
                    return;
                }

                _reader = new StreamReader(TcpClient.GetStream());
                _writer = new StreamWriter(TcpClient.GetStream());

                TcpThread = new Thread(WaitTcpFromServer);
                TcpThread.Start();
            }

            t_last_msg_from_server = DateTime.Now;

            if (!IsConnected()) throw new Exception("Couldn't connect");
            if(onConnected!=null) onConnected("Connected!");
            Send("#start#");

		    _timerKeepAlive = new System.Windows.Forms.Timer
		    {
			    Interval = 1000,
			    Enabled = true
		    };
		    _timerKeepAlive.Tick += (s, e) =>
            {
                //Send(Confirmation);
                if (_tcs == 0)
                {
                    _hpt.Start();
                    Send(TimeCheck);
                }
                _tcs = (_tcs + 1) % _timeCheckSkipper;
            };

        }

        public void Disconnect(string reason, bool show_mb)
        {
            _timerKeepAlive.Enabled = false;

            if (TcpThread != null && TcpThread.IsAlive)
            {
                try
                {
                    TcpThread.Abort();
                }
                catch
                {
                }
            }
            if (TcpClient != null && TcpClient.Connected)
            {
                TcpClient.Close();
                _reader.Close(); _reader = null;
                _writer.Close(); _writer = null;
            }

            TcpClient = null;
            TcpThread = null;

            if (onDisconnect != null) onDisconnect(reason);

            //if (show_mb) Form1.ShowMsg("Disconnect: " + reason);
        }

        private object read_write_sem = 777;

        private DateTime t_last_msg_from_server;

        private void WaitTcpFromServer()
        {
            //read

            while (true)
            {
                if (!IsConnected())
                {
                    Disconnect("Connection timeout or smth else", true);
                    break;
                }

                string data = null;
                lock (read_write_sem)
                {
                    if (TcpClient.Available > 0)
                    {
                        try
                        {
	                        var readLine = _reader.ReadLine();
	                        if (readLine != null) data = readLine.Replace("^^^", "\r\n");
                        }
                        catch(Exception ex)
                        {
                            //Form1.ShowMsg(ex.Message);
                        }
                        
                    }
                }
                if (data != null)
                {
                    // Строка, содержащая ответ от сервера
                    if (data == Confirmation)
                    {
                        t_last_msg_from_server = DateTime.Now;
                    } 
                    else if (data == TimeCheck)
                    {
                        _hpt.Stop();
                        delay = 0.5f * delay + 0.5f * (float)_hpt.Duration;
                    }
                    else
                    {
						onDataReceived?.Invoke(data.Replace("^^^", "\r\n"));
					}
                }
                else
                {
                    Thread.Sleep(30);
                }
            }
        }
        float delay;

        HiPerfTimer _hpt = new HiPerfTimer();

        private bool _msgBoxShown = false;

        bool _linux;

        public void Send(string s)
        {
            if (TcpClient == null)
            {
                _msgBoxShown = true;
                if(!_msgBoxShown) MessageBox.Show("Not connected");
                return;
            }

            lock (read_write_sem)
            {
                try
                {
                    if (_linux)
                    {
                        s = s.Replace("\r\n", "\n");
                        _writer.Write(s + "^^^");
                    }
                    else s += "\r\n";

                    _writer.Flush();
                }
                catch
                {
                    Disconnect("Can't send (server stopped?): _id = "+(_id+1), true);
                }
            }
        }

    }



}