using GameCollector.StoreHandlers.Plarium;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	// Plarium Play
	// [installed games only]
	public class PlatformPlarium : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Plarium;
		public const string PROTOCOL			= "plariumplay://";
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

            PlariumHandler handler = new(FileSystem.Shared, WindowsRegistry.Shared);
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
			return GetIconUrl(game.Title);
		}

		public static string GetIconUrl(string title)
		{
			if (string.IsNullOrEmpty(title))
				return "";

			// Webp won't be supported until we finish switch to a cross-platform graphics library
            string iconUrl = string.Format("https://cdn01.x-plarium.com/browser/content/plarium-play/games/notification_img/{0}.webp", title.ToLower());
            // Unfortunately, the following art uses an abbreviated title, so we'd have to do additional web parsing to get these:
            //string iconUrl2 = string.Format("https://cdn01.x-plarium.com/browser/content/plarium-play/games/{0}/game-grid-preview.webp", id.ToUpper());
            //string iconWideUrl = string.Format("https://cdn01.x-plarium.com/browser/content/plarium-play/games/grid/{0}.webp", id);

            return iconUrl;
		}

        /// <summary>
        /// Scan the key name and extract the Plarium game id
        /// </summary>
        /// <param name="key">The game string</param>
        /// <returns>Plarium game ID as string</returns>
        public static string GetGameID(string key)
        {
            if (key.StartsWith("plarium_"))
                return key[8..];
            return key;
        }
    }
}