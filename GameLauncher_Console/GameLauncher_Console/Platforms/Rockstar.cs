using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CRegScanner;

namespace GameLauncher_Console
{
	// Rockstar Games Launcher
	// [installed games only]
	public class PlatformRockstar : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Rockstar;
		public const string PROTOCOL			= "rockstar://";
		private const string ROCKSTAR_LAUNCHER	= "Rockstar Games Launcher";
		private const string ROCKSTAR_SOCIAL	= "Rockstar Games Social Club";
		private const string ROCKSTAR_FOLDER	= "InstallFolder";
		private const string ROCKSTAR_REG		= @"SOFTWARE\Rockstar Games\Launcher"; // HKLM32
		private const string ROCKSTAR_UNINST	= "Launcher.exe";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
				CDock.StartShellExecute(PROTOCOL);
			else
				Process.Start(PROTOCOL);
		}

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		public static int InstallGame(CGame game)
		{
			//CDock.DeleteCustomImage(game.Title, false);
			Launch();
			return -1;
		}

		public static void StartGame(CGame game)
		{
			CLogger.LogInfo($"Launch: {game.Launch}");
			if (OperatingSystem.IsWindows())
				CDock.StartShellExecute(game.Launch);
			else
				Process.Start(game.Launch);
		}

		[SupportedOSPlatform("windows")]
		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
			List<RegistryKey> keyList;
			string strPlatform = GetPlatformString(ENUM);
			string launcherPath = "";

			using (RegistryKey launcherKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, 
				RegistryView.Registry32).OpenSubKey(ROCKSTAR_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				if (launcherKey == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}
				launcherPath = Path.Combine(GetRegStrVal(launcherKey, ROCKSTAR_FOLDER), ROCKSTAR_UNINST);
			}

			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				keyList = FindGameKeys(key, launcherPath, GAME_UNINSTALL_STRING, new string[] { ROCKSTAR_LAUNCHER, ROCKSTAR_SOCIAL });

				CLogger.LogInfo("{0} {1} games found", keyList.Count, _name.ToUpper());
				foreach (var data in keyList)
				{
					string strID = "";
					string strTitle = "";
					string strLaunch = "";
					string strUninstall = "";
					string strAlias = "";

					try
					{
						strTitle = GetRegStrVal(data, GAME_DISPLAY_NAME);
						CLogger.LogDebug($"- {strTitle}");
						strLaunch = GetRegStrVal(data, GAME_DISPLAY_ICON).Trim(new char[] { ' ', '"' });
						strUninstall = GetRegStrVal(data, GAME_UNINSTALL_STRING);
						strID = strUninstall[(strUninstall.IndexOf(" -uninstall=") + 12)..];
						strAlias = GetAlias(Path.GetFileNameWithoutExtension(strLaunch.Trim(new char[] { ' ', '\'', '"' })));
						if (strAlias.Length > strTitle.Length)
							strAlias = GetAlias(strTitle);
						if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
							strAlias = "";
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}
					if (!(string.IsNullOrEmpty(strLaunch)))
						gameDataList.Add(
							new ImportGameData(strID, strTitle, strLaunch, strLaunch, strUninstall, strAlias, true, strPlatform));
				}
			}
			CLogger.LogDebug("------------------------");
		}

        public static string GetIconUrl(CGame game)
        {
            return GetIconUrl(GetGameID(game.ID), game.Title);
        }

        public static string GetIconUrl(string id, string title)
        {
            if (string.IsNullOrEmpty(title))
                return "";

			id = id.ToLower();
			if (id.EndsWith("_pc"))
				id = id[..id.LastIndexOf("_pc")];

            if (id.Equals("gtaiii"))
				id = "gta3";
            else if (id.Equals("gta5"))
				id = "gtav";
			else if (id.Equals("lanoire"))
				id = "lan";

            if (id.Equals("gta3") || id.Equals("gtasa") || id.Equals("gtavc"))
                id += "unreal";

            //id should be (as of 2022/12/15) one of: "bully", "gta3unreal", "gtavcunreal", "gtasaunreal", "gtaiv", "gtav", "lan", "lanvr", "mp3", "rdr2"
            string iconUrl = string.Format("https://s.rsg.sc/sc/images/react/games/boxart/{0}.jpg", id);

            return iconUrl;
        }

        public static string GetGameID(string key) => key;
	}
}