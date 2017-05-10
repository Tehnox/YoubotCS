using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using YoubotCS.Utils;

namespace YoubotCS.NN
{
	public struct Neuron
	{
		public double ErrX { get; set; }
		public double Output { get; set; }
	}

	public struct Weight
	{
		public double Value { get; set; }
		public double PastValue { get; set; }
		public double Err { get; set; }
	}

	public struct Connection
	{
		public int ToNeuronIndex;
		public int ToWeightIndex;

		public Connection(int toNeuronIndex, int toWeightIndex)
		{
			ToNeuronIndex = toNeuronIndex;
			ToWeightIndex = toWeightIndex;
		}
	}

	public enum LayerTypes
	{
		Input,
		FullyConnected,
		Convolutional,
		MaxPooling,
		Local,
		AvgPooling
	}

	public enum ActivationFunctions
	{
		Logistic,
		None,
		STanh,
		Tanh,
		ReLU,
		Ident,
		SoftMax
	}

	public sealed class Mappings
	{
		public bool[] Mapping;

		public Mappings(bool[] mapping)
		{
			if (mapping == null)
				throw new ArgumentException("Invalid Mappings parameter(s)");

			Mapping = mapping;
		}

		public Mappings(int previousLayerMapCount, int currentLayerMapCount, int density, int randomSeed = 0)
		{
			if ((previousLayerMapCount < 1) || (currentLayerMapCount < 1))
				throw new ArgumentException("Invalid Mappings parameter(s)");

			Mapping = new bool[previousLayerMapCount*currentLayerMapCount];
			var random = new Random(randomSeed);

			for (var channel = 0; channel < previousLayerMapCount; channel++)
				for (var map = 0; map < currentLayerMapCount; map++)
					Mapping[channel*currentLayerMapCount + map] = random.Next(100) < density;
		}

		public bool IsMapped(int map, int previousLayerMapCount, int currentLayerMapCount)
		{
			return Mapping[map + previousLayerMapCount*currentLayerMapCount];
		}
	}

	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public class Layer
	{
		public readonly Connection[][] Connections;
		public Func<double, double> ActivationFunction;
		public ActivationFunctions ActivationFunctionId;
		public Action BackpropagateAction;
		public Action CalculateAction;
		public Func<double, double> DerivativeActivationFunction;
		public Action EraseGradientWeights;
		public double[][] Gaussian2DKernel;
		public bool HasWeights;
		public double InitBias;
		public double InitWeight;
		public bool IsFullyMapped;
		public int LayerIndex;
		public LayerTypes LayerType;
		public int MapCount;
		public int MapHeight;
		public Mappings Mappings;
		public int MapSize;
		public int MapWidth;
		public double Momentum = 0.0005;
		public string Name;
		public NeuralNetwork Network;
		public int[] NeuronActive;
		public int NeuronCount;
		public Neuron[] Neurons;
		public Layer NextLayer;
		public int PadX;
		public int PadY;
		public Layer PreviousLayer;
		public int ReceptiveFieldHeight;
		public int ReceptiveFieldSize;
		public int ReceptiveFieldWidth;
		public int StrideX;
		public int StrideY;
		public double SubsamplingScalingFactor;
		public double Teta = 0.005;
		public Action UpdateWeights;
		public bool UseMapInfo;
		public int WeightCount;
		public Weight[] Weights;

		public Layer(NeuralNetwork network, LayerTypes layerType, int mapCount, int mapWidth, int mapHeight)
			: this(
				network, network.Layers.Length, layerType, ActivationFunctions.None, mapCount*mapWidth*mapHeight, true, mapCount,
				mapWidth, mapHeight, true,
				0, 0, 1, 1, 0, 0, network.Layers.Length == 0 ? null : network.Layers[network.Layers.Length - 1], null)
		{
		}

		public Layer(NeuralNetwork network, LayerTypes layerType, ActivationFunctions activationFunction, int neuronCount)
			: this(
				network, network.Layers.Length, layerType, activationFunction, neuronCount, false, 1, 1, 1, true, 0, 0, 0, 0, 0, 0,
				network.Layers.Length == 0 ? null : network.Layers[network.Layers.Length - 1], null)
		{
		}

		public Layer(NeuralNetwork network, LayerTypes layerType, ActivationFunctions activationFunction, int mapCount,
			int mapWidth,
			int mapHeight, int receptiveFieldWidth, int receptiveFieldHeight, int strideX, int strideY, int padX, int padY,
			Mappings mappings)
			: this(network, network.Layers.Length, layerType, activationFunction, mapCount*mapWidth*mapHeight, true, mapCount,
				mapWidth, mapHeight, mappings == null, receptiveFieldWidth, receptiveFieldHeight, strideX, strideY, padX, padY,
				network.Layers[network.Layers.Length - 1], mappings)
		{
		}

		public Layer(NeuralNetwork network, int layerIndex, LayerTypes layerType, ActivationFunctions activationFunction,
			int neuronCount, bool useMapInfo, int mapCount,
			int mapWidth, int mapHeight, bool isFullyMapped, int receptiveFieldWidth, int receptiveFieldHeight, int strideX,
			int strideY, int padX, int padY,
			Layer previousLayer, Mappings mappings)
		{
			Network = network;
			LayerIndex = layerIndex;
			LayerType = layerType;
			ActivationFunctionId = activationFunction;
			NeuronCount = neuronCount;
			UseMapInfo = useMapInfo;
			MapCount = mapCount;
			MapWidth = mapWidth;
			MapHeight = mapHeight;
			MapSize = MapWidth*MapHeight;
			IsFullyMapped = isFullyMapped;
			ReceptiveFieldWidth = receptiveFieldWidth;
			ReceptiveFieldHeight = receptiveFieldHeight;
			ReceptiveFieldSize = ReceptiveFieldWidth*ReceptiveFieldHeight;
			StrideX = strideX;
			StrideY = strideY;
			PadX = padX;
			PadY = padY;
			Mappings = mappings;
			PreviousLayer = previousLayer;

			NeuronActive = new int[NeuronCount];
			Neurons = new Neuron[NeuronCount];
			Connections = new Connection[NeuronCount][];
			for (var i = 0; i < NeuronCount; i++)
			{
				NeuronActive[i] = 1;
				Neurons[i].Output = 0D;
				Neurons[i].ErrX = 0D;
				Connections[i] = new Connection[0];
			}

			int totalMappings;
			int maskWidth;
			int maskHeight;
			int maskSize;
			var cMid = ReceptiveFieldWidth/2;
			var rMid = ReceptiveFieldHeight/2;
			int[] kernelTemplate;
			int[] maskMatrix;

			UpdateWeights = UpdateWeighsSGD;

			switch (LayerType)
			{
				case LayerTypes.Input:
					ActivationFunctionId = ActivationFunctions.None;
					HasWeights = false;
					ActivationFunction = null;
					WeightCount = 0;
					Weights = null;
					CalculateAction = null;
					BackpropagateAction = null;
					break;

				case LayerTypes.Convolutional:
					if (IsFullyMapped)
						totalMappings = PreviousLayer.MapCount*MapCount;
					else
					{
						if (Mappings != null)
						{
							if (Mappings.Mapping.Length == PreviousLayer.MapCount*MapCount)
								totalMappings = Mappings.Mapping.Count(p => p);
							else
								throw new ArgumentException("Invalid mappings definition");
						}
						else
							throw new ArgumentException("Empty mappings definition");
					}

					HasWeights = true;
					WeightCount = totalMappings*ReceptiveFieldSize + MapCount;
					Weights = new Weight[WeightCount];

					CalculateAction = CalculateCCF;
					BackpropagateAction = BackpropagateCCF;

					EraseGradientWeights = EraseGradientsWeights;

					maskWidth = PreviousLayer.MapWidth + 2*PadX;
					maskHeight = PreviousLayer.MapHeight + 2*PadY;
					maskSize = maskWidth*maskHeight;

					kernelTemplate = new int[ReceptiveFieldSize];
					for (var row = 0; row < ReceptiveFieldHeight; row++)
						for (var column = 0; column < ReceptiveFieldWidth; column++)
							kernelTemplate[column + row*ReceptiveFieldWidth] = column + row*maskWidth;

					maskMatrix = new int[maskSize*PreviousLayer.MapCount];
					for (var i = 0; i < maskSize*PreviousLayer.MapCount; i++)
						maskMatrix[i] = -1;
					Parallel.For(0, PreviousLayer.MapCount, map =>
					{
						for (var y = PadY; y < PreviousLayer.MapHeight + PadY; y++)
							for (var x = PadX; x < PreviousLayer.MapWidth + PadX; x++)
								maskMatrix[x + y*maskWidth + map*maskSize] = x - PadX + (y - PadY)*PreviousLayer.MapWidth +
								                                             map*PreviousLayer.MapSize;
					});

					if (!IsFullyMapped)
					{
						var mapping = 0;
						var mappingCount = new int[MapCount*PreviousLayer.MapCount];
						for (var curMap = 0; curMap < MapCount; curMap++)
							for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
							{
								mappingCount[prevMap + curMap*PreviousLayer.MapCount] = mapping;
								if (Mappings.IsMapped(curMap, prevMap, MapCount))
									mapping++;
							}

						Parallel.For(0, MapCount, curMap =>
						{
							for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
							{
								var positionPrevMap = prevMap*maskSize;

								if (!Mappings.IsMapped(curMap, prevMap, MapCount)) continue;

								for (var y = 0; y < MapHeight; y++)
									for (var x = 0; x < MapWidth; x++)
									{
										var position = x + y*MapWidth + curMap*MapSize;
										var iNumWeight = mappingCount[prevMap + curMap*PreviousLayer.MapCount]*ReceptiveFieldSize + MapCount;

										AddBias(ref Connections[position], curMap);

										for (var row = 0; row < ReceptiveFieldHeight; row++)
											for (var column = 0; column < ReceptiveFieldWidth; column++)
											{
												var pIndex = x + y*maskWidth + kernelTemplate[column + row*ReceptiveFieldWidth] + positionPrevMap;
												if (maskMatrix[pIndex] != -1)
													AddConnection(ref Connections[position], maskMatrix[pIndex], iNumWeight++);
											}
									}
							}
						});
					}
					else // Fully mapped
					{
						if (totalMappings > MapCount)
						{
							Parallel.For(0, MapCount, curMap =>
							{
								for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
								{
									var positionPrevMap = prevMap*maskSize;
									var mapping = prevMap + curMap*PreviousLayer.MapCount;
									for (var y = 0; y < MapHeight; y++)
										for (var x = 0; x < MapWidth; x++)
										{
											var position = x + y*MapWidth + curMap*MapSize;
											var iNumWeight = mapping*ReceptiveFieldSize + MapCount;

											AddBias(ref Connections[position], curMap);

											for (var row = 0; row < ReceptiveFieldHeight; row++)
												for (var column = 0; column < ReceptiveFieldWidth; column++)
												{
													var pIndex = x + y*maskWidth + kernelTemplate[column + row*ReceptiveFieldWidth] + positionPrevMap;
													if (maskMatrix[pIndex] != -1)
														AddConnection(ref Connections[position], maskMatrix[pIndex], iNumWeight++);
												}
										}
								}
							});
						}
						else
						// PreviousLayer has only one map         // 36*36 phantom input , padXY=2, filterSize=5, results in 32x32 conv layer
						{
							Parallel.For(0, MapCount, curMap =>
							{
								for (var y = 0; y < MapHeight; y++)
									for (var x = 0; x < MapWidth; x++)
									{
										var position = x + y*MapWidth + curMap*MapSize;
										var iNumWeight = MapCount + curMap*ReceptiveFieldSize;

										AddBias(ref Connections[position], curMap);

										for (var row = 0; row < ReceptiveFieldHeight; row++)
											for (var column = 0; column < ReceptiveFieldWidth; column++)
											{
												var pIndex = x + y*maskWidth + kernelTemplate[column + row*ReceptiveFieldWidth];
												if (maskMatrix[pIndex] != -1)
													AddConnection(ref Connections[position], maskMatrix[pIndex], iNumWeight++);
											}
										//int pos;
										//for (int row = 0; row < ReceptiveFieldHeight; row++)
										//    for (int column = 0; column < ReceptiveFieldWidth; column++)
										//    {
										//        pos = x + (y * MapWidth) + column + (row * previousLayer.MapWidth);
										//            AddConnection(ref Connections[position], pos, iNumWeight++);
										//    }
									}
							});
						}
					}
					break;

				case LayerTypes.MaxPooling:
				case LayerTypes.AvgPooling:
					HasWeights = true;
					WeightCount = MapCount*2;
					Weights = new Weight[WeightCount];

					if (LayerType == LayerTypes.MaxPooling)
					{
						CalculateAction = CalculateMaxPooling;
						BackpropagateAction = BackpropagateMaxPooling;
					}
					else
					{
						CalculateAction = CalculateAveragePooling;
						BackpropagateAction = BackpropagateAveragePooling;
					}

					EraseGradientWeights = EraseGradientsWeights;

					SubsamplingScalingFactor = 1.0D/(StrideX*StrideY);

					if (PreviousLayer.MapCount > 1) //fully symmetrical mapped
					{
						if (ReceptiveFieldSize != StrideX*StrideY)
						{
							Parallel.For(0, MapCount, curMap =>
							{
								for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
								{
									var positionPrevMap = prevMap*PreviousLayer.MapSize;

									if (prevMap != curMap) continue;

									for (var y = 0; y < MapHeight; y++)
										for (var x = 0; x < MapWidth; x++)
										{
											var position = x + y*MapWidth + curMap*MapSize;
											var iNumWeight = curMap*2;
											AddBias(ref Connections[position], iNumWeight++);

											var outOfBounds = false;
											for (var row = -rMid; row <= rMid; row++)
												for (var col = -cMid; col <= cMid; col++)
												{
													if (row + y*StrideY < 0)
														outOfBounds = true;
													if (row + y*StrideY >= PreviousLayer.MapHeight)
														outOfBounds = true;
													if (col + x*StrideX < 0)
														outOfBounds = true;
													if (col + x*StrideX >= PreviousLayer.MapWidth)
														outOfBounds = true;
													if (!outOfBounds)
														AddConnection(ref Connections[position],
															col + x*StrideX + (row + y*StrideY)*PreviousLayer.MapWidth + positionPrevMap, iNumWeight);
													else
														outOfBounds = false;
												}
										}
								}
							});
						}
						else
						{
							Parallel.For(0, MapCount, curMap =>
							{
								for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
								{
									var positionPrevMap = prevMap*PreviousLayer.MapWidth*PreviousLayer.MapHeight;

									if (prevMap != curMap) continue;

									for (var y = 0; y < MapHeight; y++)
										for (var x = 0; x < MapWidth; x++)
										{
											var position = x + y*MapWidth + curMap*MapSize;
											var iNumWeight = curMap*2;
											AddBias(ref Connections[position], iNumWeight++);

											for (var row = 0; row < ReceptiveFieldHeight; row++)
												for (var col = 0; col < ReceptiveFieldWidth; col++)
													AddConnection(ref Connections[position],
														col + x*StrideX + (row + y*StrideY)*PreviousLayer.MapWidth + positionPrevMap, iNumWeight);
										}
								}
							});
						}
					}
					break;

				case LayerTypes.FullyConnected:
					HasWeights = true;
					WeightCount = (PreviousLayer.NeuronCount + 1)*NeuronCount;
					Weights = new Weight[WeightCount];
					CalculateAction = CalculateFullyConnected;
					BackpropagateAction = BackpropagateCCF;

					EraseGradientWeights = EraseGradientsWeights;

					if (UseMapInfo)
					{
						var iNumWeight = 0;
						Parallel.For(0, MapCount, curMap =>
						{
							for (var yc = 0; yc < MapHeight; yc++)
								for (var xc = 0; xc < MapWidth; xc++)
								{
									var position = xc + yc*MapWidth + curMap*MapSize;
									AddBias(ref Connections[position], iNumWeight++);

									for (var prevMaps = 0; prevMaps < PreviousLayer.MapCount; prevMaps++)
										for (var y = 0; y < PreviousLayer.MapHeight; y++)
											for (var x = 0; x < PreviousLayer.MapWidth; x++)
												AddConnection(ref Connections[position], x + y*PreviousLayer.MapWidth + prevMaps*PreviousLayer.MapSize,
													iNumWeight++);
								}
						});
					}
					else
					{
						var iNumWeight = 0;
						for (var y = 0; y < NeuronCount; y++)
						{
							AddBias(ref Connections[y], iNumWeight++);
							for (var x = 0; x < PreviousLayer.NeuronCount; x++)
								AddConnection(ref Connections[y], x, iNumWeight++);
						}
					}
					break;

				case LayerTypes.Local:
					if (IsFullyMapped)
						totalMappings = PreviousLayer.MapCount*MapCount;
					else
					{
						if (Mappings != null)
						{
							if (Mappings.Mapping.Length == PreviousLayer.MapCount*MapCount)
								totalMappings = Mappings.Mapping.Count(p => p);
							else
								throw new ArgumentException("Invalid mappings definition");
						}
						else
							throw new ArgumentException("Empty mappings definition");
					}

					HasWeights = true;
					WeightCount = totalMappings*MapSize*(ReceptiveFieldSize + 1);
					Weights = new Weight[WeightCount];

					CalculateAction = CalculateCCF; //CalculateLocalConnected;
					BackpropagateAction = BackpropagateCCF;

					EraseGradientWeights = EraseGradientsWeights;


					maskWidth = PreviousLayer.MapWidth + 2*PadX;
					maskHeight = PreviousLayer.MapHeight + 2*PadY;
					maskSize = maskWidth*maskHeight;

					kernelTemplate = new int[ReceptiveFieldSize];
					for (var row = 0; row < ReceptiveFieldHeight; row++)
						for (var column = 0; column < ReceptiveFieldWidth; column++)
							kernelTemplate[column + row*ReceptiveFieldWidth] = column + row*maskWidth;

					maskMatrix = new int[maskSize*PreviousLayer.MapCount];
					for (var i = 0; i < maskSize*PreviousLayer.MapCount; i++)
						maskMatrix[i] = -1;
					Parallel.For(0, PreviousLayer.MapCount, map =>
					{
						for (var y = PadY; y < PreviousLayer.MapHeight + PadY; y++)
							for (var x = PadX; x < PreviousLayer.MapWidth + PadX; x++)
								maskMatrix[x + y*maskHeight + map*maskSize] = x - PadX + (y - PadY)*PreviousLayer.MapWidth +
								                                              map*PreviousLayer.MapSize;
					});

					if (!IsFullyMapped)
					{
						var mapping = 0;
						var mappingCount = new int[MapCount*PreviousLayer.MapCount];
						for (var curMap = 0; curMap < MapCount; curMap++)
							for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
							{
								mappingCount[prevMap + curMap*PreviousLayer.MapCount] = mapping;
								if (Mappings.IsMapped(curMap, prevMap, MapCount))
									mapping++;
							}

						Parallel.For(0, MapCount, curMap =>
						{
							for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
							{
								var positionPrevMap = prevMap*maskSize;

								if (!Mappings.IsMapped(curMap, prevMap, MapCount)) continue;

								var iNumWeight = mappingCount[prevMap + curMap*PreviousLayer.MapCount]*(ReceptiveFieldSize + 1)*MapSize;

								for (var y = 0; y < MapHeight; y++)
									for (var x = 0; x < MapWidth; x++)
									{
										var position = x + y*MapWidth + curMap*MapSize;

										AddBias(ref Connections[position], iNumWeight++);

										for (var row = 0; row < ReceptiveFieldHeight; row++)
											for (var column = 0; column < ReceptiveFieldWidth; column++)
											{
												var pIndex = x + y*maskWidth + kernelTemplate[column + row*ReceptiveFieldWidth] + positionPrevMap;
												if (maskMatrix[pIndex] != -1)
													AddConnection(ref Connections[position], maskMatrix[pIndex], iNumWeight++);
											}
									}
							}
						});
					}
					else // Fully mapped
					{
						if (totalMappings > MapCount)
						{
							Parallel.For(0, MapCount, curMap =>
							{
								for (var prevMap = 0; prevMap < PreviousLayer.MapCount; prevMap++)
								{
									var positionPrevMap = prevMap*maskSize;
									var mapping = prevMap + curMap*PreviousLayer.MapCount;
									var iNumWeight = mapping*(ReceptiveFieldSize + 1)*MapSize;
									for (var y = 0; y < MapHeight; y++)
										for (var x = 0; x < MapWidth; x++)
										{
											var position = x + y*MapWidth + curMap*MapSize;

											AddBias(ref Connections[position], iNumWeight++);

											for (var row = 0; row < ReceptiveFieldHeight; row++)
												for (var column = 0; column < ReceptiveFieldWidth; column++)
												{
													var pIndex = x + y*maskWidth + kernelTemplate[column + row*ReceptiveFieldWidth] + positionPrevMap;
													if (maskMatrix[pIndex] != -1)
														AddConnection(ref Connections[position], maskMatrix[pIndex], iNumWeight++);
												}
										}
								}
							});
						}
						else
						// PreviousLayer has only one map         // 36*36 phantom input , padXY=2, filterSize=5, results in 32x32 conv layer
						{
							Parallel.For(0, MapCount, curMap =>
							{
								var iNumWeight = curMap*(ReceptiveFieldSize + 1)*MapSize;
								for (var y = 0; y < MapHeight; y++)
									for (var x = 0; x < MapWidth; x++)
									{
										var position = x + y*MapWidth + curMap*MapSize;

										AddBias(ref Connections[position], iNumWeight++);

										for (var row = 0; row < ReceptiveFieldHeight; row++)
											for (var column = 0; column < ReceptiveFieldWidth; column++)
											{
												var pIndex = x + y*maskWidth + kernelTemplate[column + row*ReceptiveFieldWidth];
												if (maskMatrix[pIndex] != -1)
													AddConnection(ref Connections[position], maskMatrix[pIndex], iNumWeight++);
											}
									}
							});
						}
					}
					break;
			}
			switch (ActivationFunctionId)
			{
				case ActivationFunctions.Logistic:
					ActivationFunction = Logistic;
					DerivativeActivationFunction = DLogistic;
					break;

				case ActivationFunctions.None:
					ActivationFunction = null;
					DerivativeActivationFunction = null;
					break;

				case ActivationFunctions.Tanh:
					ActivationFunction = Tanh;
					DerivativeActivationFunction = DTanh;
					break;

				case ActivationFunctions.STanh:
					ActivationFunction = STanh;
					DerivativeActivationFunction = DSTanh;
					break;
				case ActivationFunctions.ReLU:
					ActivationFunction = ReLU;
					DerivativeActivationFunction = DReLU;
					break;
				case ActivationFunctions.Ident:
					ActivationFunction = Ident;
					DerivativeActivationFunction = DIdent;
					break;
				case ActivationFunctions.SoftMax:
					ActivationFunction = Ident;
					DerivativeActivationFunction = DSoftMax;
					break;
			}
			var totalConnections = 0;
			for (var i = 0; i < NeuronCount; i++)
				totalConnections += Connections[i].Length;

			if (PreviousLayer != null)
				PreviousLayer.NextLayer = this;
		}


		public void AddConnection(ref Connection[] connections, int neuronIndex, int weightIndex)
		{
			Array.Resize(ref connections, connections.Length + 1);
			connections[connections.Length - 1] = new Connection(neuronIndex, weightIndex);
		}

		public void AddBias(ref Connection[] connections, int weightIndex)
		{
			Array.Resize(ref connections, connections.Length + 1);
			connections[connections.Length - 1] = new Connection(int.MaxValue, weightIndex);
		}

		public void InitializeWeights()
		{
			checked
			{
				if (Weights != null && WeightCount > 0)
				{
					Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
					{
						var stdDev = 1D/Math.Sqrt(Connections[i].Length);
						foreach (var connection in Connections[i])
						{
							Weights[connection.ToWeightIndex].Value = Network.RandomGenerator.NextDouble(stdDev);
							Weights[connection.ToWeightIndex].PastValue = 0;
						}
					});
				}
			}
		}

		public void CalculateAveragePooling()
		{
			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var sf = 1D/(Connections[i].Length - 1);
				var dSum = 0D;
				foreach (var connection in Connections[i])
				{
					if (connection.ToNeuronIndex == int.MaxValue)
						dSum += Weights[connection.ToWeightIndex].Value;
					else
						dSum += Weights[connection.ToWeightIndex].Value*PreviousLayer.Neurons[connection.ToNeuronIndex].Output*sf;
				}
				Neurons[i].Output = ActivationFunction(dSum);
			});
		}

		public void CalculateFullyConnected()
		{
			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var idx = i*(PreviousLayer.NeuronCount + 1);
				var dSum = Weights[idx++].Value;
				for (var c = 0; c < PreviousLayer.NeuronCount; c++)
					dSum += Weights[idx + c].Value*PreviousLayer.Neurons[c].Output;
				Neurons[i].Output = ActivationFunction(dSum);
			});
		}

		public void CalculateCCF()
		{
			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var dSum = 0D;
				foreach (var connection in Connections[i])
				{
					if (connection.ToNeuronIndex == int.MaxValue)
						dSum += Weights[connection.ToWeightIndex].Value;
					else
						dSum += Weights[connection.ToWeightIndex].Value*PreviousLayer.Neurons[connection.ToNeuronIndex].Output;
				}
				Neurons[i].Output = ActivationFunction(dSum);
			});
		}

		public void CalculateMaxPooling()
		{
			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var bias = 0D;
				var weight = 0D;
				var max = double.MinValue;
				foreach (var connection in Connections[i])
				{
					if (connection.ToNeuronIndex == int.MaxValue)
						bias = Weights[connection.ToWeightIndex].Value;
					else if (PreviousLayer.Neurons[connection.ToNeuronIndex].Output >= max)
					{
						weight = Weights[connection.ToWeightIndex].Value;
						max = PreviousLayer.Neurons[connection.ToNeuronIndex].Output;
						NeuronActive[i] = connection.ToNeuronIndex;
					}
				}
				Neurons[i].Output = ActivationFunction(max*weight + bias);
			});
		}

		public void EraseGradientsWeights()
		{
			Parallel.For(0, WeightCount, Network.ParallelOption, i => Weights[i].Err = 0D);
		}

		public void UpdateWeighsSGD()
		{
			Parallel.For(0, WeightCount, Network.ParallelOption, i =>
			{
				var pastValue = Weights[i].Value;
				//Weights[i].Value -= ((Teta / Network.CurrentEpoch) * Weights[i].Err) - (Momentum / Network.CurrentEpoch * Weights[i].PastValue);
				Weights[i].Value -= Teta/Network.CurrentEpoch*Weights[i].Err - Momentum/Network.CurrentEpoch*Teta*Weights[i].Value;
				Weights[i].PastValue = pastValue;
			});
		}

		public void NoUpdate(int batchSize = 1)
		{
			// do nothing
		}

		public void NoErase()
		{
			// do nothing
		}

		public void BackpropagateCCF()
		{
			Parallel.For(0, PreviousLayer.NeuronCount, Network.ParallelOption, i => PreviousLayer.Neurons[i].ErrX = 0D);

			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var neuronD1ErrY = DerivativeActivationFunction(Neurons[i].Output)*Neurons[i].ErrX;

				foreach (var connection in Connections[i])
				{
					if (connection.ToNeuronIndex == int.MaxValue)
						Weights[connection.ToWeightIndex].Err += neuronD1ErrY;
					else
					{
						Weights[connection.ToWeightIndex].Err += neuronD1ErrY*PreviousLayer.Neurons[connection.ToNeuronIndex].Output;
						PreviousLayer.Neurons[connection.ToNeuronIndex].ErrX += neuronD1ErrY*Weights[connection.ToWeightIndex].Value;
					}
				}
			});
		}

		public void BackpropagateAveragePooling()
		{
			Parallel.For(0, PreviousLayer.NeuronCount, Network.ParallelOption, i => PreviousLayer.Neurons[i].ErrX = 0D);

			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var sf = 1D/(Connections[i].Length - 1);
				var neuronD1ErrY = DerivativeActivationFunction(Neurons[i].Output)*Neurons[i].ErrX;

				foreach (var connection in Connections[i])
				{
					if (connection.ToNeuronIndex == int.MaxValue)
						Weights[connection.ToWeightIndex].Err += neuronD1ErrY;
					else
					{
						Weights[connection.ToWeightIndex].Err += neuronD1ErrY*PreviousLayer.Neurons[connection.ToNeuronIndex].Output*sf;
						PreviousLayer.Neurons[connection.ToNeuronIndex].ErrX += neuronD1ErrY*Weights[connection.ToWeightIndex].Value;
					}
				}
			});
		}

		public void BackpropagateMaxPooling()
		{
			Parallel.For(0, PreviousLayer.NeuronCount, Network.ParallelOption, i => PreviousLayer.Neurons[i].ErrX = 0D);

			Parallel.For(0, NeuronCount, Network.ParallelOption, i =>
			{
				var neuronD1ErrY = DerivativeActivationFunction(Neurons[i].Output)*Neurons[i].ErrX;
				var idx = 0;
				foreach (var connection in Connections[i])
				{
					if (connection.ToNeuronIndex == int.MaxValue)
						Weights[connection.ToWeightIndex].Err += neuronD1ErrY;
					else
					{
						idx = connection.ToWeightIndex;
						PreviousLayer.Neurons[connection.ToNeuronIndex].ErrX += neuronD1ErrY*Weights[connection.ToWeightIndex].Value;
					}
				}
				Weights[idx].Err += neuronD1ErrY*PreviousLayer.Neurons[NeuronActive[i]].Output;
			});
		}

		private static double Tanh(double value)
		{
			return MathUtil.Tanh(value);
		}

		private static double DTanh(double value)
		{
			return 1D - value*value;
		}

		private static double STanh(double value)
		{
			return MathUtil.SymmetricTanh(value);
		}

		private static double DSTanh(double value)
		{
			return MathUtil.DSymmetricTanh(value);
		}

		private static double Logistic(double value)
		{
			return 1d/(1d + Math.Exp(-value));
		}

		private static double DLogistic(double value)
		{
			return value*(1D - value);
		}

		private static double ReLU(double value)
		{
			//return value < 0D ? 0D : value > 6 ? 6 : value;

			return value < 0D ? 0D : value;
		}

		private static double DReLU(double value)
		{
			//return (value > 0D) && (value <= 6) ? 1D : 0D;

			return value > 0D ? 1D : 0D;
		}

		private static double Ident(double value)
		{
			return value;
		}

		private static double DIdent(double value)
		{
			return 1D;
		}

		private static double DSoftMax(double value)
		{
			return value*(1D - value);
			//return value - (value * value);
		}

		public double[] GetOutput()
		{
			var ret = new double[NeuronCount];
			for (var i = 0; i < ret.Length; i++)
			{
				ret[i] = Neurons[i].Output;
			}
			return ret;
		}
	}
}