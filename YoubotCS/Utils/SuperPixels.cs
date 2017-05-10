using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YoubotCS.Utils
{
	public class SuperPixels
	{
		private int _width;
		private int _height;

		private double[] _lvec;
		private double[] _avec;
		private double[] _bvec;

		public List<double> kseedsx;
		public List<double> kseedsy;

		public SuperPixels()
		{
			kseedsx = new List<double>();
			kseedsy = new List<double>();
		}

		public int PerformForGivenK(int[] imgBuffer, int width, int height, ref int[] klabels, int K, int m)
		{
			var kseedsl = new List<double>();
			var kseedsa = new List<double>();
			var kseedsb = new List<double>();

			_width = width;
			_height = height;
			var sz = _width * _height;

			if (klabels.Length < sz) klabels = new int[sz];
			for (var s = 0; s < sz; s++) klabels[s] = -1;

			DoRGBtoLABConversion(imgBuffer, ref _lvec, ref _avec, ref _bvec);

			var edgemap = new List<double>();
			DetectLabEdges(ref _lvec, ref _avec, ref _bvec, _width, _height, ref edgemap);
			GetLABXYSeedsForGivenK(ref kseedsl, ref kseedsa, ref kseedsb, ref kseedsx, ref kseedsy, K, true, edgemap);

			var STEP = (int)(Math.Sqrt((double)sz / (double)K) + 2.0);

			PerformSuperpixelSegmentationVariableSandM(ref kseedsl, ref kseedsa, ref kseedsb, ref kseedsx, ref kseedsy, ref klabels, STEP, m);
			var numlabels = kseedsl.Count();

			//var nlabels = new int[sz];
			//EnforceLabelConnectivity(klabels, _width, _height, ref nlabels, numlabels, K);
			//for (var i = 0; i < sz; i++)
			//	klabels[i] = nlabels[i];
			return numlabels;
		}

		private void DoRGBtoLABConversion(int[] imgBuffer, ref double[] lvec, ref double[] avec, ref double[] bvec)
		{
			var sz = _width * _height;
			lvec = new double[sz];
			avec = new double[sz];
			bvec = new double[sz];

			for (var j = 0; j < sz; j++)
			{
				var r = imgBuffer[j]; //TODO: magic numbers (test it and remove)
				var g = imgBuffer[j + sz];
				var b = imgBuffer[j + sz*2];

				RGB2LAB(r, g, b, ref lvec[j], ref avec[j], ref bvec[j]);
			}
		}

		public static void RGB2LAB(int r, int g, int b, ref double lval, ref double aval, ref double bval)
		{
			double X = 0, Y = 0, Z = 0;
			RGB2XYZ(r, g, b, ref X, ref Y, ref Z);

			//------------------------
			// XYZ to LAB conversion
			//------------------------
			var epsilon = 0.008856;  //actual CIE standard
			var kappa = 903.3;       //actual CIE standard

			var Xr = 0.950456;   //reference white
			var Yr = 1.0;        //reference white
			var Zr = 1.088754;   //reference white

			var xr = X / Xr;
			var yr = Y / Yr;
			var zr = Z / Zr;

			double fx, fy, fz;
			if (xr > epsilon) fx = Math.Pow(xr, 1.0 / 3.0);
			else fx = (kappa * xr + 16.0) / 116.0;
			if (yr > epsilon) fy = Math.Pow(yr, 1.0 / 3.0);
			else fy = (kappa * yr + 16.0) / 116.0;
			if (zr > epsilon) fz = Math.Pow(zr, 1.0 / 3.0);
			else fz = (kappa * zr + 16.0) / 116.0;

			lval = 116.0 * fy - 16.0;
			aval = 500.0 * (fx - fy);
			bval = 200.0 * (fy - fz);
		}

		public static void RGB2LABbyte(int r, int g, int b, ref byte lval, ref byte aval, ref byte bval)
		{
			double X = 0, Y = 0, Z = 0;
			RGB2XYZ(r, g, b, ref X, ref Y, ref Z);

			//------------------------
			// XYZ to LAB conversion
			//------------------------
			var epsilon = 0.008856;  //actual CIE standard
			var kappa = 903.3;       //actual CIE standard

			var Xr = 0.950456;   //reference white
			var Yr = 1.0;        //reference white
			var Zr = 1.088754;   //reference white

			var xr = X / Xr;
			var yr = Y / Yr;
			var zr = Z / Zr;

			double fx, fy, fz;
			if (xr > epsilon) fx = Math.Pow(xr, 1.0 / 3.0);
			else fx = (kappa * xr + 16.0) / 116.0;
			if (yr > epsilon) fy = Math.Pow(yr, 1.0 / 3.0);
			else fy = (kappa * yr + 16.0) / 116.0;
			if (zr > epsilon) fz = Math.Pow(zr, 1.0 / 3.0);
			else fz = (kappa * zr + 16.0) / 116.0;

			lval = (byte)((116.0 * fy - 16.0) * 2.55);
			aval = (byte)((500.0 * (fx - fy)) + 127);
			bval = (byte)((200.0 * (fy - fz)) + 127);
		}

		public static void RGB2XYZ(int sR, int sG, int sB, ref double X, ref double Y, ref double Z)
		{
			var R = sR / 255.0;
			var G = sG / 255.0;
			var B = sB / 255.0;

			double r, g, b;

			if (R <= 0.04045) r = R / 12.92;
			else r = Math.Pow((R + 0.055) / 1.055, 2.4);
			if (G <= 0.04045) g = G / 12.92;
			else g = Math.Pow((G + 0.055) / 1.055, 2.4);
			if (B <= 0.04045) b = B / 12.92;
			else b = Math.Pow((B + 0.055) / 1.055, 2.4);

			X = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
			Y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
			Z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;
		}

		private void DetectLabEdges(ref double[] lvec, ref double[] avec, ref double[] bvec, int width, int height, ref List<double> edges)
		{
			var sz = width * height;

			edges = Enumerable.Repeat(0d, sz).ToList();
			for (var j = 1; j < height - 1; j++)
			{
				for (var k = 1; k < width - 1; k++)
				{
					var i = j * width + k;

					var dx = (lvec[i - 1] - lvec[i + 1]) * (lvec[i - 1] - lvec[i + 1]) +
								(avec[i - 1] - avec[i + 1]) * (avec[i - 1] - avec[i + 1]) +
								(bvec[i - 1] - bvec[i + 1]) * (bvec[i - 1] - bvec[i + 1]);

					var dy = (lvec[i - width] - lvec[i + width]) * (lvec[i - width] - lvec[i + width]) +
								(avec[i - width] - avec[i + width]) * (avec[i - width] - avec[i + width]) +
								(bvec[i - width] - bvec[i + width]) * (bvec[i - width] - bvec[i + width]);

					edges[i] = (dx + dy);
				}
			}
		}

		private void GetLABXYSeedsForGivenK(ref List<double> kseedsl, ref List<double> kseedsa, ref List<double> kseedsb, ref List<double> kseedsx, ref List<double> kseedsy,
			int K, bool perturbseeds, List<double> edgemap)
		{
			var sz = _width * _height;
			var step = Math.Sqrt((double)sz / (double)K);
			var T = (int)step;
			var xoff = (int)(step / 2);
			var yoff = (int)(step / 2);

			int n = 0, r = 0;
			for (var y = 0; y < _height; y++)
			{
				var Y = (int)(y * step + yoff);
				if (Y > _height - 1) break;

				for (var x = 0; x < _width; x++)
				{
					var X = (int)(x * step + (xoff << (r & 0x1)));//hex grid
					if (X > _width - 1) break;

					var i = Y * _width + X;

					kseedsl.Add(_lvec[i]);
					kseedsa.Add(_avec[i]);
					kseedsb.Add(_bvec[i]);
					kseedsx.Add(X);
					kseedsy.Add(Y);
					n++;
				}
				r++;
			}

			if (perturbseeds)
			{
				PerturbSeeds(ref kseedsl, ref kseedsa, ref kseedsb, ref kseedsx, ref kseedsy, edgemap);
			}
		}

		private void PerturbSeeds(ref List<double> kseedsl, ref List<double> kseedsa, ref List<double> kseedsb, ref List<double> kseedsx, ref List<double> kseedsy, List<double> edges)
		{
			int[] dx8 = { -1, -1, 0, 1, 1, 1, 0, -1 };
			int[] dy8 = { 0, -1, -1, -1, 0, 1, 1, 1 };

			var numseeds = kseedsl.Count();

			for (var n = 0; n < numseeds; n++)
			{
				var ox = (int)kseedsx[n];//original x
				var oy = (int)kseedsy[n];//original y
				var oind = oy * _width + ox;

				var storeind = oind;
				for (var i = 0; i < 8; i++)
				{
					var nx = ox + dx8[i];//new x
					var ny = oy + dy8[i];//new y

					if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
					{
						var nind = ny * _width + nx;
						if (edges[nind] < edges[storeind])
						{
							storeind = nind;
						}
					}
				}
				if (storeind != oind)
				{
					kseedsx[n] = storeind % _width;
					kseedsy[n] = storeind / _width;
					kseedsl[n] = _lvec[storeind];
					kseedsa[n] = _avec[storeind];
					kseedsb[n] = _bvec[storeind];
				}
			}
		}

		private void PerformSuperpixelSegmentationVariableSandM(ref List<double> kseedsl, ref List<double> kseedsa, ref List<double> kseedsb, ref List<double> kseedsx, ref List<double> kseedsy,
			ref int[] klabels, int STEP, int NUMITR)
		{
			var sz = _width * _height;
			var numk = kseedsl.Count();
			var numitr = 0;

			var offset = STEP;
			if (STEP < 10) offset = (int)(STEP * 1.5);

			var sigmal = Enumerable.Repeat(0d, numk).ToList();
			var sigmaa = Enumerable.Repeat(0d, numk).ToList();
			var sigmab = Enumerable.Repeat(0d, numk).ToList();
			var sigmax = Enumerable.Repeat(0d, numk).ToList();
			var sigmay = Enumerable.Repeat(0d, numk).ToList();
			var clustersize = Enumerable.Repeat(0, numk).ToList();
			var inv = Enumerable.Repeat(0d, numk).ToList();//to store 1/clustersize[k] values
			var distxy = Enumerable.Repeat(double.MaxValue, sz).ToList();
			var distlab = Enumerable.Repeat(double.MaxValue, sz).ToList();
			var distvec = Enumerable.Repeat(double.MaxValue, sz).ToList();
			var maxlab = Enumerable.Repeat(10d * 10d, numk).ToList();//THIS IS THE VARIABLE VALUE OF M, just start with 10
			var maxxy = Enumerable.Repeat((double)STEP * STEP, numk).ToList();//THIS IS THE VARIABLE VALUE OF M, just start with 10

			var invxywt = 1.0 / (STEP * STEP);//NOTE: this is different from how usual SLIC/LKM works

			while (numitr < NUMITR)
			{
				numitr++;

				distvec = Enumerable.Repeat(Double.MaxValue, sz).ToList(); //???
				for (var n = 0; n < numk; n++)
				{
					var y1 = (int)Math.Max(0, kseedsy[n] - offset);
					var y2 = (int)Math.Min(_height, kseedsy[n] + offset);
					var x1 = (int)Math.Max(0, kseedsx[n] - offset);
					var x2 = (int)Math.Min(_width, kseedsx[n] + offset);

					for (var y = y1; y < y2; y++)
					{
						for (var x = x1; x < x2; x++)
						{
							var i = y * _width + x;

							var l = _lvec[i];
							var a = _avec[i];
							var b = _bvec[i];

							distlab[i] = (l - kseedsl[n]) * (l - kseedsl[n]) +
											(a - kseedsa[n]) * (a - kseedsa[n]) +
											(b - kseedsb[n]) * (b - kseedsb[n]);

							distxy[i] = (x - kseedsx[n]) * (x - kseedsx[n]) +
											(y - kseedsy[n]) * (y - kseedsy[n]);

							//------------------------------------------------------------------------
							var dist = distlab[i] / maxlab[n] + distxy[i] * invxywt;//only varying m, prettier superpixels
																					//double dist = distlab[i]/maxlab[n] + distxy[i]/maxxy[n];//varying both m and S
																					//------------------------------------------------------------------------

							if (dist < distvec[i])
							{
								distvec[i] = dist;
								klabels[i] = n;
							}
						}
					}
				}
				//-----------------------------------------------------------------
				// Assign the max color distance for a cluster
				//-----------------------------------------------------------------
				if (0 == numitr)
				{
					maxlab = Enumerable.Repeat(1d, numk).ToList();
					maxxy = Enumerable.Repeat(1d, numk).ToList();
				}
				for (var i = 0; i < sz; i++)
				{
					if (maxlab[klabels[i]] < distlab[i]) maxlab[klabels[i]] = distlab[i];
					if (maxxy[klabels[i]] < distxy[i]) maxxy[klabels[i]] = distxy[i];
				}
				//-----------------------------------------------------------------
				// Recalculate the centroid and store in the seed values
				//-----------------------------------------------------------------
				sigmal = Enumerable.Repeat(0d, numk).ToList();
				sigmaa = Enumerable.Repeat(0d, numk).ToList();
				sigmab = Enumerable.Repeat(0d, numk).ToList();
				sigmax = Enumerable.Repeat(0d, numk).ToList();
				sigmay = Enumerable.Repeat(0d, numk).ToList();
				clustersize = Enumerable.Repeat(0, numk).ToList();

				for (var j = 0; j < sz; j++)
				{
					sigmal[klabels[j]] += _lvec[j];
					sigmaa[klabels[j]] += _avec[j];
					sigmab[klabels[j]] += _bvec[j];
					sigmax[klabels[j]] += (j % _width);
					sigmay[klabels[j]] += (j / _width);

					clustersize[klabels[j]]++;
				}

				for (var k = 0; k < numk; k++)
				{
					if (clustersize[k] <= 0) clustersize[k] = 1;
					inv[k] = 1.0 / (double)clustersize[k];//computing inverse now to multiply, than divide later
				}

				for (var k = 0; k < numk; k++)
				{
					kseedsl[k] = sigmal[k] * inv[k];
					kseedsa[k] = sigmaa[k] * inv[k];
					kseedsb[k] = sigmab[k] * inv[k];
					kseedsx[k] = sigmax[k] * inv[k];
					kseedsy[k] = sigmay[k] * inv[k];
				}
			}
		}

		private void EnforceLabelConnectivity(int[] labels, int width, int height, ref int[] nlabels, int numlabels, int K)
		{
			int[] dx4 = { -1, 0, 1, 0 };
			int[] dy4 = { 0, -1, 0, 1 };

			var sz = width * height;
			var SUPSZ = sz / K;

			for (int i = 0; i < sz; i++)
				nlabels[i] = -1;

			var label = 0;
			var xvec = new int[sz];
			var yvec = new int[sz];
			var oindex = 0;
			var adjlabel = 0;//adjacent label
			for (var j = 0; j < height; j++)
			{
				for (var k = 0; k < width; k++)
				{
					if (0 > nlabels[oindex])
					{
						nlabels[oindex] = label;
						//--------------------
						// Start a new segment
						//--------------------
						xvec[0] = k;
						yvec[0] = j;
						//-------------------------------------------------------
						// Quickly find an adjacent label for use later if needed
						//-------------------------------------------------------
						{
							for (var n = 0; n < 4; n++)
							{
								var x = xvec[0] + dx4[n];
								var y = yvec[0] + dy4[n];
								if ((x >= 0 && x < width) && (y >= 0 && y < height))
								{
									var nindex = y * width + x;
									if (nlabels[nindex] >= 0) adjlabel = nlabels[nindex];
								}
							}
						}

						var count = 1;
						for (var c = 0; c < count; c++)
						{
							for (var n = 0; n < 4; n++)
							{
								var x = xvec[c] + dx4[n];
								var y = yvec[c] + dy4[n];

								if ((x >= 0 && x < width) && (y >= 0 && y < height))
								{
									var nindex = y * width + x;

									if (0 > nlabels[nindex] && labels[oindex] == labels[nindex])
									{
										xvec[count] = x;
										yvec[count] = y;
										nlabels[nindex] = label;
										count++;
									}
								}

							}
						}
						//-------------------------------------------------------
						// If segment size is less then a limit, assign an
						// adjacent label found before, and decrement label count.
						//-------------------------------------------------------
						if (count <= SUPSZ >> 2)
						{
							for (var c = 0; c < count; c++)
							{
								int ind = yvec[c] * width + xvec[c];
								nlabels[ind] = adjlabel;
							}
							label--;
						}
						label++;
					}
					oindex++;
				}
			}
		}

		public static byte[] GetBytes(Bitmap input)
		{
			var bytesCount = input.Width * input.Height * 3;
			BitmapData inputData = input.LockBits(
			  new Rectangle(0, 0, input.Width, input.Height),
			  ImageLockMode.ReadOnly,
			  PixelFormat.Format24bppRgb);

			var output = new byte[bytesCount];
			Marshal.Copy(inputData.Scan0, output, 0, bytesCount);
			input.UnlockBits(inputData);
			return output;
		}
	}
}
