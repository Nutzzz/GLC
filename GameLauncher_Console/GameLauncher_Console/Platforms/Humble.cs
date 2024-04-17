using GameCollector.StoreHandlers.Humble;
using GameFinder.RegistryUtils;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using static System.Environment;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using FileSystem = NexusMods.Paths.FileSystem;
using GameFinder.Common;

namespace GameLauncher_Console
{
    // Humble App
    // [owned and installed games]
    public class PlatformHumble : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Humble;
		public const string PROTOCOL			= "humble://";
		public const string LAUNCH				= PROTOCOL + "launch/";
		public const string UNINST_GAME			= PROTOCOL + "uninstall/";
		private const string HUMBLE_RUN			= @"humble\shell\open\command"; // HKEY_CLASSES_ROOT
		private const string HUMBLE_CONFIG		= @"Humble App\config.json"; // AppData\Roaming
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

        // Can't call PROTOCOL directly as Humble App is launched in command line mode, and StartInfo.Redirect* don't work when ShellExecute=True
        public static void Launch()
		{
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey key = Registry.ClassesRoot.OpenSubKey(HUMBLE_RUN, RegistryKeyPermissionCheck.ReadSubTree);
                string value = GetRegStrVal(key, null);
                string[] subs = value.Split();
                string command = "";
                string args = "";
                for (int i = 0; i < subs.Length; i++)
                {
                    if (i > 0)
                        args += subs[i];
                    else
                        command = subs[0];
				}
                CDock.StartAndRedirect(command, args.Replace(" %1", ""));
            }
        }

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		public static int InstallGame(CGame game)
		{
			CDock.DeleteCustomImage(game.Title, justBackups: false);
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using RegistryKey key = Registry.ClassesRoot.OpenSubKey(HUMBLE_RUN, RegistryKeyPermissionCheck.ReadSubTree);
                    string[] subs = GetRegStrVal(key, null).Split(' ');
                    string command = "";
                    string args = "";
                    for (int i = 0; i > subs.Length; i++)
                    {
                        if (i > 0)
                            args += subs[i];
                        else
                            command = subs[0];
                    }
                    CDock.StartAndRedirect(command, args.Replace("%1", LAUNCH + GetGameID(game.ID)));
                }
                catch (Exception e)
                {
                    CLogger.LogError(e);
                    return 0;
                }
            }
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

            HumbleHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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

			CLogger.LogDebug("------------------------");
		}

        public static string GetIconUrl(CGame game)
        {
            return GetIconUrl(GetGameID(game.ID), game.Title);
        }

		public static string GetIconUrl(string id, string title)
		{
            // avif (AV1) won't be supported until we finish switch to a cross-platform graphics library
            // [403 error on attempt to download .png files directly]
            string configPath = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), HUMBLE_CONFIG); // AppData\Roaming
            if (File.Exists(configPath))
            {
                string strDocumentData = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(strDocumentData))
                {
                    using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
                    /*
                    document.RootElement.TryGetProperty("settings", out JsonElement settings);
					string loc = GetStringProperty(settings, "downloadLocation");
					*/
                    document.RootElement.TryGetProperty("user", out JsonElement user);
                    bool getChoice = GetBoolProperty(user, "owns_active_content");
					bool isPaused = GetBoolProperty(user, "is_paused");
					//bool hasPerks = GetBoolProperty(user, "has_perks");

                    document.RootElement.TryGetProperty("game-collection-4", out JsonElement games);
                    foreach (JsonElement game in games.EnumerateArray())
                    {
                        string strID = GetStringProperty(game, "gameKey");
						if (id.Equals(strID))
						{
							string imgUrl = GetStringProperty(game, "iconPath");
							if (string.IsNullOrEmpty(imgUrl))
								imgUrl = GetStringProperty(game, "imagePath");
							return imgUrl;
							//break;
                        }
                    }
                }
            }

            CLogger.LogInfo("Icon for {0} game \"{1}\" not found in file.", _name.ToUpper(), title);
            return "";
		}

        /// <summary>
        /// Scan the key name and extract the Humble game id
        /// </summary>
        /// <param name="key">The game string</param>
        /// <returns>Humble game ID as string</returns>
        public static string GetGameID(string key)
		{
			if (key.StartsWith("humble_"))
				return key[7..];
			return key;
		}
	}
}