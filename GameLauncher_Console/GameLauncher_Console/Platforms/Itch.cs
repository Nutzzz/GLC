using GameCollector.StoreHandlers.Itch;
using GameFinder.Common;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	// itch
	// [owned and installed games]
	public class PlatformItch : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Itch;
		public const string PROTOCOL			= "itch://";
		public const string LAUNCH				= PROTOCOL + "library";
		public const string START_GAME			= PROTOCOL + "caves/";	// itch://caves/<caveid>/launch
		public const string START_GAME_SUFFIX	= "/launch";
		public const string INSTALL_GAME		= PROTOCOL + "games/";  // itch://games/<gameid>
        private const string ITCH_RUN           = @"itch\shell\open\command"; // HKEY_CLASSES_ROOT
		private const string ITCH_DB			= @"itch\db\butler.db"; // AppData\Roaming
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		// Can't call PROTOCOL directly as itch is launched in command line mode, and StartInfo.Redirect* don't work when ShellExecute=True
		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = Registry.ClassesRoot.OpenSubKey(ITCH_RUN, RegistryKeyPermissionCheck.ReadSubTree);
                string value = GetRegStrVal(key, null);
                string[] subs = value.Split();
                string command = "";
                string args = "";
                for (int i = 0; i < subs.Length; i++)
                {
                    if (i > 0)
                        args += subs[i];
                    else
                        command = subs[0];
                }
                CDock.StartAndRedirect(command, args.Replace("%1", LAUNCH));
            }
		}

        // return value
        // -1 = not implemented
        // 0 = failure
        // 1 = success
        public static int InstallGame(CGame game)
		{
			CDock.DeleteCustomImage(game.Title, justBackups: false);
			if (OperatingSystem.IsWindows())
			{
				try
				{
                    using RegistryKey key = Registry.ClassesRoot.OpenSubKey(ITCH_RUN, RegistryKeyPermissionCheck.ReadSubTree);
                    string[] subs = GetRegStrVal(key, null).Split(' ');
                    string command = "";
                    string args = "";
                    for (int i = 0; i > subs.Length; i++)
                    {
                        if (i > 0)
                            args += subs[i];
                        else
                            command = subs[0];
                    }
                    CDock.StartAndRedirect(command, args.Replace("%1", INSTALL_GAME + GetGameID(game.ID)));
                }
				catch (Exception e)
				{
					CLogger.LogError(e);
                    return 0;
				}
			}
            return 1;
		}

        public static void StartGame(CGame game)
        {
            CLogger.LogInfo($"Launch: {game.Launch}");
            if (OperatingSystem.IsWindows())
                CDock.StartShellExecute(game.Launch);
            else
                Process.Start(game.Launch);
        }

        //[SupportedOSPlatform("windows")]
        public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
        {
            string strPlatform = GetPlatformString(ENUM);

            ItchHandler handler = new(FileSystem.Shared, null); // WindowsRegistry.Shared);
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
            string db = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), ITCH_DB);
            if (!File.Exists(db))
            {
                CLogger.LogInfo("{0} database not found.", _name.ToUpper());
                return "";
            }

            try
            {
                using SQLiteConnection con = new($"Data Source={db}");
                con.Open();

                using (SQLiteCommand cmd = new(string.Format("SELECT cover_url, still_cover_url FROM games WHERE id = '{0}';", GetGameID(game.ID)), con))
                using (SQLiteDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        iconUrl = rdr.GetString(1);
                        if (!string.IsNullOrEmpty(iconUrl))
                        {
                            success = true;
                            break;
                        }
                        else
                        {
                            iconUrl = rdr.GetString(0);
                            if (!string.IsNullOrEmpty(iconUrl))
                            {
                                success = true;
                                break;
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

            if (success)
                return iconUrl;

            CLogger.LogInfo("Icon for {0} game \"{1}\" not found in database.", _name.ToUpper(), game.Title);
            return "";
        }

		/// <summary>
		/// Scan the key name and extract the Itch game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>itch game ID as string</returns>
		public static string GetGameID(string key)
		{
            if (key.StartsWith("itch_"))
			    return key[5..];
            return key;
		}
    }
}