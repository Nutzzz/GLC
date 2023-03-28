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
	// Ubisoft Connect (formerly Uplay)
	// [owned and installed games]
	public class PlatformUbisoft : IPlatform
	{
		public const GamePlatform ENUM		= GamePlatform.Ubisoft;
		public const string PROTOCOL		= "uplay://";
		public const string START_GAME		= PROTOCOL + "launch/";
		public const string UPLAY_PREFIX	= "Uplay Install ";
		private const string UPLAY_UNREG	= "Uplay"; // HKLM32 Uninstall
		//private const string UPLAY_REG	= @"SOFTWARE\Ubisoft\Launcher"; // HKLM32

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
			// Some games don't provide a valid ID
			if (game.ID.StartsWith(UPLAY_PREFIX))
			{
				//CDock.DeleteCustomImage(game.Title, false);
				if (OperatingSystem.IsWindows())
					CDock.StartShellExecute(START_GAME + GetGameID(game.ID));
				else
					Process.Start(START_GAME + GetGameID(game.ID));
				return 1;
			}
			else
				return 0;
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
			List<string> uplayIds = new();
			List<string> uplayIcons = new();
			string launcherPath = "";
			string strPlatform = GetPlatformString(ENUM);

			using (RegistryKey launcherKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(Path.Combine(UNINSTALL_REG, UPLAY_UNREG), RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				if (launcherKey == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}
				launcherPath = GetRegStrVal(launcherKey, GAME_INSTALL_LOCATION);
			}

			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				keyList = FindGameFolders(key, UPLAY_PREFIX);

				CLogger.LogInfo("{0} {1} games found", keyList.Count, _name.ToUpper());
				foreach (var data in keyList)
				{
					string loc = GetRegStrVal(data, GAME_INSTALL_LOCATION);

					string strID = "";
					string strTitle = "";
					string strLaunch = "";
					string strIconPath = "";
					string strUninstall = "";
					string strAlias = "";
					try
					{
						strID = Path.GetFileName(data.Name);
						uplayIds.Add(strID);
						strTitle = GetRegStrVal(data, GAME_DISPLAY_NAME);
						CLogger.LogDebug($"- {strTitle}");
						strLaunch = START_GAME + GetGameID(strID);
						strIconPath = GetRegStrVal(data, GAME_DISPLAY_ICON).Trim(new char[] { ' ', '"' });
						uplayIcons.Add(strIconPath);
						if (string.IsNullOrEmpty(strIconPath) && expensiveIcons)
							strIconPath = CGameFinder.FindGameBinaryFile(loc, strTitle);
						strUninstall = GetRegStrVal(data, GAME_UNINSTALL_STRING); //.Trim(new char[] { ' ', '"' });
						strAlias = GetAlias(Path.GetFileNameWithoutExtension(loc.Trim(new char[] { ' ', '\'', '"' }).Trim(new char[] { '/', '\\' })));
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

			// Get not-installed games
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY) && !(string.IsNullOrEmpty(launcherPath)))
			{
				string uplayCfgFile = Path.Combine(launcherPath, @"cache\configuration\configurations");
				try
				{
					if (File.Exists(uplayCfgFile))
					{
						char[] trimChars = { ' ', '\'', '"' };
						List<string> uplayCfg = new();
						int nGames = 0;
						bool dlc = false;
						string strID = "";
						string strTitle = "";
						string strIconPath = "";

						CLogger.LogDebug("{0} not-installed games:", _name.ToUpper());
						uplayCfg.AddRange(File.ReadAllLines(uplayCfgFile));
						foreach (string line in uplayCfg)  // Note if the last game is valid, this method won't catch it; but that appears very unlikely
						{
							if (line.Trim().StartsWith("root:"))
							{
								if (nGames > 0 && !(string.IsNullOrEmpty(strID)) && dlc == false)
								{
									bool found = false;
									foreach (string id in uplayIds)
									{
										if (id.Equals(strID))
											found = true;
									}
									if (!found)
									{
										// The Uplay ID is not always available, so this is an alternative test
										// However, if user has e.g., a demo and the full game installed, this might get a false negative
										foreach (string icon in uplayIcons)
										{
											if (Path.GetFileName(icon).Equals(Path.GetFileName(strIconPath)))
												found = true;
										}
									}
									if (!found)
									{
										strTitle = strTitle.Replace("''", "'");
										CLogger.LogDebug($"- *{strTitle}");
										gameDataList.Add(
											new ImportGameData(strID, strTitle, "", strIconPath, "", "", false, strPlatform));
									}
								}

								nGames++;
								dlc = false;
								strID = "";
								strTitle = "";
								strIconPath = "";
							}

							if (dlc == true)
								continue;
							else if (line.Trim().StartsWith("is_dlc: yes"))
							{
								dlc = true;
								continue;
							}
							else if (string.IsNullOrEmpty(strTitle) && line.Trim().StartsWith("name: "))
								strTitle = line[(line.IndexOf("name:") + 6)..].Trim(trimChars);
							else if (line.Trim().StartsWith("display_name: "))  // replace "name:" if it exists
								strTitle = line[(line.IndexOf("display_name:") + 14)..].Trim(trimChars);
							else if (strTitle.Equals("NAME") && line.Trim().StartsWith("NAME: "))
								strTitle = line[(line.IndexOf("NAME:") + 6)..].Trim(trimChars);
							else if (strTitle.Equals("GAMENAME") && line.Trim().StartsWith("GAMENAME: "))
								strTitle = line[(line.IndexOf("GAMENAME:") + 10)..].Trim(trimChars);
							else if (strTitle.Equals("l1") && line.Trim().StartsWith("l1: "))
								strTitle = line[(line.IndexOf("l1:") + 4)..].Trim(trimChars);

							else if (line.Trim().StartsWith("icon_image: ") && line.Trim().EndsWith(".ico"))
								strIconPath = Path.Combine(launcherPath, "data\\games", line[(line.IndexOf("icon_image:") + 12)..].Trim());
							else if (string.IsNullOrEmpty(strID))
							{
								if (line.Trim().StartsWith("game_code: ") && !(strID.StartsWith(UPLAY_PREFIX)))
									strID = "uplay_" + line[(line.IndexOf("game_code:") + 11)..].Trim();
								else if (line.Trim().StartsWith(@"register: HKEY_LOCAL_MACHINE\SOFTWARE\Ubisoft\Launcher\Installs\"))
								{
									strID = line.Substring(0, line.LastIndexOf("\\")).Trim();
									strID = UPLAY_PREFIX + strID[(strID.LastIndexOf("\\") + 1)..];
								}
							}
						}
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), uplayCfgFile));
				}
			}
			CLogger.LogDebug("-----------------------");
		}

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		/// <summary>
		/// Scan the key name and extract the Uplay game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Uplay game ID as string</returns>
		public static string GetGameID(string key)
		{
			int index = 0;
			for (int i = key.Length - 1; i > -1; i--)
			{
				if (char.IsDigit(key[i]))
				{
					index = i;
					continue;
				}
				break;
			}

			return key[index..];
		}
	}
}