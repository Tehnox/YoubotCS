using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
	}
}
