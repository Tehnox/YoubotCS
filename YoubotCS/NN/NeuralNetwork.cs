using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YoubotCS.Utils;

namespace YoubotCS.NN
{
	public class NeuralNetwork
	{
		public delegate void ChangedEventHandler(string status);

		public delegate double GetSampleLossDelegate(int correctClass);

		public delegate void LossFunctionActionDelegate(int correctClass);

		private double _max = 1D;

		private double _min = -1D;
		public double AvgTrainLoss;
		public int CurrentEpoch;
		public TimeSpan EpochDuration;
		public GetSampleLossDelegate GetSampleLoss;
		public event ChangedEventHandler StatusChanged;

		public Layer[] Layers;
		public LossFunctionActionDelegate LossFunctionAction;
		public ParallelOptions ParallelOption;
		public ThreadSafeRandom RandomGenerator;
		public int SampleIndex;

		private bool _stopped;
		public int TotalEpochs;

		private string _status;
		private string status
		{
			get { return _status; }
			set
			{
				_status = value;
				OnChanged(_status);
			}
		}

		public NeuralNetwork(DataProvider dataprovider, string name = "Neural Network", int classCount = 10,
			double trainTo = 0.8D,
			double dmicron = 0.02D, double min = -1.0, double max = 1.0)
		{
			DataProvider = dataprovider;
			NetworkIndex = 0;
			Name = name.Trim();
			ClassCount = classCount;
			TrainToValue = trainTo;
			LossFunctionAction = MeanSquareErrorLossFunction;
			GetSampleLoss = GetSampleLossMSE;

			dMicron = dmicron;
			Min = min;
			Max = max;

			Layers = new Layer[0];

			RandomGenerator = new ThreadSafeRandom();

			ParallelOption = new ParallelOptions
			{
				TaskScheduler = null,
				MaxDegreeOfParallelism = Environment.ProcessorCount
			};
			status = "Idle";
		}

		public DataProvider DataProvider { get; }
		public int NetworkIndex { get; private set; }
		public string Name { get; }
		public int ClassCount { get; }
		public double TrainToValue { get; }
		public bool SubstractMean { get; private set; }
		public double dMicron { get; private set; }
		public bool DropOutUsed { get; set; }
		public Layer LastLayer { get; private set; }

		public Sample CurrentSample { get; set; }
		//public double SampleRate;
		public double Spread { get; private set; }

		public double Min
		{
			get { return _min; }
			set
			{
				_min = value;
				Spread = Math.Abs(_max) - _min;
			}
		}

		public double Max
		{
			get { return _max; }
			set
			{
				_max = value;
				Spread = Math.Abs(_max) - Min;
			}
		}

		public void AddLayer(LayerTypes layerType, int mapCount, int mapWidth, int mapHeight)
		{
			var newLayer = new Layer(this, layerType, mapCount, mapWidth, mapHeight);

			Array.Resize(ref Layers, Layers.Length + 1);
			Layers[Layers.Length - 1] = newLayer;
		}


		public void AddLayer(LayerTypes layerType, ActivationFunctions activationFunction, int neuronCount)
		{
			var newLayer = new Layer(this, layerType, activationFunction, neuronCount);

			Array.Resize(ref Layers, Layers.Length + 1);
			Layers[Layers.Length - 1] = newLayer;
		}

		public void AddLayer(LayerTypes layerType, ActivationFunctions activationFunction, int mapCount, int mapWidth,
			int mapHeight,
			int receptiveFieldWidth, int receptiveFieldHeight, int strideX = 1, int strideY = 1, int padX = 0, int padY = 0,
			Mappings mappings = null)
		{
			var newLayer = new Layer(this, layerType, activationFunction, mapCount, mapWidth, mapHeight, receptiveFieldWidth,
				receptiveFieldHeight, strideX, strideY, padX, padY, mappings);

			Array.Resize(ref Layers, Layers.Length + 1);
			Layers[Layers.Length - 1] = newLayer;
		}

		public void SaveWeights(string fileName)
		{
			var totalWeightsCount = 0;
			for (var l = 1; l < Layers.Length; l++)
				if (Layers[l].HasWeights)
					totalWeightsCount += Layers[l].WeightCount;

			const int indexSize = sizeof (byte);
			const int weightSize = sizeof (double);
			const int recordSize = indexSize + weightSize;
			var fileSize = totalWeightsCount*recordSize;
			var info = new byte[fileSize];
			var layerWeightsOffset = 0;
			for (var l = 1; l < Layers.Length; l++)
			{
				if (!Layers[l].HasWeights) continue;

				for (var i = 0; i < Layers[l].WeightCount; i++)
				{
					var idx = layerWeightsOffset + i*recordSize;
					info[idx] = (byte) l;
					idx += indexSize;
					var temp = BitConverter.GetBytes(Layers[l].Weights[i].Value);
					for (var j = 0; j < weightSize; j++)
						info[idx + j] = temp[j];
				}

				layerWeightsOffset += Layers[l].WeightCount*recordSize;
			}

			using (var outFile = File.Create(fileName, fileSize, FileOptions.RandomAccess))
			{
				outFile.Write(info, 0, fileSize);
				outFile.Flush();
			}
			info = null;

			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		public bool LoadWeights(string fileName)
		{
			var buffer = File.ReadAllBytes(fileName);

			var indexSize = sizeof (byte);
			var weightSize = sizeof (double);
			var recordSize = indexSize + weightSize;
			var totalWeightCount = buffer.Length/recordSize;
			var checkWeightCount = 0;
			for (var l = 1; l < Layers.Length; l++)
				if (Layers[l].HasWeights)
					checkWeightCount += Layers[l].WeightCount;

			if (totalWeightCount == checkWeightCount)
			{
				byte oldLayerIdx = 0;
				var weightIdx = 0;
				for (var index = 0; index < buffer.Length; index += recordSize)
				{
					if (buffer[index] != oldLayerIdx)
					{
						weightIdx = 0;
						oldLayerIdx = buffer[index];
					}
					var temp = new byte[weightSize];
					for (var j = 0; j < weightSize; j++)
						temp[j] = buffer[index + j + indexSize];
					Layers[buffer[index]].Weights[weightIdx++].Value = BitConverter.ToDouble(temp, 0);
				}
			}
			else
				return false;

			buffer = null;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
			GC.WaitForPendingFinalizers();
			GC.Collect();

			return true;
		}

		public void InitializeWeights()
		{
			for (var i = 1; i < Layers.Length; i++)
			{
				if (Layers[i].HasWeights)
					Layers[i].InitializeWeights();

				// set the last layer in the Network class!
				if (Layers[i].NextLayer == null)
					LastLayer = Layers[i];
			}
		}

		private int ArgMin()
		{
			var bestIndex = 0;
			var minValue = double.MaxValue;

			for (var i = 0; i < ClassCount; i++)
			{
				if (LastLayer.Neurons[i].Output < minValue)
				{
					minValue = LastLayer.Neurons[i].Output;
					bestIndex = i;
				}
			}

			return bestIndex;
		}

		private int ArgMax()
		{
			var bestIndex = 0;
			var maxValue = double.MinValue;

			for (var i = 0; i < ClassCount; i++)
			{
				if (LastLayer.Neurons[i].Output > maxValue)
				{
					maxValue = LastLayer.Neurons[i].Output;
					bestIndex = i;
				}
			}

			return bestIndex;
		}

		private double GetSampleLossMSE(int correctClass)
		{
			var patternLoss = 0D;

			for (var i = 0; i < ClassCount; i++)
			{
				if (i == correctClass)
					patternLoss += MathUtil.Pow2(LastLayer.Neurons[i].Output - TrainToValue);
				else
					patternLoss += MathUtil.Pow2(LastLayer.Neurons[i].Output + TrainToValue);
			}

			return patternLoss*0.5D;
		}

		public void TrainingTask() //TODO: repair save mechanism based on best score
		{
			var oldSaveWeightsFileName = string.Empty;
			//var bestScore = (int) (DataProvider.SamplesCount / 100D * 80d);
			var sampleSize = DataProvider.SampleSize*DataProvider.SampleChannels;
			TotalEpochs = 100;
			_stopped = false;
			CurrentEpoch = 0;
			while (CurrentEpoch < TotalEpochs)
			{
				CurrentEpoch++;

				DataProvider.ScrambleTrainingSamples();
				AvgTrainLoss = 0D;
				var totLoss = 0D;

				for (SampleIndex = 0; SampleIndex < DataProvider.SamplesCount; SampleIndex++)
				{
					status = $"Epoch:  {CurrentEpoch}/{TotalEpochs}, sample: {SampleIndex}/{DataProvider.SamplesCount}, avg loss: {AvgTrainLoss}";
					for (var i = 0; i < sampleSize; i++)
						Layers[0].Neurons[i].Output =
							DataProvider.Samples[DataProvider.RandomSample[SampleIndex]].Image[i] / 255D * Spread + Min;

					// fprop
					for (var i = 1; i < Layers.Length; i++)
						Layers[i].CalculateAction();

					// Mean Square Error loss
					var patternLoss = 0D;
					for (var i = 0; i < ClassCount; i++)
						patternLoss +=
							MathUtil.Pow2(LastLayer.Neurons[i].Output -
							              (DataProvider.Samples[DataProvider.RandomSample[SampleIndex]].Labels[i] * Spread + Min));

					patternLoss *= 0.5D;
					totLoss += patternLoss;
					AvgTrainLoss = totLoss/(SampleIndex + 1);

					for (var i = 0; i < ClassCount; i++)
						LastLayer.Neurons[i].ErrX = LastLayer.Neurons[i].Output -
						                            (DataProvider.Samples[DataProvider.RandomSample[SampleIndex]].Labels[i] * Spread + Min);

					// bprop
					for (var i = Layers.Length - 1; i > 1; i--)
					{
						Layers[i].EraseGradientWeights();
						Layers[i].BackpropagateAction();
						Layers[i].UpdateWeights();
					}

					if (_stopped)
					{
						status = "Idle";
						return;
					}
				}
				// epoch save
				SaveWeights(DataProvider.DataDirectory + @"\weights\" + Name + " (epoch " + CurrentEpoch + " - " +
				            AvgTrainLoss + " AvgTrainLoss).weights-bin");
				oldSaveWeightsFileName = DataProvider.DataDirectory + @"\weights\" + Name + " (epoch " + CurrentEpoch +
				                         " - " + AvgTrainLoss + " AvgTrainLoss).weights-bin";
			}

			// end save
			var fileName = DataProvider.DataDirectory + @"\" + Name + " (epoch " + CurrentEpoch + " - " +
			               AvgTrainLoss + " AvgTrainLoss).weights-bin";
			SaveWeights(fileName);
			if ((oldSaveWeightsFileName != string.Empty) && File.Exists(oldSaveWeightsFileName))
				File.Delete(oldSaveWeightsFileName);
		}

		public void Calculate()
		{
			for (var i = 1; i < Layers.Length; i++)
				Layers[i].CalculateAction();
		}

		public void MeanSquareErrorLossFunction(int correctClass)
		{
			for (var i = 0; i < ClassCount; i++)
				LastLayer.Neurons[i].ErrX = LastLayer.Neurons[i].Output + TrainToValue;
			LastLayer.Neurons[correctClass].ErrX = LastLayer.Neurons[correctClass].Output - TrainToValue;
		}

		public void Backpropagate(int correctClass)
		{
			LossFunctionAction(correctClass);

			for (var i = Layers.Length - 1; i > 1; i--)
				Layers[i].BackpropagateAction();
		}

		public double[] Test(byte[] image)
		{
			var sampleSize = DataProvider.SampleSize*DataProvider.SampleChannels;
			for (var i = 0; i < sampleSize; i++)
				Layers[0].Neurons[i].Output = image[i]/255D*Spread + Min;
			Calculate();
			return LastLayer.GetOutput();
		}
		protected virtual void OnChanged(string status)
		{
			StatusChanged?.Invoke(status);
		}

		public void Stop()
		{
			_stopped = true;
		}
	}
}