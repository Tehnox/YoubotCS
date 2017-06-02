using System;
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
		public ICommand ToLeftCommand { get; private set; }
		public ICommand ToRightCommand { get; private set; }
		public ICommand StopCommand { get; private set; }
        public ICommand TCPConnectCommand { get; private set; }

        public ManualControlViewModel(ManualControlPage model)
		{
			Model = model;

            ForwardCommand = new DelegateCommand(o => Forward());
            BackwardCommand = new DelegateCommand(o => Backward());
			ToLeftCommand = new DelegateCommand(o => ToLeft());
			ToRightCommand = new DelegateCommand(o => ToRight());
            StopCommand = new DelegateCommand(o => Stop());
            TCPConnectCommand = new DelegateCommand(o => YoubotHandler.TCPConnect());
        }

		public ManualControlPage Model { get; private set; }

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
        public ObservableCollection<string> LogMessagesList
        {
            get { return Model.LogMessagesList; }
            set { Model.LogMessagesList = value; }
        }

        private void Forward()
        {
            YoubotHandler.TCPPublishBase(new[] { 0.1f, 0, 0 });
        }
        private void Backward()
        {
            YoubotHandler.TCPPublishBase(new[] { -0.1f, 0, 0 });
        }
		private void ToLeft()
		{
			YoubotHandler.TCPPublishBase(new[] { 0, -0.1f, 0 });
		}
		private void ToRight()
		{
			YoubotHandler.TCPPublishBase(new[] { 0, 0.1f, 0 });
		}

		private void Stop()
        {
            YoubotHandler.TCPSendStop();
            YoubotHandler.TCPSendStop();
            YoubotHandler.TCPSendStop();
        }
	}
}
