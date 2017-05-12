using System;
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

		public ICommand LoadAutomaticControlPageCommand { get; private set; }
		public ICommand LoadManualControlPageCommand { get; private set; }

		public MainWindowViewModel()
		{
			DataProvider = new DataProvider(StorageDirectory);
			DataProvider.LoadDataset();
			Network = InitNetwork();
			LoadAutomaticControlPage();

            YouBotHandler = new RobotHandler("192.168.88.25", "root", "111111");

			// Hook up Commands to associated methods
			LoadAutomaticControlPageCommand = new DelegateCommand(o => LoadAutomaticControlPage());
			LoadManualControlPageCommand = new DelegateCommand(o => LoadManualControlPage());
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

		private void LoadAutomaticControlPage()
		{
			CurrentViewModel = new AutomaticControlViewModel(
				new AutomaticControlPage());
		}

		private void LoadManualControlPage()
		{
			CurrentViewModel = new ManualControlViewModel(
				new ManualControlPage { YoubotHandler = YouBotHandler });
		}

		public NeuralNetwork InitNetwork()
		{
			var network = new NeuralNetwork(DataProvider, "SceneClassificator-big&bad", 6);
			network.AddLayer(LayerTypes.Input, 4, 30, 32);
			var maps1 = new bool[4 * 16]
			{
				true,  true,  true,  true, false, false, false, true, false, false, true,  true, false, true,  true, true,
				false, true,  true,  true, true,  true,  true,  true, false, false, false, true, false, false, true, true,
				false, false, true,  true, false, true,  true,  true, true,  true,  true,  true, false, false, false, true,
				false, false, false, true, false, false, true,  true, false, true,  true,  true, true,  true,  true,  true
			};
			network.AddLayer(LayerTypes.Convolutional, ActivationFunctions.Tanh, 16, 28, 28, 3, 5, 1, 1, 0, 0, new Mappings(maps1));
			network.AddLayer(LayerTypes.AvgPooling, ActivationFunctions.Tanh, 16, 14, 14, 2, 2, 2, 2);
			network.AddLayer(LayerTypes.Convolutional, ActivationFunctions.Tanh, 64, 10, 10, 5, 5, 1, 1, 0, 0, new Mappings(16, 64, 60, 1488));
			network.AddLayer(LayerTypes.AvgPooling, ActivationFunctions.Tanh, 64, 5, 5, 2, 2, 2, 2);
			network.AddLayer(LayerTypes.Convolutional, ActivationFunctions.Tanh, 256, 1, 1, 5, 5, 1, 1);
			network.AddLayer(LayerTypes.FullyConnected, ActivationFunctions.Tanh, 120);
			network.AddLayer(LayerTypes.FullyConnected, ActivationFunctions.Tanh, 25);
			network.AddLayer(LayerTypes.FullyConnected, ActivationFunctions.Tanh, 6);

			network.InitializeWeights();
			return network;
		}
	}
}
