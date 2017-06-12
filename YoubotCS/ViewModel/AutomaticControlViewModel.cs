using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;
using MjpegProcessor;
using OpenTK.Graphics.ES20;
using YoubotCS.NN;
using YoubotCS.Utils;
using YoubotCS.YoubotHandler;

namespace YoubotCS.ViewModel
{
	public class AutomaticControlViewModel : ViewModelBase
	{
		public AutomaticControlPage Model { get; private set; }
		public ICommand LoadImageCommand { get; private set; }
		public ICommand FindObstaclesCommand { get; private set; }
		public ICommand BindCamerasCommand { get; private set; }
		public ICommand LoadDatasetCommand { get; private set; }
		public ICommand TrainCommand { get; private set; }
		public ICommand TestCommand { get; private set; }
		public ICommand LoadWeightsCommand { get; private set; }
		public ICommand SaveWeightsCommand { get; private set; }

		private Task _trainTask;

		public AutomaticControlViewModel(AutomaticControlPage model)
		{
			Model = model;

			LoadImageCommand = new DelegateCommand(o => LoadImage());
			FindObstaclesCommand = new DelegateCommand(o => FindObstacles());
			BindCamerasCommand = new DelegateCommand(o => BindCameras());
			LoadDatasetCommand = new DelegateCommand(o => LoadDataset());
			TrainCommand = new DelegateCommand(o => TrainNN());
			TestCommand = new DelegateCommand(o => TestNN());
			SaveWeightsCommand = new DelegateCommand(o => SaveWeights());
			LoadWeightsCommand = new DelegateCommand(o => LoadWeights());

			BindCamerasButtonText = "Bind Cameras";
		}

		public BitmapImage Image
		{
			get { return Model.Image; }
			set
			{
				Model.Image = value;
				OnPropertyChanged("Image");
			}
		}

		public BitmapImage DepthImage
		{
			get { return Model.DepthImage; }
			set
			{
				Model.DepthImage = value;
				OnPropertyChanged("DepthImage");
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

		public NeuralNetwork Network
		{
			get { return Model.Network; }
			set { Model.Network = value; }
		}

		public string BindCamerasButtonText
		{
			get { return Model.BindCamerasButtonText; }
			set
			{
				Model.BindCamerasButtonText = value;
				OnPropertyChanged("BindCamerasButtonText");
			}
		}
		public ObservableCollection<string> LogMessagesList
		{
			get { return Model.LogMessagesList; }
			set { Model.LogMessagesList = value; }
		}

		private void BindCameras()
		{
			if (BindCamerasButtonText == "Bind Cameras")
			{
				YoubotHandler.RgbFrameReady += RgbFrameReady;
				YoubotHandler.DepthFrameReady += DepthFrameReady;
				BindCamerasButtonText = "Unbind Cameras";
			}
			else
			{
				YoubotHandler.RgbFrameReady -= RgbFrameReady;
				YoubotHandler.DepthFrameReady -= DepthFrameReady;
				BindCamerasButtonText = "Bind Cameras";
			}

		}

		private void DepthFrameReady(object sender, FrameReadyEventArgs e)
		{
			DepthImage = e.BitmapImage;
		}

		private void RgbFrameReady(object sender, FrameReadyEventArgs e)
		{
			Image = e.BitmapImage;
		}

		private void LoadImage()
		{
			using (var openFileDialog = new OpenFileDialog())
			{
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					Image = new BitmapImage(new Uri(openFileDialog.FileName));
					DepthImage = new BitmapImage(new Uri(openFileDialog.FileName.Replace(".png", "d.png")));
				}
			}
		}

		private void LoadWeights()
		{
			using (OpenFileDialog sd = new OpenFileDialog())
			{
				if (sd.ShowDialog() == DialogResult.OK)
					Network.LoadWeights(sd.FileName);
			}
		}

		private void SaveWeights()
		{
			using (SaveFileDialog sd = new SaveFileDialog())
			{
				if (sd.ShowDialog() == DialogResult.OK)
					Network.SaveWeights(sd.FileName);
			}
		}

		private void FindObstacles()
		{
			if (Image == null || DepthImage == null)
				return;

			var sp = new SuperPixels();
			var width = DepthImage.PixelWidth;
			var height = DepthImage.PixelHeight;
			var size = width * height;
			var img = new Image<Bgr, byte>(ImageUtil.BitmapImage2Bitmap(DepthImage));
			var imgBuffer = new int[size * 3];
			var labels = new int[size * 3];
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					imgBuffer[(x + (y * width))] = img.Data[y, x, 2];
					imgBuffer[(x + (y * width)) + size] = img.Data[y, x, 1];
					imgBuffer[(x + (y * width)) + 2 * size] = img.Data[y, x, 0];
				}
			var numlabels = sp.PerformForGivenK(imgBuffer, width, height, ref labels, 100, 2);

			int[] dx = { -1, -1, 0, 1, 1, 1, 0, -1 };
			int[] dy = { 0, -1, -1, -1, 0, 1, 1, 1 };

			var istaken = Enumerable.Repeat(false, size).ToList();

			var contourx = Enumerable.Repeat(0, size).ToList();
			var contoury = Enumerable.Repeat(0, size).ToList();
			var mainindex = 0;
			var cind = 0;
			for (var j = 0; j < height; j++)
			{
				for (var k = 0; k < width; k++)
				{
					var np = 0;
					for (var i = 0; i < 8; i++)
					{
						var x = k + dx[i];
						var y = j + dy[i];

						if ((x >= 0 && x < width) && (y >= 0 && y < height))
						{
							var index = y * width + x;
							{
								if (labels[mainindex] != labels[index]) np++;
							}
						}
					}
					if (np > 1)
					{
						contourx[cind] = k;
						contoury[cind] = j;
						istaken[mainindex] = true;
						cind++;
					}
					mainindex++;
				}
			}

			var numboundpix = cind;
			var obstacleLabels = new List<int>();
			for (var j = 0; j < numboundpix; j++)
			{
				var depthCheck = 0;
				var index = contoury[j] * width + contourx[j];
				for (var i = 0; i < 8; i++)
				{
					var x = contourx[j] + dx[i];
					var y = contoury[j] + dy[i];

					if ((x >= 0 && x < width) && (y >= 0 && y < height))
					{
						{
							if (img.Data[contoury[j], contourx[j], 2] > 13 && img.Data[y, x, 2] > 13)
								if ((double)img.Data[contoury[j], contourx[j], 2] - img.Data[y, x, 2] < -10)
									depthCheck++;
						}
					}
				}
				if (depthCheck > 3 && !obstacleLabels.Contains(labels[index]))
				{
					obstacleLabels.Add(labels[index]);
				}
			}

			#region visualisation
			var currentLabel = labels[0];
			var isObstacle = false;
			for (var j = 0; j < height; j++)
			{
				for (var k = 0; k < width; k++)
				{
					if (currentLabel != labels[j * width + k])
					{
						currentLabel = labels[j * width + k];
						isObstacle = obstacleLabels.Contains(currentLabel);
					}
					if (isObstacle)
					{
						img.Data[j, k, 2] += 20;
						img.Data[(int)sp.kseedsy[currentLabel], (int)sp.kseedsx[currentLabel], 1] = 255;
					}
				}
			}

			for (var j = 0; j < numboundpix; j++)
			{
				img.Data[contoury[j], contourx[j], 0] = 255;
			}
			#endregion
			DepthImage = ImageUtil.BitmapToImageSource(img.Bitmap);

			img = new Image<Bgr, byte>(ImageUtil.BitmapImage2Bitmap(Image));

			var saveDirectory = Environment.CurrentDirectory + "\\samples";
			if (!Directory.Exists(saveDirectory))
				Directory.CreateDirectory(saveDirectory);

			foreach (var label in obstacleLabels)
			{
				var cx = (int)sp.kseedsx[label];
				var cy = (int)sp.kseedsy[label];
				var sx = Math.Max(cx - 16, 0);
				var sy = Math.Max(cy - 16, 0);
				sx -= cx + 16 > img.Width ? cx + 16 - img.Width : 0;
				sy -= cy + 16 > img.Height ? cy + 16 - img.Height : 0;
				img.Copy(new Rectangle(sx, sy, 32, 32))
					.Save(saveDirectory + $"\\{label}-sample.png");
			}
		}

		private void LoadDataset()
		{
			Network.DataProvider.LoadDataset();
		}

		private void TrainNN()
		{
			if (_trainTask != null && _trainTask.Status == TaskStatus.Running)
			{
				Network.Stop();
				Task.WaitAll(_trainTask);
				_trainTask.Dispose();
			}
			else
			{
				_trainTask = new Task(() => Network.TrainingTask());
				_trainTask.Start();
			}
		}

		private void TestNN()
		{
			if (Image == null || DepthImage == null)
				return;

			var sp = new SuperPixels();
			var width = DepthImage.PixelWidth;
			var height = DepthImage.PixelHeight;
			var size = width*height;
			var img = new Image<Bgr, byte>(ImageUtil.BitmapImage2Bitmap(DepthImage));
			var imgBuffer = new int[size*3];
			var labels = new int[size*3];
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					imgBuffer[(x + (y*width))] = img.Data[y, x, 2];
					imgBuffer[(x + (y*width)) + size] = img.Data[y, x, 1];
					imgBuffer[(x + (y*width)) + 2*size] = img.Data[y, x, 0];
				}
			var numlabels = sp.PerformForGivenK(imgBuffer, width, height, ref labels, 100, 2);

			int[] dx = {-1, -1, 0, 1, 1, 1, 0, -1};
			int[] dy = {0, -1, -1, -1, 0, 1, 1, 1};

			var istaken = Enumerable.Repeat(false, size).ToList();

			var contourx = Enumerable.Repeat(0, size).ToList();
			var contoury = Enumerable.Repeat(0, size).ToList();
			var mainindex = 0;
			var cind = 0;
			for (var j = 0; j < height; j++)
			{
				for (var k = 0; k < width; k++)
				{
					var np = 0;
					for (var i = 0; i < 8; i++)
					{
						var x = k + dx[i];
						var y = j + dy[i];

						if ((x >= 0 && x < width) && (y >= 0 && y < height))
						{
							var index = y*width + x;
							{
								if (labels[mainindex] != labels[index]) np++;
							}
						}
					}
					if (np > 1)
					{
						contourx[cind] = k;
						contoury[cind] = j;
						istaken[mainindex] = true;
						cind++;
					}
					mainindex++;
				}
			}

			var numboundpix = cind;
			var obstacleLabels = new List<int>();
			for (var j = 0; j < numboundpix; j++)
			{
				var depthCheck = 0;
				var index = contoury[j]*width + contourx[j];
				for (var i = 0; i < 8; i++)
				{
					var x = contourx[j] + dx[i];
					var y = contoury[j] + dy[i];

					if ((x >= 0 && x < width) && (y >= 0 && y < height))
					{
						{
							if (img.Data[contoury[j], contourx[j], 2] > 13 && img.Data[y, x, 2] > 13)
								if ((double) img.Data[contoury[j], contourx[j], 2] - img.Data[y, x, 2] < -10)
									depthCheck++;
						}
					}
				}
				if (depthCheck > 3 && !obstacleLabels.Contains(labels[index]))
				{
					obstacleLabels.Add(labels[index]);
				}
			}

			var currentLabel = labels[0];
			var isObstacle = false;
			for (var j = 0; j < height; j++)
			{
				for (var k = 0; k < width; k++)
				{
					if (currentLabel != labels[j*width + k])
					{
						currentLabel = labels[j*width + k];
						isObstacle = obstacleLabels.Contains(currentLabel);
					}
					if (isObstacle)
					{
						img.Data[j, k, 2] += 20;
						img.Data[(int) sp.kseedsy[currentLabel], (int) sp.kseedsx[currentLabel], 1] = 255;
					}
				}
			}
		}
	}
}
