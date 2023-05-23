﻿using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd.Input.FileSystem
{
	public class ImageSourceGray2 : ImageSource, IGray2Source
	{
		readonly DmdFrame _dmdFrame = new DmdFrame();

		public IObservable<DmdFrame> GetGray2Frames() => _frames;

		private readonly BehaviorSubject<DmdFrame> _frames;

		public ImageSourceGray2(BitmapSource bmp)
		{
			var pixelDim = bmp.Dimensions();
			SetDimensions(pixelDim);
			_frames = new BehaviorSubject<DmdFrame>(_dmdFrame.Update(pixelDim, ImageUtil.ConvertToGray2(bmp).Data, 2));
		}
	}	
	
	public class ImageSourceGray4 : ImageSource, IGray4Source
	{
		DmdFrame _dmdFrame = new DmdFrame();

		public IObservable<DmdFrame> GetGray4Frames() => _frames;

		private readonly BehaviorSubject<DmdFrame> _frames;

		public ImageSourceGray4(BitmapSource bmp)
		{
			var pixelDim = bmp.Dimensions();
			SetDimensions(pixelDim);
			_frames = new BehaviorSubject<DmdFrame>(_dmdFrame.Update(pixelDim, ImageUtil.ConvertToGray4(bmp), 4));
		}
	}
	
	public class ImageSourceColoredGray2 : ImageSource, IColoredGray2Source
	{
		public IObservable<ColoredFrame> GetColoredGray2Frames() => _frames;

		private readonly BehaviorSubject<ColoredFrame> _frames;

		public ImageSourceColoredGray2(BitmapSource bmp)
		{
			var pixelDim = bmp.Dimensions();
			SetDimensions(pixelDim);
			var frame = new ColoredFrame(
				bmp.Dimensions(),
				FrameUtil.Split(pixelDim, 2, ImageUtil.ConvertToGray2(bmp).Data),
				new [] { Colors.Black, Colors.Red, Colors.Green, Colors.Blue }
			);
			_frames = new BehaviorSubject<ColoredFrame>(frame);
		}
	}
	
	public class ImageSourceColoredGray4 : ImageSource, IColoredGray4Source
	{
		public IObservable<ColoredFrame> GetColoredGray4Frames() => _frames;

		private readonly BehaviorSubject<ColoredFrame> _frames;

		public ImageSourceColoredGray4(BitmapSource bmp)
		{
			var pixelDim = bmp.Dimensions();
			SetDimensions(pixelDim);
			var frame = new ColoredFrame(
				pixelDim,
				FrameUtil.Split(pixelDim, 4, ImageUtil.ConvertToGray4(bmp)),
				new[] {
					Colors.Black, Colors.Blue, Colors.Purple, Colors.DimGray,
					Colors.Green, Colors.Brown, Colors.Red, Colors.Gray, 
					Colors.Tan, Colors.Orange, Colors.Yellow, Colors.LightSkyBlue, 
					Colors.Cyan, Colors.LightGreen, Colors.Pink, Colors.White,
				}
			);
			_frames = new BehaviorSubject<ColoredFrame>(frame);
		}
	}

	public class ImageSourceColoredGray6 : ImageSource, IColoredGray6Source
	{
		public IObservable<ColoredFrame> GetColoredGray6Frames() => _frames;

		private readonly BehaviorSubject<ColoredFrame> _frames;

		public ImageSourceColoredGray6(BitmapSource bmp)
		{
			var pixelDim = bmp.Dimensions();
			SetDimensions(pixelDim);
			var frame = new ColoredFrame(
				pixelDim,
				FrameUtil.Split(pixelDim, 6, ImageUtil.ConvertToGray6(bmp)),
				new[] {
					Colors.Black, Colors.Blue, Colors.Purple, Colors.DimGray,
					Colors.Green, Colors.Brown, Colors.Red, Colors.Gray,
					Colors.Tan, Colors.Orange, Colors.Yellow, Colors.LightSkyBlue,
					Colors.Cyan, Colors.LightGreen, Colors.Pink, Colors.White,
				}
			);
			_frames = new BehaviorSubject<ColoredFrame>(frame);
		}
	}

	public class ImageSourceBitmap : ImageSource, IBitmapSource
	{
		public IObservable<BmpFrame> GetBitmapFrames() => _frames;

		private readonly BehaviorSubject<BmpFrame> _frames;

		public ImageSourceBitmap(BitmapSource bmp)
		{
			SetDimensions(bmp.Dimensions());
			_frames = new BehaviorSubject<BmpFrame>(new BmpFrame(bmp));
		}

		public ImageSourceBitmap(string fileName)
		{
			if (!File.Exists(fileName)) {
				throw new FileNotFoundException("Cannot find file \"" + fileName + "\".");
			}

			try {
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.UriSource = new Uri(Path.IsPathRooted(fileName) ? fileName : Path.Combine(Directory.GetCurrentDirectory(), fileName));
				bmp.EndInit();

				SetDimensions(bmp.Dimensions());
				_frames = new BehaviorSubject<BmpFrame>(new BmpFrame(bmp));

			} catch (UriFormatException) {
				throw new WrongFormatException($"Error parsing file name \"{fileName}\". Is this a path on the file system?");

			} catch (NotSupportedException e) {
				if (e.Message.Contains("No imaging component suitable"))
				{
					throw new WrongFormatException($"Could not determine image format. Are you sure {fileName} is an image?");
				}
				throw new Exception("Error instantiating image source.", e);
			}
		}
	}

	public abstract class ImageSource : AbstractSource
	{
		public override string Name { get; } = "Image Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();
	}

	public class WrongFormatException : Exception
	{
		public WrongFormatException(string message) : base(message)
		{
		}
	}
}
