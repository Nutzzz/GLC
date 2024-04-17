using GameCollector.StoreHandlers.Ubisoft;
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
	// Ubisoft Connect (formerly Uplay)
	// [owned and installed games]
	public class PlatformUbisoft : IPlatform
	{
		public const GamePlatform ENUM		= GamePlatform.Ubisoft;
		public const string PROTOCOL		= "uplay://";
		public const string START_GAME		= PROTOCOL + "launch/";
		public const string UPLAY_PREFIX	= "Uplay Install ";
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
				//CDock.DeleteCustomImage(game.Title, justBackups: false);
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
		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
		{
			string strPlatform = GetPlatformString(ENUM);

			UbisoftHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
			foreach (var game in handler.FindAllGames(settings))
			{
				if (game.IsT0)
				{
					if (string.IsNullOrEmpty(game.AsT0.BaseGame))
					{
						CLogger.LogDebug("* " + game.AsT0.GameName);
						gameDataList.Add(new ImportGameData(strPlatform, game.AsT0));
					}
				}
				else
					CLogger.LogWarn(game.AsT1.Message);
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