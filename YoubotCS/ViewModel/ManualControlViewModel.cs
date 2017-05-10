using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoubotCS.ViewModel
{
	public class ManualControlViewModel : ViewModelBase
	{
		public ManualControlViewModel(ManualControlPage model)
		{
			this.Model = model;
		}

		public ManualControlPage Model { get; private set; }

		public string PageTitle
		{
			get
			{
				return this.Model.PageTitle;
			}
			set
			{
				Model.PageTitle = value;
				OnPropertyChanged("PageTitle");
			}
		}
	}
}
