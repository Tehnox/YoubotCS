using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MjpegProcessor;

namespace YoubotCS.YoubotHandler
{
	public class RobotHandler
	{
		public SSHWrapper _sshRobot; //команды ssh
		RobotConnectionTCP _tcpRobot; //команда программе YTL

		public string IP;
		private string _terminalName, _terminalPassword;

		public event EventHandler<FrameReadyEventArgs> RgbFrameReady
		{
			add { _mjpegDecoderRgb.FrameReady += value; }
			remove { _mjpegDecoderRgb.FrameReady -= value; }
		}

		public event EventHandler<FrameReadyEventArgs> DepthFrameReady
		{
			add { _mjpegDecoderD.FrameReady += value; }
			remove { _mjpegDecoderD.FrameReady -= value; }
		}

		private MjpegDecoder _mjpegDecoderRgb;
		private MjpegDecoder _mjpegDecoderD;

		public RobotHandler(string robotIP, string terminalName, string terminalPassword)
		{
			IP = robotIP;
			_terminalName = terminalName;
			_terminalPassword = terminalPassword;

			InitSSH();
			_sshRobot.ssh_connect();
            _sshRobot.Send("roslaunch youbot_tactical_level ytl.launch");
			//SetupVideoEncoders();
			//_tcpRobot = new RobotConnectionTCP(IP);
		}
		#region Helpers

		public void InitSSH()
		{
			_sshRobot = new SSHWrapper(IP, _terminalName, _terminalPassword); //root:111111

			//TODO: process shell output
			//_sshRobot.OnShellData = s => lo.Invoke(new Action(() =>
			//{
			//    s = Regex.Replace(s, @"[^\u0000-\u007F]", string.Empty);
			//    s = Regex.Replace(s, @"s/\x1b\[[0-9;]*m//g", string.Empty);
			//    s = Regex.Replace(s, @"[\r\n]+", "\r\n");

			//	if (s.Trim() == "#ok#")
			//	{
			//		//label1.Text = label1.Text == "#OK#" ? "#ok#" : "#OK#";
			//		return;
			//	}
			//	//if (s.Contains("Base is initialized."))
			//	//{
			//	//    Form1.ShowMsg(string.Format("Youbot {0} is ready", ID + 1));

			//	//    if (f1.cb_autoytl.Checked)
			//	//    {
			//	//        ReConnectRobot();
			//	//        ReOpenRouterSSH();
			//	//    }
			//	//}
			//	//else if (s.Contains("[100%] Built"))
			//	//{
			//	//    Form1.ShowMsg(string.Format("Youbot {0} is compiled", ID + 1));
			//	//}

			//	rtb_ssh.Text += s; rtb_ssh.SelectionStart = rtb_ssh.Text.Length; //Set the current caret position at the end
			//	rtb_ssh.ScrollToCaret();
			//}));
		}

		public bool ping()
		{
			var ping = new Ping();
			var repl = ping.Send(IP, 100);
			return repl != null && (repl.Status == IPStatus.Success);
		}
		#endregion

		public void SSHSend(string cmd)
		{
			//if (!ping()) ShowMsg("Bad IP");
			_sshRobot.Send(cmd);
		}

		public void SSHSendCtrlC()
		{
			_sshRobot.SendCtrlC();
		}

		public void TCPSendStop()
		{
			_tcpRobot.PublishBase(new float[] { 0, 0, 0 });
			_tcpRobot.PublishArm(new float[] { 0, 0, 0, 0, 0 }, 0);
		}

        public void TCPPublishBase(float[] vec)
        {
            _tcpRobot.PublishBase(vec);
        }

		public void TCPSend(string cmd)
		{
			_tcpRobot.PublishString(cmd);
		}

        public void TCPConnect()
        {
            _tcpRobot = new RobotConnectionTCP(IP);
        }

		private void SetupVideoEncoders()
		{
			_mjpegDecoderRgb = new MjpegDecoder();
			_mjpegDecoderD = new MjpegDecoder();

			_mjpegDecoderRgb.ParseStream(new Uri($"http://{IP}:8080/stream?topic=/camera/rgb/image_raw&width=640&height=480&quality=50"));
			_mjpegDecoderD.ParseStream(new Uri($"http://{IP}:8080/stream?topic=/camera/depth_registered/image_raw&width=640&height=480&quality=50"));
		}
	}
}
