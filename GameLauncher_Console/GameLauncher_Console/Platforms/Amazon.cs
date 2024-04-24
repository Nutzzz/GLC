using GameCollector.StoreHandlers.Amazon;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using Logger;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;

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
        //private const string AMAZON_DB			= @"Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite"; // AppData\Local
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
			CDock.DeleteCustomImage(game.Title, justBackups: false);
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
		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
        {
            string strPlatform = GetPlatformString(ENUM);
            
            var realFileSystem = FileSystem.Shared;
            var windowsRegistry = WindowsRegistry.Shared;
            
            AmazonHandler handler = new(windowsRegistry, realFileSystem);
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