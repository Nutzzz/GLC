using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// Legacy Games
	// [installed games only]
	public class PlatformLegacy : IPlatform
	{
		public const GamePlatform ENUM		= GamePlatform.Legacy;
		public const string PROTOCOL		= "";
		private const string LEG_REG		= @"SOFTWARE\Legacy Games"; // HKCU64
		//private const string LEG_UNREG	= "da414c81-a9fd-5732-bd5e-8acced116298da414c81-a9fd-5732-bd5e-8acced116298"; // HKLM64 Uninstall
		private const string LEG_JSON		= @"legacy-games-launcher\app-state.json"; // AppData\Roaming
		private const string LEG_LAUNCHER	= "Legacy Games Launcher";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
					RegistryView.Registry64).OpenSubKey(LEG_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM64
                Process legacyProcess = new();
                string launcherPath = Path.Combine(Path.GetDirectoryName(GetRegStrVal(key, GAME_DISPLAY_ICON)), "Legacy Games Launcher.exe");
                if (File.Exists(launcherPath))
                    CDock.StartAndRedirect(launcherPath);
                else
                {
                    //SetFgColour(cols.errorCC, cols.errorLtCC);
                    CLogger.LogWarn("Cannot start {0} launcher.", _name.ToUpper());
                    Console.WriteLine("ERROR: Launcher couldn't start. Is it installed properly?");
                    //Console.ResetColor();
                }
            }
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
			List<RegistryKey> keyList = new();
			List<string> instDirs = new();
			string strPlatform = GetPlatformString(ENUM);

			// Get installed games
			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
                RegistryView.Registry64).OpenSubKey(LEG_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKCU64
			{
				keyList = FindGameFolders(key, "");

				CLogger.LogInfo("{0} {1} games found", keyList.Count, _name.ToUpper());
				foreach (var data in keyList)
				{
					string strID = "";
					string strTitle = "";
					//string strDescription = "";
					string strLaunch = "";
					string strIconPath = "";
					string strUninstall = "";
					string strAlias = "";
					try
					{
						strID = GetRegStrVal(data, "InstallerUUID");
						if (string.IsNullOrEmpty(strID))
							strID = data.Name;
						strTitle = GetRegStrVal(data, "ProductName");
						if (string.IsNullOrEmpty(strTitle))
							strTitle = data.Name;
						CLogger.LogDebug($"- {strTitle}");
						string exePath = GetRegStrVal(data, "InstDir").Trim(new char[] { ' ', '"' });
						instDirs.Add(exePath);
						string exeFile = GetRegStrVal(data, "GameExe");
						strLaunch = Path.Combine(exePath, exeFile);
						if (expensiveIcons)
						{
                            using RegistryKey key2 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
											      RegistryView.Registry32).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree), // HKLM32
                                              key3 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
											      RegistryView.Registry64).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM64
                            List<RegistryKey> unList = FindGameKeys(key2, GAME_DISPLAY_NAME, strTitle, new string[] { LEG_LAUNCHER });
                            if (unList.Count <= 0)
                                unList = FindGameKeys(key3, GAME_DISPLAY_NAME, strTitle, new string[] { LEG_LAUNCHER });
                            foreach (RegistryKey unKey in unList)
                            {
                                strUninstall = GetRegStrVal(unKey, GAME_UNINSTALL_STRING); //.Trim(new char[] { ' ', '"' });
                                strIconPath = GetRegStrVal(unKey, GAME_DISPLAY_ICON); //.Trim(new char[] { ' ', '"' });
                                break;
                            }
                        }
						if (string.IsNullOrEmpty(strIconPath))
							strIconPath = strLaunch;
						strAlias = GetAlias(Path.GetFileNameWithoutExtension(exeFile));
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

					/*
					// Use website to download missing icons
					if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
					{
						string file = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), LEG_JSON);
						try
						{
							string strDocumentData = File.ReadAllText(file);

							if (string.IsNullOrEmpty(strDocumentData))
								CLogger.LogWarn(string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
							else
							{
								using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
								if (document.RootElement.TryGetProperty("siteData", out JsonElement siteData) && siteData.TryGetProperty("catalog", out JsonElement catalog))
								{
									foreach (JsonElement item in catalog.EnumerateArray())
									{
										if (item.TryGetProperty("games", out JsonElement games))
										{
											foreach (JsonElement gameItem in games.EnumerateArray())
											{
												if (strID.Equals(GetStringProperty(gameItem, "installer_uuid")))
												{
													// TODO: metadata description
													//strDescription = GetStringProperty(gameItem, "game_description");
													string iconUrl = GetStringProperty(gameItem, "game_coverart");
													if (!string.IsNullOrEmpty(iconUrl))
														CDock.DownloadCustomImage(strTitle, iconUrl);
												}
											}
										}
									}
								}
							}
						}
						catch (Exception e)
						{
							CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
						}
					}
					*/
				}
			}

			if (expensiveIcons)
			{
				// check installation paths manually

				List<string> libPaths = new();

				string file = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), LEG_JSON);
				if (!File.Exists(file))
				{
					CLogger.LogInfo("{0} file not found in AppData", _name.ToUpper());
					return;
				}
				else
				{
					string strDocumentData = File.ReadAllText(file);

					if (string.IsNullOrEmpty(strDocumentData))
						CLogger.LogWarn(string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
					else
					{
						try
						{
							using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
							if (document.RootElement.TryGetProperty("settings", out JsonElement settings))
							{
								if (settings.TryGetProperty("gameLibraryPath", out JsonElement paths))
								{
									foreach (JsonElement path in paths.EnumerateArray())
									{
										if (!path.Equals(null))
											libPaths.Add(path.ToString());
									}
								}
							}
						}
						catch (Exception e)
						{
							CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
						}
					}
				}

				foreach (string path in libPaths)
				{
					List<string> dirs = new();

					try
					{
						if (!path.Equals(null) && Directory.Exists(path))
						{
							dirs.AddRange(Directory.GetDirectories(path, "*.*", SearchOption.TopDirectoryOnly));
							foreach (string dir in dirs)
							{
								string strTitle = Path.GetFileName(dir);
								if (strTitle == LEG_LAUNCHER || instDirs.Contains(dir, StringComparer.CurrentCultureIgnoreCase))
									continue;
								string strID = "legacy_" + strTitle;
								string strLaunch = "";
								string strAlias = "";

								CLogger.LogDebug($"- {strTitle}");
								strLaunch = CGameFinder.FindGameBinaryFile(dir, strTitle);
								strAlias = GetAlias(Path.GetFileNameWithoutExtension(strLaunch));
								if (strAlias.Length > strTitle.Length)
									strAlias = GetAlias(strTitle);
								if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
									strAlias = "";
								if (!(string.IsNullOrEmpty(strLaunch)))
									gameDataList.Add(
										new ImportGameData(strID, strTitle, strLaunch, strLaunch, "", strAlias, true, strPlatform));
							}

						}
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}
				}
			}
			CLogger.LogDebug("--------------------");
		}

		public static string GetIconUrl(CGame game)
        {
			string iconUrl = "";

			if (!game.ID.StartsWith("legacy_")) // Currently doesn't work without InstallerUUID from registry
			{
				string file = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), LEG_JSON);
				if (!File.Exists(file))
					CLogger.LogInfo("{0} installed games not found in AppData", _name.ToUpper());
				else
				{
					try
					{
						string strDocumentData = File.ReadAllText(file);

						if (string.IsNullOrEmpty(strDocumentData))
							CLogger.LogWarn(string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
						else
						{
							using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
							if (document.RootElement.TryGetProperty("siteData", out JsonElement siteData) && siteData.TryGetProperty("catalog", out JsonElement catalog))
							{
								foreach (JsonElement item in catalog.EnumerateArray())
								{
									if (item.TryGetProperty("games", out JsonElement games))
									{
										foreach (JsonElement gameItem in games.EnumerateArray())
										{
											if (GetGameID(game.ID).Equals(GetStringProperty(gameItem, "installer_uuid")))
											{
												iconUrl = GetStringProperty(gameItem, "game_coverart");
												if (!string.IsNullOrEmpty(iconUrl))
													return iconUrl;
											}
										}
									}
								}
							}
						}
					}
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
					}
				}
			}

			CLogger.LogInfo("Icon for {0} game \"{1}\" not found in registry + catalog.", _name.ToUpper(), game.Title);
			return "";
        }

		public static string GetGameID(string key) => key;
	}
}