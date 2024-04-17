using GameCollector.StoreHandlers.Legacy;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	// Legacy Games
	// [installed games only]
	public class PlatformLegacy : IPlatform
	{
		public const GamePlatform ENUM		= GamePlatform.Legacy;
		public const string PROTOCOL		= "";
		private const string LEG_REG		= @"SOFTWARE\Legacy Games"; // HKCU64
		private const string LEG_JSON		= @"legacy-games-launcher\app-state.json"; // AppData\Roaming
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64).OpenSubKey(LEG_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM64
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
			//CDock.DeleteCustomImage(game.Title, justBackups: false);
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
		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
		{
            string strPlatform = GetPlatformString(ENUM);

            LegacyHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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