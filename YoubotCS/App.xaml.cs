using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YoubotCS.ViewModel;

namespace YoubotCS
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			var window = new MainWindow() { DataContext = new MainWindowViewModel() };
			window.Show();
		}
	}
}
