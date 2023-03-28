using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// Oculus
	// [installed games only]
	public class PlatformOculus : IPlatform
	{
		public const GamePlatform ENUM          = GamePlatform.Oculus;
		public const string PROTOCOL            = "oculus://";
		//private const string OCULUS_UNREG		= "Oculus"; // HKLM64 Uninstall
		private const string OCULUS_LIBS        = @"SOFTWARE\Oculus VR, LLC\Oculus\Libraries"; // HKCU64
		private const string OCULUS_DB          = @"Oculus\sessions\_oaf\data.sqlite"; // AppData\Roaming
		private const string OCULUS_LIBPATH     = "OriginalPath"; // "Path" might be better, but may require converting "\\?\Volume{guid}\" to drive letter
		private const ulong OCULUS_ENV_RIFT     = 3082125255194578;

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
            //CDock.DeleteCustomImage(game.Title, false);
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
		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
            string strPlatform = GetPlatformString(ENUM);

            // Stop service (otherwise database is locked)
            ServiceController sc = new("OVRService");
            //bool restartSvc = false;
            try
            {
                if (sc.Status.Equals(ServiceControllerStatus.Running) || sc.Status.Equals(ServiceControllerStatus.StartPending))
                {
                    //restartSvc = true;
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                }
            }
            catch (Exception e)
            {
                CLogger.LogError(e);
            }

			List<string> libPaths = new();
			Dictionary<ulong, string> exePaths = new();
			string db = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), OCULUS_DB);

			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
                RegistryView.Registry64).OpenSubKey(OCULUS_LIBS, RegistryKeyPermissionCheck.ReadSubTree)) // HKCU64
			{
				if (key != null)
				{
					foreach (string lib in key.GetSubKeyNames())
					{
						using RegistryKey key2 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
                            RegistryView.Registry64).OpenSubKey(Path.Combine(OCULUS_LIBS, lib), RegistryKeyPermissionCheck.ReadSubTree); // HKCU64
						libPaths.Add(GetRegStrVal(key2, OCULUS_LIBPATH));
					}
				}
			}

			foreach (string lib in libPaths)
			{
				List<string> libFiles = new();
				try
				{
					string manifestPath = Path.Combine(lib, "Manifests");
					libFiles = Directory.GetFiles(manifestPath, "*.json.mini", SearchOption.TopDirectoryOnly).ToList();
					CLogger.LogInfo("{0} {1} games found in library {2}", libFiles.Count, _name.ToUpper(), lib);
				}
				catch (Exception e)
				{
					CLogger.LogError(e, string.Format("{0} directory read error: {1}", _name.ToUpper(), lib));
					continue;
				}

				foreach (string file in libFiles)
				{
					try
					{
						var options = new JsonDocumentOptions
						{
							AllowTrailingCommas = true
						};

						string strDocumentData = File.ReadAllText(file);

						if (string.IsNullOrEmpty(strDocumentData))
							CLogger.LogWarn(string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
						else
						{
							using JsonDocument document = JsonDocument.Parse(@strDocumentData, options);
							string name = GetStringProperty(document.RootElement, "canonicalName");
							if (ulong.TryParse(GetStringProperty(document.RootElement, "appId"), out ulong id))
							{
								exePaths.Add(id, Path.Combine(lib, "Software", name, GetStringProperty(document.RootElement, "launchFile")));
							}
						}
					}
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
					}
				}
			}

			try
			{
				CultureInfo ci = new("en-GB");
				TextInfo ti = ci.TextInfo;
                //ulong userId = 0;
                GetUserName(out string userName);

				using SQLiteConnection con = new($"Data Source={db}");
                con.Open();

                // Get the user ID to check entitlements for expired trials
                /*
				using (SQLiteCommand cmdU = new("SELECT hashkey, value FROM Objects WHERE typename = 'User'", con))
				{
					using SQLiteDataReader rdrU = cmdU.ExecuteReader();
					while (rdrU.Read())
					{
						byte[] valU = new byte[rdrU.GetBytes(1, 0, null, 0, int.MaxValue) - 1];
						rdrU.GetBytes(1, 0, valU, 0, valU.Length);
						string strValU = Encoding.Default.GetString(valU);

						string alias = ParseBlob(strValU, "alias", "app_entitlements");
						if (string.IsNullOrEmpty(userName) || userName.Equals("skipped"))
						{
							if (ulong.TryParse(rdrU.GetString(0), out userId))
							{
								userName = alias;
								break;
							}
						}
						else if (userName.Equals(alias, CDock.IGNORE_CASE))
                        {
							ulong.TryParse(rdrU.GetString(0), out userId);
							break;
                        }
					}
				}
                */

                using SQLiteCommand cmd = new("SELECT hashkey, value FROM Objects WHERE typename = 'Application'", con);
                using SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string strID = "";
                    string strTitle = "";
                    //string strDescription = "";
                    //List<string> genres = new();
                    string strLaunch = "";
                    string strAlias = "";

                    string url = "";
                    /*
                    string exePath = "", exePath2d = "", exeParams = "", exeParams2d = "";
                    string state = "", time = "";
                    bool isInstalled = false;
                    */
                    bool isInstalled = true;

                    if (ulong.TryParse(rdr.GetString(0), out ulong id))
                        strID = "oculus_" + id;
                    //else
                    //	strID = "oculus_" + name;

                    if (id == OCULUS_ENV_RIFT)
                        continue;

                    byte[] val = new byte[rdr.GetBytes(1, 0, null, 0, int.MaxValue) - 1];
                    rdr.GetBytes(1, 0, val, 0, val.Length);
                    string strVal = Encoding.Default.GetString(val);

                    _ = ulong.TryParse(ParseBlob(strVal, "ApplicationAssetBundle", "can_access_feature_keys", -1, 0), out ulong assets);
                    //ulong.TryParse(ParseBlob(strVal, "PCBinary", "livestreaming_status", -1, 0), out ulong bin);
                    string name = ParseBlob(strVal, "canonical_name", "category");
                    strTitle = ParseBlob(strVal, "display_name", "display_short_description");
                    if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(strTitle))
                        strTitle = ti.ToTitleCase(name.Replace('-', ' '));

                    //TODO: metadata
                    /*
                    strDescription = ParseBlob(strVal, "display_short_description", "genres");
                    string strGenres = ParseBlob(strVal, "genres", "grouping", 1);
                    string[] genreArray = strGenres.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string genre in genreArray)
                    {
                        genres.Add(genre[0..^1]);
                    }
                    */

                    using (SQLiteCommand cmd2 = new($"SELECT value FROM Objects WHERE hashkey = '{assets}'", con))
                    {
                        using SQLiteDataReader rdr2 = cmd2.ExecuteReader();
                        while (rdr2.Read())
                        {
                            byte[] val2 = new byte[rdr2.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                            rdr2.GetBytes(0, 0, val2, 0, val2.Length);
                            string strVal2 = Encoding.Default.GetString(val2);
                            url = ParseBlob(strVal2, "uri", "version_code", strStart1: "size");
                        }
                    }

                    // The exe's can be gotten from the .json files, which we have to get anyway to figure out the install path
                    /*
                    using (SQLiteCommand cmd3 = new($"SELECT value FROM Objects WHERE hashkey = '{bin}'", con))
                    {
                        using SQLiteDataReader rdr3 = cmd3.ExecuteReader();
                        while (rdr3.Read())
                        {
                            byte[] val3 = new byte[rdr3.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                            rdr3.GetBytes(0, 0, val3, 0, val3.Length);
                            string strVal3 = Encoding.Default.GetString(val3);
                            exePath = ParseBlob(strVal3, "launch_file", "launch_file_2d");
                            exePath2d = ParseBlob(strVal3, "launch_file_2d", "launch_parameters");
                            exeParams = ParseBlob(strVal3, "launch_parameters", "launch_parameters_2d");
                            exeParams2d = ParseBlob(strVal3, "launch_parameters_2d", "manifest_signature");
                        }
                    }

                    if (userId > 0)
                    {
                        // TODO: If this is an expired trial, count it as not-installed
                        using SQLiteCommand cmd5 = new($"SELECT value FROM Objects WHERE hashkey = '{userId}:{id}'", con);
                        using SQLiteDataReader rdr5 = cmd5.ExecuteReader();
                        while (rdr5.Read())
                        {
                            byte[] val5 = new byte[rdr5.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                            rdr5.GetBytes(0, 0, val5, 0, val5.Length);
                            string strVal5 = Encoding.Default.GetString(val5);
                            state = ParseBlob(strVal5, "active_state", "expiration_time");
                            if (state.Equals("PERMANENT"))
                                isInstalled = true;
                            else
                            {
                                time = ParseBlob(strVal5, "expiration_time", "grant_reason");
                                CLogger.LogDebug($"expiry: {state} {time}");
                                //if (!...expired)
                                isInstalled = true;
                            }
                        }
                    }
                    else
                        isInstalled = true;
                    */

                    if (exePaths.ContainsKey(id))
                    {
                        CLogger.LogDebug($"- {strTitle}");
                        strLaunch = exePaths[id];
                        strAlias = GetAlias(Path.GetFileNameWithoutExtension(exePaths[id]));
                        if (strAlias.Length > strTitle.Length)
                            strAlias = GetAlias(strTitle);
                        if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
                            strAlias = "";
                        gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strLaunch, "", strAlias, isInstalled, strPlatform));
                    }
                    else
                    {
                        CLogger.LogDebug($"- *{strTitle}");
                        gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", "", false, strPlatform));

                        /*
                        if (expensiveIcons && !string.IsNullOrEmpty(url))
                        {
                            // Download missing icons
                            // Downloading zip doesn't work anymore; now gives 403 error
                            
                            string imgfile = Path.Combine(CDock.currentPath, CDock.IMAGE_FOLDER_NAME,
                                string.Concat(strTitle.Split(Path.GetInvalidFileNameChars())));
                            bool iconFound = false;
                            foreach (string ext in CDock.supportedImages)
                            {
                                if (File.Exists(imgfile + "." + ext))
                                {
                                    iconFound = true;
                                    break;
                                }
                            }
                            if (iconFound)
                                continue;

                            string zipfile = $"tmp_{_name}_{id}.zip";
                            try
                            {
#if DEBUG
                                // Don't re-download if file exists
                                if (!File.Exists(zipfile))
                                {
#endif
                                    using WebClient client = new();
                                    client.DownloadFile(url, zipfile);
#if DEBUG
                                }
#endif
                                if (File.Exists(zipfile))
                                {
                                    using ZipArchive archive = ZipFile.OpenRead(zipfile);
                                    foreach (ZipArchiveEntry entry in archive.Entries)
                                    {
                                        foreach (string ext in CDock.supportedImages)
                                        {
                                            if (entry.Name.Equals("cover_square_image." + ext, CDock.IGNORE_CASE))
                                            {
                                                entry.ExtractToFile(imgfile + "." + ext, true);
                                                break;
                                            }
                                        }
                                    }
#if !DEBUG
                                    File.Delete(zipfile);
#endif
                                }
                            }
                            catch (Exception e)
                            {
                                CLogger.LogError(e, string.Format("Malformed {0} zip file!", _name.ToUpper()));
                            }
                        }
                        */
                    }
                }
                con.Close();
            }
			catch (Exception e)
			{
                CLogger.LogError(e, string.Format("Malformed {0} database output!", _name.ToUpper()));
            }
            //if (restartSvc)
            //    sc.Start();

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

        // Downloading zip doesn't work anymore; now gives 403 error
        /*
        public static bool DownloadCustomImage(CGame game)
        {
            bool success = false;
            string db = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), OCULUS_DB);
            string id = GetGameID(game.ID);
            string imgfile = Path.Combine(CDock.currentPath, CDock.IMAGE_FOLDER_NAME,
                string.Concat(game.Title.Split(Path.GetInvalidFileNameChars())));
            string zipfile = string.Format("tmp_{0}_{1}.zip", _name, id);

            using SQLiteConnection con = new($"Data Source={db}");
            con.Open();
            using SQLiteCommand cmd = new($"SELECT value FROM Objects WHERE hashkey = '{id}'", con); // AND typename = 'Application'", con);
            using SQLiteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string url = "";

                byte[] val = new byte[rdr.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                rdr.GetBytes(0, 0, val, 0, val.Length);
                string strVal = Encoding.Default.GetString(val);

                _ = ulong.TryParse(ParseBlob(strVal, "ApplicationAssetBundle", "can_access_feature_keys", -1, 0), out ulong assets);
                using (SQLiteCommand cmd2 = new($"SELECT value FROM Objects WHERE hashkey = '{assets}'", con))
                {
                    using SQLiteDataReader rdr2 = cmd2.ExecuteReader();
                    while (rdr2.Read())
                    {
                        byte[] val2 = new byte[rdr2.GetBytes(0, 0, null, 0, int.MaxValue) - 1];
                        rdr2.GetBytes(0, 0, val2, 0, val2.Length);
                        string strVal2 = Encoding.Default.GetString(val2);
                        url = ParseBlob(strVal2, "uri", "version_code", strStart1: "size");
                    }
                }
                try
                {
                    using WebClient client = new();
                    client.DownloadFile(url, zipfile);
                    if (File.Exists(zipfile))
                    {
                        using ZipArchive archive = ZipFile.OpenRead(zipfile);
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            foreach (string ext in CDock.supportedImages)
                            {
                                if (entry.Name.Equals("cover_square_image." + ext, CDock.IGNORE_CASE))
                                {
                                    entry.ExtractToFile(imgfile + "." + ext, true);
                                    success = true;
                                    break;
                                }
                            }
                        }
                        File.Delete(zipfile);
                    }

                    if (success)
                        break;
                }
                catch (Exception e)
                {
                    CLogger.LogError(e, string.Format("Malformed {0} zip file!", _name.ToUpper()));
                }
            }
            con.Close();

            return success;
        }
        */

        private static bool GetUserName(out string userName)
        {
            userName = CConfig.GetConfigString(CConfig.CFG_OCULUSID);
            try
            {
                // Don't request input for username yet (currently just an experiment for expired trials)
                /*
                if (string.IsNullOrEmpty(userName))
                {
                    userName = CDock.InputPrompt(_name + " username >>> ", new());
                    CDock.ClearInputLine(new());
                }
                */

                /*
                if (string.IsNullOrEmpty(userName))
                    CConfig.SetConfigValue(CConfig.CFG_OCULUSID, "skipped");
                else
                {
                */
                    CConfig.SetConfigValue(CConfig.CFG_OCULUSID, userName);
                    return true;
                //}
            }
            catch (Exception e)
            {
                CLogger.LogError(e);
            }

            return false;
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