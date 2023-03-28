using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Xml;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CGameFinder;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// Wargaming.net Game Center
	// [installed games only]
	public class PlatformWargaming : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Wargaming;
		public const string PROTOCOL			= "wgc://";
		private const string WARGAMING_UNREG	= "Wargaming.net Game Center"; // HKCU64 Uninstall
		private const string WARGAMING_DATA		= @"Wargaming.net\GameCenter\apps"; // ProgramData

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
			Dictionary<string, string> installDict = new();
			List<RegistryKey> keyList;
			string strPlatform = GetPlatformString(ENUM);
			/*
			string launcherPath = "";

			using (RegistryKey launcherKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, 
				RegistryView.Registry64).OpenSubKey(Path.Combine(UNINSTALL_REG, WARGAMING_UNREG), RegistryKeyPermissionCheck.ReadSubTree)) // HKCU64
			{
				if (launcherKey == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}
				launcherPath = GetRegStrVal(launcherKey, GAME_DISPLAY_ICON);
				int pathIndex = launcherPath.IndexOf(",");
				if (pathIndex > -1)
					launcherPath = launcherPath.Substring(0, pathIndex);
			}
			*/
			string dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), WARGAMING_DATA);

			try
			{
				if (Directory.Exists(dataPath))
				{
					foreach (var dir in Directory.EnumerateDirectories(dataPath))
					{
						foreach (var file in Directory.EnumerateFiles(dir))
                        {
							foreach (string line in File.ReadLines(file))
							{
								installDict.Add(Path.GetFileName(dir).ToUpper(), line.Trim());
								break;
							}
							break;
						}
					}
				}
			}
			catch (Exception e)
            {
				CLogger.LogError(e);
            }

			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
                RegistryView.Registry64).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKCU64
			{
				keyList = FindGameKeys(key, "Wargaming.net", "Publisher", new string[] { WARGAMING_UNREG });

				CLogger.LogInfo("{0} {1} games found", keyList.Count, _name.ToUpper());
				foreach (var data in keyList)
				{
					string strID = "";
					string strTitle = "";
					string strLaunch = "";
					string strIconPath = "";
					string strUninstall = "";
					string strAlias = "";

					try
					{
						strID = Path.GetFileName(data.Name).ToUpper();
						strTitle = GetRegStrVal(data, GAME_DISPLAY_NAME);
						CLogger.LogDebug($"- {strTitle}");
						string installPath = GetRegStrVal(data, GAME_INSTALL_LOCATION);
						installDict.Remove(strID);
						//installDict.Remove(installPath);
						strLaunch = FindGameBinaryFile(installPath, strTitle);
						strIconPath = GetRegStrVal(data, GAME_DISPLAY_ICON);
						int iconIndex = strIconPath.IndexOf(',');
						if (iconIndex > -1)
							strIconPath = strIconPath.Substring(0, iconIndex);
						strUninstall = GetRegStrVal(data, GAME_UNINSTALL_STRING);
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
							new ImportGameData(strID, strTitle, strLaunch, strIconPath, strUninstall, strAlias, true, strPlatform));
				}
			}

			foreach (var kv in installDict)
            {
				string path = kv.Value;
				string strID = kv.Key;
				string strTitle = "";
				string strLaunch = "";
				string strIconPath = "";
				string strAlias = "";

				try
				{
					string xmlPath = Path.Combine(path, "game_metadata", "metadata.xml");
					if (File.Exists(xmlPath))
					{
						XmlDocument doc = new();
						doc.Load(xmlPath);
						foreach (XmlNode exeNode in 
							doc.SelectSingleNode("/protocol/predefined_section/executables").ChildNodes)
						{
							XmlAttribute arch = exeNode.Attributes["arch"];
							if (arch != null && arch.Value.Equals("x64", CDock.IGNORE_CASE))
								strLaunch = Path.Combine(path, exeNode.InnerText);
							if (string.IsNullOrEmpty(strLaunch))
								strLaunch = exeNode.InnerText;
						}
						XmlNode name = doc.SelectSingleNode("/protocol/predefined_section/name");
						if (name != null && !string.IsNullOrEmpty(name.InnerText))
							strTitle = name.InnerText;
					}
					if (string.IsNullOrEmpty(strLaunch))
						strLaunch = FindGameBinaryFile(path, strID);
					if (string.IsNullOrEmpty(strTitle))
						strTitle = Path.GetFileNameWithoutExtension(strLaunch);
					CLogger.LogDebug($"- {strTitle}");
					if (expensiveIcons)
					{
						strIconPath = Path.Combine(path, "game_metadata", "game.ico");
						if (!File.Exists(strIconPath))
							strIconPath = strLaunch;
					}
					else
						strIconPath = strLaunch;
					strAlias = GetAlias(Path.GetFileNameWithoutExtension(strLaunch.Trim(new char[] { ' ', '\'', '"' })));
					if (strAlias.Length > strTitle.Length)
						strAlias = GetAlias(strTitle);
					if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
						strAlias = "";
					if (!(string.IsNullOrEmpty(strLaunch)))
						gameDataList.Add(
							new ImportGameData(strID, strTitle, strLaunch, strIconPath, "", strAlias, true, strPlatform));
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}
			CLogger.LogDebug("------------------------");
		}

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		public static string GetGameID(string key) => key;
    }
}