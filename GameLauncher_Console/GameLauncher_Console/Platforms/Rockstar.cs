using GameCollector.StoreHandlers.Rockstar;
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
	// Rockstar Games Launcher
	// [installed games only]
	public class PlatformRockstar : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Rockstar;
		public const string PROTOCOL			= "rockstar://";
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

            RockstarHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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