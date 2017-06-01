using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using YoubotCS.YoubotHandler;

namespace YoubotCS.ViewModel
{
	public class AutomaticControlPage
	{
		public BitmapImage Image { get; set; }
		public BitmapImage DepthImage { get; set; }
		public RobotHandler YoubotHandler { get; set; }
		public string BindCamerasButtonText { get; set; }
	}
}
