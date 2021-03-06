﻿using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using YoubotCS.NN;
using YoubotCS.YoubotHandler;

namespace YoubotCS.ViewModel
{
	public class MainWindowViewModel : ViewModelBase
	{
		public DataProvider DataProvider;
		public static string StorageDirectory = Environment.CurrentDirectory + @"\dataset";
		public NeuralNetwork Network;
        public RobotHandler YouBotHandler;
		public ObservableCollection<string> LogMessagesList { get; private set; }

		public ICommand LoadAutomaticControlPageCommand { get; private set; }
		public ICommand LoadManualControlPageCommand { get; private set; }

		public MainWindowViewModel()
		{
			DataProvider = new DataProvider(StorageDirectory);
			//DataProvider.LoadDataset();
			Network = InitNetwork();
			Network.StatusChanged += status => Status = status;

            YouBotHandler = new RobotHandler("192.168.88.25", "root", "111111");
			LogMessagesList = new ObservableCollection<string>();

			YouBotHandler._sshRobot.OnShellData = s =>
			{
				s = Regex.Replace(s, @"[^\u0000-\u007F]", string.Empty);
				s = Regex.Replace(s, @"s/\x1b\[[0-9;]*m//g", string.Empty);
				s = Regex.Replace(s, @"[\r\n]+", "\r\n");
				App.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					LogMessagesList.Add(s);
				}));
			};

			// Hook up Commands to associated methods
			LoadAutomaticControlPageCommand = new DelegateCommand(o => LoadAutomaticControlPage());
			LoadManualControlPageCommand = new DelegateCommand(o => LoadManualControlPage());

			LoadAutomaticControlPage();
		}

		// ViewModel that is currently bound to the ContentControl
		private ViewModelBase _currentViewModel;

		public ViewModelBase CurrentViewModel
		{
			get { return _currentViewModel; }
			set
			{
				_currentViewModel = value;
				OnPropertyChanged("CurrentViewModel");
			}
		}

		private string _status;

		public string Status
		{
			get { return _status; }
			set
			{
				_status = "YoubotCS | " + value;
				OnPropertyChanged("Status");
			}
		}

		private void LoadAutomaticControlPage()
		{
			CurrentViewModel = new AutomaticControlViewModel(
				new AutomaticControlPage { YoubotHandler = YouBotHandler, LogMessagesList = LogMessagesList, Network = Network});
		}

		private void LoadManualControlPage()
		{
			CurrentViewModel = new ManualControlViewModel(
				new ManualControlPage { YoubotHandler = YouBotHandler, LogMessagesList = LogMessagesList });
		}

		public NeuralNetwork InitNetwork()
		{
			var network = new NeuralNetwork(DataProvider, "NN-youbot", 3);
			network.AddLayer(LayerTypes.Input, 4, 40, 40);
			var maps1 = new bool[4 * 16]
			{
				true,  true,  true,  true, false, false, false, true, false, false, true,  true, false, true,  true, true,
				false, true,  true,  true, true,  true,  true,  true, false, false, false, true, false, false, true, true,
				false, false, true,  true, false, true,  true,  true, true,  true,  true,  true, false, false, false, true,
				false, false, false, true, false, false, true,  true, false, true,  true,  true, true,  true,  true,  true
			};
			network.AddLayer(LayerTypes.Convolutional, ActivationFunctions.Tanh, 16, 32, 32, 9, 9, 1, 1, 0, 0, new Mappings(maps1));
			network.AddLayer(LayerTypes.AvgPooling, ActivationFunctions.Tanh, 16, 16, 16, 2, 2, 2, 2);
			network.AddLayer(LayerTypes.Convolutional, ActivationFunctions.Tanh, 64, 10, 10, 7, 7, 1, 1, 0, 0, new Mappings(16, 64, 70, 1488));
			network.AddLayer(LayerTypes.AvgPooling, ActivationFunctions.Tanh, 64, 5, 5, 2, 2, 2, 2);
			network.AddLayer(LayerTypes.Convolutional, ActivationFunctions.Tanh, 256, 1, 1, 5, 5, 1, 1);
			network.AddLayer(LayerTypes.FullyConnected, ActivationFunctions.Tanh, 60);
			network.AddLayer(LayerTypes.FullyConnected, ActivationFunctions.Tanh, 3);

			network.InitializeWeights();
			return network;
		}
	}
}
