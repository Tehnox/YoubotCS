using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoubotCS.Utils
{
	public struct Vector
	{
		public int x;
		public int y;
		public int z;

		public Vector(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
	}
	public static class MathUtil
	{
		public const double SymmetricTanhAlpha = 1.71593428D;
		public const double SymmetricTanhBeta = 2D / 3D;
		public const double Scale = 0.001D;
		public const double Pow = 0.75D;

		public static double Bound(double value)
		{
			if (value < -1.0E20)
				return -1.0E20;

			return value > 1.0E20 ? 1.0E20 : value;
		}

		public static double Pow2(double value)
		{
			return value * value;
		}

		public static double Pow3(double value)
		{
			return value * value * value;
		}

		public static double Exp(double value)
		{
			return Bound(Math.Exp(value));
		}

		public static double Log(double value)
		{
			return Bound(Math.Log(value));
		}

		public static double Tanh(double value)
		{
			return 2D / (1D + Exp(-2D * value)) - 1D;
		}

		private static double Sech(double value)
		{
			return 2D / (Exp(value) + Exp(-value));
		}

		public static double SymmetricTanh(double value)
		{
			return SymmetricTanhAlpha * Tanh(SymmetricTanhBeta * value);
		}

		public static double DSymmetricTanh(double value)
		{
			return SymmetricTanhAlpha * SymmetricTanhBeta * Pow2(Sech(SymmetricTanhBeta * value));
		}

		public static double[][] CreateGaussian2DKernel(int width, int height, double stdDeviation = 1D)
		{
			double[][] gKernel = new double[width][];

			// set standard deviation to 1.0     
			double sigma = stdDeviation;
			double r, s = 2D * sigma * sigma;
			// sum is for normalization     
			double sum = 0D;
			// generate 5x5 kernel
			int hX = width / 2;
			int hY = height / 2;
			for (int x = -hX; x <= hX; x++)
			{
				gKernel[x + hX] = new double[height];
				for (int y = -hY; y <= hY; y++)
				{
					r = Math.Sqrt((x * x) + (y * y));
					gKernel[x + hX][y + hY] = (Exp(-(r * r) / s)) / (Math.PI * s);
					sum += gKernel[x + hX][y + hY];
				}
			}

			// normalize the Kernel     
			for (int x = 0; x < width; x++)
				for (int y = 0; y < height; y++)
					gKernel[x][y] /= sum;

			return gKernel;
		}
	}
}

