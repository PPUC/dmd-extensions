﻿using System;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd
{
	public class ColoredFrame : BaseFrame, ICloneable
	{
		/// <summary>
		/// Frame data, split into bit planes
		/// </summary>
		public byte[][] Planes { get; private set; }

		/// <summary>
		/// Color palette
		/// </summary>
		public Color[] Palette { get; private set; }

		/// <summary>
		/// Palette index from animation or -1 if not set.
		/// </summary>
		public int PaletteIndex { get; }

		/// <summary>
		/// Colour Rotation descriptions.
		/// </summary>
		/// <remarks>
		/// Size: 8*3 bytes: 8 colour rotations available per frame, 1 byte for the first colour,
		/// 1 byte for the number of colours, 1 byte for the time interval between 2 rotations in 10ms
		/// </remarks>
		public byte[] Rotations { get; }

		/// <summary>
		/// If set, colors defined in <see cref="Rotations" are rotated./>
		/// </summary>
		public readonly bool RotateColors;

		private int BitLength => Planes.Length;

		private byte[] Data => FrameUtil.Join(Dimensions, Planes);
		
		public ColoredFrame()
		{
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, int paletteIndex = -1, byte[] rotations = null, bool rotateColors = false)
		{
			Dimensions = dim;
			Planes = planes;
			Palette = palette;
			PaletteIndex = paletteIndex;
			Rotations = rotations;
			RotateColors = rotateColors;

			#if DEBUG
			if (planes.Length != palette.Length.GetBitLength()) {
				throw new ArgumentException("Number of planes must match palette size");
			}
			#endif
		}

		public ColoredFrame(Dimensions dim, byte[][] planes, Color[] palette, byte[] rotations)
			: this(dim, planes, palette, -1, rotations) { }

		public ColoredFrame(DmdFrame frame, Color color)
		{
			Dimensions = frame.Dimensions;
			Planes = FrameUtil.Split(Dimensions, frame.BitLength, frame.Data);
			Palette = ColorUtil.GetPalette(new[] { Colors.Black, color }, 4);
			RotateColors = false;
		}

		public ColoredFrame(DmdFrame frame, params Color[] palette)
		{
			Dimensions = frame.Dimensions;
			Planes = FrameUtil.Split(frame.Dimensions, frame.BitLength, frame.Data);
			Palette = palette;
			RotateColors = false;
		}

		public object Clone() => new ColoredFrame(Dimensions, Planes, Palette, PaletteIndex);
		
		public DmdFrame ConvertToGray()
		{
			return new DmdFrame(Dimensions, FrameUtil.Join(Dimensions, Planes), Planes.Length);
		}
		
		public DmdFrame ConvertToGray(params byte[] mapping)
		{
			var data = FrameUtil.Join(Dimensions, Planes);
			return new DmdFrame(Dimensions, FrameUtil.ConvertGrayToGray(data, mapping), mapping.Length.GetBitLength());
		}

		public ColoredFrame Update(byte[][] planes, Color[] palette)
		{
			Planes = planes;
			Palette = palette;
			return this;
		}

		public ColoredFrame Transform(RenderGraph renderGraph, IFixedSizeDestination fixedDest, IMultiSizeDestination destMultiSize)
		{
			var targetDim = GetTargetDimensions(fixedDest, destMultiSize);

			// for dynamic or equal target dimensions, just flip
			if (targetDim == Dimensions.Dynamic || targetDim == Dimensions) {
				Planes = TransformationUtil.Flip(Dimensions, Planes, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
				return this;
			}

			// if we need to scale down by factor 2, do it here more efficiently
			if (Dimensions.IsDoubleSizeOf(targetDim) && !renderGraph.FlipHorizontally && !renderGraph.FlipVertically) {
				Planes = FrameUtil.ScaleDown(targetDim, Planes);
				return this;
			}

			// otherwise, convert to grayscale bitmap, transform, convert back.
			var bmp = ConvertToBitmap();
			var transformedBmp = TransformationUtil.Transform(bmp, targetDim, renderGraph.Resize, renderGraph.FlipHorizontally, renderGraph.FlipVertically);
			var transformedData = ConvertFromBitmap(transformedBmp);

			Planes = FrameUtil.Split(targetDim, BitLength, transformedData);
			Dimensions = targetDim;
			return this;
		}

		/// <summary>
		/// Up-scales the frame with the given algorithm, if the destination allows it.
		/// </summary>
		/// <param name="fixedDest">The fixed destination, null if dynamic. If fixed, DmdAllowHdScaling must be true, and the dimensions must be greater or equal the double of the frame size.</param>
		/// <param name="scalerMode">If and how to scale</param>
		/// <returns>Updated frame instance</returns>
		/// <exception cref="ArgumentException"></exception>
		public ColoredFrame TransformHdScaling(IFixedSizeDestination fixedDest, ScalerMode scalerMode)
		{

			// skip if disabled
			if (scalerMode == ScalerMode.None) {
				return this;
			}

			// if destination doesn't allow scaling (e.g. pup), return
			if (fixedDest != null && !fixedDest.DmdAllowHdScaling) {
				return this;
			}

			// if double of frame size doesn't fit into destination, return
			if (fixedDest != null && !(Dimensions * 2).FitInto(fixedDest.FixedSize)) {
				return this;
			}

			// resize
			var data = scalerMode == ScalerMode.Doubler
				? FrameUtil.ScaleDouble(Dimensions, FrameUtil.Join(Dimensions, Planes))
				: FrameUtil.Scale2X(Dimensions, FrameUtil.Join(Dimensions, Planes));
			Dimensions *= 2;
			FrameUtil.Split(Dimensions, BitLength, data, Planes);

			return this;
		}

		public override string ToString()
		{
			var bitLength = Planes.Length;
			var data = FrameUtil.Join(Dimensions, Planes);
			var sb = new StringBuilder();
			sb.AppendLine($"Colored Frame {Dimensions}@{bitLength}, {Palette.Length} colors ({data.Length} bytes):");
			if (bitLength <= 8) {
				for (var y = 0; y < Dimensions.Height; y++) {
					for (var x = 0; x < Dimensions.Width; x++) {
						sb.Append(data[y * Dimensions.Width + x].ToString("X"));
					}
					sb.AppendLine();
				}
			} else if (bitLength == 24) {
				for (var p = 0; p < 3; p++) {
					sb.AppendLine($"::{PlaneName(p)}::");
					for (var y = 0; y < Dimensions.Height; y++) {
						for (var x = 0; x < Dimensions.Width; x++) {
							sb.Append(data[y * Dimensions.Width * 3 + x * 3 + p].ToString("X2") + " ");
						}
						sb.AppendLine();
					}
				}
			} else {
				throw new ArgumentException("Cannot print frame with bit length " + bitLength);
			}

			sb.AppendLine("Palette: [");
			sb.Append(string.Join(", ", Palette.Select(c => c.ToString())));
			sb.AppendLine("]");

			return sb.ToString();
		}

		private static string PlaneName(int p)
		{
			switch (p) {
				case 0: return "RED";
				case 1: return "GREEN";
				case 2: return "BLUE";
				default: return "PLANE " + p;
			}
		}

		private BitmapSource ConvertToBitmap()
		{
			switch (BitLength) {
				case 2: return ImageUtil.ConvertFromGray2(Dimensions, Data, 0, 1, 1);
				case 4: return ImageUtil.ConvertFromGray4(Dimensions, Data, 0, 1, 1);
				case 6: return ImageUtil.ConvertFromGray6(Dimensions, Data, 0, 1, 1);
				default:
					throw new ArgumentException("Cannot convert frame with bit length " + BitLength);
			}
		}

		private byte[] ConvertFromBitmap(BitmapSource bmp)
		{
			switch (BitLength) {
				case 2: return ImageUtil.ConvertToGray2(bmp, 0, 1, out _);
				case 4: return ImageUtil.ConvertToGray4(bmp);
				case 6: return ImageUtil.ConvertToGray6(bmp);
				default:
					throw new ArgumentException("Cannot convert frame with bit length " + BitLength);
			}
		}
	}
}
