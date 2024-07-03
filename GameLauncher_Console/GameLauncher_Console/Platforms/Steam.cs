using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.Steam;
using HtmlAgilityPack;
using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using FileSystem = NexusMods.Paths.FileSystem;
using GameFinder.Common;

namespace GameLauncher_Console
{
	// Steam (Valve)
	// [installed games + owned games if account is public]
	// [NOTE: DLCs are currently listed as owned not-installed games]
	public class PlatformSteam : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Steam;
		public const string PROTOCOL			= "steam://";
		public const string LAUNCH				= PROTOCOL + "open/games";
		public const string INSTALL_GAME		= PROTOCOL + "install/";
		public const string START_GAME			= PROTOCOL + "rungameid/";
		public const string UNINST_GAME			= PROTOCOL + "uninstall/";
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
				_ = CDock.StartShellExecute(LAUNCH);
			else
				_ = Process.Start(LAUNCH);
		}

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		public static int InstallGame(CGame game)
		{
			CDock.DeleteCustomImage(game.Title, justBackups: false);
			if (OperatingSystem.IsWindows())
				_ = CDock.StartShellExecute(INSTALL_GAME + GetGameID(game.ID));
			else
				_ = Process.Start(INSTALL_GAME + GetGameID(game.ID));
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

            _ = CDock.GetLogin(_name + " API key <steamcommunity.com/dev/apikey>", CConfig.CFG_STEAMAPI, out string? apiKey, false) && (!apiKey.Equals("skipped"));

            SteamHandler handler = new(FileSystem.Shared, WindowsRegistry.Shared, apiKey);
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

			CLogger.LogDebug("---------------------");
		}

		public static string GetIconUrl(CGame game)
        {
			ulong userId = (ulong)CConfig.GetConfigULong(CConfig.CFG_STEAMID);
			if (userId > 0)
			{
				// Download game list from public user profile
				try
				{
					string url = string.Format("https://steamcommunity.com/profiles/{0}/games/?tab=all", userId);
					HtmlWeb web = new()
					{
						UseCookies = true
					};
					HtmlDocument doc = web.Load(url);
					doc.OptionUseIdAttribute = true;
					HtmlNode gameList = doc.DocumentNode.SelectSingleNode("//script[@language='javascript']");
					if (gameList == null)
                    {
						CLogger.LogInfo("Can't get {0} game list. Profile may not be public.\n" +
										"To change this, go to <https://steamcommunity.com/my/edit/settings>.",
							_name.ToUpper());
					}
					else
					{
						string rgGames = gameList.InnerText.Remove(0, gameList.InnerText.IndexOf('['));
						rgGames = rgGames.Remove(rgGames.IndexOf(';'));

						using JsonDocument document = JsonDocument.Parse(@rgGames, jsonTrailingCommas);
						foreach (JsonElement rggame in document.RootElement.EnumerateArray())
						{
							ulong id = GetULongProperty(rggame, "appid");
							if (id > 0 && id.ToString().Equals(GetGameID(game.ID)))
							{
								string iconUrl = GetStringProperty(rggame, "logo");
								if (!string.IsNullOrEmpty(iconUrl))
									return iconUrl;
							}
						}
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
			}

			CLogger.LogInfo("Icon for {0} game \"{1}\" not found on profile page.", _name.ToUpper(), game.Title);
			return "";
		}

		/// <summary>
		/// Scan the key name and extract the Steam game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Steam game ID as string</returns>
		public static string GetGameID(string key)
		{
			if (key.StartsWith("appmanifest_"))
				return Path.GetFileNameWithoutExtension(key[11..]);
			return key;
		}
	}
}