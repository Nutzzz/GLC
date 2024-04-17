using GameCollector.StoreHandlers.GameJolt;
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
	// Game Jolt Client
	// [installed games only]
	public class PlatformGameJolt : IPlatform
	{
		public const GamePlatform ENUM = GamePlatform.GameJolt;
		private const string GAMEJOLT_REG = "game-jolt-client_is1"; // HKCU64 Uninstall
		private const string GAMEJOLT_EXE = "GameJoltClient.exe";
		private const string GAMEJOLT_PKGS = @"game-jolt-client\User Data\Default\packages.wttf"; // AppData\Local
		private const string GAMEJOLT_GAMES = @"game-jolt-client\User Data\Default\games.wttf"; // AppData\Local

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser,
                    Microsoft.Win32.RegistryView.Registry64).OpenSubKey(Path.Combine(UNINSTALL_REG, GAMEJOLT_REG), RegistryKeyPermissionCheck.ReadSubTree); // HKCU64
                if (key == null)
                {
                    CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
                    return;
                }

                string strClientPath = Path.Combine(GetRegStrVal(key, GAME_INSTALL_LOCATION), GAMEJOLT_EXE);
                // NOTE: There's a ".manifest" json file in this location; perhaps we should grab "launchOptions" > "executable" to get .exe instead of hard-coding
                if (!File.Exists(strClientPath))
                {
                    CLogger.LogInfo("{0} client file not found.", _name.ToUpper());
                    return;
                }

                _ = CDock.StartShellExecute(strClientPath);
            }
			/*
			else
			{
				_ = Process.Start(...);
			}
			*/
		}


    // return value
    // -1 = not implemented
    // 0 = failure
    // 1 = success
    public static int InstallGame(CGame _) => throw new NotImplementedException();

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

            GameJoltHandler handler = new(FileSystem.Shared, WindowsRegistry.Shared);
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
            string strDocumentData = File.ReadAllText(Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), GAMEJOLT_GAMES));
            if (!string.IsNullOrEmpty(strDocumentData))
            {
                using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
                document.RootElement.TryGetProperty("objects", out JsonElement objs);
                foreach (JsonProperty obj in objs.EnumerateObject())
                {
                    if (id.Equals(obj.Name))
                    {
                        obj.Value.TryGetProperty("thumbnail_media_item", out JsonElement thumb);
                        return GetStringProperty(thumb, "img_url");
                    }
                }
            }

            CLogger.LogInfo("Icon for {0} game \"{1}\" not found in file.", _name.ToUpper(), title);
            return "";
        }

		/// <summary>
		/// Scan the key name and extract the Game Jolt game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Game Jolt game ID as string</returns>
		public static string GetGameID(string key)
		{
			if (key.StartsWith("gamejolt_"))
				return key[9..];
			return key;
		}
	}
}