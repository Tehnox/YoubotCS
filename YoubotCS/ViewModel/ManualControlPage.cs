using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoubotCS.YoubotHandler;

namespace YoubotCS.ViewModel
{
	public class ManualControlPage
	{
        public RobotHandler YoubotHandler { get; set; }
		public ObservableCollection<string> LogMessagesList { get; set; }
	}
}
