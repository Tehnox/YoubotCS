using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using YoubotCS.NN;
using YoubotCS.YoubotHandler;

namespace YoubotCS.ViewModel
{
	public class AutomaticControlPage
	{
		public BitmapImage Image { get; set; }
		public BitmapImage DepthImage { get; set; }
		public RobotHandler YoubotHandler { get; set; }
		public string BindCamerasButtonText { get; set; }
		public NeuralNetwork Network { get; set; }
		public ObservableCollection<string> LogMessagesList { get; set; }
	}
}
