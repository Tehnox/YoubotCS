//WinRos project by Sekou Diane (sekoudiane1990@gmail.com) and Alexey Novoselsky
//Moscow, MIREA, 2014-2015
//The aim of the project is to control a group of Ubuntu-driven robots (youBots) from Windows laptop

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using YouBotConnect;

namespace YoubotCS.YoubotHandler
{
    public class RobotConnectionTCP
    {
        TcpConnection tcpcon;

        public RobotConnectionTCP(string ip)
        {
            tcpcon = new TcpConnection(0, null, null, null);
            tcpcon.Connect(ip, "7777", true);
        }

        public bool IsConnected
        {
            get
            {
                if (tcpcon == null) return false;
                return tcpcon.IsConnected();
            }
        }

        public void PublishString(string p)
        {
            tcpcon.Send(p);
        }

        string f2s(float v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }
        public void PublishArm(float[] arr, int ind)
        {
            for (int i = 0; i < 1; i++) //2 раза для надежности - из-за ошибки "exeeded timeout"
            {
                PublishString(string.Format("LUA_ManipDeg({0}, {1}, {2}, {3}, {4}, {5})",
                    ind, f2s(arr[0]), f2s(arr[1]), f2s(arr[2]), f2s(arr[3]), f2s(arr[4])));
            }
        }
        public void PublishBase(float[] arr)
        {
            for (int i = 0; i < 1; i++) //2 раза для надежности - из-за ошибки "exeeded timeout"
            {
                PublishString(string.Format("LUA_Base({0}, {1}, {2})",
               f2s(arr[0]), f2s(arr[1]), f2s(arr[2])));
            }
        }

        public void PublishGripper(float[] arr, int ind)
        {
            var x = arr[0];

            for (int i = 0; i < 1; i++) //2 раза для надежности - из-за ошибки "exeeded timeout"
            {
                PublishString(string.Format("LUA_Gripper({0}, {1})",
                ind, f2s(x)));
            }
        }
    }
}
