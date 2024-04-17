using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.GOG;
using Logger;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;
using GameFinder.Common;

namespace GameLauncher_Console
{
	// GOG Galaxy
	// [owned and installed games]
	public class PlatformGOG : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.GOG;
		public const string PROTOCOL			= "goggalaxy://";
        public const string LAUNCH				= "GalaxyClient.exe";
        public const string INSTALL_GAME		= PROTOCOL + "openGameView/";
        public const string START_GAME 			= LAUNCH;
        public const string START_GAME_ARGS		= "/command=runGame /gameId=";
        public const string START_GAME_ARGS2	= "/path=";
		private const string GOG_DB				= @"GOG.com\Galaxy\storage\galaxy-2.0.db"; // ProgramData

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

        public static void Launch()
        {
            if (OperatingSystem.IsWindows())
                _ = CDock.StartShellExecute(PROTOCOL);
            else
                _ = Process.Start(PROTOCOL);
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
            if ((bool)CConfig.GetConfigBool(CConfig.CFG_USEGAL))
            {
                //CLogger.LogInfo("Setting up a {0} game...", GOG.NAME_LONG);
                ProcessStartInfo gogProcess = new();
                string gogClientPath = game.Launch.Contains(".") ? game.Launch[..(game.Launch.IndexOf('.') + 4)] : game.Launch;
                string gogArguments = game.Launch.Contains(".") ? game.Launch[(game.Launch.IndexOf('.') + 4)..] : string.Empty;
                CLogger.LogInfo($"Launch: \"{gogClientPath}\" {gogArguments}");
                gogProcess.FileName = gogClientPath;
                gogProcess.Arguments = gogArguments;
                Process.Start(gogProcess);
                if (OperatingSystem.IsWindows())
                {
                    Thread.Sleep(4000);
                    Process[] procs = Process.GetProcessesByName("GalaxyClient");
                    foreach (Process proc in procs)
                    {
                        CDock.WindowMessage.ShowWindowAsync(procs[0].MainWindowHandle, CDock.WindowMessage.SW_FORCEMINIMIZE);
                    }
                }
            }
            else
            {
                CLogger.LogInfo($"Launch: {game.Icon}");
                if (OperatingSystem.IsWindows())
                    _ = CDock.StartShellExecute(game.Icon);
                else
                    _ = Process.Start(game.Icon);
            }
        }

		[SupportedOSPlatform("windows")]
		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
		{
            string strPlatform = GetPlatformString(ENUM);

            GOGHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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

			CLogger.LogDebug("-------------------");
		}

        public static string GetIconUrl(CGame game)
        {
            bool success = false;
            string iconUrl = "";
            string iconWideUrl = "";
            string db = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), GOG_DB);
            if (!File.Exists(db))
            {
                CLogger.LogInfo("{0} database not found.", _name.ToUpper());
                return "";
            }

            try
            {
                using SQLiteConnection con = new($"Data Source={db}");
                con.Open();

                using SQLiteCommand cmd = new(string.Format("SELECT links, images FROM LimitedDetails WHERE productId = '{0}';", GetGameID(game.ID)), con);
                using SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string linksJson = rdr.GetString(0);
                    string imagesJson = rdr.GetString(1);

                    using (JsonDocument document = JsonDocument.Parse(@linksJson, jsonTrailingCommas))
                    {
                        if (document.RootElement.TryGetProperty("logo", out JsonElement logo))
                            iconWideUrl = GetStringProperty(logo, "href");
                        if (document.RootElement.TryGetProperty("iconSquare", out JsonElement icon))
                        {
                            iconUrl = GetStringProperty(icon, "href");
                            if (!string.IsNullOrEmpty(iconUrl) && !iconUrl.Equals("null"))
                            {
                                success = true;
                                break;
                            }
                        }
                        if (document.RootElement.TryGetProperty("boxArtImage", out JsonElement boxart))
                        {
                            iconUrl = GetStringProperty(boxart, "href");
                            if (!string.IsNullOrEmpty(iconUrl) && !iconUrl.Equals("null"))
                            {
                                success = true;
                                break;
                            }
                        }
                    }

                    if (!success)
                    {
                        using JsonDocument document = JsonDocument.Parse(@imagesJson, jsonTrailingCommas);
                        iconUrl = GetStringProperty(document.RootElement, "logo2x");
                        if (!string.IsNullOrEmpty(iconUrl))
                        {
                            success = true;
                            break;
                        }
                        else if (!string.IsNullOrEmpty(iconWideUrl))
                        {
                            iconUrl = iconWideUrl;
                            success = true;
                        }
                    }
                }
                con.Close();

            }
            catch (Exception e)
            {
                CLogger.LogError(e, string.Format("Malformed {0} database output!", _name.ToUpper()));
            }

            if (success)
                return iconUrl;

            CLogger.LogInfo("Icon for {0} game \"{1}\" not found in database.", _name.ToUpper(), game.Title);
            return "";
        }

        /// <summary>
        /// Scan the key name and extract the GOG game id
        /// </summary>
        /// <param name="key">The game string</param>
        /// <returns>GOG game ID as string</returns>
        public static string GetGameID(string key)
		{
            if (key.StartsWith("gog_"))
			    return key[4..];
            return key;
		}
	}
}