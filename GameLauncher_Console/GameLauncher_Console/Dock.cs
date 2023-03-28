using Logger;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
//using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
//using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
//using System.Security;
//using System.Security.Permissions;
//using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Forms;
//using System.Windows.Interop;
using static GameLauncher_Console.CGameData;

namespace GameLauncher_Console
{
	/// <summary>
	/// Main program logic - this is where all classes meet and get used
	/// </summary>
	public class CDock
	{
		public const int COLUMN_CUSHION = 2;
		public const int INPUT_BOTTOM_CUSHION = 2;
		public const int INPUT_ITEM_CUSHION = 1;
		public const int INSTRUCT_CUSHION = 1;
		public const int IMG_BORDER_X_CUSHION = 1;
		public const int IMG_BORDER_Y_CUSHION = 0;
		public const int ICON_LEFT_CUSHION = 0;
		public const int ICON_RIGHT_CUSHION = 1;
		public const CompareOptions IGNORE_ALL = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols;
		public const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
		public const string IMAGE_FOLDER_NAME = "CustomImages";
		public const char SEPARATOR_SYMBOL = '│';
		public const char RATING_SYMBOL = '*';

		public static readonly string filename = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

		CConsoleHelper m_dockConsole;
		private int nColumnCount = 2;
		public static int m_nSelectedPlatform = -1;
		public static int m_nSelectedCategory = -1; // for future
		public static int m_nSelectedGame = -1;
		public static int m_nCurrentSelection = 0;

		public static readonly string currentPath = Path.GetDirectoryName(AppContext.BaseDirectory);
        public static readonly string title = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;
        //public static readonly string product = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
        public static readonly string version = Assembly.GetEntryAssembly().GetName().Version.ToString();
        public static readonly string description = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;

        public static readonly List<string> supportedImages = new() { "ICO", "PNG", "JPG", "JPE", "JPEG", "GIF", "BMP", "TIF", "TIFF", "EPR", "EPRT", "EXI", "EXIF" };
		public static bool noInteractive = false;
		public static Size sizeIcon;
		public static Size sizeImage;
		public static Point locImage;

		/// <summary>
		/// Array of string containing the content of the help screen.
		/// </summary>
		private readonly string[] m_helpLines =
		{
		//  0|-------|---------|---------|---------|---------|---------|---------|---------|80
			" This program will scan your system for installed video games and display",
			" them as a list. The following platforms are supported:",
			" *Amazon *BattleNet *BigFish *EA *Epic *GameJolt *GOG *Humble *Indiegala *itch",
			" *Legacy *Oculus *Paradox *Plarium *Riot *Rockstar *Steam *Ubisoft *Wargaming",
			"",
			" The games list and configuration are stored in .json files in the same folder",
			" as this program. You can manually add games by placing a shortcut (.lnk) in",
			" the \'CustomGames\' subfolder."
		};

		/// <summary>
		/// Constructor
		/// </summary>
		public CDock()
		{ }

		/// <summary>
		/// Run the main program loop.
		/// Return when game is launched or the user decided to exit.
		/// </summary>
		public void MainLoop(string[] args)
		{
			CPlatform platforms = new();
			platforms.AddSupportedPlatform(new PlatformAmazon());
			platforms.AddSupportedPlatform(new PlatformArc());
			platforms.AddSupportedPlatform(new PlatformBattlenet());
			//platforms.AddSupportedPlatform(new PlatformBethesda());	// deprecated May 2022
			platforms.AddSupportedPlatform(new PlatformBigFish());
			//platforms.AddSupportedPlatform(new PlatformCustom());		// See CPlatform.ScanGames()
			platforms.AddSupportedPlatform(new PlatformEA());
			platforms.AddSupportedPlatform(new PlatformEpic());
            platforms.AddSupportedPlatform(new PlatformGameJolt());
            platforms.AddSupportedPlatform(new PlatformGOG());
            platforms.AddSupportedPlatform(new PlatformHumble());
            platforms.AddSupportedPlatform(new PlatformIGClient());
			platforms.AddSupportedPlatform(new PlatformItch());
			platforms.AddSupportedPlatform(new PlatformLegacy());
            platforms.AddSupportedPlatform(new PlatformOculus());
			platforms.AddSupportedPlatform(new PlatformParadox());
			platforms.AddSupportedPlatform(new PlatformPlarium());
			platforms.AddSupportedPlatform(new PlatformRiot());
			platforms.AddSupportedPlatform(new PlatformRockstar());
			platforms.AddSupportedPlatform(new PlatformSteam());
			platforms.AddSupportedPlatform(new PlatformUbisoft());
			platforms.AddSupportedPlatform(new PlatformWargaming());
#if DEBUG
			platforms.AddSupportedPlatform(new PlatformMicrosoft());	// an experiment for now
			//platforms.AddSupportedPlatform(new PlatformMisc());			// another experiment
#endif
			bool import, parseError = false;
			import = CJsonWrapper.ImportFromINI(out CConfig.ConfigVolatile cfgv, out CConfig.Hotkeys keys, out CConfig.Colours cols);
			if (!import) parseError = true;
			import = CJsonWrapper.ImportFromJSON(platforms, out List<CMatch> matches);
			if (!import) parseError = true;

			if (parseError)
			{
				Console.Write("Press any key to continue...");
				Console.ReadKey();
				Console.WriteLine();
			}

			bool consoleOutput = (bool)CConfig.GetConfigBool(CConfig.CFG_USECMD);
			if (consoleOutput) noInteractive = true;

			bool setup = false;
			bool browse = false;
			//string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string path = Directory.GetCurrentDirectory();

			int gameIndex = -1;
			string gameSearch = "";

			foreach (string arg in args)
			{
				gameSearch += arg + " ";
			}
			if (args.Length > 0)
			{
				if (gameSearch[0] == '/' || gameSearch[0] == '-')
				{
					gameSearch = gameSearch[1..];
					if (gameSearch[0].Equals('?') || gameSearch[0].Equals('h') || gameSearch[0].Equals('H'))
						noInteractive = true;
					else if (gameSearch[0].Equals('s') || gameSearch[0].Equals('S'))
					{
						CLogger.LogInfo("Scanning for games...");
						Console.Write("Scanning for games");  // ScanGames() will append a dot for each platform
						platforms.ScanGames((bool)CConfig.GetConfigBool(CConfig.CFG_USECUST), !(bool)CConfig.GetConfigBool(CConfig.CFG_IMGSCAN), false);
						return;
					}
					else if (gameSearch[0].Equals('c') || gameSearch[0].Equals('C'))
					{
						if (consoleOutput)
						{
							CLogger.LogInfo("Switching {0} = false...", CConfig.CFG_USECMD);
							Console.WriteLine("Switching {0} = false...", CConfig.CFG_USECMD);
							CConfig.SetConfigValue(CConfig.CFG_USECMD, false);
							CJsonWrapper.ExportConfig();
						}
						else
						{
							CLogger.LogInfo("Switching {0} = true...", CConfig.CFG_USECMD);
							Console.WriteLine("Switching {0} = true...", CConfig.CFG_USECMD);
							CConfig.SetConfigValue(CConfig.CFG_USECMD, true);
							CJsonWrapper.ExportConfig();
						}
						return;
					}
					else if (gameSearch[0].Equals('p') || gameSearch[0].Equals('P'))
					{
						if (OperatingSystem.IsWindows())
						{
							if (PathEnvironmentUpdate.Add(currentPath, false))
							{
								CLogger.LogInfo("Added program location to PATH.");
								Console.WriteLine("Added {0}.exe location to your PATH environment variable.", filename);
							}
							else
							{
								SetFgColour(cols.errorCC, cols.errorLtCC);
								CLogger.LogWarn("Unable to add program location to PATH!");
								Console.WriteLine("ERROR: Unable to add the program location to your PATH environment variable!");
								Console.ResetColor();
							}
						}
						return;
					}
					else
					{
						if (int.TryParse(gameSearch, out gameIndex))
						{
							if (gameIndex > 0 && gameIndex <= matches.ToArray().Length)
							{
								CLogger.LogInfo("Select {0} from prior search of {1} matches", gameIndex, matches.ToArray().Length);
							}
							else
							{
								SetFgColour(cols.errorCC, cols.errorLtCC);
								CLogger.LogWarn("Invalid search index number!");
								Console.WriteLine($"ERROR: {gameIndex} is an invalid search index number!");
								Console.ResetColor();
								return;
							}
						}
						else
						{
							SetFgColour(cols.errorCC, cols.errorLtCC);
							CLogger.LogWarn("Invalid parameter!");
							Console.WriteLine($"ERROR: /{gameSearch}is an invalid parameter!"); // there's a trailing space
							Console.ResetColor();
							return;
						}
					}
					gameSearch = "";
				}
				else
				{
					gameSearch = gameSearch[0..^1];
					CLogger.LogInfo($"Search from command line: {gameSearch}");
				}
			}

			// Some shell hosts, e.g., Git Bash, Mingw, MSYS2, won't support interactive mode properly
			// PowerCmd displays no output at all

			// Additionally, displaying images on the console is not possible with all shell hosts.
			// I've tested a number of alternative shells. These work (probably because they leverage conhost.exe):
			//   4NT, Clink, Cmder, Far Manager, Git CMD, Midnight Commander, ZTreeWin
			// These do not:
			//   Console2, FireCMD, Take Command (tcmd), Windows Terminal
			// Note that using Take Command (tcc) works, though if you run it through ConEmu some keys aren't supported

			int imgMargin = 0;

			CheckShellCapabilities(ref cfgv, out string parent);

			if (cfgv.imageSize > 0)
				imgMargin = cfgv.imageSize + COLUMN_CUSHION + (cfgv.imageBorder ? IMG_BORDER_X_CUSHION : 0);
			try
			{
				/*
				req'd width for columns, assuming default values, e.g., image size = 16:
				2 = 72+
				3 = 99+
				4 = 126+
				...
				*/
				int ww = Console.WindowWidth;
				if (!cfgv.listView)
					nColumnCount = (ww - imgMargin) / ((ushort)CConfig.GetConfigNum(CConfig.CFG_COLSIZE) + COLUMN_CUSHION);
				else nColumnCount = 1;
				if (nColumnCount <= 1) // shrink image to fit
				{
					nColumnCount = 1;
					cfgv.imageBorder = false;
					if (cfgv.imageSize > 0)
						cfgv.imageSize = (ushort)Math.Min(cfgv.imageSize, ww / 2);
				}
			}
			catch (Exception e)
			{
				if (!consoleOutput)
				{
					SetFgColour(cols.errorCC, cols.errorLtCC);
					CLogger.LogError(e, "Couldn't get console width. Your terminal host does not support interactive mode.");
					Console.WriteLine("ERROR: Your terminal host does not support interactive mode.");  // e.g., Git Bash, Mingw, MSYS2 (though strangely mintty works fine with Cygwin)
					Console.ResetColor();
					noInteractive = true;
				}
			}

			if (noInteractive && gameIndex < 1 && string.IsNullOrEmpty(gameSearch))
			{
				/*
				if (!consoleOutput)
				{
					SetFgColour(cols.errorCC, cols.errorLtCC);
					CLogger.LogWarn("Interactive mode is disabled.");
					Console.WriteLine("ERROR: Interactive mode is disabled.");
					Console.ResetColor();
				}
				*/
				DisplayUsage(cols, parent, matches);
				return;
			}

			// TODO: Redraw after window is moved to another monitor (and adjust DPI scaling, though at the moment it is checked every time an image is drawn)
			/*
			int monCount = 0;
			RefreshActualScreens();
			bool callback(IntPtr hDesktop, IntPtr hdc, ref Rect _, int d) => ++monCount > 0;
			if (WindowUtilities.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, 0))
			{
				CLogger.LogDebug("{0} monitors", monCount);
			}
			else
				CLogger.LogWarn("ERROR: Could not enumerate monitors.");
			*/

			if (cfgv.imageSize > 0)
				CConsoleImage.GetImageProperties(cfgv.imageSize, (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGPOS), out sizeImage, out locImage);
			if (cfgv.iconSize > 0)
				CConsoleImage.GetIconSize(cfgv.iconSize, out sizeIcon);
			if (cfgv.typingInput && CConsoleHelper.m_ConsoleState == CConsoleHelper.ConsoleState.cState_Unknown)
				CConsoleHelper.m_ConsoleState = CConsoleHelper.ConsoleState.cState_Insert;
			else CConsoleHelper.m_ConsoleState = CConsoleHelper.ConsoleState.cState_Navigate;
			if (cfgv.listView && CConsoleHelper.m_MenuType == CConsoleHelper.MenuType.cType_Unknown)
				CConsoleHelper.m_MenuType = CConsoleHelper.MenuType.cType_List;
			else
				CConsoleHelper.m_MenuType = (nColumnCount > 1) ? CConsoleHelper.MenuType.cType_Grid : CConsoleHelper.MenuType.cType_List;
			if ((bool)CConfig.GetConfigBool(CConfig.CFG_USELITE) && CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Unknown)
				CConsoleHelper.m_LightMode = CConsoleHelper.LightMode.cColour_Light;
			else CConsoleHelper.m_LightMode = CConsoleHelper.LightMode.cColour_Dark;
			if (cfgv.imageSize > 0 || cfgv.iconSize > 0)
				CConsoleHelper.m_ImageMien = CConsoleHelper.ImageMien.cImage_Enabled;
			else CConsoleHelper.m_ImageMien = CConsoleHelper.ImageMien.cImage_Disabled;
			if ((bool)CConfig.GetConfigBool(CConfig.CFG_USEFREQ))
				CConsoleHelper.m_SortMethod = CConsoleHelper.SortMethod.cSort_Freq;
			else if ((bool)CConfig.GetConfigBool(CConfig.CFG_USERATE))
				CConsoleHelper.m_SortMethod = CConsoleHelper.SortMethod.cSort_Rating;
			else if ((bool)CConfig.GetConfigBool(CConfig.CFG_USEALPH))
				CConsoleHelper.m_SortMethod = CConsoleHelper.SortMethod.cSort_Alpha;
			else CConsoleHelper.m_SortMethod = CConsoleHelper.SortMethod.cSort_Date;

			m_dockConsole = new CConsoleHelper(nColumnCount, (Console.WindowWidth - imgMargin) / nColumnCount, Console.WindowHeight * nColumnCount); //, state, type, mode, method);

			for (; ; )
			{
				m_nSelectedGame = -1;
				CGame selectedGame = null;
				int nSelectionCode = -1;

				if (gameIndex < 1)
				{
					MenuSwitchboard(cfgv, keys, cols, ref setup, ref browse, ref path, ref gameSearch, ref nSelectionCode);
					CLogger.LogDebug("MenuSwitchboard:{0},{1}", nSelectionCode, m_nCurrentSelection);
				}
				else
				{
					m_nSelectedGame = (int)Array.FindIndex(
						GetPlatformTitles(GamePlatform.All).ToArray(),
						s => s.Equals(matches.ToArray()[gameIndex - 1].m_strTitle, IGNORE_CASE));
					if (m_nSelectedGame > -1)
						nSelectionCode = (int)CConsoleHelper.DockSelection.cSel_Default;
					else
						nSelectionCode = (int)CConsoleHelper.DockSelection.cSel_Exit;
				}

				// Game selection not required

				switch ((CConsoleHelper.DockSelection)nSelectionCode)
				{
					case CConsoleHelper.DockSelection.cSel_Exit:
						Console.ResetColor();
						Console.CursorVisible = true;
						if (!consoleOutput)
						{
							if (!cfgv.dontSaveChanges) CJsonWrapper.ExportConfig();
							ClearNewGames();
							CJsonWrapper.ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
							Console.Clear();
						}
						return;

					case CConsoleHelper.DockSelection.cSel_Fail:
						Console.ResetColor();
						Console.CursorVisible = true;
						if (!consoleOutput) Console.Clear();
						SetFgColour(cols.errorCC, cols.errorLtCC);
						CLogger.LogWarn("Console height too small to support interactive mode.");
						Console.WriteLine("ERROR: Console height is too small to support interactive mode.");
						DisplayUsage(cols, parent, matches);
						return;

					case CConsoleHelper.DockSelection.cSel_Redraw:
						continue;

					case CConsoleHelper.DockSelection.cSel_Back:
						m_nSelectedPlatform = -1;
						m_nCurrentSelection = 0;
						continue;

					case CConsoleHelper.DockSelection.cSel_Help:
						DisplayHelp(Console.WindowHeight, cols);
						continue;

					case CConsoleHelper.DockSelection.cSel_Rescan: // Rescan the game list
						try
						{
							Console.SetCursorPosition(0, Console.WindowHeight - INPUT_BOTTOM_CUSHION);
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						Console.ResetColor();
						CLogger.LogInfo("Scanning for games...");
						Console.Write("Scanning for games");  // ScanGames() will add dots for each platform
						platforms.ScanGames((bool)CConfig.GetConfigBool(CConfig.CFG_USECUST), !(bool)CConfig.GetConfigBool(CConfig.CFG_IMGSCAN), false);
						continue;

					case CConsoleHelper.DockSelection.cSel_Input: // Toggle arrows/typing input
						if (CConsoleHelper.m_ConsoleState == CConsoleHelper.ConsoleState.cState_Navigate)
						{
							CLogger.LogInfo("Switching to insert input...");
							CConfig.SetConfigValue(CConfig.CFG_USETYPE, true);
							cfgv.typingInput = true;
						}
						else
						{
							CLogger.LogInfo("Switching to navigate input...");
							CConfig.SetConfigValue(CConfig.CFG_USETYPE, false);
							cfgv.typingInput = false;
						}
						CConsoleHelper.SwitchState();
						//m_dockConsole = new CConsoleHelper(nColumnCount, (Console.WindowWidth - imgMargin) / nColumnCount, Console.WindowHeight * nColumnCount);
						continue;

					case CConsoleHelper.DockSelection.cSel_View: // Toggle grid/list view
						int ww = 0;
						int wh = 0;
						try
						{
							ww = Console.WindowWidth;
							wh = Console.WindowHeight;
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						if (CConsoleHelper.m_MenuType == CConsoleHelper.MenuType.cType_Grid)
						{
							// Switch to single-column list (with small icons possible)
							CLogger.LogInfo("Switching to list menu type...");
							CConfig.SetConfigValue(CConfig.CFG_USELIST, true);
							cfgv.listView = true;
							nColumnCount = 1;
							if (cfgv.imageSize > 0)
								cfgv.imageSize = (ushort)Math.Min(cfgv.imageSize, ww / 2);
							CConsoleHelper.m_nMaxItemsPerPage = Math.Max(1, wh);
						}
						else
						{
							// Switch to multi-column grid (no small icons)
							CLogger.LogInfo("Switching to grid menu type...");
							CConfig.SetConfigValue(CConfig.CFG_USELIST, false);
							cfgv.listView = false;
							nColumnCount = (ww - imgMargin) / ((ushort)CConfig.GetConfigNum(CConfig.CFG_COLSIZE) + COLUMN_CUSHION);
							if (nColumnCount <= 1) // shrink image to fit
							{
								nColumnCount = 1;
								cfgv.imageBorder = false;
								if (cfgv.imageSize > 0)
									cfgv.imageSize = (ushort)Math.Min(cfgv.imageSize, ww / 2);
							}
							CConsoleHelper.m_nMaxItemsPerPage = Math.Max(nColumnCount, wh * nColumnCount);
						}
						CConsoleHelper.SwitchType();
						m_dockConsole = new CConsoleHelper(nColumnCount, (ww - imgMargin) / nColumnCount, wh * nColumnCount);
						continue;

					case CConsoleHelper.DockSelection.cSel_Colour: // Toggle dark/light mode
						if (CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Dark)
						{
							CLogger.LogInfo("Switching to light colour mode...");
							CConfig.SetConfigValue(CConfig.CFG_USELITE, true);
						}
						else
						{
							CLogger.LogInfo("Switching to dark colour mode...");
							CConfig.SetConfigValue(CConfig.CFG_USELITE, false);
						}
						CConsoleHelper.SwitchMode();
						/*
						try
						{
							m_dockConsole = new CConsoleHelper(nColumnCount, (Console.WindowWidth - imgMargin) / nColumnCount, Console.WindowHeight * nColumnCount);
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						*/
						continue;

					case CConsoleHelper.DockSelection.cSel_Image: // Toggle image mien
						if (CConsoleHelper.m_ImageMien == CConsoleHelper.ImageMien.cImage_Enabled)
						{
							CLogger.LogInfo("Disabling images...");
							if (!cfgv.dontSaveChanges)
							{
								CConfig.SetConfigValue(CConfig.CFG_ICONSIZE, 0);
								CConfig.SetConfigValue(CConfig.CFG_IMGSIZE, 0);
							}
							cfgv.iconSize = 0;
							cfgv.imageSize = 0;
							cfgv.imageBorder = (bool)CConfig.GetConfigBool(CConfig.CFG_IMGBORD);
							imgMargin = 0;
						}
						else
						{
							CLogger.LogInfo("Enabling images...");
							if (!cfgv.dontSaveChanges)
							{
								CConfig.SetConfigDefault(CConfig.CFG_ICONSIZE);
								cfgv.iconSize = (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONSIZE);
								CConfig.SetConfigDefault(CConfig.CFG_IMGSIZE);
								cfgv.imageSize = (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGSIZE);
							}
							else
							{
								if (!ushort.TryParse(CConfig.GetConfigDefault(CConfig.CFG_ICONSIZE), out cfgv.iconSize))
									cfgv.iconSize = 0;
								if (!ushort.TryParse(CConfig.GetConfigDefault(CConfig.CFG_IMGSIZE), out cfgv.imageSize))
									cfgv.imageSize = 0;
							}
							cfgv.imageBorder = (bool)CConfig.GetConfigBool(CConfig.CFG_IMGBORD);
							imgMargin = cfgv.imageSize + COLUMN_CUSHION + (cfgv.imageBorder ? IMG_BORDER_X_CUSHION : 0);
							m_nCurrentSelection = 0;
						}
						CConsoleHelper.SwitchMien();
						try
						{
							ww = Console.WindowWidth;
							nColumnCount = (ww - imgMargin) / ((ushort)CConfig.GetConfigNum(CConfig.CFG_COLSIZE) + COLUMN_CUSHION);
							if (nColumnCount <= 1) // shrink image to fit
							{
								nColumnCount = 1;
								cfgv.imageBorder = false;
								if (cfgv.imageSize > 0)
									cfgv.imageSize = (ushort)Math.Min(cfgv.imageSize, ww / 2);
							}
							m_dockConsole = new CConsoleHelper(nColumnCount, (ww - imgMargin) / nColumnCount, Console.WindowHeight * nColumnCount);
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						continue;

					case CConsoleHelper.DockSelection.cSel_Sort: // Toggle lastrun/freq/rating/alpha method
						string method = "date";
						if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Date)
						{
							method = "frequency";
							CConfig.SetConfigValue(CConfig.CFG_USEALPH, false);
							CConfig.SetConfigValue(CConfig.CFG_USEFREQ, true);
							CConfig.SetConfigValue(CConfig.CFG_USERATE, false);
						}
						else if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Freq)
						{
							method = "rating";
							CConfig.SetConfigValue(CConfig.CFG_USEALPH, false);
							CConfig.SetConfigValue(CConfig.CFG_USEFREQ, false);
							CConfig.SetConfigValue(CConfig.CFG_USERATE, true);
						}
						else if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
						{
							method = "alphabetic";
							CConfig.SetConfigValue(CConfig.CFG_USEALPH, true);
							CConfig.SetConfigValue(CConfig.CFG_USEFREQ, false);
							CConfig.SetConfigValue(CConfig.CFG_USERATE, false);
						}
						else
						{
							CConfig.SetConfigValue(CConfig.CFG_USEALPH, false);
							CConfig.SetConfigValue(CConfig.CFG_USEFREQ, false);
							CConfig.SetConfigValue(CConfig.CFG_USERATE, false);
						}
						try
						{
							Console.SetCursorPosition(0, Console.WindowHeight - INPUT_BOTTOM_CUSHION - 1);
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						CLogger.LogInfo($"Switching to {method} sort method...");
						Console.WriteLine($"Switching to {method} sort method...");
						CConsoleHelper.SwitchMethod();
						SortGames(CConsoleHelper.m_SortMethod,
							(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
							(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
							CultureInfo.InstalledUICulture.Equals("en_US") || CultureInfo.InstalledUICulture.Equals("en_GB"));
						Thread.Sleep(2000);
						/*
						try
						{
							m_dockConsole = new CConsoleHelper(nColumnCount, (Console.WindowWidth - imgMargin) / nColumnCount, Console.WindowHeight * nColumnCount);
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						*/
						continue;

					case CConsoleHelper.DockSelection.cSel_Settings: // Game settings
																	 //setup = true;
						break;

					case CConsoleHelper.DockSelection.cSel_Search: // Find game
						gameSearch = InputPrompt("Search >>> ", cols);
						/*
						char key = Console.ReadKey();
						if (key == keys.cancelCK1 || key == keys.cancelCK2)
						{
							//[cancel search]
						}
						else if (key == keys.completeCK1 || key == keys.completeCK2)
						{
							//[auto-complete]
						}
						else if char.IsLetterOrDigit(key) Console.Write(key);
						*/
						/*
						try
						{
							Console.SetCursorPosition(0, Console.WindowHeight - INPUT_BOTTOM_CUSHION);
							Console.WriteLine();
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						*/
						if (!string.IsNullOrEmpty(gameSearch))
						{
							CLogger.LogInfo($"Search for: {gameSearch}");
							m_nSelectedPlatform = (int)GamePlatform.Search;
						}
						else
							CLogger.LogInfo("No search term provided.");
						Console.ResetColor();
						continue;

					case CConsoleHelper.DockSelection.cSel_Default: // Platform/game selection
						if (gameIndex > 0)
							m_nSelectedPlatform = (int)GamePlatform.All;
						else if (!string.IsNullOrEmpty(gameSearch))
							m_nSelectedPlatform = (int)GamePlatform.Search;
						break;

					default:
						break;
				}

				CLogger.LogDebug("Platform {0}, Game {1}, Selection {2}", m_nSelectedPlatform, m_nSelectedGame, m_nCurrentSelection); //, m_nSelectedCategory
				if (m_nSelectedPlatform < 0)
				{
					List<string> platformList = CConsoleHelper.GetPlatformNames();
					if (!noInteractive && CConsoleHelper.IsSelectionValid(m_nCurrentSelection, platformList.Count))
					{
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOCFG) && m_nCurrentSelection == platformList.Count - 1)
						{
							// calling directly won't work currently, as all of these functions aren't static
							//CConsoleHelper.DisplayCfg(cfgv, keys, cols, ref setup);
							//setup = true;
						}
						else
						{
							m_nSelectedPlatform = GetPlatformEnum(platformList[m_nCurrentSelection], true);
							m_nSelectedGame = -1;
							m_nCurrentSelection = 0;
						}
					}
				}
				else
				{
					string selectedPlatform = GetPlatformString(m_nSelectedPlatform);
					CLogger.LogDebug($"Selected platform: {selectedPlatform}");
					/*
					if (m_nSelectedCategory > -1)
					{
						string selectedCategory = GetCategoryString(m_nSelectedCategory);
						CLogger.LogDebug($"Selected category: {selectedCategory}");
					}
					*/
					if (CConsoleHelper.IsSelectionValid(m_nCurrentSelection, GetPlatformTitles((GamePlatform)m_nSelectedPlatform).Count))
					{
						m_nSelectedGame = m_nCurrentSelection;
						selectedGame = GetPlatformGame((GamePlatform)m_nSelectedPlatform, m_nSelectedGame);
						if (selectedGame != null) CLogger.LogDebug($"Selected game: {selectedGame.Title}");
					}
				}

				// Game selection required

				if (selectedGame != null)
				{
					switch ((CConsoleHelper.DockSelection)nSelectionCode)
					{
						case CConsoleHelper.DockSelection.cSel_Shortcut: // Export [a single] shortcut
							if (OperatingSystem.IsWindows())
							{
								string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
								CLogger.LogInfo("Add shortcut: {0}\\{1}.LNK", desktopPath, selectedGame.Title);
								CConsoleHelper.MakeShortcut(selectedGame.Title, selectedGame.Launch, selectedGame.Icon, desktopPath);
							}
							continue;

						case CConsoleHelper.DockSelection.cSel_Uninst: // Uninstall game
							if (!UninstallGame(selectedGame))
								Thread.Sleep(4000);
							else
							{
								RemoveGame(selectedGame);
								CJsonWrapper.ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
								m_nCurrentSelection--;
								if (GetPlatformGameList((GamePlatform)m_nSelectedPlatform).ToList().Count < 1)
									m_nSelectedPlatform = -1;
							}
							continue;

						// TODO: Hide the game with a flag rather than removing the game from the database
						case CConsoleHelper.DockSelection.cSel_Hide: // Remove from list
							CLogger.LogInfo($"Hiding game: {selectedGame.Title}");
							RemoveGame(selectedGame);
							CJsonWrapper.ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
							//m_nCurrentSelection--;
							m_nCurrentSelection = -1;
							m_nSelectedGame = -1;
							//if (GetPlatformGameList((GamePlatform)m_nSelectedPlatform).ToList().Count < 1)
							m_nSelectedPlatform = -1;
							continue;
						/*
						case CConsoleHelper.DockSelection.cSel_Hide: // Toggle game hidden
							CLogger.LogInfo($"Toggling hidden: {selectedGame.Title}");
							ToggleHidden(
								(GamePlatform)m_nSelectedPlatform,
								m_nCurrentSelection,
								(int)CConsoleHelper.m_SortMethod,
								(bool)config.faveSort,
								true);
							CJsonWrapper.ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
							if (m_nSelectedPlatform == (int)GamePlatform.Hidden &&
								GetPlatformGameList(GamePlatform.Hidden).ToList().Count < 1)
								m_nSelectedPlatform = -1;
							continue;
						*/

						case CConsoleHelper.DockSelection.cSel_Fav: // Toggle game favourite
							CLogger.LogInfo($"Toggling favourite: {selectedGame.Title}");
							ToggleFavourite(
								(GamePlatform)m_nSelectedPlatform,
								m_nCurrentSelection,
								CConsoleHelper.m_SortMethod,
								(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
								(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
								true);
							CJsonWrapper.ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
							if (m_nSelectedPlatform == (int)GamePlatform.Favourites &&
								GetPlatformGameList(GamePlatform.Favourites).ToList().Count < 1)
								m_nSelectedPlatform = -1;
							continue;

						case CConsoleHelper.DockSelection.cSel_Alias: // Set alias for insert mode or command line parameter
							string newAlias = InputPrompt(string.Format("Alias [{0}] >>> ", selectedGame.Alias), cols);
							if (!string.IsNullOrEmpty(newAlias))
							{
								CLogger.LogInfo("Set alias to: {0} for {1}", newAlias, selectedGame.Title);
								selectedGame.Alias = newAlias;
							}
							else
								CLogger.LogInfo("No alias provided.");
							continue;

						case CConsoleHelper.DockSelection.cSel_Tags: // Set pipe-separated tags (TODO: Use tags as custom platforms)
							string newTags = InputPrompt(string.Format("Tags (separated by | ) [{0}] >>> ", selectedGame.Tags), cols);
							if (!string.IsNullOrEmpty(newTags))
							{
								CLogger.LogInfo("Set tags to: {0} for {1}", newTags, selectedGame.Title);
								selectedGame.Tags = new List<string>(
									from part in newTags.Split('|')
									select part.Trim());
							}
							else
								CLogger.LogInfo("No tags provided.");
							continue;

						case CConsoleHelper.DockSelection.cSel_raiseRating: // Raise rating
							if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
							{
								// If currently sorting by ranking, ask user to enter a number
								// (This is a stop-gap solution until we can follow the selection after a re-sort)
								string newRatingR = InputPrompt(string.Format("Rating [{0}] >>> ", selectedGame.Rating), cols);
								if (!string.IsNullOrEmpty(newRatingR))
								{
									if (ushort.TryParse(newRatingR, out ushort rating))
									{
										if (rating < 6)
										{
											selectedGame.Rating = rating;
											if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
											{
												SortGames(CConsoleHelper.m_SortMethod,
													(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
													(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
													CultureInfo.InstalledUICulture.Equals("en_US") || CultureInfo.InstalledUICulture.Equals("en_GB"));
											}
										}
									}
								}
							}
							else if (selectedGame.IncrementRating())
							{
								/*
								if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
								{
									SortGames(CConsoleHelper.m_SortMethod,
										(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
										(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
										CultureInfo.InstalledUICulture.Equals("en_US") || CultureInfo.InstalledUICulture.Equals("en_GB"));
								}
								*/
								// If InfoBar isn't shown, shouldn't we provide some kind of user feedback?
							}
							continue;

						case CConsoleHelper.DockSelection.cSel_lowerRating: // Lower rating
							if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
							{
								// If currently sorting by ranking, ask user to enter a number
								// (This is a stop-gap solution until we can follow the selection after a re-sort)
								string newRatingL = InputPrompt(string.Format("Rating [{0}] >>> ", selectedGame.Rating), cols);
								if (!string.IsNullOrEmpty(newRatingL))
								{
									if (ushort.TryParse(newRatingL, out ushort rating))
									{
										if (rating < 6)
										{
											selectedGame.Rating = rating;
											if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
											{
												SortGames(CConsoleHelper.m_SortMethod,
													(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
													(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
													CultureInfo.InstalledUICulture.Equals("en_US") || CultureInfo.InstalledUICulture.Equals("en_GB"));
											}
										}
									}
								}
							}
							else if (selectedGame.DecrementRating())
							{
								/*
								if (CConsoleHelper.m_SortMethod == CConsoleHelper.SortMethod.cSort_Rating)
								{
									SortGames(CConsoleHelper.m_SortMethod,
										(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
										(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
										CultureInfo.InstalledUICulture.Equals("en_US") || CultureInfo.InstalledUICulture.Equals("en_GB"));
								}
								*/
								// If InfoBar isn't shown, shouldn't we provide some kind of user feedback?
							}
							continue;

						case CConsoleHelper.DockSelection.cSel_downloadImage: // Download image
							CLogger.LogDebug("Download image for {0}", CGameData.GetDescription((GamePlatform)m_nSelectedPlatform));
							switch ((GamePlatform)m_nSelectedPlatform)
							{
								case GamePlatform.Steam:
									DownloadCustomImage(selectedGame.Title, PlatformSteam.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.GOG:
									DownloadCustomImage(selectedGame.Title, PlatformGOG.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Ubisoft:
									//DownloadCustomImage(selectedGame.Title, PlatformUbisoft.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.EA:
									DownloadCustomImage(selectedGame.Title, PlatformEA.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Epic:
									DownloadCustomImage(selectedGame.Title, PlatformEpic.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Bethesda:
									//DownloadCustomImage(selectedGame.Title, PlatformBethesda.GetIconUrl(selectedGame), true);
									//SetFgColour(cols.errorCC, cols.errorLtCC);
									CLogger.LogWarn("Bethesda Launcher was deprecated May 2022");
									Console.WriteLine("ERROR: Bethesda Launcher was deprecated in May 2022!");
									//Console.ResetColor();
									break;
								case GamePlatform.Battlenet:
									//DownloadCustomImage(selectedGame.Title, PlatformBattlenet.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Rockstar:
									DownloadCustomImage(selectedGame.Title, PlatformRockstar.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Amazon:
									DownloadCustomImage(selectedGame.Title, PlatformAmazon.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.BigFish:
									DownloadCustomImage(selectedGame.Title, PlatformBigFish.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Arc:
									//DownloadCustomImage(selectedGame.Title, PlatformArc.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Itch:
									DownloadCustomImage(selectedGame.Title, PlatformItch.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Paradox:
									//DownloadCustomImage(selectedGame.Title, PlatformParadox.GetIconUrl(selectedGame), true);
									break;
                                case GamePlatform.Plarium:			// Webp won't be supported until we finish switch to a cross-platform graphics library
									//DownloadCustomImage(selectedGame.Title, PlatformPlarium.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Twitch:           // deprecated, never implemented
									break;
								case GamePlatform.Wargaming:
									//DownloadCustomImage(selectedGame.Title, PlatformWargaming.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.IGClient:
									DownloadCustomImage(selectedGame.Title, PlatformIGClient.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Microsoft:        // TODO?
									//DownloadCustomImage(selectedGame.Title, PlatformMicrosoft.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Oculus:
									//PlatformOculus.DownloadCustomImage(selectedGame); // Download and extract from zip file
									DownloadCustomImage(selectedGame.Title, PlatformOculus.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Legacy:
									//DownloadCustomImage(selectedGame.Title, PlatformLegacy.GetIconUrl(selectedGame), true);
									break;
								case GamePlatform.Riot:
									//DownloadCustomImage(selectedGame.Title, PlatformRiot.GetIconUrl(selectedGame), true);
									break;
                                case GamePlatform.GameJolt:
                                    DownloadCustomImage(selectedGame.Title, PlatformGameJolt.GetIconUrl(selectedGame), true);
                                    break;
                                case GamePlatform.Humble:			// avif (AV1) won't be supported until we finish switch to a cross-platform graphics library
                                    //DownloadCustomImage(selectedGame.Title, PlatformHumble.GetIconUrl(selectedGame), true);
                                    break;
                                default:
									break;
							}
							continue;

						default:
							break;
					}
				}
				else
				{
					if ((CConsoleHelper.DockSelection)nSelectionCode == CConsoleHelper.DockSelection.cSel_Shortcut) // Export [multiple] shortcuts
						browse = true;
					else if ((CConsoleHelper.DockSelection)nSelectionCode == CConsoleHelper.DockSelection.cSel_Launcher) // Open launcher
					{
						CLogger.LogDebug("Run Launcher: {0}", CGameData.GetDescription((GamePlatform)m_nSelectedPlatform));
						try
						{
							switch ((GamePlatform)m_nSelectedPlatform)
							{
								case GamePlatform.Steam:
									PlatformSteam.Launch();
									break;
								case GamePlatform.GOG:
									PlatformGOG.Launch();
									break;
								case GamePlatform.Ubisoft:
									PlatformUbisoft.Launch();
									break;
								case GamePlatform.EA:
									PlatformEA.Launch();
									break;
								case GamePlatform.Epic:
									PlatformEpic.Launch();
									break;
								case GamePlatform.Bethesda:
									//PlatformBethesda.Launch();
									//SetFgColour(cols.errorCC, cols.errorLtCC);
									CLogger.LogWarn("Bethesda Launcher was deprecated May 2022");
									Console.WriteLine("ERROR: Bethesda Launcher was deprecated in May 2022!");
									//Console.ResetColor();
									break;
								case GamePlatform.Battlenet:
									PlatformBattlenet.Launch();
									break;
								case GamePlatform.Rockstar:
									PlatformRockstar.Launch();
									break;
								case GamePlatform.Amazon:
									PlatformAmazon.Launch();
									break;
								case GamePlatform.BigFish:
									PlatformBigFish.Launch();
									break;
								case GamePlatform.Arc:
									PlatformArc.Launch();
									break;
								case GamePlatform.Itch:
									PlatformItch.Launch();
									break;
								case GamePlatform.Paradox:
									PlatformParadox.Launch();
									break;
								case GamePlatform.Plarium:
									PlatformPlarium.Launch();
									break;
								case GamePlatform.Twitch:           // deprecated, never implemented
									break;
								case GamePlatform.Wargaming:
									PlatformWargaming.Launch();
									break;
								case GamePlatform.IGClient:
									PlatformIGClient.Launch();
									break;
								case GamePlatform.Microsoft:        // TODO?
									if (OperatingSystem.IsWindows())
										PlatformMicrosoft.Launch();
									break;
								case GamePlatform.Oculus:
									PlatformOculus.Launch();
									break;
								case GamePlatform.Legacy:
									PlatformLegacy.Launch();
									break;
								case GamePlatform.Riot:
									PlatformRiot.Launch();
									break;
                                case GamePlatform.GameJolt:
                                    PlatformGameJolt.Launch();
                                    break;
                                case GamePlatform.Humble:
                                    PlatformHumble.Launch();
                                    break;
                                default:
									break;
							}
						}
						catch (Exception e)
						{
							//SetFgColour(cols.errorCC, cols.errorLtCC);
							CLogger.LogError(e, "Cannot start launcher.");
							Console.WriteLine("ERROR: Launcher couldn't start. Is it installed properly?");
							//Console.ResetColor();
						}
						m_nSelectedPlatform = -1; // This prevents the platform from being chosen, but selection ends up going back to the first item in the list
					}

				}

				if (m_nSelectedGame > -1 && selectedGame != null)
				{
					if (selectedGame.IsInstalled)
					{
						NormaliseFrequencies(selectedGame);
						selectedGame.IncrementRuns();
						selectedGame.SetRunDate();
						matches = new List<CMatch>() { new CMatch(selectedGame.Title, 1, 100) };
						CJsonWrapper.ExportSearch(matches);
						if (!cfgv.dontSaveChanges) CJsonWrapper.ExportConfig();

						Console.ResetColor();
						Console.CursorVisible = true;
						if (!noInteractive) Console.Clear();
						else Console.WriteLine();
#if !DEBUG
						if (!StartGame(selectedGame))
						{
							if (noInteractive)
							{
                                SetFgColour(cols.errorCC, cols.errorLtCC);
                                CLogger.LogWarn("Remove game from file.");
                                Console.WriteLine("{0} will be removed from the list.", selectedGame.Title);
                                Console.ResetColor();
                                RemoveGame(selectedGame);
							}
							else
							{
                                //string answer = InputPrompt(string.Format("{0} not found! Remove from list? [y/n] >>> ", selectedGame.Title), cols);
                                //ClearInputLine(cols);
                                SetFgColour(cols.inputCC, cols.inputLtCC);
                                Console.Write("{0} not found! Remove from list? [y/n] >>> ", selectedGame.Title);
                                string answer = Console.ReadLine();
                                if (answer[0] == 'Y' || answer[0] == 'y')
                                {
                                    CLogger.LogWarn("Remove game from file.");
                                    RemoveGame(selectedGame);
                                }
                                Console.ResetColor();
                            }
                        }
#else
						// DEBUG MODE
						// Make sure we've written to configuration file *before* this point, as we're setting overrides to CConfig.config below
						// Don't run the game in debug mode
						Console.BackgroundColor = ConsoleColor.Black;
						Console.ForegroundColor = ConsoleColor.Gray;
						Console.WriteLine("       ID : {0}", selectedGame.ID);
						Console.WriteLine("    Title : {0}", selectedGame.Title);
						Console.WriteLine("    Alias : {0}", selectedGame.Alias);
						Console.WriteLine("   Launch : {0}", selectedGame.Launch);
						Console.WriteLine("     Icon : {0}", selectedGame.Icon);
						Console.WriteLine("Uninstall : {0}", selectedGame.Uninstaller);
						Console.WriteLine(" Last Run : {0}", selectedGame.LastRunDate);
						Console.WriteLine(" Num Runs : {0}", selectedGame.NumRuns);
						Console.WriteLine("Frequency : {0}", selectedGame.Frequency);
						Console.WriteLine("   Rating : {0}", selectedGame.Rating);
						Console.WriteLine();
						Console.Write("DEBUG mode - game will not be launched; press Enter to exit...");
						if (noInteractive)
							cfgv.imageSize = 0;
						else
						{
							cfgv.imageSize = 32;
							CConfig.SetConfigValue(CConfig.CFG_IMGPOS, 100);
							CConfig.SetConfigValue(CConfig.CFG_IMGRES, 256);
							cfgv.imageBorder = true;
							CConfig.SetConfigValue(CConfig.CFG_IMGRTIO, false);
						}
						if (OperatingSystem.IsWindows() && cfgv.imageSize > 0)
						{
							CConsoleImage.GetImageProperties(cfgv.imageSize, (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGPOS), out sizeImage, out locImage);
							if (cfgv.imageBorder)
							{
								CConsoleImage.ShowImageBorder(sizeImage, locImage, IMG_BORDER_X_CUSHION, IMG_BORDER_Y_CUSHION);
								CConsoleImage.ShowImage(m_nCurrentSelection, selectedGame.Title, selectedGame.Icon, false, sizeImage, locImage, CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Light ? cols.bgLtCC : cols.bgCC);
								int ww = Console.WindowWidth;
								int y = 10;
								if (ww > 62)
								{
									ww = 62;
									y++;
								}
								Console.SetCursorPosition(ww, y);
							}
							else
								CConsoleImage.ShowImage(m_nCurrentSelection, selectedGame.Title, selectedGame.Icon, false, sizeImage, locImage, CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Light ? cols.bgLtCC : cols.bgCC);
						}
						Console.ReadLine();
#endif
						ClearNewGames();
						CJsonWrapper.ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
						return;
					}
					else  // game not installed
					{
						if (!InstallGame(selectedGame, cols))
							Thread.Sleep(4000);
						else
						{
							RemoveGame(selectedGame);
							platforms.ScanGames((bool)CConfig.GetConfigBool(CConfig.CFG_USECUST), !(bool)CConfig.GetConfigBool(CConfig.CFG_IMGSCAN), false);
							m_nCurrentSelection--;
							if (GetPlatformGameList((GamePlatform)m_nSelectedPlatform).ToList().Count < 1)
								m_nSelectedPlatform = -1;
						}
					}
				}
			}
		}

		/// <summary>
		/// Display menu and handle the selection
		/// </summary>
		private void MenuSwitchboard(CConfig.ConfigVolatile cfgv, CConfig.Hotkeys keys, CConfig.Colours cols, ref bool setup, ref bool browse, ref string path, ref string gameSearch, ref int nSelectionCode)
		{
			// Show initial options - platforms or all
			// Take the selection as a string (we'll figure out the enum later)
			// Display the options related to the initial selection (all games will show everything)
			//	Allow cancel with escape (make sure to print that in the heading)
			//  Run selected game.

			nSelectionCode = -1;

			List<string> platformList = CConsoleHelper.GetPlatformNames();
			if (platformList.Count < 1)
			{
				SetFgColour(cols.errorCC, cols.errorLtCC);
				CLogger.LogWarn("No games found!");
				Console.WriteLine("ERROR: No games found!");
				Console.ResetColor();
				System.Environment.Exit(-1);
			}

			if (!noInteractive)
			{
				if ((bool)CConfig.GetConfigBool(CConfig.CFG_USEALL))
					m_nSelectedPlatform = (int)GamePlatform.All;
				else if (!(platformList.Contains(GetPlatformString(GamePlatform.Favourites))) &&
					platformList.Count < 3)  // if there's only one platform + All, then choose All
				{
					CLogger.LogDebug("Only one valid platform found.");
					m_nSelectedPlatform = (int)GamePlatform.All;
				}
			}

			if (setup)                          // show setting menu
			{
				ushort oldImgSize = cfgv.imageSize;
				cfgv.imageSize = 0;
				ushort oldIconSize = cfgv.iconSize;
				cfgv.iconSize = 0;
				bool oldListView = cfgv.listView;
				cfgv.listView = true;

				nSelectionCode = m_dockConsole.DisplayCfg(cfgv, keys, cols, ref setup);

				cfgv.listView = oldListView;
				cfgv.imageSize = oldImgSize;
				cfgv.iconSize = oldIconSize;

				// TODO: Do setup

				nSelectionCode = (int)CConsoleHelper.DockSelection.cSel_Redraw;
				m_nCurrentSelection = 0;
				m_nSelectedGame = -1;
				m_nSelectedPlatform = -1;
				setup = false;
			}
			else if (browse && OperatingSystem.IsWindows())                     // show filesystem browser
			{
				string oldPath = path;
				ushort oldImgSize = cfgv.imageSize;
				cfgv.imageSize = 0;
				ushort oldIconSize = cfgv.iconSize;
				cfgv.iconSize = 0;

				nSelectionCode = m_dockConsole.DisplayFS(cfgv, keys, cols, ref browse, ref path);

				if (!string.IsNullOrEmpty(path) && oldPath.Equals(path) && !path.Equals(CConfig.GetConfigString(CConfig.CFG_TXTROOT)))
				{
					int ww = 0;
					string strPlatform = GetPlatformString(m_nSelectedPlatform);
					cfgv.imageSize = oldImgSize;
					cfgv.iconSize = oldIconSize;
					string answer = InputPrompt($"Create shortcuts for \"{strPlatform}\" here [y/n]? >>> ", cols);
					ClearInputLine(cols);
					if (answer[0] == 'Y' || answer[0] == 'y')
					{
						try
						{
							ww = Console.WindowWidth;
							SetBgColour(cols.bgCC, cols.bgLtCC);
							Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - INPUT_BOTTOM_CUSHION);
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
						//SetFgColour(cols.titleCC, cols.titleLtCC);
						CLogger.LogInfo($"Creating shortcuts for \"{strPlatform}\" in {path}");
						int x = 0;
						int nGame = 0;
						GamePlatform platform = (GamePlatform)m_nSelectedPlatform;

						foreach (string strGame in GetPlatformTitles(platform))
						{
							CGame game = GetPlatformGame(platform, nGame);
							if (game != null)
								CConsoleHelper.MakeShortcut(game.Title, game.Launch, game.Icon, path);
							if (x > ww)
							{
								x = 0;
								ClearInputLine(cols);
							}
							Console.Write(".");
							x++;
							nGame++;
						}
					}
					nSelectionCode = (int)CConsoleHelper.DockSelection.cSel_Redraw;
					m_nSelectedGame = -1;
					m_nSelectedPlatform = -1;
					browse = false;
				}
			}
			else if (m_nSelectedPlatform < 0)   // show main menu (platform list)
				nSelectionCode = m_dockConsole.DisplayMenu(cfgv, keys, cols, ref gameSearch, platformList.ToArray());
			else if (m_nSelectedGame < 0)       // show game list
				nSelectionCode = m_dockConsole.DisplayMenu(cfgv, keys, cols, ref gameSearch, GetPlatformTitles((GamePlatform)m_nSelectedPlatform).ToArray());
		}

		/// <summary>
		/// Install the game
		/// </summary>
		/// <param name="game"></param>
		private bool InstallGame(CGame game, CConfig.Colours cols)
		{
			try
			{
				Console.SetCursorPosition(0, Console.WindowHeight - INPUT_BOTTOM_CUSHION);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			try
			{
				switch (game.Platform)
				{
					case GamePlatform.Steam:
						if (InputInstall(game.Title, cols))
							return (PlatformSteam.InstallGame(game) != 0);
						return false;
					case GamePlatform.GOG:
						//if (InputInstall(game.Title, cols))
							return (PlatformGOG.InstallGame(game) != 0);
						//return false;
					case GamePlatform.Ubisoft:
						if (InputInstall(game.Title, cols))
						{
							if (PlatformUbisoft.InstallGame(game) != 0)
								return true;
							else
							{
								// Some games don't provide a valid ID; provide an error in that case
								//SetFgColour(cols.errorCC, cols.errorLtCC);
								CLogger.LogWarn("Cannot get {0} ID for this title.", Enum.GetName(typeof(GamePlatform), GamePlatform.Ubisoft).ToUpper());
								Console.WriteLine("ERROR: Couldn't get ID for this title.");
								//Console.ResetColor();
							}
						}
						return false;
					case GamePlatform.EA:
						if (InputInstall(game.Title, cols))
							return (PlatformEA.InstallGame(game) != 0);
						return false;
					case GamePlatform.Epic:
						if (InputInstall(game.Title, cols))
							return (PlatformEpic.InstallGame(game) != 0);
						return false;
					case GamePlatform.Bethesda:
						/*
						if (InputInstall(game.Title, cols))
							return (PlatformBethesda.InstallGame(game) != 0);
						*/
						//SetFgColour(cols.errorCC, cols.errorLtCC);
						CLogger.LogWarn("Bethesda Launcher was deprecated May 2022");
						Console.WriteLine("ERROR: Bethesda Launcher was deprecated in May 2022!");
						//Console.ResetColor();
						return false;
					case GamePlatform.Battlenet:
						//if (InputInstall(game.Title, cols))
							return (PlatformBattlenet.InstallGame(game) != 0);	// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Rockstar:
						//if (InputInstall(game.Title, cols))
							return (PlatformRockstar.InstallGame(game) != 0);	// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Amazon:
						//if (InputInstall(game.Title, cols))
							return (PlatformAmazon.InstallGame(game) != 0);
						//return false;
					case GamePlatform.BigFish:
						//if (InputInstall(game.Title, cols))
							return (PlatformBigFish.InstallGame(game) != 0);	// TODO: Open client directly to game page
						//return false;
					case GamePlatform.Arc:
						//if (InputInstall(game.Title, cols))
							return (PlatformArc.InstallGame(game) != 0);		// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Itch:
						//if (InputInstall(game.Title, cols))
							return (PlatformItch.InstallGame(game) != 0);
						//return false;
					case GamePlatform.Paradox:
						//if (InputInstall(game.Title, cols))
							return (PlatformParadox.InstallGame(game) != 0);	// TODO: Open client directly to game page
						//return false;
					case GamePlatform.Plarium:
						//if (InputInstall(game.Title, cols))
							return (PlatformPlarium.InstallGame(game) != 0);	// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Twitch:
						return false; // deprecated
					case GamePlatform.Wargaming:
						//if (InputInstall(game.Title, cols))
							return (PlatformWargaming.InstallGame(game) != 0);	// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.IGClient:
						//if (InputInstall(game.Title, cols))
							return (PlatformIGClient.InstallGame(game) != 0);	// TODO: Open client directly to game page
						//return false;
					case GamePlatform.Microsoft:
						if (OperatingSystem.IsWindows())
						{
							//if (InputInstall(game.Title, cols))
								return (PlatformMicrosoft.InstallGame(game) != 0);  // TODO
						}
						return false;
					case GamePlatform.Oculus:
						//if (InputInstall(game.Title, cols))
							return (PlatformOculus.InstallGame(game) != 0);		// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Legacy:
						//if (InputInstall(game.Title, cols))
							return (PlatformLegacy.InstallGame(game) != 0);		// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Riot:
						//if (InputInstall(game.Title, cols))
							return (PlatformRiot.InstallGame(game) != 0);		// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.GameJolt:
						//if (InputInstall(game.Title, cols))
							return (PlatformGameJolt.InstallGame(game) != 0);	// [Doesn't currently show not-installed games]
						//return false;
					case GamePlatform.Humble:
						//if (InputInstall(game.Title, cols))
							return (PlatformHumble.InstallGame(game) != 0);		// [Doesn't currently show not-installed games]
						//return false;
					default:
						//SetFgColour(cols.errorCC, cols.errorLtCC);
						CLogger.LogWarn("Install not supported for this platform.");
						Console.WriteLine("Install not supported for this platform.");
						//Console.ResetColor();
						return false;
				}
			}
			catch (Exception e)
			{
				//SetFgColour(cols.errorCC, cols.errorLtCC);
				CLogger.LogError(e, "Cannot start launcher.");
				Console.WriteLine("ERROR: Launcher couldn't start. Is it installed properly?");
				//Console.ResetColor();
				return false;
			}
		}

		/// <summary>
		/// Uninstall the game
		/// </summary>
		/// <param name="game"></param>
		private bool UninstallGame(CGame game)
		{
			try
			{
				Console.SetCursorPosition(0, Console.WindowHeight - INPUT_BOTTOM_CUSHION);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			if (string.IsNullOrEmpty(game.Uninstaller))
			{
				if (game.Platform == GamePlatform.Epic)
				{
					if (PlatformEpic.UninstallGame(game) != 0)
						return true;
				}
				else
				{
					//SetFgColour(cols.errorCC, cols.errorLtCC);
					CLogger.LogWarn("Uninstaller not found.");
					Console.WriteLine("An uninstaller wasn't found.");
					//Console.ResetColor();
				}
				return false;
			}
			Console.ResetColor();
			Console.Clear();
			CLogger.LogInfo($"Uninstalling game: {game.Title}");
			Console.WriteLine($"Uninstalling game: {game.Title}");
			try
			{
				CLogger.LogInfo("Launch: " + game.Uninstaller);
				if (OperatingSystem.IsWindows())
					_ = StartShellExecute(game.Uninstaller);
				else
					_ = Process.Start(game.Uninstaller);
				return true;
			}
			catch (Exception e)
			{
				//SetFgColour(cols.errorCC, cols.errorLtCC);
				CLogger.LogError(e, "Cannot start uninstaller.");
				Console.WriteLine("ERROR: Uninstaller couldn't start. Were the game files manually deleted?");
				//Console.ResetColor();
				return false;
			}
		}

		/// <summary>
		/// Start the game process
		/// </summary>
		/// <param name="game"></param>
		private bool StartGame(CGame game)
		{
			CLogger.LogInfo($"Starting game: {game.Title}");
			Console.WriteLine($"Starting game: {game.Title}");
			try
			{
				switch (game.Platform)
				{
					case GamePlatform.Steam:
						PlatformSteam.StartGame(game);
						break;
					case GamePlatform.GOG:
						PlatformGOG.StartGame(game);
						break;
					case GamePlatform.Ubisoft:
						PlatformUbisoft.StartGame(game);
						break;
					case GamePlatform.EA:
						PlatformEA.StartGame(game);
						break;
					case GamePlatform.Epic:
						PlatformEpic.StartGame(game);
						break;
					case GamePlatform.Bethesda:
						//PlatformBethesda.StartGame(game);
						//SetFgColour(cols.errorCC, cols.errorLtCC);
						CLogger.LogWarn("Bethesda Launcher was deprecated May 2022");
						Console.WriteLine("ERROR: Bethesda Launcher was deprecated in May 2022!");
						//Console.ResetColor();
						break;
					case GamePlatform.Battlenet:
						PlatformBattlenet.StartGame(game);
						break;
					case GamePlatform.Rockstar:
						PlatformRockstar.StartGame(game);
						break;
					case GamePlatform.Amazon:
						PlatformAmazon.StartGame(game);
						break;
					case GamePlatform.BigFish:
						PlatformBigFish.StartGame(game);
						break;
					case GamePlatform.Arc:
						PlatformArc.StartGame(game);
						break;
					case GamePlatform.Itch:
						PlatformItch.StartGame(game);
						break;
					case GamePlatform.Paradox:
						PlatformParadox.StartGame(game);
						break;
					case GamePlatform.Plarium:
						PlatformPlarium.StartGame(game);
						break;
					case GamePlatform.Twitch:           // deprecated, never implemented
						break;
					case GamePlatform.Wargaming:
						PlatformWargaming.StartGame(game);
						break;
					case GamePlatform.IGClient:
						PlatformIGClient.StartGame(game);
						break;
					case GamePlatform.Microsoft:        // TODO?
						PlatformMicrosoft.StartGame(game);
						break;
					case GamePlatform.Oculus:
						PlatformOculus.StartGame(game);
						break;
					case GamePlatform.Legacy:
						PlatformLegacy.StartGame(game);
						break;
					case GamePlatform.Riot:
						PlatformRiot.StartGame(game);
						break;
                    case GamePlatform.GameJolt:
                        PlatformGameJolt.StartGame(game);
                        break;
                    case GamePlatform.Humble:
                        PlatformHumble.StartGame(game);
                        break;
                    default:
						CLogger.LogInfo($"Launch: {game.Launch}");
						if (OperatingSystem.IsWindows())
							_ = StartShellExecute(game.Launch);
						else
							_ = Process.Start(game.Launch);
						break;
				}
				return true;
			}
			catch (Exception e)
			{
				//SetFgColour(cols.errorCC, cols.errorLtCC);
				CLogger.LogError(e, "Cannot start game.");
				Console.WriteLine("ERROR: Cannot launch game. Were the game files manually deleted?");
				//Console.ResetColor();
				return false;
			}
		}

		/// <summary>
		/// Print output and wait until the user has pressed a key to show next page
		/// </summary>
		private static void WriteWithBreak(ref int line, int height, ConsoleColor outputCol, ConsoleColor outputColLt, ConsoleColor breakCol, ConsoleColor breakColLt, string output)
		{
			if (line > height - INPUT_BOTTOM_CUSHION)
			{
				SetFgColour(breakCol, breakColLt);
				Console.Write("Press any key for more...");
				Console.ReadKey();
				Console.Clear();
				line = 0;
			}
			SetFgColour(outputCol, outputColLt);
			Console.WriteLine(output);
			line++;
		}

		private static void WriteWithBreak(ref int line, int height, ConsoleColor breakCol, ConsoleColor breakColLt)
		{
			WriteWithBreak(ref line, height, ConsoleColor.Black, ConsoleColor.Black, breakCol, breakColLt, string.Empty);
		}

		/// <summary>
		/// Print help screen and wait until the user has pressed a key
		/// </summary>
		private void DisplayHelp(int height, CConfig.Colours cols)
		{
			int line = 0;

			Console.Clear();
			WriteWithBreak(ref line, height, cols.titleCC, cols.titleLtCC, cols.titleCC, cols.titleLtCC,
				string.Format("{0} version {1}", title, version));
			foreach (string str in m_helpLines)
			{
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC, str);
			}
			WriteWithBreak(ref line, height, cols.titleCC, cols.titleLtCC);
			if (CConsoleHelper.m_ConsoleState == CConsoleHelper.ConsoleState.cState_Insert)
			{
				SetFgColour(cols.titleCC, cols.titleLtCC);
				WriteWithBreak(ref line, height, cols.titleCC, cols.titleLtCC, cols.titleCC, cols.titleLtCC,
					"These are the currently accepted commands:");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"   /help | /h | /?");
				if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USEALL)) //&& !(bool)config.onlyCustom)
					WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
						"        /back | /b");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"         /nav | /n");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"        /scan | /s");
				/*
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					("    /alias | /a");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					("/uninstall | /uninst | /u");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"     /view | /v");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"     /grid | /g");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"     /list | /l");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"   /colour | /color | /col");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"    /light | /lt");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"     /dark | /dk");
				*/
				if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT))
					WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
						"   /exit | /x | /quit | /q");
				else
					WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
						"     [Ctrl]+[C] to quit.");
			}
			else //if (CConsoleHelper.m_ConsoleState == CConsoleHelper.ConsoleState.cState_Navigate)
			{
				WriteWithBreak(ref line, height, cols.titleCC, cols.titleLtCC, cols.titleCC, cols.titleLtCC,
					" These are the currently available keys:");
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"            Help: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHELP1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHELP2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"            Left: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYLT1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYLT2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"              Up: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"           Right: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRT1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRT2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"            Down: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"          Select: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL2)), "[", "]", " | ", "N/A", 8));
				if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USEALL)) //&& !(bool)config.onlyCustom)
					WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"            Back: " +
						CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK1)),
						CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK2)), "[", "]", " | ", "N/A", 8));
				/*
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"        Settings: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYCFG1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYCFG2)), "[", "]", " | ", "N/A", 8));
				*/
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"          Search: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYFIND1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYFIND2)), "[", "]", " | ", "N/A", 8));
				/*
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"Search Auto-Comp: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYTAB1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYTAB2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"   Search Cancel: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYESC1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYESC2)), "[", "]", " | ", "N/A", 8));
				*/
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"         Page Up: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYPGUP1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYPGUP2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"       Page Down: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYPGDN1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYPGDN2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"           First: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHOME1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHOME2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"            Last: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYEND1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYEND2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"    Rescan Games: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSCAN1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSCAN2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"  Start Launcher: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYPLAT1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYPLAT2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"Toggle Favourite: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYFAVE1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYFAVE2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"       Set Alias: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYALIAS1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYALIAS2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"       Hide Game: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHIDE1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHIDE2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"  Uninstall Game: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUNIN1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUNIN2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"Export Shortcuts: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYCUT1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYCUT2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"  Nav/Type Input: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYTYPE1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYTYPE2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"  Grid/List View: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYVIEW1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYVIEW2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					" Dark/Light Mode: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYMODE1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYMODE2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"   Toggle Images: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYIMG1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYIMG2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"     Sort Method: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSORT1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSORT2)), "[", "]", " | ", "N/A", 8));
				/*
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"       Edit Tags: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYTAGS1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYTAGS2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"    Raise Rating: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRATEUP1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRATEUP2)), "[", "]", " | ", "N/A", 8));
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"    Lower Rating: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRATEDN1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRATEDN2)), "[", "]", " | ", "N/A", 8));
				*/
				WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
					"  Download Image: " +
					CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDLIMG1)),
					CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDLIMG2)), "[", "]", " | ", "N/A", 8));
				if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT))
					WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
						"            Quit: " +
						CConsoleHelper.OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYQUIT1)),
						CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYQUIT2)), "[", "]", " | ", "N/A", 8));
				else
					WriteWithBreak(ref line, height, cols.entryCC, cols.entryLtCC, cols.titleCC, cols.titleLtCC,
						"            Quit: [Ctrl]+[C]");
			}
			WriteWithBreak(ref line, height, cols.titleCC, cols.titleLtCC);
			SetFgColour(cols.titleCC, cols.titleLtCC);
			Console.Write("Press any key to return to previous menu...");
			Console.ReadKey();
		}

		/// <summary>
		/// Print usage screen
		/// </summary>
		public static void DisplayUsage(CConfig.Colours cols, string parent, List<CMatch> matches)
		{
			Console.WriteLine();
			//				  0|-------|---------|---------|---------|---------|---------|---------|---------|80
			SetFgColour(cols.titleCC);
			Console.WriteLine("{0} version {1}", title, version);
			Console.WriteLine(description);
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.ResetColor();
			Console.WriteLine("Start in interactive mode by running with no parameters,");
			Console.WriteLine("or launch a game by entering search on the command-line, e.g.:");
			Console.WriteLine(" .\\{0} \"My Game\"", filename);
			Console.WriteLine("If there are multiple results in command-line mode, to select an item from a");
			Console.WriteLine("prior search, enter e.g.:");
			Console.WriteLine(" .\\{0} /2", filename);
			Console.WriteLine();
			SetFgColour(cols.titleCC);
			Console.WriteLine("Other parameters:");
			Console.ResetColor();
			Console.WriteLine("   /1 : Replay the previously launched game");
			if (matches.Count > 0)
			{
				SetFgColour(cols.titleCC);
				Console.WriteLine("        [{0}]", matches[0].m_strTitle);
				Console.ResetColor();
			}
			Console.WriteLine("   /S : Rescan your games");
			//Console.WriteLine("/U \"My Game\": Uninstall game");						// TODO
			//Console.WriteLine("/A myalias \"My Game Name\": Change game's alias");	// TODO
			Console.WriteLine("   /C : Toggle command-line only mode");
			Console.WriteLine("   /P : Add {0}.exe location to your path", filename);
			Console.WriteLine("/?|/H : Display this help");
			if (parent.Equals("explorer"))
			{
				Console.WriteLine();
				Console.Write("Press any key to close...");
				Console.ReadKey();
			}
		}

		/// <summary>
		/// Set background colour
		/// </summary>
		public static void SetBgColour(ConsoleColor colour)
		{
			if (colour > (ConsoleColor)(-1)) Console.BackgroundColor = colour;
		}

		/// <summary>
		/// Set background colour for light or dark mode
		/// </summary>
		public static void SetBgColour(ConsoleColor colour, ConsoleColor lightColour)
		{
			if (CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Light && lightColour > (ConsoleColor)(-1))
				Console.BackgroundColor = lightColour;
			else if (colour > (ConsoleColor)(-1))
				Console.BackgroundColor = colour;
		}

		/// <summary>
		/// Set foreground colour
		/// </summary>
		public static void SetFgColour(ConsoleColor colour)
		{
			if (colour > (ConsoleColor)(-1))
			{
				if (Console.BackgroundColor != colour)
					Console.ForegroundColor = colour;
			}
		}

		/// <summary>
		/// Set foreground colour for light or dark mode
		/// </summary>
		public static void SetFgColour(ConsoleColor colour, ConsoleColor lightColour)
		{
			if (CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Light && lightColour > (ConsoleColor)(-1))
			{
				if (Console.BackgroundColor != lightColour)
					Console.ForegroundColor = lightColour;
			}
			else if (colour > (ConsoleColor)(-1))
			{
				if (Console.BackgroundColor != colour)
					Console.ForegroundColor = colour;
			}
		}

		/// <summary>
		/// Draw background on input line
		/// </summary>
		public static bool InputInstall(string title, CConfig.Colours cols)
		{
			string answer = InputPrompt($"Install game {title} [y/n]? >>> ", cols);
			ClearInputLine(cols);
			if (answer[0] == 'Y' || answer[0] == 'y')
			{
				Console.ResetColor();
				Console.Clear();
				CLogger.LogInfo($"Installing game: {title}");
				return true;
			}
			return false;
		}

		/// <summary>
		/// Draw background on input line and get user input
		/// </summary>
		public static string InputPrompt(string prompt, CConfig.Colours cols)
		{
			SetFgColour(cols.inputCC, cols.inputLtCC);
			ClearInputLine(cols);
			Console.Write(prompt);
			Console.CursorVisible = true;

			return Console.ReadLine();
		}

		/// <summary>
		/// Draw background on input line and get masked user input
		/// </summary>
		public static string InputPassword(string prompt, CConfig.Colours cols)
		{
			SetFgColour(cols.inputCC, cols.inputLtCC);
			ClearInputLine(cols);
			Console.Write(prompt);
			Console.CursorVisible = true;

			// Adapted from https://stackoverflow.com/a/3404464/6754996
			string pwd = "";
			while (true)
			{
				ConsoleKeyInfo inp = Console.ReadKey(true);
				if (inp.Key == ConsoleKey.Enter)
					break;
				else if (inp.Key == ConsoleKey.Escape)
                {
					pwd = "";
					break;
                }
				else if (inp.Key == ConsoleKey.Backspace)
				{
					if (pwd.Length > 0)
					{
						pwd = pwd.Remove(pwd.Length - 1);
						Console.Write("\b \b");
					}
				}
				else if (inp.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
				{
					pwd += inp.KeyChar;
					Console.Write("*");
				}
			}
			return pwd;
		}

		/// <summary>
		/// Draw background on input line
		/// </summary>
		public static void ClearInputLine(CConfig.Colours cols)
		{
			try
			{
				int y = Console.WindowTop + Console.WindowHeight - INPUT_BOTTOM_CUSHION;
				SetBgColour(cols.bgCC, cols.bgLtCC);
				Console.SetCursorPosition(0, y);
				for (int i = 0; i < Console.WindowWidth; ++i)
				{
					Console.Write(" ");
				}
				Console.SetCursorPosition(0, y);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
		}

        /// <summary>
        /// Clear image with appropriate light or dark mode
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static void ClearColour(ConsoleColor bgDark, ConsoleColor bgLight)
		{
			if (CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Light && bgLight > (ConsoleColor)(-1))
				CConsoleImage.ClearImage(sizeImage, locImage, bgLight);
			else if (CConsoleHelper.m_LightMode == CConsoleHelper.LightMode.cColour_Dark && bgDark > (ConsoleColor)(-1))
				CConsoleImage.ClearImage(sizeImage, locImage, bgDark);
			else
				CConsoleImage.ClearImage(sizeImage, locImage, ConsoleColor.Black);
		}

		/// <summary>
		/// Check whether current shell has known issues with interactive applications and image display
		/// <returns>bool, whether interactive mode is supported</returns>
		/// </summary>
		public static void CheckShellCapabilities(ref CConfig.ConfigVolatile cfgv, out string parentName)
		{
			parentName = "";
			bool shellError = false;
			try
			{
				parentName = ParentProcessUtilities.GetParentProcess().ProcessName;
				CLogger.LogInfo($"Parent process: {parentName}");
				/*
				if (parentName.Equals("explorer") && !(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
                {
					MessageBox.Show("ERROR: Interactive mode is disabled.");  // Show usage hint in Message Box? Or just pause before closing?
				}
				else
				*/

				// 4NT and clink work just fine, but don't have process parents; although clink's parent is cmd, so it winds up in the catch()
				if (!parentName.Equals("4nt"))
				{
					IntPtr parentHandle = ParentProcessUtilities.GetParentProcess().Handle;

					// if cmd or PowerShell is launched via shortcut or Run box, this will be "explorer"
					var parentProcess = ParentProcessUtilities.GetParentProcess(parentHandle);
					if (parentProcess != null)
					{
						string parentParentName = parentProcess.ProcessName;
						CLogger.LogInfo($"Parent process parent: {parentParentName}");

						// With PowerCmd, Console.Write() is not visible at all, so we can't even show an error message!
						if (parentParentName.Equals("PowerCmd"))
						{
							if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
							{
								//SetFgColour(cols.errorCC, cols.errorLtCC);
								CLogger.LogWarn("{0} host not supported.", parentParentName);
								Console.WriteLine("ERROR: Your {0} host is not not supported.", parentParentName);
								Console.ResetColor();
								shellError = true;
								noInteractive = true;
							}
						}
						else if (!((bool)CConfig.GetConfigBool(CConfig.CFG_USECMD)))
						{
							if (parentName.Equals("tcc") &&            // many keys won't work with combination of ConEmu64 + Take Command (tcc)
									 parentParentName.Equals("ConEmuC64"))  // (though tcc is fine otherwise)
							{
								if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD) ||
									(!cfgv.typingInput &&
									// This isn't really the best test, but it's for a corner case...
									(CConfig.GetConfigString(CConfig.CFG_KEYUP1).Equals("UpArrow") ||
									CConfig.GetConfigString(CConfig.CFG_KEYUP2).Equals("UpArrow"))))
								{
									//SetFgColour(cols.errorCC, cols.errorLtCC);
									CLogger.LogWarn("Many keys (arrows, F1-F12) do not work in {0} host with {1}.\nSwitching to typing input...", parentParentName, parentName);
									Console.WriteLine("WARNING: Many keys (arrows, F1-F12) do not work in {0} host with {1}.\nSwitching to typing input...", parentParentName, parentName);
									//Console.ResetColor();
									cfgv.typingInput = true;
									shellError = true;
								}
							}

							else if (parentParentName.Equals("FireCMD"))    // Displays menus, but colours aren't displayed, making navigation mode nigh useless
							{
								if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USETYPE))
								{
									//SetFgColour(cols.errorCC, cols.errorLtCC);
									/*
									CLogger.LogWarn("{0} host not supported.", parentParentName);
									Console.WriteLine("ERROR: Your {0} host is not supported.", parentParentName);
									//Console.ResetColor();
									return;
									*/
									CLogger.LogWarn("{0} host does not support navigation mode.\nSwitching input state...", CConfig.CFG_USETYPE, parentParentName);
									Console.WriteLine("WARNING: {0} == false, but your {1} host does not support this.\nSwitching input state...", CConfig.CFG_USETYPE, parentParentName);
									//Console.ResetColor();
									cfgv.typingInput = true;
									shellError = true;
								}
								if ((ushort)CConfig.GetConfigNum(CConfig.CFG_IMGSIZE) > 0 || (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONSIZE) > 0)
								{
									//SetFgColour(cols.errorCC, cols.errorLtCC);
									CLogger.LogWarn("{0} host does not support images.\nDisabling images...", parentParentName);
									Console.WriteLine("WARNING: {0} or \n{1} > 0, but your {2} host does not support this.\nDisabling images...", CConfig.CFG_IMGSIZE, CConfig.CFG_ICONSIZE, parentParentName);
									//Console.ResetColor();
									cfgv.iconSize = 0;
									cfgv.imageSize = 0;
									shellError = true;
								}
							}
							/*
							else if (parentParentName.Equals("ZTW64"))		// I have inconsistently observed weird issues with newline characters
							{
								//SetFgColour(cols.errorCC, cols.errorLtCC);
								CLogger.LogWarn("{0} host sometimes has display issues.", parentParentName);
								Console.WriteLine("WARNING: Your {0} host may have display issues.", parentParentName);
								//Console.ResetColor();
								shellError = true;
							}
							*/
							else
							{
								// check for known non-conhost terminal hosts (images not supported)
								if (((ushort)CConfig.GetConfigNum(CConfig.CFG_IMGSIZE) > 0 || (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONSIZE) > 0) &&
									(parentParentName.Equals("WindowsTerminal") ||          // Windows Terminal
									 parentParentName.StartsWith("ServiceHub.Host.CLR") ||  // Visual Studio 
									 parentParentName.Equals("Code") ||                     // Visual Studio Code
									 parentParentName.Equals("Hyper") ||                    // Hyper
									 parentParentName.Equals("tcmd") ||                     // Take Command
									 parentParentName.Equals("bash") ||                     // Cygwin Terminal (mintty), though this one may give false negatives (MSYS in 
									 parentParentName.Equals("Console")))                   // Console2 or ConsoleZ
								{
									//TODO: Show this warning only once?

									//SetFgColour(cols.errorCC, cols.errorLtCC);
									CLogger.LogWarn("{0} host does not support images.\nDisabling images...", parentParentName);
									Console.WriteLine("WARNING: {0} or \n{1} > 0, but your {2} host does not support this.\nDisabling images...", CConfig.CFG_IMGSIZE, CConfig.CFG_ICONSIZE, parentParentName);
									//Console.ResetColor();
									cfgv.iconSize = 0;
									cfgv.imageSize = 0;
									shellError = true;
								}
							}
						}
					}
				}

				//TODO: Enable this warning after we have a method to show it only once.

				/*
				CConsoleImage.CONSOLE_FONT_INFO_EX currentFont = new CConsoleImage.CONSOLE_FONT_INFO_EX();
				CConsoleImage.GetCurrentConsoleFontEx(CConsoleImage.GetStdHandle(CConsoleImage.STD_OUTPUT_HANDLE), false, currentFont);
				if (currentFont.FaceName.Equals("Terminal"))
				{
					//SetFgColour(cols.errorCC, cols.errorLtCC);
					CLogger.LogWarn("Terminal set to \"Raster Fonts,\" which may display incorrect characters.");
					Console.WriteLine("WARNING: Your terminal is set to \"Raster Fonts,\" which may display incorrect characters.");
					//Console.ResetColor();
					shellError = true;
				}
				*/
			}
			catch (Exception e)
			{
				CLogger.LogError(e, "INFO: Couldn't get parent process. Terminal host may not be supported.");
			}
			if (shellError)
				Thread.Sleep(4000);
		}

		public static bool DownloadCustomImage(string title, string url, bool overwrite = false)
        {
			if (!string.IsNullOrEmpty(url))
			{
				string ext = Path.GetExtension(url);
				if (string.IsNullOrEmpty(ext))
					ext = ".jpg";
				string iconFile = Path.Combine(currentPath, IMAGE_FOLDER_NAME,
					string.Concat(title.Split(Path.GetInvalidFileNameChars())) + ext);

				if (overwrite)
					BackupCustomImage(title, false);

				if (!File.Exists(iconFile))
				{
					CLogger.LogDebug("Download image <{0}>", url);
					var task = Task.Run(async () =>
					{
						try
						{
							//using WebClient client = new();
							//client.DownloadFile(url, iconFile);
							using HttpClient client = new();
							using HttpResponseMessage response = await client.GetAsync(url);
							using FileStream fs = new(iconFile, FileMode.CreateNew);
							await response.Content.CopyToAsync(fs);
						}
						catch (WebException we)
						{
							CLogger.LogWarn(we.Message);
						}
					});

					if (File.Exists(iconFile))
					{
						if (overwrite)
							DeleteCustomImage(title, true);
						return true;
					}
					else if (overwrite)
						BackupCustomImage(title, true);
				}
			}
			return false;
		}

		public static void DeleteCustomImage(string title, bool justBackups = false)
        {
			foreach (string ext in supportedImages)
			{
				string iconFile = Path.Combine(currentPath, IMAGE_FOLDER_NAME,
					string.Concat(title.Split(Path.GetInvalidFileNameChars())) +
					"." + ext);
				try
				{
					if (justBackups)
					{
						string iconFileBak = iconFile + ".backup";
						if (File.Exists(iconFileBak))
						{
							CLogger.LogDebug("Deleting \"{0}\"", iconFileBak);
							File.Delete(iconFileBak);
						}
					}
					else if (File.Exists(iconFile))
					{
						CLogger.LogDebug("Deleting \"{0}\"", iconFile);
						File.Delete(iconFile);
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}
		}

		public static void BackupCustomImage(string title, bool restore = false)
		{
			foreach (string ext in supportedImages)
			{
				string iconFile = Path.Combine(currentPath, IMAGE_FOLDER_NAME,
					string.Concat(title.Split(Path.GetInvalidFileNameChars())) +
					"." + ext);
				try
				{
					string iconFileBak = iconFile + ".backup";
					if (restore)
					{
						if (File.Exists(iconFileBak))
						{
							CLogger.LogDebug("Restoring \"{0}\"", iconFile);
							File.Move(iconFileBak, iconFile, true);
						}
						break;
					}
					else
					{
						if (File.Exists(iconFile))
						{
							CLogger.LogDebug("Backing up \"{0}\"", iconFile);
							File.Move(iconFile, iconFileBak, true);
						}
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}
		}

		[SupportedOSPlatform("windows")]
		public static Guid GetGuid()
		{
            using RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry64).OpenSubKey("SOFTWARE\\Microsoft\\Cryptography", RegistryKeyPermissionCheck.ReadSubTree); // HKLM64
            return Guid.Parse((string)key.GetValue("MachineGuid"));
        }

		[SupportedOSPlatform("windows")]
		public static Process StartShellExecute(string file)
		{
			Process cmdProcess = new();
			cmdProcess.StartInfo.FileName = file;
			cmdProcess.StartInfo.UseShellExecute = true;
			cmdProcess.Start();
			return cmdProcess;
		}

		[SupportedOSPlatform("windows")]
		public static Process StartAndRedirect(string file, string args = "", string dir = "")
		{
			Process cmdProcess = new();
			cmdProcess.StartInfo.FileName = file;
			cmdProcess.StartInfo.Arguments = args;
			cmdProcess.StartInfo.WorkingDirectory = dir;
			cmdProcess.StartInfo.UseShellExecute = false;
			cmdProcess.StartInfo.RedirectStandardOutput = true;
			cmdProcess.StartInfo.RedirectStandardError = true;
			//cmdProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
			cmdProcess.Start();
			return cmdProcess;
		}

		/*
		public class MonitorInfo
		{
			public bool IsPrimary = false;
			public Rectangle Bounds = new Rectangle();
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Rect
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		public static List<MonitorInfo> ActualScreens = new List<MonitorInfo>();

		public delegate bool MonitorEnumProc(IntPtr hDesktop, IntPtr hdc, ref Rect pRect, int dwData);

		public static void RefreshActualScreens()
		{
			ActualScreens.Clear();
			MonitorEnumProc callback = (IntPtr hDesktop, IntPtr hdc, ref Rect prect, int d) =>
			{
				ActualScreens.Add(new MonitorInfo()
				{
					Bounds = new Rectangle()
					{
						X = prect.left,
						Y = prect.top,
						Width = prect.right - prect.left,
						Height = prect.bottom - prect.top,
					},
					IsPrimary = (prect.left == 0) && (prect.top == 0),
				});

				return true;
			};

			WindowUtilities.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, 0);
		}

		[DllImport("User32.dll")]
		public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRect, MonitorEnumProc callback, int dwData);
		*/

		/*
		// https://www.sadrobot.co.nz/blog/2011/06/21/when-system-diagnostics-process-creates-a-process-it-inherits-inheritable-handles-from-the-parent-process/
		[SupportedOSPlatform("windows")]
		public static void StartDisinherit(string command)
		{
			// We can use the string builder to build up our full command line, including arguments
			var sb = new StringBuilder(command);
			var processSecurity = new SecurityAttributes();
			var threadSecurity = new SecurityAttributes();

			processSecurity.nLength = Marshal.SizeOf(processSecurity);
			threadSecurity.nLength = Marshal.SizeOf(threadSecurity);

			if (CreateProcess(null, sb, processSecurity, threadSecurity, false, normalPriorityClass,
				 IntPtr.Zero, null, new StartupInfo(), new ProcessInformation()))
			{
				// Process was created successfully
				return;
			}

			// We couldn't create the process, so raise an exception with the details.
			throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		const int normalPriorityClass = 0x0020;

		[DllImport("Kernel32", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
		internal static extern bool CreateProcess(
			[MarshalAs(UnmanagedType.LPTStr)] string applicationName,
			StringBuilder commandLine,
			SecurityAttributes processAttributes,
			SecurityAttributes threadAttributes,
			bool inheritHandles,
			int creationFlags,
			IntPtr environment,
			[MarshalAs(UnmanagedType.LPTStr)] string currentDirectory,
			StartupInfo startupInfo,
			ProcessInformation processInformation
		);
		*/

		[StructLayout(LayoutKind.Sequential)]
		internal class ProcessInformation
		{
			public IntPtr hProcess = IntPtr.Zero;
			public IntPtr hThread = IntPtr.Zero;
			public int dwProcessId;
			public int dwThreadId;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal class StartupInfo
		{
			public int cb;
			public IntPtr lpReserved = IntPtr.Zero;
			public IntPtr lpDesktop = IntPtr.Zero;
			public IntPtr lpTitle = IntPtr.Zero;
			public int dwX;
			public int dwY;
			public int dwXSize;
			public int dwYSize;
			public int dwXCountChars;
			public int dwYCountChars;
			public int dwFillAttribute;
			public int dwFlags;
			public short wShowWindow;
			public short cbReserved2;
			public IntPtr lpReserved2 = IntPtr.Zero;
			public SafeFileHandle hStdInput = new(IntPtr.Zero, false);
			public SafeFileHandle hStdOutput = new(IntPtr.Zero, false);
			public SafeFileHandle hStdError = new(IntPtr.Zero, false);

			public StartupInfo()
			{
				dwY = 0;
				cb = Marshal.SizeOf(this);
			}

			public void Dispose()
			{
				// close the handles created for child process
				if (hStdInput != null && !hStdInput.IsInvalid)
				{
					hStdInput.Close();
					hStdInput = null;
				}

				if (hStdOutput != null && !hStdOutput.IsInvalid)
				{
					hStdOutput.Close();
					hStdOutput = null;
				}

				if (hStdError == null || hStdError.IsInvalid) return;

				hStdError.Close();
				hStdError = null;
			}
		}

		/*
		[StructLayout(LayoutKind.Sequential)]
		internal class SecurityAttributes
		{
			public int nLength = 12;
			public SafeLocalMemHandle lpSecurityDescriptor = new SafeLocalMemHandle(IntPtr.Zero, false);
			public bool bInheritHandle;
		}

		[SuppressUnmanagedCodeSecurity, HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
		internal sealed class SafeLocalMemHandle : SafeHandleZeroOrMinusOneIsInvalid
		{
			internal SafeLocalMemHandle() : base(true) { }

			[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
			internal SafeLocalMemHandle(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle)
			{
				SetHandle(existingHandle);
			}

			[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
			internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string stringSecurityDescriptor,
				int stringSDRevision, out SafeLocalMemHandle pSecurityDescriptor, IntPtr securityDescriptorSize);

			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success), DllImport("kernel32.dll")]
			private static extern IntPtr LocalFree(IntPtr hMem);

			protected override bool ReleaseHandle()
			{
				return (LocalFree(handle) == IntPtr.Zero);
			}
		}
		*/

		/// <summary>
		/// A utility class to minimise/restore/maximise windows.
		/// </summary>
		public struct WindowMessage
        {
			public const int SW_HIDE = 0;
			public const int SW_NORMAL = 1;
			public const int SW_SHOWMINIMIZED = 2;
			public const int SW_MAXIMIZE = 3;
			public const int SW_SHOWNOACTIVATE = 4;
			public const int SW_SHOW = 5;
			public const int SW_MINIMIZE = 6;
			public const int SW_SHOWMINNOACTIVE = 7;
			public const int SW_SHOWNA = 8;
			public const int SW_RESTORE = 9;
			public const int SW_SHOWDEFAULT = 10;
			public const int SW_FORCEMINIMIZE = 11;

			[DllImport("user32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

			[DllImport("user32.dll")]
			public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
		}

		/// <summary>
		/// A utility class to update path environment variable.
		/// </summary>
		public struct PathEnvironmentUpdate
		{
			const int HWND_BROADCAST = 0xffff;
			const uint WM_SETTINGCHANGE = 0x001a;

			[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
			static extern bool SendNotifyMessage(
				IntPtr hWnd,
				uint Msg,
				UIntPtr wParam,
				string lParam);

			[SupportedOSPlatform("windows")]
			public static bool Add(string inPath, bool allUsers)
			{
				try
				{
					RegistryKey envKey;
					string newPath;
					if (allUsers)
					{
						envKey = Registry.LocalMachine.OpenSubKey(
							@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
							true);
					}
					else
					{
						envKey = Registry.CurrentUser.OpenSubKey("Environment", true);
						string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
						if (inPath.Length > profile.Length &&
							inPath.Substring(0, profile.Length).Equals(profile))
						{
							inPath = "%USERPROFILE%" + inPath[profile.Length..];
						}
					}
					using (envKey)
					{
						if (envKey == null)
						{
							CLogger.LogWarn("Could not access environment!");
							return false;
						}
						var oldPath = envKey.GetValue("PATH");
						if (oldPath != null)
							newPath = (string)oldPath + ";" + inPath;
						else
							newPath = inPath;
						CLogger.LogDebug($"New PATH: {newPath}");
						envKey.SetValue("PATH", newPath);
						SendNotifyMessage((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, (UIntPtr)0, "Environment");
						return true;
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
				return false;
			}
		}
	}

	/// <summary>
	/// A utility class to determine a process parent.
	/// https://stackoverflow.com/a/3346055
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct ParentProcessUtilities
	{
		const int PROCESS_BASIC_INFORMATION = 0;

		// These members must match PROCESS_BASIC_INFORMATION
		internal IntPtr Reserved1;
		internal IntPtr PebBaseAddress;
		internal IntPtr Reserved2_0;
		internal IntPtr Reserved2_1;
		internal IntPtr UniqueProcessId;
		internal IntPtr InheritedFromUniqueProcessId;

		[DllImport("ntdll.dll")]
		private static extern int NtQueryInformationProcess(
			IntPtr processHandle,
			int processInformationClass,
			ref ParentProcessUtilities processInformation,
			int processInformationLength,
			out uint returnLength);

		/// <summary>
		/// Gets the parent process of the current process.
		/// </summary>
		/// <returns>An instance of the Process class.</returns>
		public static Process GetParentProcess()
		{
			return GetParentProcess(Process.GetCurrentProcess().Handle);
		}

		/// <summary>
		/// Gets the parent process of specified process.
		/// </summary>
		/// <param name="id">The process id.</param>
		/// <returns>An instance of the Process class.</returns>
		public static Process GetParentProcess(int id)
		{
			Process process = Process.GetProcessById(id);
			return GetParentProcess(process.Handle);
		}

		/// <summary>
		/// Gets the parent process of a specified process.
		/// </summary>
		/// <param name="handle">The process handle.</param>
		/// <returns>An instance of the Process class.</returns>
		public static Process GetParentProcess(IntPtr handle)
		{
			ParentProcessUtilities pbi = new();
			int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out uint _); // out uint returnLength);
			if (status != 0)
				throw new Win32Exception(status);

			try
			{
				return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
			}
			catch (ArgumentException)
			{
				// not found
				return null;
			}
		}
	}
}
