using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static GameLauncher_Console.CGameData;

namespace GameLauncher_Console
{
	/// <summary>
	/// Originally based on:
	/// https://stackoverflow.com/a/33652557/6754996
	/// </summary>
	public class CConsoleImage
	{
		//public static bool IsRunning { get; set; }

		[Flags]
		public enum SIIGBF
		{
			SIIGBF_RESIZETOFIT = 0x000,
			SIIGBF_BIGGERSIZEOK = 0x001,
			SIIGBF_MEMORYONLY = 0x002,
			SIIGBF_ICONONLY = 0x004,
			SIIGBF_THUMBNAILONLY = 0x008,
			SIIGBF_INCACHEONLY = 0x010,
			SIIGBF_CROPTOSQUARE = 0x020,
			SIIGBF_WIDETHUMBNAILS = 0x040,
			SIIGBF_ICONBACKGROUND = 0x080,
			SIIGBF_SCALEUP = 0x100
		}
		public enum SIGDN : uint
		{
			NORMALDISPLAY = 0,
			PARENTRELATIVEPARSING = 0x80018001,
			PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
			DESKTOPABSOLUTEPARSING = 0x80028000,
			PARENTRELATIVEEDITING = 0x80031001,
			DESKTOPABSOLUTEEDITING = 0x8004c000,
			FILESYSPATH = 0x80058000,
			URL = 0x80068000
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
		public interface IShellItem
		{
			void BindToHandler(IntPtr pbc,
				[MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
				[MarshalAs(UnmanagedType.LPStruct)] Guid riid,
				out IntPtr ppv);

			void GetParent(out IShellItem ppsi);

			void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

			void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

			void Compare(IShellItem psi, uint hint, out int piOrder);
		};
		
		[ComImportAttribute()]
		[GuidAttribute("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
		[InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
		public interface IShellItemImageFactory
		{
			void GetImage(
			[In, MarshalAs(UnmanagedType.Struct)] Size size,
			[In] SIIGBF flags,
			[Out] out IntPtr phbm);
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		public static extern void SHCreateItemFromParsingName(
			[In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
			[In] IntPtr pbc,
			[In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
			[Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem ppv);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetConsoleWindow();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateFile(
			string lpFileName,
			int dwDesiredAccess,
			int dwShareMode,
			IntPtr lpSecurityAttributes,
			int dwCreationDisposition,
			int dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GetCurrentConsoleFont(
			IntPtr hConsoleOutput,
			bool bMaximumWindow,
			[Out][MarshalAs(UnmanagedType.LPStruct)] ConsoleFontInfo lpConsoleCurrentFont);

		[StructLayout(LayoutKind.Sequential)]
		internal class ConsoleFontInfo
		{
			internal int nFont;
			internal Coord dwFontSize;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool GetCurrentConsoleFontEx(
			IntPtr hConsoleOutput,
			bool bMaximumWindow,
			[In, Out] CONSOLE_FONT_INFO_EX lpConsoleCurrentFont);

		[DllImport("User32.dll")]
		public static extern uint GetDpiForWindow([In] IntPtr hwindow);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public class CONSOLE_FONT_INFO_EX
		{
			private int cbSize;
			public CONSOLE_FONT_INFO_EX()
			{
				cbSize = Marshal.SizeOf(typeof(CONSOLE_FONT_INFO_EX));
			}
			public int FontIndex;
			public COORD dwFontSize;
			public int FontFamily;
			public int FontWeight;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string FaceName;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct COORD
		{
			public short X;
			public short Y;

			public COORD(short X, short Y)
			{
				this.X = X;
				this.Y = Y;
			}
		};

		[StructLayout(LayoutKind.Explicit)]
		internal struct Coord
		{
			[FieldOffset(0)]
			internal short X;
			[FieldOffset(2)]
			internal short Y;
		}

		public const int STD_OUTPUT_HANDLE = -11;
		private const int FILE_SHARE_READ = 1;
		private const int FILE_SHARE_WRITE = 2;
		private const int GENERIC_READ = unchecked((int)0x80000000);
		private const int GENERIC_WRITE = 0x40000000;
		private const int INVALID_HANDLE_VALUE = -1;
		private const int OPEN_EXISTING = 3;

		public static Size GetConsoleFontSize()
		{
			// getting the console out buffer handle
			IntPtr outHandle = CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE,
				FILE_SHARE_READ | FILE_SHARE_WRITE,
				IntPtr.Zero,
				OPEN_EXISTING,
				0,
				IntPtr.Zero);
			int errorCode = Marshal.GetLastWin32Error();
			if (outHandle.ToInt32() == INVALID_HANDLE_VALUE)
			{
				throw new IOException("Unable to open CONOUT$", errorCode);
			}

			ConsoleFontInfo cfi = new();
			if (!GetCurrentConsoleFont(outHandle, false, cfi))
			{
				throw new InvalidOperationException("Unable to get font information.");
			}
			//CLogger.LogDebug("Console Font Size: {0}x{1}", cfi.dwFontSize.X, cfi.dwFontSize.Y);
			return new Size(cfi.dwFontSize.X, cfi.dwFontSize.Y);
		}

		// This is for the cmd legacy color scheme
		private static Color ToGrColourLegacy(ConsoleColor cc)
		{
			// this works for old schema, but this has changed
			int cInt = (int)cc;
			int brightnessCoefficient = ((cInt & 8) > 0) ? 2 : 1;
			int r = ((cInt & 4) > 0) ? 128 * brightnessCoefficient - 1 : 0;
			int g = ((cInt & 2) > 0) ? 128 * brightnessCoefficient - 1 : 0;
			int b = ((cInt & 1) > 0) ? 128 * brightnessCoefficient - 1 : 0;
			return Color.FromArgb(r, g, b);
		}

		// This is for the new (campbell) color scheme
		private static Color ToGrColourCampbell(ConsoleColor cc)
		{
            return cc switch
            {
                ConsoleColor.Black => Color.FromArgb(12, 12, 12),
                ConsoleColor.DarkBlue => Color.FromArgb(0, 55, 218),
                ConsoleColor.DarkGreen => Color.FromArgb(19, 161, 14),
                ConsoleColor.DarkCyan => Color.FromArgb(58, 150, 221),
                ConsoleColor.DarkRed => Color.FromArgb(197, 15, 31),
                ConsoleColor.DarkMagenta => Color.FromArgb(136, 23, 152),
                ConsoleColor.DarkYellow => Color.FromArgb(193, 156, 0),
                ConsoleColor.Gray => Color.FromArgb(204, 204, 204),
                ConsoleColor.DarkGray => Color.FromArgb(118, 118, 118),
                ConsoleColor.Blue => Color.FromArgb(59, 120, 255),
                ConsoleColor.Green => Color.FromArgb(22, 198, 12),
                ConsoleColor.Cyan => Color.FromArgb(97, 214, 214),
                ConsoleColor.Red => Color.FromArgb(231, 72, 86),
                ConsoleColor.Magenta => Color.FromArgb(180, 0, 158),
                ConsoleColor.Yellow => Color.FromArgb(249, 241, 165),
                ConsoleColor.White => Color.FromArgb(242, 242, 242),
                _ => Color.FromArgb(12, 12, 12),
            };
        }

		public static void GetImageProperties(int imgWidth, int imgPercent, out Size size, out Point location)
		{
			int imgHeight;
			if (imgWidth % 2 == 0)
				imgHeight = imgWidth / 2;
			else
				imgHeight = imgWidth / 2 + 1;

			size = new Size(imgWidth, imgHeight);
			location = new Point(Console.WindowWidth - imgWidth, decimal.ToInt32(Math.Floor((Console.WindowHeight - imgHeight) * ((decimal)imgPercent / 100))));
		}

		public static void GetIconSize(int iconWidth, out Size sizeIcon)
		{
			/*
			int iconHeight;
			if (iconWidth % 2 == 0)
				iconHeight = iconWidth / 2;
			else
				iconHeight = iconWidth / 2 + 1;
			*/

			sizeIcon = new Size(iconWidth, 1);
		}

		/// <summary>
		/// Show text border placeholder around image
		/// </summary>
		/// <param name="imgWidth">x size (y will be half of x per GetImageProperties())</param>
		/// <param name="bImgBottom">Whether to put image in bottom right or top right</param>
		/// <param name="xCushion">Text mode spaces between image and border in x direction</param>
		/// <param name="yCushion">Text mode spaces between image and border in y direction</param>
		public static void ShowImageBorder(Size size, Point point, int xCushion, int yCushion)  // showing the border sometimes causes the image to disappear
		{
            int linePercent;
            try
			{
				linePercent = 100 / Console.WindowHeight;
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
				linePercent = 4;
			}
			
			for (int y = point.Y - yCushion; y < point.Y + size.Height; ++y)
			{
				if (y < 0) y = 0;
				Console.SetCursorPosition(point.X - xCushion - 1, y);
				Console.Write("│");
				for (int i = 1; i < (xCushion - 1); ++i)
				{
					Console.Write(" ");
				}
			}
			if (CConfig.GetConfigInt(CConfig.CFG_IMGPOS) < 100 - linePercent)
			{
				Console.SetCursorPosition((point.X - xCushion - 1) > 0 ? point.X - xCushion - 1 : 0, point.Y + size.Height + yCushion);
				Console.Write("└");
				for (int x = point.X - xCushion; x < point.X + size.Width; ++x)
				{
					Console.SetCursorPosition(x, point.Y + size.Height + yCushion);
					Console.Write("─");
					for (int j = yCushion; j > 0; --j)
					{
						Console.SetCursorPosition(x, point.Y + size.Height + j);
						Console.Write(" ");
					}
				}
			}
			if (CConfig.GetConfigInt(CConfig.CFG_IMGPOS) > linePercent)
			{
				Console.SetCursorPosition((point.X - xCushion - 1) > 0 ? point.X - xCushion - 1 : 0, (point.Y - yCushion - 1) > 0 ? point.Y - yCushion - 1 : 0);
				Console.Write("┌");
				for (int x = point.X - xCushion; x < point.X + size.Width; ++x)
				{
					Console.SetCursorPosition(x, (point.Y - yCushion - 1) > 0 ? point.Y - yCushion - 1 : 0);
					Console.Write("─");
					for (int j = 1; j < (yCushion - 1); ++j)
					{
						Console.SetCursorPosition(x, size.Height + j);
						Console.Write(" ");
					}
				}
			}
			Thread.Sleep(50);  // icon sometimes becomes hidden otherwise
			Console.SetCursorPosition(0, 0);
		}

        [SupportedOSPlatform("windows")]
        public static void ShowImage(int selection, string title, string imgPath, bool bPlatform, Size size, Point location, ConsoleColor? bg)
        {
			if (bPlatform)
				title = title.Substring(0, title.LastIndexOf(':'));

			string titleFile = string.Concat(title.Split(Path.GetInvalidFileNameChars()));

			if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGCUST)) && !string.IsNullOrEmpty(title))
			{
				foreach (string ext in CDock.supportedImages)
				{
					string imgPathChk = Path.Combine(CDock.currentPath, CDock.IMAGE_FOLDER_NAME, titleFile + "." + ext);
					if (File.Exists(imgPathChk))
					{
						bPlatform = false;
						imgPath = imgPathChk;
						break;
					}
				}
			}
			if (bPlatform)
				ShowPlatformImage(selection, size, location, imgPath, bg);
			else
				ShowGameImage(selection, size, location, imgPath, bg);
		}

        [SupportedOSPlatform("windows")]
        private static void ShowGameImage(int selection, Size size, Point point, string imgPath, ConsoleColor? bg)
		{
			Size fontSize = GetConsoleFontSize();
			decimal scalingFactor = GetDpiForWindow(GetConsoleWindow()) / 96m;

			if (size.Width > 0) //&& !(IsRunning))
			{
				//IsRunning = true;

				if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
				{
					Bitmap image;
					bool customImage = false;
					bool embeddedIcon = false;
					bool defaultIcon = true;
					int x = (int)(point.X * fontSize.Width * scalingFactor);
					int y = (int)(point.Y * fontSize.Height * scalingFactor);
					int w = (int)(size.Width * fontSize.Width * scalingFactor);
					int h = (int)(size.Height * fontSize.Height * scalingFactor);

					foreach (string ext in CDock.supportedImages)
					{
						if (Path.GetExtension(imgPath).Equals("." + ext, CDock.IGNORE_CASE))
						{
							customImage = true;
							defaultIcon = false;
							break;
						}
					}
					foreach (string ext in new List<string> { "EXE", "DLL", "ICL" })
					{
						if (Path.GetExtension(imgPath).Equals("." + ext, CDock.IGNORE_CASE))
						{
							embeddedIcon = true;
							defaultIcon = false;
							break;
						}
					}
					if (customImage)
					{
						try
						{
							using (image = (Bitmap)Image.FromFile(imgPath))
							{
								ClearImage(size, point, bg);
								DrawImage(image, x, y, w, h, (bool)CConfig.GetConfigBool(CConfig.CFG_IMGRTIO));
							}
						}
						catch (Exception e)
						{
							defaultIcon = true;
							//ClearImage(size, point, bg);
							CLogger.LogError(e);
						}
					}
					else if (embeddedIcon)  // show embedded icon in .exe, .dll, .icl
					{
						try
						{
                            using Graphics g = Graphics.FromHwnd(GetConsoleWindow());
                            int res = (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGRES) < 256 ? (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGRES) : 256;
                            if (w > h && w < res)
                                res = w;
                            else if (h > w && h < res)
                                res = h;

                            IntPtr hBitmap = IntPtr.Zero;
                            Guid uuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");  // IShellItem
                            SHCreateItemFromParsingName(imgPath, IntPtr.Zero, uuid, out IShellItem ppsi);
                            ((IShellItemImageFactory)ppsi).GetImage(new Size(res, res), SIIGBF.SIIGBF_ICONONLY, out hBitmap);

                            using (image = Image.FromHbitmap(hBitmap))
                            {
                                // translating the character positions to pixels
                                if (selection == CDock.m_nCurrentSelection)
                                {
                                    ClearImage(size, point, bg);
                                    Rectangle imageRect = new(x, y, w, h);
                                    g.DrawImage(image, imageRect);
                                }
                            }
                        }
						//SHCreateItemFromParsingName() causes this catch fairly often
						catch //(Exception e)
						{
							defaultIcon = true;
							//ClearImage(size, point, bg);
							//CLogger.LogError(e);
						}
					}

					if (defaultIcon)  // show the default icon for the extension
					{
						try
						{
							using (Graphics g = Graphics.FromHwnd(GetConsoleWindow()))
							using (image = Icon.ExtractAssociatedIcon(imgPath).ToBitmap())
							{
								// translating the character positions to pixels
								if (selection == CDock.m_nCurrentSelection)
								{
									ClearImage(size, point, bg);
									Rectangle imageRect = new(x, y, w, h);
									g.DrawImage(image, imageRect);
								}
							}
						}
						catch (Exception e)
						{
							ClearImage(size, point, bg);
							CLogger.LogError(e);
						}
					}
				}
				else
					ClearImage(size, point, bg);
			}
			//IsRunning = false;
		}

        [SupportedOSPlatform("windows")]
        private static void ShowPlatformImage(int selection, Size size, Point point, string platform, ConsoleColor? bg)
		{
			Size fontSize = GetConsoleFontSize();
			decimal scalingFactor = GetDpiForWindow(GetConsoleWindow()) / 96m;

			if (size.Width > 0)
			{
				Icon icon;
				int x = (int)(point.X * fontSize.Width * scalingFactor);
				int y = (int)(point.Y * fontSize.Height * scalingFactor);
				int w = (int)(size.Width * fontSize.Width * scalingFactor);
				int h = (int)(size.Height * fontSize.Height * scalingFactor);

				int res = (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGRES) < 256 ? (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGRES) : 256;
				if (w > h && w < res)
					res = w;
				else if (h > w && h < res)
					res = h;

                using Graphics g = Graphics.FromHwnd(GetConsoleWindow());
                try
                {
                    if (platform.StartsWith(GetPlatformString(-1)))               // Unknown
                        icon = new Icon(Properties.Resources.unknown, res, res);
                    else if (platform.StartsWith(GetPlatformString(0)))           // Favourites
                        icon = new Icon(Properties.Resources._0, res, res);
                    else if (platform.StartsWith(GetPlatformString(1)))           // Custom
                        icon = new Icon(Properties.Resources._1, res, res);
                    else if (platform.StartsWith(GetPlatformString(2)))           // All
                        icon = new Icon(Properties.Resources._2, res, res);
                    else if (platform.StartsWith(GetPlatformString(3)))
                        icon = new Icon(Properties.Resources._3, res, res);
                    else if (platform.StartsWith(GetPlatformString(4)))
                        icon = new Icon(Properties.Resources._4, res, res);
                    else if (platform.StartsWith(GetPlatformString(5)))
                        icon = new Icon(Properties.Resources._5, res, res);
                    else if (platform.StartsWith(GetPlatformString(6)))
                        icon = new Icon(Properties.Resources._6, res, res);
                    else if (platform.StartsWith(GetPlatformString(7)))
                        icon = new Icon(Properties.Resources._7, res, res);
                    else if (platform.StartsWith(GetPlatformString(8)))
                        icon = new Icon(Properties.Resources._8, res, res);
                    else if (platform.StartsWith(GetPlatformString(9)))
                        icon = new Icon(Properties.Resources._9, res, res);
                    else if (platform.StartsWith(GetPlatformString(10)))
                        icon = new Icon(Properties.Resources._10, res, res);
                    else if (platform.StartsWith(GetPlatformString(11)))          // Hidden
                        icon = new Icon(Properties.Resources._11, res, res);
                    else if (platform.StartsWith(GetPlatformString(12)))          // Search
                        icon = new Icon(Properties.Resources._12, res, res);
                    else if (platform.StartsWith(GetPlatformString(13)))
                        icon = new Icon(Properties.Resources._13, res, res);
                    else if (platform.StartsWith(GetPlatformString(14)))
                        icon = new Icon(Properties.Resources._14, res, res);
                    else if (platform.StartsWith(GetPlatformString(15)))
                        icon = new Icon(Properties.Resources._15, res, res);
                    else if (platform.StartsWith(GetPlatformString(16)))
                        icon = new Icon(Properties.Resources._16, res, res);
                    else if (platform.StartsWith(GetPlatformString(17)))
                        icon = new Icon(Properties.Resources._17, res, res);
                    else if (platform.StartsWith(GetPlatformString(18)))
                        icon = new Icon(Properties.Resources._18, res, res);
                    else if (platform.StartsWith(GetPlatformString(19)))
                        icon = new Icon(Properties.Resources._19, res, res);
                    else if (platform.StartsWith(GetPlatformString(20)))
                        icon = new Icon(Properties.Resources._20, res, res);
                    else if (platform.StartsWith(GetPlatformString(21)))
                        icon = new Icon(Properties.Resources._21, res, res);
                    else if (platform.StartsWith(GetPlatformString(22)))          // New
                        icon = new Icon(Properties.Resources._22, res, res);
                    else if (platform.StartsWith(GetPlatformString(23)))          // Not installed
                        icon = new Icon(Properties.Resources._23, res, res);
                    else if (platform.StartsWith(GetPlatformString(24)))
                        icon = new Icon(Properties.Resources._24, res, res);
                    else if (platform.StartsWith(GetPlatformString(25)))
                        icon = new Icon(Properties.Resources._25, res, res);
                    else if (platform.StartsWith(GetPlatformString(26)))
                        icon = new Icon(Properties.Resources._26, res, res);
                    else if (platform.StartsWith(GetPlatformString(27)))
                        icon = new Icon(Properties.Resources._27, res, res);
                    else if (platform.StartsWith(GetPlatformString(28)))
                        icon = new Icon(Properties.Resources._28, res, res);
                    else if (platform.StartsWith(GetPlatformString(29)))
                        icon = new Icon(Properties.Resources._29, res, res);
                    else if (platform.Equals(CConfig.GetConfigString(CConfig.CFG_TXTCFGT))) // Settings
                        icon = new Icon(Properties.Resources.settings, res, res);
                    else
                        icon = new Icon(Properties.Resources.icon, res, res);

                    // translating the character positions to pixels
                    if (selection == CDock.m_nCurrentSelection)
                    {
                        ClearImage(size, point, bg);
                        Rectangle imageRect = new(x, y, w, h);
                        g.DrawImage(icon.ToBitmap(), imageRect);
                    }
                }
                catch (Exception e)
                {
                    CLogger.LogError(e);
                }
            }
		}

        [SupportedOSPlatform("windows")]
        public static void ClearImage(Size size, Point point, ConsoleColor? bg)
		{
			if (bg != null && size.Width > 0)
			{
                using Graphics g = Graphics.FromHwnd(GetConsoleWindow());
                SolidBrush brush;
                Size fontSize = GetConsoleFontSize();
				decimal scalingFactor = GetDpiForWindow(GetConsoleWindow()) / 96m;

				// translating the character positions to pixels
				Rectangle imageRect = new(
					(int)(point.X * fontSize.Width * scalingFactor),
					(int)(point.Y * fontSize.Height * scalingFactor),
					(int)(size.Width * fontSize.Width * scalingFactor),
					(int)(size.Height * fontSize.Height * scalingFactor));
                if ((bool)CConfig.GetConfigBool(CConfig.CFG_IMGBGLEG))
                    brush = new SolidBrush(ToGrColourLegacy((ConsoleColor)bg));
                else
                    brush = new SolidBrush(ToGrColourCampbell((ConsoleColor)bg));
                g.FillRectangle(brush, imageRect);
            }
		}

        /// <summary>
        /// Draw an image, optionally enforcing its aspect ratio (slight cropping may occur).
        /// </summary>
        /// <param name="source">image</param>
        /// <param name="x">x-position</param>
        /// <param name="y">y-position</param>
        /// <param name="w">width</param>
        /// <param name="h">height</param>
        /// <param name="ignoreRatio">ignore aspect ratio</param>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        public static void DrawImage(Image source, int x, int y, int w, int h, bool ignoreRatio)
		{
			//Image result = null;

			if (w == source.Width && h == source.Height)  // Image size matches the given size
			{
				try
				{
                    using Graphics g = Graphics.FromHwnd(GetConsoleWindow());
                    /*
                    if (selection == CDock.m_nCurrentSelection)
                    {
                        ClearImage(size, point, bg);
                    */
                    Rectangle imageRect = new(x, y, w, h);
                    g.DrawImage(source, imageRect);
                    //}
                }
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}
			else  // scaling required
			{
				try
				{
                    using Bitmap target = new(w, h);
                    using Graphics g = Graphics.FromHwnd(GetConsoleWindow());
                    /*
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    */

                    // Scaling
                    float scaling = 1;
                    if (!ignoreRatio)
                    {
                        float scalingY = (float)source.Height / h;
                        float scalingX = (float)source.Width / w;
                        if (scalingX > scalingY) scaling = scalingX;
                        else scaling = scalingY;
                    }

                    int newWidth = (int)(source.Width / scaling);
                    int newHeight = (int)(source.Height / scaling);

                    // Correct float to int rounding
                    if (newWidth > w) newWidth = w;
                    if (newHeight > h) newHeight = h;

                    // See if image needs to be cropped
                    int shiftX = 0;
                    int shiftY = 0;

                    if (newWidth < w)
                        shiftX = (newWidth - w) / 2;
                    if (newHeight < h)
                        shiftY = (newHeight - h) / 2;

                    // Draw image
                    /*
                    if (selection == CDock.m_nCurrentSelection)
                    {
                        ClearImage(size, point, bg);
                    */
                    Rectangle imageRect = new(x - shiftX, y - shiftY, newWidth, newHeight);
                    g.DrawImage(source, imageRect);
                    //}
                }
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}
		}



	}
}
