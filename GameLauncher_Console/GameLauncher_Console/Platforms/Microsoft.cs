using GameCollector.StoreHandlers.Xbox;
using GameFinder.Common;
using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	public class PlatformMicrosoft : IPlatform
	{
		// Microsoft Store/Xbox Game Pass

		public const GamePlatform ENUM = GamePlatform.Microsoft;
		public const string PROTOCOL = "msxbox://";
		private const string MSSTORE_APP = "Microsoft.WindowsStore_8wekyb3d8bbwe!App";
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		[SupportedOSPlatform("windows")]
		public static void Launch() => Process.Start($"explorer.exe shell:AppsFolder\\{MSSTORE_APP}"); // Microsoft Store
																									   //public static void Launch() => CDock.StartShellExecute(PROTOCOL); // Xbox app

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		[SupportedOSPlatform("windows")]
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

			XboxHandler handler = new(FileSystem.Shared);
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

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		public static string GetGameID(string key) => key;
	}
}