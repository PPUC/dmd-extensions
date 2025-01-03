﻿using System.Runtime.InteropServices;
using System;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public abstract class ZeDMDUsbBase : ZeDMDBase
	{
		protected new void Init()
		{
			base.Init();

			if (!string.IsNullOrEmpty(Port)) {
				ZeDMD_SetDevice(_pZeDMD, @"\\.\" + Port);
			}

			IsAvailable = ZeDMD_Open(_pZeDMD);

			if (!IsAvailable) {
				Logger.Info(Name + " device not found at port " + Port);
				return;
			}
			Logger.Info(Name + " device found at port " + Port + ", libzedmd version: " + DriverVersion);
		}

		#region libzedmd

		/// <summary>
		/// WiFi specific libzedmd functions declarations
		/// See https://ppuc.github.io/libzedmd/docs/html/class_ze_d_m_d.html
		/// </summary>

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern bool ZeDMD_Open(IntPtr pZeDMD);

		#endregion
	}
}