using Logger;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static GameLauncher_Console.CGameData;

namespace GameLauncher_Console
{
	[ComImport]
	[Guid("00021401-0000-0000-C000-000000000046")]
	internal class ShellLink
	{
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("000214F9-0000-0000-C000-000000000046")]
	internal interface IShellLink
	{
		void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
		void GetIDList(out IntPtr ppidl);
		void SetIDList(IntPtr pidl);
		void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
		void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
		void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
		void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
		void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
		void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
		void GetHotkey(out short pwHotkey);
		void SetHotkey(short wHotkey);
		void GetShowCmd(out int piShowCmd);
		void SetShowCmd(int iShowCmd);
		void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
		void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
		void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
		void Resolve(IntPtr hwnd, int fFlags);
		void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
	}

	/// <summary>
	/// Console implementation for this project
	/// </summary>
	public class CConsoleHelper
	{
		/// <summary>
		/// Console state enum.
		/// The console supports two states:
		/// * Type	: User can type out commands
		/// * Browse: User can use the WASD keys to browse the items and enter/space to select item (early stage gui)
		/// </summary>
		public enum ConsoleState
		{
			cState_Unknown = -1,
			cState_Insert = 0,
			cState_Navigate = 1
		};

		/// <summary>
		/// Menu type enum.
		/// Menu elements can be displayed as either:
		/// * Grid: Print the elements across X columns (column number is controlled by m_OptionsPerLine)
		/// * List: Print the elements in a single list (m_nOptionsPerLine should be set to 1 to support arrow selection)
		/// </summary>
		public enum MenuType
		{
			cType_Unknown = -1,
			cType_List = 0,
			cType_Grid = 1
		}

		/// <summary>
		/// Colour mode enum.
		/// Colours can be switched between two sets of customizable presets:
		/// * Dark
		/// * Light
		/// </summary>
		public enum LightMode
		{
			cColour_Unknown = -1,
			cColour_Light = 0,
			cColour_Dark = 1
		}

		/// <summary>
		/// Image mien enum.
		/// Images can be enabled or disabled.
		/// </summary>
		public enum ImageMien
		{
			cImage_Unknown = -1,
			cImage_Disabled = 0,
			cImage_Enabled = 1
		}

		/// <summary>
		/// Sort method enum.
		/// Games can be sorted in one of the following methods:
		/// * Alphabetically: Sorted A-Z
		/// * Date: Sorted by the most recent run
		/// * Frequency: Sorted by the most often run
		/// </summary>
		public enum SortMethod
		{
			cSort_Unknown = -1,
			cSort_Alpha = 0,
			cSort_Freq = 1,
			cSort_Rating = 2,
			cSort_Date = 3,
		}

		public static ConsoleState m_ConsoleState = ConsoleState.cState_Unknown;
		public static MenuType m_MenuType = MenuType.cType_Unknown;
		public static LightMode m_LightMode = LightMode.cColour_Unknown;
		public static ImageMien m_ImageMien = ImageMien.cImage_Unknown;
		public static SortMethod m_SortMethod = SortMethod.cSort_Unknown;

		public static int m_nOptionsPerLine = 1;   // Control the number of columns
		public static int m_nSpacingPerLine = 15;  // Control the width of the columns
		public static int m_nMaxItemsPerPage = 1;

		public class OptionVal
		{
			public string name;
			public string strVal;
			public bool bVal;
			public ushort nVal;
			public int iVal;
			public Type type;
		};

		/// <summary>
		/// Constructor:
		/// Call base class constructor
		/// </summary>
		/// <param name="nColumnCount">Numer of columns</param>
		/// <param name="nSpacing">Spacing between columns</param>
		/// <param name="state">Initial console state</param>
		public CConsoleHelper(int nColumnCount, int nSpacing, int nPaging) //, ConsoleState state) : base(nColumnCount, nSpacing, state)
		{
			m_nOptionsPerLine = Math.Max(1, nColumnCount);
			m_nSpacingPerLine = Math.Max(15, nSpacing);
			m_nMaxItemsPerPage = Math.Max(m_nOptionsPerLine, nPaging);
			//m_ConsoleState = (state == ConsoleState.cState_Unknown) ? ConsoleState.cState_Navigate : state;
			//m_MenuType = (type == MenuType.cType_Unknown) ? ((nColumnCount > 1) ? MenuType.cType_Grid : MenuType.cType_List) : type;
			//m_LightMode = (mode == LightMode.cColour_Unknown) ? LightMode.cColour_Dark : mode;
			//m_ImageMien = (mien == ImageMien.cImage_Unknown) ? ImageMien.cImage_Enabled : mien;
			//m_SortMethod = (method == SortMethod.cSort_Unknown) ? SortMethod.cSort_Date : method;
		}

		/// <summary>
		/// Selection options for special commands
		/// -1 should be used as the default state
		/// Once a command such as exit or back is executed, the state should be set to -1.
		/// </summary>
		public enum DockSelection
		{
			cSel_downloadAll = -26,
			cSel_Tags = -25,
			cSel_raiseRating = -24,
			cSel_lowerRating = -23,
			cSel_downloadImage = -22,
			cSel_Launcher = -21,
			cSel_Settings = -20,
			cSel_Search = -19,
			cSel_Sort = -18,
			cSel_Image = -17,
			cSel_Colour = -16,
			cSel_View = -15,
			cSel_Input = -14,
			cSel_New = -13,
			cSel_Hide = -12,
			cSel_Shortcut = -11,
			cSel_Alias = -10,
			cSel_Uninst = -9,
			cSel_Redraw = -8,
			cSel_Fail = -7,
			cSel_Help = -6,
			cSel_Exit = -5,
			cSel_Back = -4,
			cSel_Fav = -3,
			cSel_Rescan = -2,
			cSel_Default = -1
		}

		/// <summary>
		/// Switch the console state
		/// </summary>
		public static void SwitchState()
		{
			m_ConsoleState = (ConsoleState)(((int)m_ConsoleState + 1) % 2);
		}

		/// <summary>
		/// Switch the menu type
		/// </summary>
		public static void SwitchType()
		{
			m_MenuType = (MenuType)(((int)m_MenuType + 1) % 2);
		}

		/// <summary>
		/// Switch the colour mode
		/// </summary>
		public static void SwitchMode()
		{
			m_LightMode = (LightMode)(((int)m_LightMode + 1) % 2);
		}

		/// <summary>
		/// Switch whether images are displayed
		/// </summary>
		public static void SwitchMien()
		{
			m_ImageMien = (ImageMien)(((int)m_ImageMien + 1) % 2);
		}

		/// <summary>
		/// Switch the sort mode
		/// </summary>
		public static void SwitchMethod()
		{
			m_SortMethod = (SortMethod)(((int)m_SortMethod + 1) % 3);
		}

		/// <summary
		/// </summary>
		public int DisplayCfg(CConfig.ConfigVolatile cfgv, CConfig.Hotkeys keys, CConfig.Colours cols, ref bool setup)
		{
			int code;
			string game = "";

			CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
			Console.Clear();
			DrawCfgMenu(cols, string.IsNullOrEmpty(CConfig.GetConfigString(CConfig.CFG_TXTCFGT)) ? Properties.Settings.Default.text_settings_title : CConfig.GetConfigString(CConfig.CFG_TXTCFGT));

			int nStartY = Console.CursorTop + CDock.INSTRUCT_CUSHION;
			int nStopY = CDock.INPUT_BOTTOM_CUSHION + CDock.INPUT_ITEM_CUSHION;

			List<string> options = new();
			Dictionary<string, OptionVal> optionsDict = new();
			IOrderedEnumerable<SettingsProperty> settings = Properties.Settings.Default.Properties.OfType<SettingsProperty>().OrderBy(s => s.Name);
			foreach (SettingsProperty setting in settings)
			{
				OptionVal value = new();
				options.Add(setting.Name);

				value.name = setting.Name;
				value.type = setting.PropertyType;
				if (setting.PropertyType == typeof(bool))
					value.bVal = (bool)CConfig.GetConfigBool(setting.Name);
				else if (setting.PropertyType == typeof(ushort))
					value.nVal = (ushort)CConfig.GetConfigNum(setting.Name);
				else if (setting.PropertyType == typeof(int))
					value.iVal = (int)CConfig.GetConfigInt(setting.Name);
				else //if (setting.PropertyType == typeof(string))
					value.strVal = CConfig.GetConfigString(setting.Name);
			}

			do
			{
				if (m_ConsoleState == ConsoleState.cState_Insert)
				{
					code = HandleInsertMenu(nStartY, nStopY, cfgv, cols, setup: true, browse: false, ref game, options.ToArray());
					if (code > -1)
						CDock.m_nCurrentSelection = code;
					CLogger.LogDebug("HandleInsertMenu:{0},{1}", code, CDock.m_nCurrentSelection);
				}
				else //if (m_ConsoleState == ConsoleState.cState_Navigate)
				{
					code = HandleNavigationMenu(nStartY, nStopY, cfgv, keys, cols, setup: true, browse: false, ref game, options.ToArray());
					CLogger.LogDebug("HandleNavigationMenu:{0},{1}", code, CDock.m_nCurrentSelection);
					if (options.Count < 2)
						CDock.m_nCurrentSelection = -1;
				}
			} while (!IsSelectionValid(CDock.m_nCurrentSelection, options.Count));

			if (CDock.m_nCurrentSelection == -1) // quit
			{
				code = (int)DockSelection.cSel_Exit;
			}
			else if (CDock.m_nCurrentSelection == 0) // select setting
			{
				string option = options[CDock.m_nCurrentSelection];
				string prompt = "Change Setting";
				try
				{
					OptionVal value = optionsDict[options[CDock.m_nCurrentSelection]];
					if (value.type == typeof(bool))
					{
						string newVal = CDock.InputPrompt(string.Format("{0} [{1}] >>> ", prompt, value.bVal), cols);
						if (!string.IsNullOrEmpty(newVal))
						{
							if (bool.TryParse(newVal, out bool newBool)) CConfig.SetConfigValue(option, newBool);
						}
					}
					else if (value.type == typeof(ushort))
					{
						string newVal = CDock.InputPrompt(string.Format("{0} [{1}] >>> ", prompt, value.nVal), cols);
						if (!string.IsNullOrEmpty(newVal))
						{
							if (ushort.TryParse(newVal, out ushort newNum)) CConfig.SetConfigValue(option, newNum);
						}
					}
					else if (value.type == typeof(int))
					{
						string newVal = CDock.InputPrompt(string.Format("{0} [{1}] >>> ", prompt, value.iVal), cols);
						if (!string.IsNullOrEmpty(newVal))
						{
							if (int.TryParse(newVal, out int newInt)) CConfig.SetConfigValue(option, newInt);
						}
					}
					else //if (value.type == typeof(string))
					{
						string newVal = CDock.InputPrompt(string.Format("{0} [{1}] >>> ", prompt, value.strVal), cols);
						if (!string.IsNullOrEmpty(newVal))
						{
							CConfig.SetConfigValue(option, newVal);
						}
					}
				}
				catch (Exception e)
                {
					CLogger.LogError(e);
                }
			}
			if (code == (int)DockSelection.cSel_Exit)
			{
				CDock.m_nSelectedPlatform = -1;
				setup = false;
				return (int)DockSelection.cSel_Redraw;
			}
			return code;
		}

		/// <summary
		/// </summary>
		public int DisplayFS(CConfig.ConfigVolatile cfgv, CConfig.Hotkeys keys, CConfig.Colours cols, ref bool browse, ref string path)
		{
			int code;
			string game = "";
			string mask = "*.*";

			CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
			Console.Clear();
			CDock.SetFgColour(cols.titleCC, cols.titleLtCC);
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - CDock.INPUT_BOTTOM_CUSHION);
			Console.WriteLine(mask + " in " + path);
			DrawFSMenu(cols, string.IsNullOrEmpty(CConfig.GetConfigString(CConfig.CFG_TXTFILET)) ? Properties.Settings.Default.text_browse_title : CConfig.GetConfigString(CConfig.CFG_TXTFILET));

			int nStartY = Console.CursorTop + CDock.INSTRUCT_CUSHION;
			int nStopY = CDock.INPUT_BOTTOM_CUSHION + CDock.INPUT_ITEM_CUSHION;

			List<string> options = new();
			List<string> dirs = new();
			List<string> files = new();
			string rootName = string.IsNullOrEmpty(CConfig.GetConfigString(CConfig.CFG_TXTROOT)) ? Properties.Settings.Default.text_root_folder : CConfig.GetConfigString(CConfig.CFG_TXTROOT);
			try
			{
				if (path.Equals(rootName))
				{
					dirs.AddRange(Directory.GetLogicalDrives());
					foreach (string dir in dirs)
					{
						options.Add($"<{dir}>");
					}
				}
				else
				{
					dirs = new List<string>()
					{
						string.IsNullOrEmpty(CConfig.GetConfigString(CConfig.CFG_TXTSELECT)) ? Properties.Settings.Default.text_select_folder : CConfig.GetConfigString(CConfig.CFG_TXTSELECT),
						string.IsNullOrEmpty(CConfig.GetConfigString(CConfig.CFG_TXTCREATE)) ? Properties.Settings.Default.text_create_folder : CConfig.GetConfigString(CConfig.CFG_TXTCREATE),
						".."
					};
					dirs.AddRange(Directory.GetDirectories(path));
					foreach (string dir in dirs)
					{
						options.Add(string.Format("<{0}>", Path.GetFileName(dir)));
					}
					files.AddRange(Directory.GetFiles(path, mask));
					foreach (string file in files)
					{
						options.Add(Path.GetFileName(file));
					}
				}
			}
            catch (Exception e)
            {
				CDock.SetFgColour(cols.errorCC, cols.errorLtCC);
				CLogger.LogError(e);
				Console.WriteLine("Error reading path!");
				if (path.Equals(Path.GetPathRoot(path)))
				{
					path = rootName;
				}
				else
					path = Path.GetDirectoryName(path);
				Thread.Sleep(2000);
				return (int)DockSelection.cSel_Back;
			}
			
            do
			{
				if (m_ConsoleState == ConsoleState.cState_Insert)
				{
					code = HandleInsertMenu(nStartY, nStopY, cfgv, cols, setup: false, browse: true, ref game, options.ToArray());
					if (code > -1)
						CDock.m_nCurrentSelection = code;
					CLogger.LogDebug("HandleInsertMenu:{0},{1}", code, CDock.m_nCurrentSelection);
				}
				else //if (m_ConsoleState == ConsoleState.cState_Navigate)
				{
					code = HandleNavigationMenu(nStartY, nStopY, cfgv, keys, cols, setup: false, browse: true, ref game, options.ToArray());
					CLogger.LogDebug("HandleNavigationMenu:{0},{1}", code, CDock.m_nCurrentSelection);
					if (options.Count < 2)
						CDock.m_nCurrentSelection = -1;
				}
			} while (!IsFSSelectionValid(CDock.m_nCurrentSelection, dirs.Count));

			if (path.Equals(rootName))
            {
				path = dirs[CDock.m_nCurrentSelection];
			}
			else
			{
				if (CDock.m_nCurrentSelection == -1) // quit
				{
					code = (int)DockSelection.cSel_Exit;
				}
				else if (CDock.m_nCurrentSelection == 0) // select folder
				{
					code = (int)DockSelection.cSel_Shortcut;
				}
				else if (code == (int)DockSelection.cSel_New ||
					CDock.m_nCurrentSelection == 1) // create folder
				{
					string dir = CDock.InputPrompt("Create folder name >>> ", cols);
					if (!string.IsNullOrEmpty(dir))
					{
						try
						{
							Directory.CreateDirectory(Path.Combine(path, dir));
						}
						catch (Exception e)
						{
							CDock.SetFgColour(cols.errorCC, cols.errorLtCC);
							CLogger.LogError(e);
							Console.WriteLine("Error creating folder!");
							Thread.Sleep(2000);
						}
						code = (int)DockSelection.cSel_Shortcut;
					}
				}
				else if (code == (int)DockSelection.cSel_Back ||
					CDock.m_nCurrentSelection == 2) // <..>
				{
					if (path.Equals(Path.GetPathRoot(path)))
						path = rootName;
					else
						path = Path.GetDirectoryName(path);
				}
				else //if (IsFSSelectionValid(CDock.m_nCurrentSelection, dirs.Count()))
					path = dirs[CDock.m_nCurrentSelection];
			}
			CDock.m_nCurrentSelection = 0;

			if (code == (int)DockSelection.cSel_Default ||
				code == (int)DockSelection.cSel_Back)
				return (int)DockSelection.cSel_Shortcut;
			if (code == (int)DockSelection.cSel_Exit)
			{
				CDock.m_nSelectedPlatform = -1;
				browse = false;
				return (int)DockSelection.cSel_Redraw;
			}
			return code;
		}
		
		/// <summary>
		/// Function overload
		/// Display either the insert or navigate menu, depending on the console state.
		/// Returns selection code and selection index
		/// </summary>
		/// <param name="options">String array representing the available options</param>
		/// <returns>Selection code</returns>
		public int DisplayMenu(CConfig.ConfigVolatile cfgv, CConfig.Hotkeys keys, CConfig.Colours cols, ref string game, params string[] options)
		{
			int code; //= -1;

			CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
				Console.Clear();

			DrawMenuTitle(cols);
			if (IsSelectionValid(CDock.m_nSelectedPlatform, options.Length)) DrawInstruct(cols, subMenu: true);
			else DrawInstruct(cols, subMenu: false);
			int nStartY = Console.CursorTop + CDock.INSTRUCT_CUSHION;
			int nStopY = CDock.INPUT_BOTTOM_CUSHION + CDock.INPUT_ITEM_CUSHION;

			do
			{
				if (m_ConsoleState == ConsoleState.cState_Insert)
				{
					code = HandleInsertMenu(nStartY, nStopY, cfgv, cols, setup: false, browse: false, ref game, options);
					CLogger.LogDebug("HandleInsertMenu:{0},{1}", code, CDock.m_nCurrentSelection);
				}
				else //if (m_ConsoleState == ConsoleState.cState_Navigate)
				{
					code = HandleNavigationMenu(nStartY, nStopY, cfgv, keys, cols, setup: false, browse: false, ref game, options);
					CLogger.LogDebug("HandleNavigationMenu:{0},{1}", code, CDock.m_nCurrentSelection);
				}
				if (code == (int)DockSelection.cSel_Exit ||
					code == (int)DockSelection.cSel_Default)
					break;
				if (options.Length == 0)
				{
					CDock.m_nCurrentSelection = -1;
					break;
				}
			} while (!IsSelectionValid(CDock.m_nCurrentSelection, options.Length));
			return code;
		}

		/// <summary>
		/// Function override
		/// Validate selection
		/// </summary>
		/// <param name="nSelection">Selection as integer</param>
		/// <param name="nItemCount">Count of the possible selections</param>
		/// <returns>True if valid, otherwise false</returns>
		public static bool IsSelectionValid(int nSelection, int nItemCount)
		{
			return (nSelection > -1 && nSelection < nItemCount);
		}

		/// <summary>
		/// Validate selection for filesystem menu
		/// </summary>
		/// <param name="nSelection">Selection as integer</param>
		/// <param name="nItemCount">Count of the possible selections</param>
		/// <returns>True if valid, otherwise false</returns>
		public static bool IsFSSelectionValid(int nSelection, int nItemCount)
		{
			return (nSelection > -1 && nSelection < nItemCount);
		}

		/// <summary>
		/// Function overload
		/// Selection handler in the browse state.
		/// Return selection code and selection index
		/// </summary>
		/// <param name="options">Array of available options</param>
		/// <returns>Selection code</returns>
		public int HandleNavigationMenu(int nStartY, int nStopY, CConfig.ConfigVolatile cfgv, CConfig.Hotkeys keys, CConfig.Colours cols, bool setup, bool browse, ref string game, params string[] options)
		{
			Console.CursorVisible = false;

			ConsoleKey key;
			int nLastSelection = 0;
			if (IsSelectionValid(CDock.m_nCurrentSelection, options.Length))
				nLastSelection = CDock.m_nCurrentSelection;
			else
				CDock.m_nCurrentSelection = 0;

			EnlargeWindow(nStartY, options);

			// Print the selections
			int itemsPerPage = m_nMaxItemsPerPage - (m_nOptionsPerLine * (nStartY + nStopY));
			if (itemsPerPage < 1 && (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD)))
				return (int)DockSelection.cSel_Fail;
			int nPage = 0;
			if (CDock.m_nCurrentSelection >= itemsPerPage)
				nPage = CDock.m_nCurrentSelection / itemsPerPage;
			int pages = options.Length / itemsPerPage + (options.Length % itemsPerPage == 0 ? 0 : 1);
			CLogger.LogDebug("total items:{0}, lines:{1}, items per pg:{2}, page:{3}/{4}", options.Length, m_nMaxItemsPerPage, itemsPerPage, nPage + 1, pages);

			if (!string.IsNullOrEmpty(game))
			{
				int maxTitles = (Math.Min(9, Console.WindowHeight - nStartY - CDock.INPUT_BOTTOM_CUSHION - CDock.INPUT_ITEM_CUSHION - 1));
				options = GetPlatformTitles(GamePlatform.All).ToArray();
				if (DoGameSearch(game, maxTitles, options, out int nMatches) && nMatches > 0)
				{
					CDock.m_nSelectedPlatform = (int)GamePlatform.Search;
					CDock.m_nSelectedGame = 0;
					nPage = 0;
					options = GetPlatformTitles(GamePlatform.Search).ToArray();
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
					{
						if (options.Length == 1) //nMatches == 1)
							return (int)DockSelection.cSel_Default;
						return (int)DockSelection.cSel_Exit;
					}
					
					DrawMenuTitle(cols);
				}
				else
				{
					//CDock.m_nCurrentSelection = 0;
					CDock.m_nSelectedPlatform = -1;
					CDock.SetFgColour(cols.errorCC, cols.errorLtCC);
					Console.WriteLine("{0}: {1}!", CGameData.GetDescription(GamePlatform.Search), CGameData.GetDescription(Match.NoMatches));
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
					{
						options = Array.Empty<string>();
						return (int)DockSelection.cSel_Exit;
					}

					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USEALL))
					{
						CDock.m_nSelectedPlatform = (int)GamePlatform.All;
						//DrawMenuTitle(cols);
					}
					else
						options = GetPlatformNames().ToArray();
				}
				game = "";
			}

			if (cfgv.imageSize > 0 && cfgv.imageBorder)
				CConsoleImage.ShowImageBorder(CDock.sizeImage, CDock.locImage, CDock.IMG_BORDER_X_CUSHION, CDock.IMG_BORDER_Y_CUSHION);

			if (!IsSelectionValid(CDock.m_nCurrentSelection, options.Length))
				CDock.m_nCurrentSelection = 0;

			if (m_MenuType == MenuType.cType_List)
				DrawListMenu(CDock.m_nCurrentSelection, nPage, nStartY, nStopY, cfgv, cols, options);
			
			else //if (m_MenuType == MenuType.cType_Grid)
				DrawGridMenu(CDock.m_nCurrentSelection, nPage, nStartY, nStopY, cfgv, cols, options);

			CDock.SetFgColour(cols.subCC, cols.subLtCC);
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE))
			{
				if (pages > 1 && nPage < pages - 1)
					Console.WriteLine("... ({0}/{1})", nPage + 1, pages);
				if (!(m_MenuType == MenuType.cType_List && (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONSIZE) > 0))
					DrawInfoBar(options[CDock.m_nCurrentSelection], cols);
			}

			do
			{
				// Track the current selection
				if (CDock.m_nCurrentSelection != nLastSelection && IsSelectionValid(nLastSelection, options.Length) &&
					IsSelectionValid(CDock.m_nCurrentSelection, options.Length))
					UpdateMenu(nLastSelection, nPage,
						cfgv.iconSize > 0 ? CDock.ICON_LEFT_CUSHION + cfgv.iconSize + CDock.ICON_RIGHT_CUSHION: 0,
						nStartY, itemsPerPage, cfgv, cols, options);

				if (OperatingSystem.IsWindows() && cfgv.imageSize > 0 && IsSelectionValid(CDock.m_nCurrentSelection, options.Length))
				{
					ConsoleColor imageColour = (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGRES) > 48 ? ConsoleColor.Black : (m_LightMode == LightMode.cColour_Light ? cols.bgLtCC : cols.bgCC);
					var t = Task.Run(() =>
					{
						if (OperatingSystem.IsWindows())
						{
							if (CDock.m_nSelectedPlatform > -1)
							{
								CGame selectedGame = GetPlatformGame((GamePlatform)CDock.m_nSelectedPlatform, CDock.m_nCurrentSelection);
								if (selectedGame != null)
									CConsoleImage.ShowImage(CDock.m_nCurrentSelection, selectedGame.Title, selectedGame.Icon, bPlatform: false, CDock.sizeImage, CDock.locImage, imageColour);
							}
							else
								CConsoleImage.ShowImage(CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection], options[CDock.m_nCurrentSelection], bPlatform: true, CDock.sizeImage, CDock.locImage, imageColour);
						}
					});
				}

				key = Console.ReadKey(true).Key;
				nLastSelection = CDock.m_nCurrentSelection;

				CLogger.LogDebug($"READKEY: {key}");
				if (key == keys.leftCK1 || key == keys.leftCK2)
				{
					if (CDock.m_nCurrentSelection > 0)
					{
						HandleSelectionLeft();
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
					}
				}
				else if (key == keys.rightCK1 || key == keys.rightCK2)
				{
					if (CDock.m_nCurrentSelection < options.Length - 1)
					{
						HandleSelectionRight(options.Length);
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
					}
				}
				else if (key == keys.upCK1 || key == keys.upCK2)
				{
					if (CDock.m_nCurrentSelection > 0)
					{
						HandleSelectionUp();
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && CDock.m_nCurrentSelection < itemsPerPage * nPage)
							return (int)DockSelection.cSel_Redraw;
					}
				}
				else if (key == keys.downCK1 || key == keys.downCK2)
				{
					if (CDock.m_nCurrentSelection < options.Length - 1)
					{
						HandleSelectionDown(options.Length);
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && CDock.m_nCurrentSelection >= itemsPerPage * (nPage + 1))
							return (int)DockSelection.cSel_Redraw;
					}
				}
				else if (key == keys.pageUpCK1 || key == keys.pageUpCK2)
				{
					if (CDock.m_nCurrentSelection > 0)
					{
						HandleSelectionPageUp(itemsPerPage, options.Length);
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && nPage > 0)
							return (int)DockSelection.cSel_Redraw;
					}
				}
				else if (key == keys.pageDownCK1 || key == keys.pageDownCK2)
				{
					if (CDock.m_nCurrentSelection < options.Length - 1)
					{
						HandleSelectionPageDown(itemsPerPage, options.Length);
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && nPage < pages)
							return (int)DockSelection.cSel_Redraw;
					}
				}
				else if (key == keys.downloadCK1 || key == keys.downloadCK2)
					return (int)DockSelection.cSel_downloadImage; // Download image
				else if (key == keys.tagsCK1 || key == keys.tagsCK2)
					return (int)DockSelection.cSel_Tags; // Edit tags
				else if (key == keys.ratingUpCK1 || key == keys.ratingUpCK2)
					return (int)DockSelection.cSel_raiseRating; // Raise rating
				else if (key == keys.ratingDownCK1 || key == keys.ratingDownCK2)
					return (int)DockSelection.cSel_lowerRating; // Lower rating
				else if (key == keys.launcherCK1 || key == keys.launcherCK2)
					return (int)DockSelection.cSel_Launcher; // Open launcher
				else if (key == keys.settingsCK1 || key == keys.settingsCK2)
					return (int)DockSelection.cSel_Settings; // Program settings
				else if (key == keys.searchCK1 || key == keys.searchCK2)
					return (int)DockSelection.cSel_Search; // Game search
				else if (key == keys.firstCK1 || key == keys.firstCK2)
				{
					if (CDock.m_nCurrentSelection > 0)
					{
						HandleSelectionFirst();
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && pages > 1)
							return (int)DockSelection.cSel_Redraw;
					}
				}
				else if (key == keys.lastCK1 || key == keys.lastCK2)
				{
					if (CDock.m_nCurrentSelection < options.Length - 1)
					{
						HandleSelectionLast(options.Length);
						CLogger.LogDebug("Selected {0}: [{1}]", CDock.m_nCurrentSelection, options[CDock.m_nCurrentSelection]);
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && pages > 1)
							return (int)DockSelection.cSel_Redraw;
					}
				}
				else if (key == keys.imageCK1 || key == keys.imageCK2)
					return (int)DockSelection.cSel_Image; // Image Mien (Enabled/Disabled)

				else if (key == keys.sortCK1 || key == keys.sortCK2)
					return (int)DockSelection.cSel_Sort; // Sort Method (Last Run/Frequency/Rating/Alphabetic)

				else if (key == keys.modeCK1 || key == keys.modeCK2)
					return (int)DockSelection.cSel_Colour; // Colour Mode (Dark/Light)

				else if (key == keys.viewCK1 || key == keys.viewCK2)
					return (int)DockSelection.cSel_View; // View Type (Grid/List)

				else if (key == keys.typeCK1 || key == keys.typeCK2)
					return (int)DockSelection.cSel_Input; // Input State (Arrow Navigation/Typing)

				else if (key == keys.shortcutCK1 || key == keys.shortcutCK2)
				{
					//if (CDock.m_nSelectedPlatform > -1)
					return (int)DockSelection.cSel_Shortcut; // Export shortcuts
				}
				else if (key == keys.uninstCK1 || key == keys.uninstCK2)
				{
					if (CDock.m_nSelectedPlatform > -1)
						return (int)DockSelection.cSel_Uninst; // Uninstall program
				}
				else if (key == keys.helpCK1 || key == keys.helpCK2)
					return (int)DockSelection.cSel_Help; // Print help

				else if (key == keys.cancelCK1 || key == keys.cancelCK2) // by default, cancelCK1 and quitCK1 are both Esc, so check for cancel before quit
				{
					if (setup || browse)
					{
						CDock.m_nCurrentSelection = -1;
						return (int)DockSelection.cSel_Back;
					}
					else if (key == keys.quitCK1)
					{
						if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT))
							return (int)DockSelection.cSel_Exit; // Exit program
					}
				}
				else if (key == keys.quitCK1 || key == keys.quitCK2)
				{
					if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT))
						return (int)DockSelection.cSel_Exit; // Exit program
				}
				else if (key == keys.backCK1 || key == keys.backCK2)
				{
					if (CDock.m_nSelectedPlatform > -1 && !((bool)CConfig.GetConfigBool(CConfig.CFG_USEALL)))
						return (int)DockSelection.cSel_Back; // Return to first menu
				}
				else if (key == keys.hideCK1 || key == keys.hideCK2)
				{
					if (CDock.m_nSelectedPlatform > -1)
						return (int)DockSelection.cSel_Hide; // Remove game from list
				}
				else if (key == keys.newCK1 || key == keys.newCK2)
				{
					return (int)DockSelection.cSel_New; // Add game to list (or create folder)
				}
				else if (key == keys.faveCK1 || key == keys.faveCK2)
				{
					if (CDock.m_nSelectedPlatform > -1)
						return (int)DockSelection.cSel_Fav; // Add/remove game from favourites
				}
				else if (key == keys.aliasCK1 || key == keys.aliasCK2)
				{
					if (CDock.m_nSelectedPlatform > -1)
						return (int)DockSelection.cSel_Alias; // Set alias for insert mode or command line parameter
				}
				else if (key == keys.scanCK1 || key == keys.scanCK2)
					return (int)DockSelection.cSel_Rescan; // Rescan the registry and the 'CustomGames' folder for new games

				else if (key == keys.allimgCK1 || key == keys.allimgCK2)
					return (int)DockSelection.cSel_downloadAll; // Rescan the registry and the 'CustomGames' folder for new games

			} while (key != keys.selectCK1 && key != keys.selectCK2);
			
			return (int)DockSelection.cSel_Default;
		}

		/// <summary>
		/// Function overload
		/// Selection handler in the 'insert' mode
		/// </summary>
		/// <param name="options">List of menu items</param>
		/// <returns>Index of the selected item from the options parameter</returns>
		public int HandleInsertMenu(int nStartY, int nStopY, CConfig.ConfigVolatile cfgv, CConfig.Colours cols, bool setup, bool browse, ref string game, params string[] options)
		{
			Console.CursorVisible = true;

			int nSelection = -1;
			bool bIsValidSelection = false;

			EnlargeWindow(nStartY, options);

			// Print the selections
			int itemsPerPage = m_nMaxItemsPerPage - (m_nOptionsPerLine * (nStartY + nStopY));
			if (itemsPerPage < 1 && (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD)))
				return (int)DockSelection.cSel_Fail;
			int nPage = 0;
			if (nSelection >= itemsPerPage)
				nPage = nSelection / itemsPerPage;
			int pages = options.Length / itemsPerPage + (options.Length % itemsPerPage == 0 ? 0 : 1);
			CLogger.LogDebug("total items:{0}, lines:{1}, items per pg:{2}, page:{3}/{4}", options.Length, m_nMaxItemsPerPage, itemsPerPage, nPage + 1, pages);

			if (!string.IsNullOrEmpty(game))
			{
				int maxTitles = (Math.Min(9, Console.WindowHeight - nStartY - CDock.INPUT_BOTTOM_CUSHION - CDock.INPUT_ITEM_CUSHION - 1));
				options = GetPlatformTitles(GamePlatform.All).ToArray();
				if (DoGameSearch(game, maxTitles, options, out int nMatches) && nMatches > 0)
				{
					CDock.m_nSelectedPlatform = (int)GamePlatform.Search;
					CDock.m_nSelectedGame = 0;
					nPage = 0;
					options = GetPlatformTitles(GamePlatform.Search).ToArray();
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
					{
						if (options.Length == 1) //nMatches == 1)
							return (int)DockSelection.cSel_Default;
						return (int)DockSelection.cSel_Exit;
					}

					DrawMenuTitle(cols);
				}
				else
				{
					nSelection = -1;
					CDock.m_nSelectedPlatform = -1;
					CDock.SetFgColour(cols.errorCC, cols.errorLtCC);
					Console.WriteLine("{0}: {1}!", CGameData.GetDescription(GamePlatform.Search), CGameData.GetDescription(Match.NoMatches));
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
					{
						options = Array.Empty<string>();
						return (int)DockSelection.cSel_Exit;
					}

					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USEALL))
					{
						CDock.m_nSelectedPlatform = (int)GamePlatform.All;
						//DrawMenuTitle(cols);
					}
					else
						options = GetPlatformNames().ToArray();
				}
				game = "";
			}

			if (cfgv.imageSize > 0 && cfgv.imageBorder)
				CConsoleImage.ShowImageBorder(CDock.sizeImage, CDock.locImage, CDock.IMG_BORDER_X_CUSHION, CDock.IMG_BORDER_Y_CUSHION);
			
			if (m_MenuType == MenuType.cType_List)
				DrawListMenu(nSelection, nPage, nStartY, nStopY, cfgv, cols, options);

			else //if (m_MenuType == MenuType.cType_Grid)
				DrawGridMenu(nSelection, nPage, nStartY, nStopY, cfgv, cols, options);

			CDock.SetFgColour(cols.subCC, cols.subLtCC);
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && pages > 1 && nPage < pages - 1)
				Console.WriteLine("... ({0}/{1})", nPage + 1, pages);

			do
			{
				string strInput = CDock.InputPrompt(">>> ", cols);

				if (strInput.Length < 1) // Empty string input are invalid
					continue;

				else if (strInput.Equals("/help", CDock.IGNORE_CASE) || strInput.Equals("/h", CDock.IGNORE_CASE) ||
					strInput.Equals("/?", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Help;

				else if (strInput.Equals("/scan", CDock.IGNORE_CASE) || strInput.Equals("/s", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Rescan;

				else if (strInput.Equals("/back", CDock.IGNORE_CASE) || strInput.Equals("/b", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Back;

				else if (strInput.Equals("/nav", CDock.IGNORE_CASE) || strInput.Equals("/n", CDock.IGNORE_CASE) ||
					strInput.Equals("/input", CDock.IGNORE_CASE) || strInput.Equals("/i", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Input;

				else if (strInput.Equals("/view", CDock.IGNORE_CASE) || strInput.Equals("/v", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_View;

				else if (strInput.Equals("/grid", CDock.IGNORE_CASE) || strInput.Equals("/g", CDock.IGNORE_CASE))
				{
					m_MenuType = (MenuType)MenuType.cType_Grid;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/list", CDock.IGNORE_CASE) || strInput.Equals("/l", CDock.IGNORE_CASE))
				{
					m_MenuType = (MenuType)MenuType.cType_List;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/icon", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Image;

				else if (strInput.Equals("/allicon", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_downloadAll;

				else if (strInput.Equals("/sort", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Sort;

				else if (strInput.Equals("/alpha", CDock.IGNORE_CASE))
				{
					m_SortMethod = (SortMethod)SortMethod.cSort_Alpha;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/date", CDock.IGNORE_CASE))
				{
					m_SortMethod = (SortMethod)SortMethod.cSort_Date;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/freq", CDock.IGNORE_CASE))
				{
					m_SortMethod = (SortMethod)SortMethod.cSort_Freq;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/rating", CDock.IGNORE_CASE))
				{
					m_SortMethod = (SortMethod)SortMethod.cSort_Rating;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/colour", CDock.IGNORE_CASE) || strInput.Equals("/color", CDock.IGNORE_CASE) ||
					strInput.Equals("/col", CDock.IGNORE_CASE) || strInput.Equals("/mode", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Colour;

				else if (strInput.Equals("/dark", CDock.IGNORE_CASE) || strInput.Equals("/dk", CDock.IGNORE_CASE))
				{
					m_LightMode = (LightMode)LightMode.cColour_Dark;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/light", CDock.IGNORE_CASE) || strInput.Equals("/li", CDock.IGNORE_CASE))
				{
					m_LightMode = (LightMode)LightMode.cColour_Light;
					return (int)DockSelection.cSel_Default;
				}
				else if (strInput.Equals("/exit", CDock.IGNORE_CASE) || strInput.Equals("/x", CDock.IGNORE_CASE) ||
					strInput.Equals("/quit", CDock.IGNORE_CASE) || strInput.Equals("/q", CDock.IGNORE_CASE))
					return (int)DockSelection.cSel_Exit;

				else
				{
					nSelection = Array.FindIndex(options, s => s.StartsWith(strInput, CDock.IGNORE_CASE));
					if (nSelection >= 0) bIsValidSelection = true;
				}

			} while (!bIsValidSelection);

			return nSelection;
		}

		public void DrawInfoBar(string selection, CConfig.Colours cols)
        {
			try
			{
				Console.SetCursorPosition(0, Console.WindowHeight - CDock.INPUT_BOTTOM_CUSHION);
				//CDock.ClearInputLine(cols);
				CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
				CDock.SetFgColour(cols.titleCC, cols.titleLtCC);
				string leftSide = CDock.SEPARATOR_SYMBOL.ToString();
				string rightSide = CDock.SEPARATOR_SYMBOL.ToString();
				if (CDock.m_nSelectedPlatform > -1)
				{
					string aliasString = "";
					string ratingString = "";
					CGame selectedGame = GetPlatformGame((GamePlatform)CDock.m_nSelectedPlatform, CDock.m_nCurrentSelection);

					if (selectedGame != null)
					{
						if (selectedGame.Alias.Length > 0)
							aliasString = string.Format(" {0} {1}", CDock.SEPARATOR_SYMBOL, selectedGame.Alias);

						if (selectedGame.Rating > 0)
						{
							if (string.IsNullOrEmpty(CDock.RATING_SYMBOL.ToString()))
								ratingString = string.Format(" {0} {1} *", CDock.SEPARATOR_SYMBOL, selectedGame.Rating);
							else
								//ratingString = string.Format(" {0} {1}", CDock.SEPARATOR_SYMBOL, new string(CDock.RATING_SYMBOL, selectedGame.Rating));
								ratingString = string.Format(" {0} {1} {2}", CDock.SEPARATOR_SYMBOL, selectedGame.Rating, CDock.RATING_SYMBOL);
						}

						leftSide = string.Format("{0} {1} {0}", CDock.SEPARATOR_SYMBOL, selectedGame.Title + aliasString + ratingString);
						/*
						if (CDock.m_nSelectedPlatform == (int)GamePlatform.All ||
							CDock.m_nSelectedPlatform == (int)GamePlatform.Favourites ||
							CDock.m_nSelectedPlatform == (int)GamePlatform.Hidden ||
							CDock.m_nSelectedPlatform == (int)GamePlatform.New ||
							CDock.m_nSelectedPlatform == (int)GamePlatform.NotInstalled ||
							CDock.m_nSelectedPlatform == (int)GamePlatform.Search ||
							CDock.m_nSelectedPlatform == (int)GamePlatform.Unknown)
						*/
						rightSide = string.Format("{0} {1} {0}", CDock.SEPARATOR_SYMBOL, selectedGame.PlatformString);
					}
				}
				else
				{
					selection = selection[..selection.IndexOf(": ")];
					leftSide = string.Format("{0} {1} {0}", CDock.SEPARATOR_SYMBOL, selection);
				}
				Console.WriteLine(leftSide + rightSide.PadLeft(Console.WindowWidth - leftSide.Length - 1));
				Thread.Sleep(50);  // image sometimes become written over otherwise
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
		}

        [SupportedOSPlatform("windows")]
        public void DrawIcon(int nY, int nItem, string strItem, ConsoleColor? iconColour)
        {
			var t = Task.Run(() =>
			{
				Thread.Sleep(5);  // icons sometimes become hidden otherwise
				Point iconPosition = new(CDock.ICON_LEFT_CUSHION, Console.WindowHeight / m_nMaxItemsPerPage + nY);
				if (CDock.m_nSelectedPlatform > -1)
				{
					CGame selectedGame = GetPlatformGame((GamePlatform)CDock.m_nSelectedPlatform, nItem);
					CConsoleImage.ShowImage(CDock.m_nCurrentSelection, selectedGame.Title, selectedGame.Icon, bPlatform: false, CDock.sizeIcon, iconPosition, iconColour);
				}
				else
					CConsoleImage.ShowImage(CDock.m_nCurrentSelection, strItem, strItem, bPlatform: true, CDock.sizeIcon, iconPosition, iconColour);
			});
		}

		/// <summary>
		/// Function overload
		/// Draw the list options in a grid layout.
		/// </summary>
		/// <param name="nCursorPosition">Current position, which will be printed in a highlight colour</param>
		/// <param name="nStartY">Offset from the top of the window</param>
		/// <param name="itemList">List of items to be displayed</param>
		public void DrawGridMenu(int nCursorPosition, int nPage, int nStartY, int nStopY, CConfig.ConfigVolatile cfgv, CConfig.Colours cols, params string[] itemList)
		{
			int itemsPerPage = m_nMaxItemsPerPage - (m_nOptionsPerLine * (nStartY + nStopY));
			int startIndex = 0;
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE))
				startIndex = nPage * itemsPerPage;

			for (int i = startIndex; i < itemList.Length; ++i)
			{
				if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && i >= itemsPerPage * (nPage + 1))
					break;
				try
				{
					Console.SetCursorPosition((i % m_nOptionsPerLine) * m_nSpacingPerLine,
						nStartY + ((i - (nPage * itemsPerPage)) / m_nOptionsPerLine));
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
				CLogger.LogDebug("Item {0} @ {1}x{2}x{3}", i, Console.CursorLeft, Console.CursorTop, nPage);
				if (i == nCursorPosition)
				{
					CDock.SetBgColour(cols.highbgCC, cols.highbgLtCC);
					CDock.SetFgColour(cols.highlightCC, cols.highlightLtCC);
				}
				else
				{
					if (itemList[i][0] == '*')
						CDock.SetFgColour(cols.uninstCC, cols.uninstLtCC);
					else
						CDock.SetFgColour(cols.entryCC, cols.entryLtCC);
				}
				Console.WriteLine(itemList[i][..Math.Min(itemList[i].Length, m_nSpacingPerLine - CDock.COLUMN_CUSHION)]);
				CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
				CDock.SetFgColour(cols.entryCC, cols.entryLtCC);
			}
		}

		/// <summary>
		/// Function overload
		/// Draw a list of options as a list
		/// </summary>
		/// <param name="nCursorPosition">Current position, which will be printed in a red colour</param>
		/// <param name="nStartY">Offset from the top of the window</param>
		/// <param name="itemList">List of items to be displayed</param>
		public void DrawListMenu(int nSelection, int nPage, int nStartY, int nStopY, CConfig.ConfigVolatile cfgv, CConfig.Colours cols, params string[] itemList)
		{
			int itemsPerPage = m_nMaxItemsPerPage - (nStartY + nStopY);
			bool showIcons = (cfgv.iconSize > 0);
			int startIndex = 0;
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE))
				startIndex = nPage * itemsPerPage;
			// for now, don't show small icons if paging is disabled and items exceed window size
			else
			{
				if (showIcons && itemList.Length > itemsPerPage)
					showIcons = false;
			}
			//ConsoleColor iconColour = (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONRES) > 48 ? ConsoleColor.Black : (m_LightMode == LightMode.cColour_Light ? cols.bgLtCC : cols.bgCC);
			if (cfgv.iconSize > 0)
				Thread.Sleep(50);  // icons sometimes become hidden otherwise
			for (int i = startIndex; i < itemList.Length; ++i)
			{
				if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && i >= itemsPerPage * (nPage + 1))
					break;
				try
				{
					Console.SetCursorPosition(cfgv.iconSize > 0 ? CDock.ICON_LEFT_CUSHION + cfgv.iconSize + CDock.ICON_RIGHT_CUSHION : 0,
						nStartY + i - (nPage * itemsPerPage));
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
				CLogger.LogDebug("Item {0} @ {1}x{2}x{3}", i, Console.CursorLeft, Console.CursorTop, nPage);
				if (i == nSelection)
				{
					CDock.SetBgColour(cols.highbgCC, cols.highbgLtCC);
					CDock.SetFgColour(cols.highlightCC, cols.highlightLtCC);
				}
				else
				{
					if (itemList[i][0] == '*')
						CDock.SetFgColour(cols.uninstCC, cols.uninstLtCC);
					else
						CDock.SetFgColour(cols.entryCC, cols.entryLtCC);
				}
				Console.WriteLine(itemList[i]);
				CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
				CDock.SetFgColour(cols.entryCC, cols.entryLtCC);
				if (OperatingSystem.IsWindows() && showIcons)
					DrawIcon(Console.CursorTop - 2, i, itemList[i], null);
			}
		}


		/// <summary>
		/// Handle selection calculation when 'up' is selected
		/// </summary>
		public void HandleSelectionUp()
		{
			if (m_MenuType == MenuType.cType_Grid && CDock.m_nCurrentSelection >= m_nOptionsPerLine)
				CDock.m_nCurrentSelection -= m_nOptionsPerLine;

			else if (m_MenuType == MenuType.cType_List && CDock.m_nCurrentSelection >= 1)
				CDock.m_nCurrentSelection--;
		}

		/// <summary>
		/// Handle selection calculation when 'down' is selected
		/// </summary>
		/// <param name="nOptionCount">Number of items in the list</param>
		public void HandleSelectionDown(int nOptionCount)
		{
			if (m_MenuType == MenuType.cType_Grid && CDock.m_nCurrentSelection + m_nOptionsPerLine < nOptionCount)
				CDock.m_nCurrentSelection += m_nOptionsPerLine;

			else if (m_MenuType == MenuType.cType_List && CDock.m_nCurrentSelection + 1 < nOptionCount)
				CDock.m_nCurrentSelection++;
		}

		/// <summary>
		/// Handle selection calculation when 'left' is selected
		/// </summary>
		public void HandleSelectionLeft()
		{
			if (CDock.m_nCurrentSelection > 0)
			{
				if (m_MenuType == MenuType.cType_List)
					CDock.m_nCurrentSelection--;

				else //if (m_MenuType == MenuType.cType_Grid)
				{
					if (CDock.m_nCurrentSelection % m_nOptionsPerLine > 0)
						CDock.m_nCurrentSelection--;
				}
			}
		}

		/// <summary>
		/// Handle selection calculation when 'right' is selected
		/// </summary>
		/// <param name="nOptionCount">Numbers of items in the list</param>
		public void HandleSelectionRight(int nOptionCount)
		{
			if (CDock.m_nCurrentSelection + 1 < nOptionCount)
			{
				if (m_MenuType == MenuType.cType_List)
					CDock.m_nCurrentSelection++;

				else //if (m_MenuType == MenuType.cType_Grid)
				{
					if (CDock.m_nCurrentSelection % m_nOptionsPerLine < m_nOptionsPerLine - 1)
						CDock.m_nCurrentSelection++;
				}
			}
		}

		/// <summary>
		/// Handle selection calculation when 'PgUp' is selected
		/// </summary>
		public void HandleSelectionPageUp(int nItemsPerPage, int nOptionCount)
		{
			//if (m_MenuType == MenuType.cType_Grid)
			//{
				if (CDock.m_nCurrentSelection >= m_nOptionsPerLine)
				{
					CDock.m_nCurrentSelection -= nItemsPerPage;
					while (CDock.m_nCurrentSelection < 0)
					{
						CDock.m_nCurrentSelection += m_nOptionsPerLine;
					}
					if (CDock.m_nCurrentSelection > nOptionCount - 1)
						CDock.m_nCurrentSelection = 0;
				}
				else if (CDock.m_nCurrentSelection >= 1)
					CDock.m_nCurrentSelection = 0;  // same as Home
			//} else CDock.m_nCurrentSelection = 0;  // same as Home
		}

		/// <summary>
		/// Handle selection calculation when 'PgDn' is selected
		/// </summary>
		/// <param name="nOptionCount">Number of items in the list</param>
		public void HandleSelectionPageDown(int nItemsPerPage, int nOptionCount)
		{
			//if (m_MenuType == MenuType.cType_Grid)
			//{
				if (CDock.m_nCurrentSelection + m_nOptionsPerLine < nOptionCount)
				{
					CDock.m_nCurrentSelection += nItemsPerPage;
					while (CDock.m_nCurrentSelection > nOptionCount - 1)
					{
						CDock.m_nCurrentSelection -= m_nOptionsPerLine;
					}
					if (CDock.m_nCurrentSelection < 0)
						CDock.m_nCurrentSelection = 0;
				}
				else if (CDock.m_nCurrentSelection + 1 < nOptionCount)
					CDock.m_nCurrentSelection = nOptionCount - 1;  // same as End
			//} else CDock.m_nCurrentSelection = nOptionCount - 1;  // same as End
		}

		/// <summary>
		/// Handle selection calculation when 'Home' is selected
		/// </summary>
		public void HandleSelectionFirst()
		{
			CDock.m_nCurrentSelection = 0;
		}

		/// <summary>
		/// Handle selection calculation when 'End' is selected
		/// </summary>
		/// <param name="nOptionCount">Number of items in the list</param>
		public void HandleSelectionLast(int nOptionCount)
		{
			CDock.m_nCurrentSelection = nOptionCount - 1;
		}

		/// <summary>
		/// Function overload
		/// Update the printed list of menu items by re-colouring only the changed items on the list
		/// </summary>
		/// <param name="nPreviousSelection">Index of the previously highlighted item</param>
		/// <param name="nStartY">Starting Y position (places from top)</param>
		/// <param name="strPreviousOption">String value of the previously selected option</param>
		/// <param name="strCurrentOption">String value of the currently selected option</param>
		public void UpdateMenu(int nPreviousSelection, int nPage, int nListStartX, int nStartY, int nItemsPerPage, CConfig.ConfigVolatile cfgv, CConfig.Colours cols, params string[] options)
		{
			string strPreviousOption = options[nPreviousSelection];
			string strCurrentOption = options[CDock.m_nCurrentSelection];
			try
			{
				if (m_MenuType == MenuType.cType_List)
					Console.SetCursorPosition(
						nListStartX,
						nStartY + CDock.m_nCurrentSelection - (nPage * nItemsPerPage));

				else //if (m_MenuType == MenuType.cType_Grid)
					Console.SetCursorPosition(
						(CDock.m_nCurrentSelection % m_nOptionsPerLine) * m_nSpacingPerLine,
						nStartY + ((CDock.m_nCurrentSelection - (nPage * nItemsPerPage)) / m_nOptionsPerLine));
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}

			CDock.SetBgColour(cols.highbgCC, cols.highbgLtCC);
			CDock.SetFgColour(cols.highlightCC, cols.highlightLtCC);
			if (m_MenuType == MenuType.cType_List)
				Console.Write(strCurrentOption);
			else if (m_ConsoleState == ConsoleState.cState_Navigate)  // if (m_MenuType == MenuType.cType_Grid &&
				Console.Write(strCurrentOption[..Math.Min(strCurrentOption.Length, m_nSpacingPerLine - CDock.COLUMN_CUSHION)]);
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && !(m_MenuType == MenuType.cType_List && (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONSIZE) > 0))
				DrawInfoBar(strCurrentOption, cols);

			try
			{
				if (m_MenuType == MenuType.cType_List)
					Console.SetCursorPosition(
						nListStartX,
						nStartY + nPreviousSelection - (nPage * nItemsPerPage));

				else //if (m_MenuType == MenuType.cType_Grid)
					Console.SetCursorPosition(
						(nPreviousSelection % m_nOptionsPerLine) * m_nSpacingPerLine,
						nStartY + ((nPreviousSelection - (nPage * nItemsPerPage)) / m_nOptionsPerLine));

				CDock.SetBgColour(cols.bgCC, cols.bgLtCC);
				if (strPreviousOption[0] == '*')
					CDock.SetFgColour(cols.uninstCC, cols.uninstLtCC);
				else
					CDock.SetFgColour(cols.entryCC, cols.entryLtCC);
				if (m_MenuType == MenuType.cType_List)
				{
					Console.Write(strPreviousOption);

					// Redraw all icons below current selection
					if (OperatingSystem.IsWindows() && cfgv.iconSize > 0)
					{
						// for now, don't show small icons if paging is disabled and items exceed window size
						if (!((bool)CConfig.GetConfigBool(CConfig.CFG_NOPAGE) && options.Length > nItemsPerPage))
						{
							Thread.Sleep(50);  // icons sometimes become hidden otherwise
							int maxItems = Math.Min(nItemsPerPage * (nPage + 1), options.Length);
							//ConsoleColor iconColour = (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONRES) > 48 ? ConsoleColor.Black : (m_LightMode == LightMode.cColour_Light ? cols.bgLtCC : cols.bgCC);
							for (int i = CDock.m_nCurrentSelection; i < maxItems; ++i)
							{
								DrawIcon(nStartY + i - (nPage * nItemsPerPage) - 1, i, options[i], null);
							}
						}
					}
				}
				else
					Console.Write(strPreviousOption[..Math.Min(strPreviousOption.Length, m_nSpacingPerLine - CDock.COLUMN_CUSHION)]);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}

			CDock.SetFgColour(cols.entryCC, cols.entryLtCC);
		}

		/// <summary>
		/// Search for a game by title or alias
		/// </summary>
		public static List<string> GetPlatformNames()
		{
			List<string> platformList = new();

			foreach (var platformPair in GetPlatforms())
			{
				if (platformPair.Value > 0)
					platformList.Add(platformPair.Key + ": " + platformPair.Value);
			}
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_NOCFG)) platformList.Add(string.IsNullOrEmpty(CConfig.GetConfigString(CConfig.CFG_TXTCFGT)) ? Properties.Settings.Default.text_settings_title : CConfig.GetConfigString(CConfig.CFG_TXTCFGT));
			return platformList;
		}

		/// <summary>
		/// Create shortcut
		/// </summary>
		[SupportedOSPlatform("windows")]
		public static void MakeShortcut(string title, string path, string icon, string location)
        {
			try
			{
				IShellLink link = (IShellLink)new ShellLink();

				// setup shortcut information
				if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(path))
					return;
				link.SetDescription(title);
				link.SetPath(path);
				if (!string.IsNullOrEmpty(icon))
					link.SetIconLocation(icon, 0);

				// save it
				IPersistFile file = (IPersistFile)link;
				if (Directory.Exists(location))
					file.Save(Path.Combine(location, title + ".LNK"), fRemember: false);
			}
            catch (Exception e)
			{
				CLogger.LogError(e);
			}
		}

		/// <summary>
		/// Search for a game by title or alias
		/// </summary>
		public static bool DoGameSearch(string strInput, int maxTitles, string[] options, out int nMatches)
        {
			Dictionary<string, int> matches = new();
			List<CMatch> searchResults = new();
			bool bValid = false;
			bool consoleOutput = (bool)CConfig.GetConfigBool(CConfig.CFG_USECMD);
			/*
			nSelection = Array.FindIndex(options, s => s.Equals(strInput, CDock.IGNORE_CASE));
			if (nSelection >= 0)
			{
				CLogger.LogInfo("{0}: {1}! [{2}]", GetDescription(GamePlatform.Search), GetDescription(Match.ExactTitle), nSelection);
				nMatches = 1;
				nSelection = 0;
				bValid = true;
			}
			else
			{
			*/
				matches = FindMatchingTitles(strInput, maxTitles);
				nMatches = matches.Count;
				if (nMatches > 0)
				{
					if (consoleOutput) Console.WriteLine("{0}: {1} matches found!", CGameData.GetDescription(GamePlatform.Search), nMatches);
					CLogger.LogInfo("{0}: {1} matches found!", CGameData.GetDescription(GamePlatform.Search), nMatches);
					int i = 0;
					foreach (var match in matches.OrderByDescending(j => j.Value))
					{
						CLogger.LogInfo("- [{0}%] {1}. {2}", match.Value, i, match.Key);
						if (i == 0 && match.Value >= (int)Match.ExactAlias)
						{
							CDock.m_nCurrentSelection = Array.FindIndex(options, s => s.Equals(match.Key, CDock.IGNORE_CASE));
							if (consoleOutput)
							{
								if (match.Value == (int)Match.ExactTitle)
									Console.WriteLine("- {0}: {1}", CGameData.GetDescription(Match.ExactTitle), match.Key);
								else
									Console.WriteLine("- {0}: {1}", CGameData.GetDescription(Match.ExactAlias), match.Key);
								break;
							}
						}

						if (consoleOutput)
						{
							Console.WriteLine(" {0}. {1}", i + 1, match.Key);
							searchResults.Add(new CMatch(match.Key, i + 1, match.Value));
						}

						i++;
					}
					if (nMatches == 1) CDock.m_nCurrentSelection = 0;
					bValid = true;
					if (consoleOutput) { CJsonWrapper.ExportSearch(searchResults); }
				}
				else
				{
					CLogger.LogInfo("{0}: {1}!", CGameData.GetDescription(GamePlatform.Search), CGameData.GetDescription(Match.NoMatches));
				}
			//}

			return bValid;
		}

		/// <summary>
		/// Increase window size
		/// </summary>
		public static void EnlargeWindow(int nStartY, string[] options)
        {
			if (OperatingSystem.IsWindows() && ((bool)CConfig.GetConfigBool(CConfig.CFG_USESIZE)))
			{
				// If the list items won't fit in the current window, resize to fit
				int numLines = (options.Length / m_nOptionsPerLine) + nStartY + CDock.INPUT_BOTTOM_CUSHION;
				try
				{
					int wh = Console.WindowHeight;
					if (numLines > wh)
					{
						Console.WindowHeight = Math.Min(numLines, Console.LargestWindowHeight);
						m_nMaxItemsPerPage = Math.Max(m_nOptionsPerLine, wh * m_nOptionsPerLine);
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}
		}

		/// <summary>
		/// Construct key output
		/// </summary>
		public static string OutputKeys(string key1, string key2, string bracketL, string bracketR, string separator, string na, int padding)
		{
			string output;

			if (string.IsNullOrEmpty(key1) || key1.Equals("NoName"))
			{
				if (string.IsNullOrEmpty(key2) || key2.Equals("NoName"))
					//($"{i,-4}{myArray[i]}")
					output = na;
				else
					output = bracketL + key2 + bracketR;
			}
			else
			{
				output = bracketL + key1 + bracketR;
				if (!string.IsNullOrEmpty(key2) && !(key2.Equals("NoName")))
				{
					output = output.PadRight(padding) + separator + bracketL + key2 + bracketR;
				}
			}

			return output;
		}

		/// <summary>
		/// Write menu title
		/// </summary>
		public static void DrawMenuTitle(CConfig.Colours cols)
		{
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
			{
				CDock.SetFgColour(cols.titleCC, cols.titleLtCC);
				Console.SetCursorPosition(0, 0);
				if (CDock.m_nSelectedPlatform > -1)
				{
					Console.WriteLine(GetPlatformString(CDock.m_nSelectedPlatform));
					Console.WriteLine(new string('-', GetPlatformString(CDock.m_nSelectedPlatform).Length));
				}
				else
				{
					Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTMAINT));
					Console.WriteLine(new string('-', CConfig.GetConfigString(CConfig.CFG_TXTMAINT).Length));
				}
			}
		}

		/// <summary>
		/// Write filesystem instructions
		/// </summary>
		public static void DrawCfgMenu(CConfig.Colours cols, string title)
		{
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
			{
				CDock.SetFgColour(cols.titleCC, cols.titleLtCC);
				Console.SetCursorPosition(0, 0);
				Console.WriteLine(title);
				Console.WriteLine(new String('-', title.Length));

				CDock.SetFgColour(cols.subCC, cols.subLtCC);
				//  0|-------|---------|---------|---------|40
				Console.WriteLine(
					" Use {0}/{1} + {2} to select a setting;\n" +
					" Use {3}/{4} or enter string to change.",
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYLT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
				//  0|-------|---------|---------|---------|40
				Console.WriteLine(
					" Press {0} to return to previous menu.",
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK2)), string.Empty, string.Empty, " or ", "N/A", 0));
			}
		}

		/// <summary>
		/// Write filesystem instructions
		/// </summary>
		public static void DrawFSMenu(CConfig.Colours cols, string title)
        {
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
			{
				CDock.SetFgColour(cols.titleCC, cols.titleLtCC);
				Console.SetCursorPosition(0, 0);
				Console.WriteLine(title);
				Console.WriteLine(new String('-', title.Length));

				CDock.SetFgColour(cols.subCC, cols.subLtCC);
				if (m_MenuType == (MenuType)MenuType.cType_Grid)
				{
					//	0|-------|---------|---------|---------|40
					Console.WriteLine(
						" Use {0}/{1}/{2}/{3} + {4} to select a folder.",
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYLT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
				}
				else
				{
					Console.WriteLine(" Use {0}/{1} + {2} to select a folder.",
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
						OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
				}
				//  0|-------|---------|---------|---------|40
				Console.WriteLine(
					" Press {0} to cancel selection;\n" +
					" Press {1} to go to the previous folder;\n" +
					" Press {2} to create a new folder.",
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYESC1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYESC2)), string.Empty, string.Empty, " or ", "N/A", 0),
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK2)), string.Empty, string.Empty, " or ", "N/A", 0),
					OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYNEW1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYNEW2)), string.Empty, string.Empty, " or ", "N/A", 0));
			}
		}

		/// <summary>
		/// Write menu instructions
		/// </summary>
		public static void DrawInstruct(CConfig.Colours cols, bool subMenu)
		{
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USECMD))
			{
				// MESSAGE 1
				//CDock.SetFgColour(cols.titleCC, cols.titleLtCC);
				CDock.SetFgColour(cols.subCC, cols.subLtCC);
				if (m_ConsoleState == ConsoleState.cState_Insert)
				{
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USETEXT))
					{
						if (subMenu)
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTISUB1));
						else
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTIMAIN1));
					}
					else
					{
						if (subMenu)
							Console.WriteLine(" Type title of a game.");
						else
							Console.WriteLine(" Type name of a platform.");
					}
				}
				else // Navigate
				{
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USETEXT))
					{
						if (subMenu)
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTNSUB1));
						else
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTNMAIN1));
					}
					else
					{
						if (subMenu)
						{
							if (m_MenuType == (MenuType)MenuType.cType_Grid)
							{
								Console.WriteLine(" Use {0}/{1}/{2}/{3} + {4} to select a game.",
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYLT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
							}
							else
							{
								Console.WriteLine(" Use {0}/{1} + {2} to select a game.",
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
							}
						}
						else  // main menu
						{
							if (m_MenuType == (MenuType)MenuType.cType_Grid)
							{
								Console.WriteLine(" Use {0}/{1}/{2}/{3} + {4} to select platform.",
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYLT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYRT1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
									OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
							}
							else Console.WriteLine(" Use {0}/{1} + {2} to select platform.",
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYUP1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYDN1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0),
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSEL1)), string.Empty, string.Empty, string.Empty, string.Empty, "NA", 0));
						}
					}
				}

				// MESSAGE 2
				//CDock.SetFgColour(cols.subCC, cols.subLtCC);
				if (m_ConsoleState == ConsoleState.cState_Insert)
				{
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USETEXT))
					{
						if (subMenu)
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTISUB2));
						else
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTIMAIN2));
					}
					else
					{
						if (subMenu)
						{
							Console.WriteLine(((bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT) ? string.Empty :
								" Enter \'/exit\' to quit;\n") +
								" Enter \'/back\' to return to previous.");
						}
						else  // main menu
						{
						Console.WriteLine(((bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT) ? string.Empty :
							" Enter \'/exit\' to quit;\n") +
							" Enter \'/help\' for more commands.");
						}
					}
				}
				else // Navigate
				{
					if ((bool)CConfig.GetConfigBool(CConfig.CFG_USETEXT))
					{
						if (subMenu)
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTNSUB2));
						else
							Console.WriteLine(CConfig.GetConfigString(CConfig.CFG_TXTNMAIN2));
					}
					else
					{
						if (subMenu)
						{
							//  0|-------|---------|---------|---------|40
							Console.WriteLine(((bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT) ? string.Empty :
								" Press {0} to exit;\n") +
								" Press {1} to return to main;\n" +
								" Press {2} to toggle favourites.",
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYQUIT1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYQUIT2)), string.Empty, string.Empty, " or ", "N/A", 0),
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYBACK2)), string.Empty, string.Empty, " or ", "N/A", 0),
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYFAVE1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYFAVE2)), string.Empty, string.Empty, " or ", "N/A", 0));
						}
						else  // main menu
						{
							//  0|-------|---------|---------|---------|40
							Console.WriteLine(((bool)CConfig.GetConfigBool(CConfig.CFG_NOQUIT) ? string.Empty :
								" Press {0} to exit;\n") +
								" Press {1} to rescan games;\n" +
								" Press {2} for help.",
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYQUIT1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYQUIT2)), string.Empty, string.Empty, " or ", "N/A", 0),
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSCAN1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYSCAN2)), string.Empty, string.Empty, " or ", "N/A", 0),
								OutputKeys(CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHELP1)), CConfig.ShortenKeyName(CConfig.GetConfigString(CConfig.CFG_KEYHELP2)), string.Empty, string.Empty, " or ", "N/A", 0));
						}
					}
				}
			}
		}
	}
}