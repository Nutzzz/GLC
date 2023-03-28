using Logger;
using Microsoft.Win32;
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
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

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
		private const string GOG_REG_GAMES		= @"SOFTWARE\GOG.com\Games"; // HKLM32
		private const string GOG_REG_CLIENT		= @"SOFTWARE\GOG.com\GalaxyClient\paths"; // HKLM32
		private const string GOG_DB				= @"GOG.com\Galaxy\storage\galaxy-2.0.db"; // ProgramData
		//private const string GOG_GALAXY_UNREG	= "{7258BA11-600C-430E-A759-27E2C691A335}_is1"; // HKLM32 Uninstall

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

        public static void Launch()
        {
            if (OperatingSystem.IsWindows())
            {
                /*
                using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, 
				    RegistryView.Registry32).OpenSubKey(GOG_REG_CLIENT, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
                {
                    string launcherPath = Path.Combine(GetRegStrVal(key, "client"), LAUNCH);
                    if (File.Exists(launcherPath))
                        Process.Start(launcherPath);
                    else
                    {
                        //SetFgColour(cols.errorCC, cols.errorLtCC);
                        CLogger.LogWarn("Cannot start {0} launcher.", _name.ToUpper());
                        Console.WriteLine("ERROR: Launcher couldn't start. Is it installed properly?");
                        //Console.ResetColor();
                    }
                }
                */
                _ = CDock.StartShellExecute(PROTOCOL);
            }
            else
                _ = Process.Start(PROTOCOL);
        }

        // return value
        // -1 = not implemented
        // 0 = failure
        // 1 = success
        public static int InstallGame(CGame game)
		{
			CDock.DeleteCustomImage(game.Title, false);
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
                string gogClientPath = game.Launch.Contains(".") ? game.Launch.Substring(0, game.Launch.IndexOf('.') + 4) : game.Launch;
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
		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
            string strPlatform = GetPlatformString(ENUM);

            /*
			productId from ProductAuthorizations
			productId, installationPath from InstalledBaseProducts
			productId, images, title from LimitedDetails
			//images = icon from json
			id, gameReleaseKey from PlayTasks
			playTaskId, executablePath, commandLineArgs from PlayTaskLaunchParameters
            limitedDetailsId, releaseDate from Details
			*/

            // Get installed games
            string db = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), GOG_DB);
			if (!File.Exists(db))
			{
				CLogger.LogInfo("{0} database not found.", _name.ToUpper());
				return;
			}
			string launcherPath = "";
			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(GOG_REG_CLIENT, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				launcherPath = Path.Combine(GetRegStrVal(key, "client"), START_GAME);
				if (!File.Exists(launcherPath))
					launcherPath = "";
			}

			try
			{
                using SQLiteConnection con = new($"Data Source={db}");
                con.Open();

                // Get both installed and not-installed games

                using (SQLiteCommand cmd = new("SELECT productId FROM Builds", con))
                using (SQLiteDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int id = rdr.GetInt32(0);

                        using SQLiteCommand cmd2 = new($"SELECT links, images, title FROM LimitedDetails WHERE productId = {id};", con);
                        using SQLiteDataReader rdr2 = cmd2.ExecuteReader();
                        while (rdr2.Read())
                        {
                            string linksJson = rdr2.GetString(0);
                            string imagesJson = rdr2.GetString(1);

                            // To be safe, we might want to confirm "gog_{id}" is correct here with
                            // "SELECT releaseKey FROM ProductsToReleaseKeys WHERE gogId = {id};"
                            string strID = $"gog_{id}";
                            string strTitle = rdr2.GetString(2);
                            string strAlias = "";
                            string strLaunch = "";
                            string strIconPath = "";
                            bool hidden = false;
                            List<string> tagList = new();
                            DateTime lastRun = DateTime.MinValue;
                            ushort userRating = 0;
                            DateTime releaseDate = DateTime.MinValue;

                            strAlias = GetAlias(strTitle);
                            if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
                                strAlias = "";

                            using (SQLiteCommand cmd3 = new($"SELECT installationPath FROM InstalledBaseProducts WHERE productId = {id};", con))
                            using (SQLiteDataReader rdr3 = cmd3.ExecuteReader())
                            {
                                while (rdr3.Read())
                                {
                                    using SQLiteCommand cmd4 = new($"SELECT id FROM PlayTasks WHERE gameReleaseKey = '{strID}';", con);
                                    using SQLiteDataReader rdr4 = cmd4.ExecuteReader();
                                    while (rdr4.Read())
                                    {
                                        int task = rdr4.GetInt32(0);

                                        using SQLiteCommand cmd5 = new($"SELECT executablePath, commandLineArgs FROM PlayTaskLaunchParameters WHERE playTaskId = {task};", con);
                                        using SQLiteDataReader rdr5 = cmd5.ExecuteReader();
                                        while (rdr5.Read())
                                        {
                                            // Add installed games
                                            strIconPath = rdr5.GetString(0);
                                            if (string.IsNullOrEmpty(launcherPath))
                                            {
                                                string args = rdr5.GetString(1);
                                                if (!string.IsNullOrEmpty(strLaunch))
                                                {
                                                    if (!string.IsNullOrEmpty(args))
                                                    {
                                                        strLaunch += " " + args;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                strLaunch = launcherPath + " " + START_GAME_ARGS + id + " " + START_GAME_ARGS2 + "\"" + Path.GetDirectoryName(strIconPath) + "\"";
                                                if (strLaunch.Length > 8191)
                                                    strLaunch = launcherPath + " " + START_GAME_ARGS + id;
                                            }
                                            CLogger.LogDebug($"- {strTitle}");

                                            // grab hidden status
                                            using (SQLiteCommand cmd6 = new($"SELECT isHidden FROM UserReleaseProperties WHERE releaseKey = '{strID}';", con))
                                            using (SQLiteDataReader rdr6 = cmd6.ExecuteReader())
                                            {
                                                while (rdr6.Read())
                                                {
                                                    hidden = rdr6.GetBoolean(0);
                                                    //CLogger.LogDebug("    isHidden: " + rdr6.GetBoolean(0).ToString());
                                                    break;
                                                }
                                            }
                                            
                                            // grab user tags
                                            using (SQLiteCommand cmd7 = new($"SELECT tag FROM UserReleaseTags WHERE releaseKey = '{strID}';", con))
                                            using (SQLiteDataReader rdr7 = cmd7.ExecuteReader())
                                            {
                                                while (rdr7.Read())
                                                {
                                                    tagList.Add(rdr7.GetString(0));
                                                    //CLogger.LogDebug("    tag: " + rdr7.GetString(0));
                                                }
                                            }
                                            
                                            // grab last run date
                                            using (SQLiteCommand cmd8 = new($"SELECT lastPlayedDate FROM LastPlayedDates WHERE gameReleaseKey = '{strID}';", con))
                                            using (SQLiteDataReader rdr8 = cmd8.ExecuteReader())
                                            {
                                                while (rdr8.Read())
                                                {
                                                    if (!rdr8.IsDBNull(0))
                                                    {
                                                        lastRun = rdr8.GetDateTime(0);
                                                        //CLogger.LogDebug("    lastPlayedDate: " + rdr8.GetString(0) + " -> " + lastRun.ToShortDateString());
                                                        break;
                                                    }
                                                }
                                            }

                                            // grab user rating
                                            using (SQLiteCommand cmd9 = new($"SELECT value FROM GamePieces WHERE releaseKey = '{strID}';", con))
                                            using (SQLiteDataReader rdr9 = cmd9.ExecuteReader())
                                            {
                                                while (rdr9.Read())
                                                {
                                                    string pieces = rdr9.GetString(0);
                                                    using JsonDocument document2 = JsonDocument.Parse(@pieces, jsonTrailingCommas);
                                                    if (document2.RootElement.TryGetProperty("myRating", out JsonElement jRating))
                                                    {
                                                        if (jRating.ValueKind != JsonValueKind.Null)
                                                            jRating.TryGetUInt16(out userRating);
                                                        //CLogger.LogDebug("    myRating: " + userRating.ToString());
                                                        break;
                                                    }
                                                }
                                            }

                                            // TODO: metadata release date
                                            // Details table only applies to installed GOG games
                                            /*
                                            using (SQLiteCommand cmd10 = new($"SELECT releaseDate FROM Details WHERE limitedDetailsId = '{id}';", con))
                                            using (SQLiteDataReader rdr10 = cmd10.ExecuteReader())
                                            {
                                                while (rdr10.Read())
                                                {
                                                    if (!rdr10.IsDBNull(0))
                                                    {
                                                        releaseDate = rdr10.GetDateTime(0);
                                                        //CLogger.LogDebug("    releaseDate: " + rdr10.GetString(0) + " -> " + releaseDate.ToShortDateString());
                                                        break;
                                                    }
                                                }
                                            }
                                            */

                                            gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strIconPath, "", strAlias, true, strPlatform, bHidden:hidden, tags:tagList, dateLastRun:lastRun, rating:userRating));
                                        }
                                    }
                                }
                            }

                            // Add not-installed games
                            if (string.IsNullOrEmpty(strLaunch) && !(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY))
                            {
                                CLogger.LogDebug($"- *{strTitle}");
                                gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", "", false, strPlatform));

                                // Use icon from images (json) to download not-installed icons
                                if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
                                {
                                    bool success = false;
                                    string iconUrl = "";
                                    string iconWideUrl = "";

                                    using (JsonDocument document = JsonDocument.Parse(@linksJson, jsonTrailingCommas))
                                    {
                                        if (document.RootElement.TryGetProperty("logo", out JsonElement logo))
                                            iconWideUrl = GetStringProperty(logo, "href");
                                        if (document.RootElement.TryGetProperty("boxArtImage", out JsonElement boxart))
                                        {
                                            iconUrl = GetStringProperty(boxart, "href");
                                            if (!string.IsNullOrEmpty(iconUrl) && !iconUrl.Equals("null"))
                                                success = true;
                                        }
                                        if (document.RootElement.TryGetProperty("iconSquare", out JsonElement icon))
                                        {
                                            iconUrl = GetStringProperty(icon, "href");
                                            if (!string.IsNullOrEmpty(iconUrl) && !iconUrl.Equals("null"))
                                                success = true;
                                        }
                                    }
                                    if (!success)
                                    {
                                        using JsonDocument document = JsonDocument.Parse(@imagesJson, jsonTrailingCommas);
                                        iconUrl = GetStringProperty(document.RootElement, "logo2x");
                                        if (!string.IsNullOrEmpty(iconUrl))
                                            success = true;
                                        else if(!string.IsNullOrEmpty(iconWideUrl))
                                        {
                                            iconUrl = iconWideUrl;
                                            success = true;
                                        }
                                    }

                                    if (success)
                                        CDock.DownloadCustomImage(strTitle, iconUrl);
                                }
                            }
                        }
                    }
                }
                con.Close();
            }
			catch (Exception e)
			{
				CLogger.LogError(e, string.Format("Malformed {0} database output!", _name.ToUpper()));
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