using GameCollector.StoreHandlers.Oculus;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using Logger;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using static GameLauncher_Console.CGameData;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	// Oculus
	// [installed games only]
	public class PlatformOculus : IPlatform
	{
		public const GamePlatform ENUM          = GamePlatform.Oculus;
		public const string PROTOCOL            = "oculus://";
		private const string OCULUS_DB          = @"Oculus\sessions\_oaf\data.sqlite"; // AppData\Roaming
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

            OculusHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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

            CLogger.LogDebug("--------------------");
		}

        // gives 403 error
        public static string GetIconUrl(CGame game)
        {
            bool success = false;
            string db = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), OCULUS_DB);
            string id = GetGameID(game.ID);
            string iconUrl = "";

            using SQLiteConnection con = new($"Data Source={db}");
            con.Open();
            using SQLiteCommand cmd = new($"SELECT value FROM Objects WHERE hashkey = '{id}'", con); // AND typename = 'Application'", con);
            using SQLiteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                byte[] val = new byte[rdr.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                rdr.GetBytes(0, 0, val, 0, val.Length);
                string strVal = Encoding.Default.GetString(val);
                string iconWideUrl = ParseBlob(strVal, "uri", "cover_square_image", 0, 1, "cover_landscape_image");
                iconUrl = ParseBlob(strVal, "uri", "display_long_description", 0, 1, "cover_square_image");

                if (!string.IsNullOrEmpty(iconUrl))
                {
                    success = true;
                    break;
                }
                else
                {
                    iconUrl = ParseBlob(strVal, "uri", "id", 0, 1, "icon_image");
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        success = true;
                        break;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(iconWideUrl))
                        {
                            iconUrl = iconWideUrl;
                            success = true;
                            break;
                        }
                    }
                }
            }
            con.Close();

            if (success)
                return iconUrl;

            CLogger.LogInfo("Icon for {0} game \"{1}\" not found in database.", _name.ToUpper(), game.Title);
            return "";
        }

        /// <summary>
        /// Scan the key name and extract the Oculus game id
        /// </summary>
        /// <param name="key">The game string</param>
        /// <returns>Oculus game ID as string</returns>
        public static string GetGameID(string key)
        {
            if (key.StartsWith("oculus_"))
                return key[7..];
            return key;
        }

        static string ParseBlob(string strVal, string strStart, string strEnd, int startAdjust = 0, int stopAdjust = 0, string strStart1 = "")
		{
			if (!string.IsNullOrEmpty(strStart1))
			{
				int start1 = strVal.IndexOf(strStart1);
				if (start1 > 0)
					strVal = strVal[start1..];
			}
			int start = strVal.IndexOf(strStart);
			int stop = strVal.IndexOf(strEnd);
			if (start > 0 && stop > start)
			{
				start += strStart.Length + 10 + startAdjust;
				stop -= 5 + stopAdjust;
				if (stop - start < 1)
					stop = start + 1;
				return strVal[start..stop];
			}
			return "";
		}
	}
}