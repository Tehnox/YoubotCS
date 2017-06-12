using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using YoubotCS.Utils;

namespace YoubotCS.NN
{
	public struct Sample
	{
		public double[] Labels;
		public byte[] Image;

		public Sample(double[] labels, byte[] image) : this()
		{
			Labels = labels;
			Image = image;
		}
	}
	public class DataProvider
	{
		public int ClassCount { get; private set; }
		public int SampleWidth { get; private set; }
		public int SampleHeight { get; private set; }
		public int SampleSize { get; private set; }
		public int SampleChannels { get; private set; }
		public int SamplesCount { get; private set; }
		public int ImagesCount { get; private set; }
		public int[] RandomSample;
		public Sample[] Samples;
		public string DataDirectory { get; private set; }
		public ThreadSafeRandom RandomGenerator;
		public ParallelOptions ParallelOption { get; }

		private int _maxDegreeOfParallelism = Environment.ProcessorCount;
		public int MaxDegreeOfParallelism
		{
			get
			{
				return _maxDegreeOfParallelism;
			}

			set
			{
				if (value == _maxDegreeOfParallelism)
					return;

				if ((value == 0) || (value > Environment.ProcessorCount))
					_maxDegreeOfParallelism = Environment.ProcessorCount;
				else
					_maxDegreeOfParallelism = value;

				ParallelOption.MaxDegreeOfParallelism = _maxDegreeOfParallelism;
			}
		}

		public DataProvider(string path)
		{
			ParallelOption = new ParallelOptions
			{
				TaskScheduler = null,
				MaxDegreeOfParallelism = Environment.ProcessorCount
			};
			RandomGenerator = new ThreadSafeRandom();

			DataDirectory = path;
		}

		public void LoadDataset()
		{
			ClassCount = 3;
			SampleWidth = 40;
			SampleHeight = 40;
			SampleSize = SampleWidth * SampleHeight;
			SampleChannels = 4;

			int counter = 0;
			List<Task> tasks = new List<Task>();
			tasks.Add(new Task(() => LoadLABImageTrainingSamples(ref counter)));

			foreach (Task task in tasks)
				task.Start();

			while (!tasks.TrueForAll(task => task.IsCompleted))
			{
				Thread.Sleep(10);
			}

			foreach (Task task in tasks)
				task.Dispose();
			tasks = null;

			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		public void ScrambleTrainingSamples()
		{
			for (var i = 0; i < SamplesCount; i++)
			{
				var l = RandomGenerator.Next(SamplesCount);
				var k = RandomSample[i];
				RandomSample[i] = RandomSample[l];
				RandomSample[l] = k;
			}
		}

		private void LoadLABImageTrainingSamples(ref int counter)
		{
			string imagePath = DataDirectory + @"\images\";
			int imgWidth;
			int imgHeight;
			try
			{
				var info = new DirectoryInfo(imagePath);
				var files = info.GetFiles();
				using (Image img = Image.FromFile(imagePath + files[0].Name))
				{
					imgHeight = img.Height;
					imgWidth = img.Width;
				}
				ImagesCount = files.Length;
				SamplesCount = files.Length * (imgWidth / SampleWidth) * (imgHeight / SampleHeight);

				Samples = new Sample[SamplesCount];
				RandomSample = new int[SamplesCount];
				Parallel.For(0, files.Length, ParallelOption, j =>
				{
					using (var img = new Image<Bgr, byte>(imagePath + files[j].Name))
					{
						string[] labels;
						string[] depth;

						using (StreamReader srLabels = new StreamReader((imagePath + files[j].Name).Replace("images", "labels").Replace("jpg", "regions.txt")))
						{
							var fileLabelContent = srLabels.ReadToEnd();
							if (fileLabelContent[fileLabelContent.Length - 1] == '\n')
								fileLabelContent.Remove(fileLabelContent.Length - 1);
							labels = fileLabelContent.Replace('\n', ' ').Split(' ');
						}
						using (StreamReader srDepth = new StreamReader((imagePath + files[j].Name).Replace("images", "labels").Replace("jpg", "depth.txt")))
						{
							var fileDepthContent = srDepth.ReadToEnd();
							if (fileDepthContent[fileDepthContent.Length - 1] == '\n')
								fileDepthContent.Remove(fileDepthContent.Length - 1);
							depth = fileDepthContent.Replace('\n', ' ').Split(' ');
						}

						for (int dy = 0; dy < imgHeight / SampleHeight; dy++)
							for (int dx = 0; dx < imgWidth / SampleWidth; dx++)
							{
								var sample = new Sample(new double[ClassCount], new byte[SampleSize * SampleChannels]);
								var sampleIndex = j * (imgHeight / SampleHeight) * (imgWidth / SampleWidth) + (dy * (imgWidth / SampleWidth) + dx);
								RandomSample[sampleIndex] = sampleIndex;
								for (int i = 0; i < SampleSize * SampleChannels; i++)
									sample.Image[i] = 0;
								for (int i = 0; i < sample.Labels.Length; i++)
									sample.Labels[i] = 0;

								for (int y = 0; y < SampleHeight; y++)
									for (int x = 0; x < SampleWidth; x++)
									{
										SuperPixels.RGB2LABbyte(
											img.Data[dy * SampleHeight + y, dx * SampleWidth + x, 2],
											img.Data[dy * SampleHeight + y, dx * SampleWidth + x, 1],
											img.Data[dy * SampleHeight + y, dx * SampleWidth + x, 0],
											ref sample.Image[(x + (y * SampleWidth))],
											ref sample.Image[(x + (y * SampleWidth)) + SampleSize],
											ref sample.Image[(x + (y * SampleWidth)) + 2 * SampleSize]);

										sample.Image[(x + (y * SampleWidth)) + 3 * SampleSize] = (byte)(3.18 * double.Parse(depth[(dy * SampleHeight + y) * imgWidth + (dx * SampleWidth + x)], CultureInfo.InvariantCulture));
										sample.Labels[ParseDAGSLabel(labels[(dy * SampleHeight + y) * imgWidth + (dx * SampleWidth + x)])]++;
									}
								for (int i = 0; i < sample.Labels.Length; i++)
								{
									sample.Labels[i] /= SampleSize;
								}
								Samples[sampleIndex] = sample;
							}
					}
				});
			}
			catch (Exception e)
			{
				MessageBox.Show(e.StackTrace, e.Message);
			}
		}

		private int ParseDAGSLabel(string strLabel)
		{
			var lbl = int.Parse(strLabel);
			switch (lbl)
			{
				case -1: //unknown
				case 0: //sky
				case 1: //tree
				case 4: //water
				case 5: //building
				case 6: //mountain
				case 7: //foreground obj
					lbl = 1;
					break;
				case 2:
				case 3:
					lbl = 0;
					break;
				default:
					lbl = 2;
					break;
			}
			return lbl;
		}
	}
}
