using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// Amazon Games
	// [owned and installed games]
	public class PlatformAmazon : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Amazon;
		public const string PROTOCOL			= "amazon-games://";
		public const string START_GAME			= PROTOCOL + "play/";
		public const string UNINST_GAME			= @"__InstallData__\Amazon Game Remover.exe";
        public const string UNINST_GAME_ARGS	= "-m Game -p";
        private const string AMAZON_DB			= @"Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite"; // AppData\Local
		private const string AMAZON_OWN_DB		= @"Amazon Games\Data\Games\Sql\GameProductInfo.sqlite"; // AppData\Local
		//private const string AMAZON_UNREG		= @"{4DD10B06-78A4-4E6F-AA39-25E9C38FA568}"; // HKCU64 Uninstall

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
			CDock.DeleteCustomImage(game.Title, false);
            if (OperatingSystem.IsWindows())
                _ = CDock.StartShellExecute(START_GAME + GetGameID(game.ID));
            else
                _ = Process.Start(START_GAME + GetGameID(game.ID));
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

        enum AzEsrbRating
        {
            NO_RATING = -1,
            //Early Childhood ? [deprecated]
            everyone = 1,
            everyone_10_plus,
            teen,
            mature,
            //Adults Only 18+ ?
            rating_pending = 6,
            //Rating Pending - Likely Mature 17+ ?
        }
        enum AzPegiRating
        {
            NO_RATING = -1,
            ages_3_and_over,
            ages_7_and_over,
            ages_12_and_over,
            ages_16_and_over,
            ages_18_and_over,
            to_be_announced
            //Parental Guidance Recommended ?
        }
        enum AzUskRating
        {
            NO_RATING = -1,
            //Zero?
            SIX = 1,
            TWELVE,
            SIXTEEN,
            EIGHTEEN
        }

        [SupportedOSPlatform("windows")]
		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
        {
			List<string> azIds = new();
            string strPlatform = GetPlatformString(ENUM);

            // Get installed games
            string db = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), AMAZON_DB);
			if (!File.Exists(db))
			{
				CLogger.LogInfo("{0} installed game database not found.", _name.ToUpper());
				//return;
			}

			try
			{
                using SQLiteConnection con = new($"Data Source={db}");
                con.Open();

                using SQLiteCommand cmd = new("SELECT Id, InstallDirectory, ProductTitle FROM DbSet;", con);
                using SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string dir = rdr.GetString(1);
                    string strID = rdr.GetString(0);
                    azIds.Add(strID);
                    string strTitle = rdr.GetString(2);
                    CLogger.LogDebug($"- {strTitle}");
                    string strLaunch = START_GAME + strID;
                    string strIconPath = "";
                    string strUninstall = "";

                    using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
                        RegistryView.Registry64).OpenSubKey(Path.Combine(UNINSTALL_REG, "AmazonGames", strTitle), RegistryKeyPermissionCheck.ReadSubTree)) // HKCU64
                    {
                        if (key != null)
                        {
                            strIconPath = GetRegStrVal(key, GAME_DISPLAY_ICON);
                            strUninstall = GetRegStrVal(key, GAME_UNINSTALL_STRING);
                        }
                    }
                    if (string.IsNullOrEmpty(strIconPath))
                    {
                        if (expensiveIcons)
                        {
                            bool success = false;
                            string fuelPath = Path.Combine(dir, "fuel.json");
                            if (File.Exists(fuelPath))
                            {
                                string strDocumentData = File.ReadAllText(fuelPath);

                                if (!string.IsNullOrEmpty(strDocumentData))
                                {
                                    using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
                                    document.RootElement.TryGetProperty("Main", out JsonElement main);
                                    if (!main.Equals(null))
                                    {
                                        string iconFile = GetStringProperty(main, "Command");
                                        if (!string.IsNullOrEmpty(strIconPath))
                                        {
                                            strIconPath = Path.Combine(dir, iconFile);
                                            success = true;
                                        }
                                    }
                                }
                            }
                            if (!success)
                                strIconPath = CGameFinder.FindGameBinaryFile(dir, strTitle);
                        }
                    }
                    if (string.IsNullOrEmpty(strUninstall))
                    {
                        strUninstall = Path.Combine(Directory.GetParent(dir).FullName, UNINST_GAME) + " " + UNINST_GAME_ARGS + " " + strID;
                    }
                    string strAlias = GetAlias(strTitle);

                    if (!string.IsNullOrEmpty(strLaunch))
                    {
                        if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
                            strAlias = "";
                        gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strIconPath, strUninstall, strAlias, true, strPlatform));
                    }
                }
                con.Close();
            }
			catch (Exception e)
			{
				CLogger.LogError(e, string.Format("Malformed {0} database output!", _name.ToUpper()));
			}

			// Get not-installed games
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY))
			{
				db = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), AMAZON_OWN_DB);
				if (!File.Exists(db))
					CLogger.LogInfo("{0} not-installed game database not found.", _name.ToUpper());
				else
				{
					CLogger.LogDebug("{0} not-installed games:", _name.ToUpper());
					try
					{
                        using SQLiteConnection con = new($"Data Source={db}");
                        con.Open();

                        using SQLiteCommand cmd = new("SELECT " +
                            "ProductDescription, " +    // (0)
                            "ProductIconUrl, " +        // (1)
                            "ProductIdStr, " +          // (2)
                            "ProductPublisher, " +      // (3)
                            "ProductTitle, " +          // (4)
                            "DevelopersJson, " +        // (5)
                            "EsrbRating, " +            // (6)
                            "GameModesJson, " +         // (7)
                            "GenresJson, " +            // (8)
                            //"PegiRating, " +
                            "ProductLogoUrl, " +        // (9)
                            "ReleaseDate " +            // (10)
                            //"UskRating " +
                            "FROM DbSet;", con);
                        using SQLiteDataReader rdr = cmd.ExecuteReader();
                        while (rdr.Read())
                        {
                            bool found = false;
                            string strID = rdr.GetString(2);
                            foreach (string id in azIds)
                            {
                                if (id.Equals(strID))
                                    found = true;
                            }
                            if (!found)
                            {
                                string strTitle = rdr.GetString(4);
                                CLogger.LogDebug($"- *{strTitle}");

                                // TODO: metadata
                                /*
                                string strDescription = rdr.GetString(0);
                                string strPublisher = rdr.GetString(3);
                                string strAgeRating = rdr.GetString(6);
                                List<string> developers = new();
                                using (JsonDocument developersJson = JsonDocument.Parse(@rdr.GetString(5), jsonTrailingCommas))
                                {
                                    foreach (JsonElement developer in developersJson.RootElement.EnumerateArray())
                                    {
                                        developers.Add(developer.GetString());
                                    }
                                }
                                List<string> players = new();
                                using (JsonDocument gameModesJson = JsonDocument.Parse(@rdr.GetString(7), jsonTrailingCommas))
                                {
                                    foreach (JsonElement gameMode in gameModesJson.RootElement.EnumerateArray())
                                    {
                                        players.Add(gameMode.GetString());
                                    }
                                }
                                List<string> genres = new();
                                using (JsonDocument genresJson = JsonDocument.Parse(@rdr.GetString(8), jsonTrailingCommas))
                                {
                                    foreach (JsonElement genre in genresJson.RootElement.EnumerateArray())
                                    {
                                        genres.Add(genre.GetString());
                                    }
                                }
                                DateTime releaseDate = rdr.GetDateTime(10);
                                */

                                gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", "", false, strPlatform));

                                // Use ProductIconUrl to download not-installed icons
                                if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
                                {
                                    string iconUrl = rdr.GetString(1);
                                    //string iconWideUrl = rdr.GetString(9);
                                    CDock.DownloadCustomImage(strTitle, iconUrl);
                                }
                            }
                        }
                        con.Close();
                    }
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("Malformed {0} database output!", _name.ToUpper()));
					}
				}
			}
			CLogger.LogDebug("-------------------");
		}

        public static string GetIconUrl(CGame game)
        {
            bool success = false;
            string iconUrl = "";
            //string iconWideUrl = "";
            string db = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), AMAZON_OWN_DB);
            if (!File.Exists(db))
            {
                CLogger.LogInfo("{0} database not found.", _name.ToUpper());
                return "";
            }

            try
            {
                using SQLiteConnection con = new($"Data Source={db}");
                con.Open();

                using SQLiteCommand cmd = new(string.Format("SELECT ProductIconUrl, ProductLogoUrl FROM DbSet WHERE ProductIdStr = '{0}';", GetGameID(game.ID)), con); // ... ScreenshotsJson
                using SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    iconUrl = rdr.GetString(0);
                    //iconWideUrl = rdr.GetString(1);
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        success = true;
                        break;
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
        /// Scan the key name and extract the Amazon game id [no longer necessary after moving to SQLite method]
        /// </summary>
        /// <param name="key">The game string</param>
        /// <returns>Amazon game ID as string</returns>
        public static string GetGameID(string key)
        {
            //return key[(key.LastIndexOf(" -p ") + 4)..];  // no longer applicable
            return key;
        }
	}
}