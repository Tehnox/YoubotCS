﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using YoubotCS.YoubotHandler;

namespace YoubotCS.ViewModel
{
	public class ManualControlViewModel : ViewModelBase
	{
        public ICommand ForwardCommand { get; private set; }
        public ICommand BackwardCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand TCPConnectCommand { get; private set; }

        public ObservableCollection<string> LogMessagesList { get; private set; }

        public ManualControlViewModel(ManualControlPage model)
		{
			Model = model;

            ForwardCommand = new DelegateCommand(o => Forward());
            BackwardCommand = new DelegateCommand(o => Backward());
            StopCommand = new DelegateCommand(o => Stop());
            TCPConnectCommand = new DelegateCommand(o => YoubotHandler.TCPConnect());

            LogMessagesList = new ObservableCollection<string>();

            YoubotHandler._sshRobot.OnShellData = s =>
            {
                s = Regex.Replace(s, @"[^\u0000-\u007F]", string.Empty);
                s = Regex.Replace(s, @"s/\x1b\[[0-9;]*m//g", string.Empty);
                s = Regex.Replace(s, @"[\r\n]+", "\r\n");
                App.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogMessagesList.Add(s);
                }));
            };
        }

		public ManualControlPage Model { get; private set; }

		public string PageTitle
		{
			get
			{
				return Model.PageTitle;
			}
			set
			{
				Model.PageTitle = value;
				OnPropertyChanged("PageTitle");
			}
		}

        public RobotHandler YoubotHandler
        {
            get
            {
                return Model.YoubotHandler;
            }
            set
            {
                Model.YoubotHandler = value;
            }
        }

        private void Forward()
        {
            YoubotHandler.TCPPublishBase(new float[3] { 0.1f, 0, 0 });
        }
        private void Backward()
        {
            YoubotHandler.TCPPublishBase(new float[3] { -0.1f, 0, 0 });
        }

        private void Stop()
        {
            YoubotHandler.TCPSendStop();
            YoubotHandler.TCPSendStop();
            YoubotHandler.TCPSendStop();

        }
	}
}
