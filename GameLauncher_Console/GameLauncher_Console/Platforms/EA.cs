using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.EADesktop;
using GameCollector.StoreHandlers.EADesktop.Crypto.Windows;
using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using FileSystem = NexusMods.Paths.FileSystem;
using GameFinder.Common;

namespace GameLauncher_Console
{
	// EA (formerly Origin)
	// [installed games + owned games; accurate titles only if login is provided]
	public class PlatformEA : IPlatform
	{
		public const GamePlatform ENUM = GamePlatform.EA;
		public const string PROTOCOL = "origin2://"; //"eadm://" and "ealink://" added after move to EA branding, but "origin://" or "origin2://" still seem to be the correct ones
		public const string LAUNCH = PROTOCOL + "library/open";
		public const string START_GAME = PROTOCOL + "game/launch?offerIds=";
		public const string EA_REG = "EA Desktop";
		private const string EA_DB = @"EA Desktop\530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e\IS"; // ProgramData
		private const string EA_KEY_PREFIX = "allUsersGenericIdIS";

		private const string EA_LANGDEF = "en_US";
		private const string ORIGIN_CTRYDEF = "US";
		private const string ORIGIN_CONTENT = @"Origin\LocalContent"; // ProgramData
		private const string ORIGIN_PATH = "dipinstallpath=";

		private const string WMI_CLASS_MOBO = "Win32_BaseBoard";
		private const string WMI_CLASS_BIOS = "Win32_BIOS";
		private const string WMI_CLASS_VID = "Win32_VideoController";
		private const string WMI_CLASS_PROC = "Win32_Processor";
		private const string WMI_PROP_MFG = "Manufacturer";
		private const string WMI_PROP_SERIAL = "SerialNumber";
		private const string WMI_PROP_PNPID = "PNPDeviceID";
		private const string WMI_PROP_NAME = "Name";
		private const string WMI_PROP_PROCID = "ProcessorID";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);
		private static readonly string _hwfile = string.Format("{0}_hwinfo.txt", _name);
		private readonly static byte[] _ea_iv = [0x84, 0xef, 0xc4, 0xb8, 0x36, 0x11, 0x9c, 0x20, 0x41, 0x93, 0x98, 0xc3, 0xf3, 0xf2, 0xbc, 0xef,];

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
				_ = CDock.StartShellExecute(LAUNCH);
			else
				_ = Process.Start(LAUNCH);
		}

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		public static int InstallGame(CGame game)
		{
			CDock.DeleteCustomImage(game.Title, justBackups: false);
			Launch();
			return 1;
		}

		public static void StartGame(CGame game)
		{
			CLogger.LogInfo($"Launch: {game.Launch}");
			if (OperatingSystem.IsWindows())
				_ = CDock.StartShellExecute(game.Launch);
			else
				_ = Process.Start(game.Launch);
		}

		[SupportedOSPlatform("windows")]
		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
		{
			string strPlatform = GetPlatformString(ENUM);

			EADesktopHandler handler = new(FileSystem.Shared, WindowsRegistry.Shared, new HardwareInfoProvider());
			foreach (var game in handler.FindAllGames(settings))
			{
				if (game.IsT0)
				{
					CLogger.LogDebug("* " + game.AsT0.GameName);
					gameDataList.Add(new ImportGameData(strPlatform, game.AsT0));
				}
				else
					CLogger.LogWarn(game.AsT1.Message);
			}

			CLogger.LogDebug("----------------------");
		}

		public static string GetIconUrl(CGame game)
		{
			return "";
		}

		/// <summary>
		/// Scan the key name and extract the Origin game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Origin game ID as string</returns>
		public static string GetGameID(string key)
		{
			if (key.EndsWith(".mfst"))
			{
				return Path.GetFileNameWithoutExtension(key);
			}
			return key;
		}
	}
}