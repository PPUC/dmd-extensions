﻿using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using LibDmd.Frame;
using LibDmd.Input;
using System.Windows.Media;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class SerumApiV1 : ISerumApi
	{
		public uint NumColors { get; }

		private readonly DmdFrame _frame;
		private readonly Dimensions _dimensions;

		private readonly Subject<ColoredFrame> _coloredGray6Frames;
		private readonly Subject<FrameEvent> _frameEvents;

		private readonly byte[] _bytePalette = new byte[64 * 3];
		private readonly Color[] _colorPalette = new Color[64];

		private readonly FrameEvent _frameEvent = new FrameEvent();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SerumApiV1(Subject<ColoredFrame> coloredGray6Frames, Subject<FrameEvent> frameEvents, ref SerumFrame serumFrame)
		{
			_coloredGray6Frames = coloredGray6Frames;
			_frameEvents = frameEvents;
			_dimensions = serumFrame.Width32 > 0
				? new Dimensions((int)serumFrame.Width32, 32)
				: new Dimensions((int)serumFrame.Width64, 64);

			NumColors = serumFrame.nocolors;
			_frame = new DmdFrame(_dimensions, ((int)NumColors).GetBitLength());
		}

		public bool Convert(ref SerumFrame serumFrame, uint rotations)
		{
			if (serumFrame.Frame == null) {
				Logger.Warn("[serum] Frame data is null.");
			}

			// copy data from unmanaged to managed
			if (serumFrame.Palette != null) {
				Marshal.Copy(serumFrame.Palette, _bytePalette, 0, _bytePalette.Length);
			}
			Marshal.Copy(serumFrame.Frame, _frame.Data, 0, _dimensions.Width * _dimensions.Height);
			BytesToColors(_bytePalette, _colorPalette);

			// 0 => no rotation
			// 1 - 0xFFFF => time in ms to next rotation
			// 0xFFFFFFFF => frame wasn't colorized
			// 0xFFFFFFFE => same frame as before
			var hasRotations = rotations != 0xffffffff && rotations > 0;

			// send event trigger
			if (serumFrame.triggerID != 0xffffffff) {
				_frameEvents.OnNext(_frameEvent.Update((ushort)serumFrame.triggerID));
			}

			// send the colored frame
			_coloredGray6Frames.OnNext(new ColoredFrame(_dimensions, _frame.Data, _colorPalette));
			return hasRotations;
		}

		public void UpdateRotations(ref SerumFrame serumFrame, Color[] palette, uint _)
		{
			Marshal.Copy(serumFrame.Palette, _bytePalette, 0, _bytePalette.Length);
			BytesToColors(_bytePalette, palette);
		}

		private static void BytesToColors(IReadOnlyList<byte> bytes, Color[] palette)
		{
			for (int ti = 0; ti < 64; ti++) {
				palette[ti].A = 255;
				palette[ti].R = bytes[ti * 3];
				palette[ti].G = bytes[ti * 3 + 1];
				palette[ti].B = bytes[ti * 3 + 2];
			}
		}
	}
}
