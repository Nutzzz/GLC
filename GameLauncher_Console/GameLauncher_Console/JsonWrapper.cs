using IniParser;
using Logger;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
//using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using static GameLauncher_Console.CGameData;

namespace GameLauncher_Console
{
	/// <summary>
	/// Class for serializing and deserializing JSON data. 
	/// JSON data is stored and handled in a dynamically sized dictionary structure.
	/// </summary>
	public static class CJsonWrapper
	{
		public static JsonDocumentOptions jsonTrailingCommas = new()
        {
			AllowTrailingCommas = true
		};

		// minimum version numbers
		private static readonly Version _min_cfg_version	= Version.Parse("1.2.0");
		private static readonly Version _min_last_version	= Version.Parse("1.1.0");
		private static readonly Version _min_game_version	= Version.Parse("1.1.0");

		// .json filenames (they should be in the same directory as the executable)
		private static readonly string _cfg_ini_file		= CDock.filename + ".ini";
		private static readonly string _cfgold_json_file	= CDock.filename + "-cfg.json";
		private static readonly string _last_json_file		= CDock.filename + "-last.json";
		private static readonly string _game_json_file		= CDock.filename + "-games.json";
		private const string GAMEOLD_JSON_FILE				= "games.json";

		// INI sections
		private const string VERSION_SECTION				= "version";
		private const string CONFIG_SECTION					= "settings";

		// JSON field names

		private const string INI_VERSION					= "ini_version";
		private const string JSON_VERSION					= "json_version";
		
		// games json
		private const string GAMES_ARRAY					= "games";
		private const string GAMES_ARRAY_ID					= "id";
		private const string GAMES_ARRAY_TITLE				= "title";
		private const string GAMES_ARRAY_LAUNCH				= "launch";
		private const string GAMES_ARRAY_ICON				= "icon";
		private const string GAMES_ARRAY_UNINSTALLER		= "uninstaller";
		private const string GAMES_ARRAY_PLATFORM			= "platform";
		private const string GAMES_ARRAY_INSTALLED			= "installed";
		private const string GAMES_ARRAY_FAVOURITE			= "favourite";
		private const string GAMES_ARRAY_NEW				= "new";
		private const string GAMES_ARRAY_HIDDEN				= "hidden";
		private const string GAMES_ARRAY_ALIAS				= "alias";
		private const string GAMES_ARRAY_LASTRUN			= "lastrun";
		private const string GAMES_ARRAY_NUMRUNS			= "numruns";
		private const string GAMES_ARRAY_RATING				= "rating";
		private const string GAMES_ARRAY_FREQUENCY			= "frequency";

		// search json (for command line non-interactive interactions)
		private const string LAST_ARRAY						= "matches";
		private const string LAST_ARRAY_INDEX				= "index";
		private const string LAST_ARRAY_TITLE				= "title";
		private const string LAST_ARRAY_PERCENT				= "percent";

		private static readonly string _configPath	= Path.Combine(CDock.currentPath, _cfg_ini_file);
		private static readonly string _configOldPath = Path.Combine(CDock.currentPath, _cfgold_json_file);
		private static readonly string _searchPath	= Path.Combine(CDock.currentPath, _last_json_file);
		private static readonly string _gamesPath	= Path.Combine(CDock.currentPath, _game_json_file);
		private static readonly string _gamesOldPath	= Path.Combine(CDock.currentPath, GAMEOLD_JSON_FILE);

		// Configuration data
		public static bool ImportFromINI(out CConfig.ConfigVolatile cfgv, out CConfig.Hotkeys keys, out CConfig.Colours cols)
        {
			CConfig.config = new Dictionary<string, string>();
			cfgv = new CConfig.ConfigVolatile();
			keys = new CConfig.Hotkeys();
			cols = new CConfig.Colours();
			bool parseError = false;

			// Set up default configuration
			IOrderedEnumerable<SettingsProperty> settings = Properties.Settings.Default.Properties.OfType<SettingsProperty>().OrderBy(s => s.Name);
			foreach (SettingsProperty setting in settings)
			{
				CConfig.config.Add(setting.Name, setting.DefaultValue.ToString());
			}

			try
			{
				if (!File.Exists(_configPath))
				{
					if (File.Exists(_configOldPath))
					{
						File.Delete(_configOldPath);
						CLogger.LogWarn("{0} is outdated. Creating new {1}...", _cfgold_json_file, _cfg_ini_file);
					}
					else
						CLogger.LogInfo("{0} is missing. Writing new defaults...", _cfg_ini_file);

					CreateNewConfigFile(_configPath);
					SetConfigVolatile(ref cfgv);
					TranslateConfig(ref keys, ref cols);
				}
				else if (!ImportConfig(_configPath, ref cfgv, ref keys, ref cols))
					parseError = true;
			}
			catch (Exception e)
            {
				CLogger.LogError(e);
            }

			return !parseError;
		}

		/// <summary>
		/// Import games from the games json config file
		/// </summary>
		/// <returns>True if successful, otherwise false</returns>
		public static bool ImportFromJSON(CPlatform platforms, out List<CMatch> matches)
		{
			bool parseError = false;
			matches = new List<CMatch>();

			// Previous search matches
			Version verSearch = Version.Parse("0.0");
			try
			{
				if (File.Exists(_searchPath))
				{
					CLogger.LogInfo("{0} was found.", _last_json_file);
					ImportSearch(_searchPath, ref verSearch, ref matches);
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}

			if (verSearch < _min_last_version)
				matches = new List<CMatch>();

			// Game data
			int nGameCount = 0;
			Version verGames = Version.Parse("0.0");

			try
			{
				if (!File.Exists(_gamesPath))
				{
					if (File.Exists(_gamesOldPath))
					{
						File.Delete(_gamesOldPath);
						CLogger.LogWarn("{0} is outdated. Creating new {1}...", GAMEOLD_JSON_FILE, _game_json_file);
					}
					else
						CLogger.LogInfo("{0} is missing. Creating new file...", _game_json_file);
					CreateNewGamesFile(_gamesPath);
				}
				else if (!(bool)CConfig.GetConfigBool(CConfig.CFG_USESCAN))
				{
					if (!ImportGames(
						_gamesPath,
						ref nGameCount,
						ref verGames,
						(bool)CConfig.GetConfigBool(CConfig.CFG_USEFREQ) ? CConsoleHelper.SortMethod.cSort_Freq : 
						((bool)CConfig.GetConfigBool(CConfig.CFG_USERATE) ? CConsoleHelper.SortMethod.cSort_Rating : 
						(((bool)CConfig.GetConfigBool(CConfig.CFG_USEALPH) ? CConsoleHelper.SortMethod.cSort_Alpha : 
						CConsoleHelper.SortMethod.cSort_Date))),
						(bool)CConfig.GetConfigBool(CConfig.CFG_USEFAVE),
						(bool)CConfig.GetConfigBool(CConfig.CFG_USEINST),
						true))
						parseError = true;
				}
			}
			catch (Exception e)
            {
				CLogger.LogError(e);
            }

			if (nGameCount < 1)
			{
				CLogger.LogInfo("{0} is empty, corrupt, or outdated. Scanning for games...", _game_json_file);
				Console.Write("Scanning for games");  // ScanGames() will add dots for each platform
				platforms.ScanGames((bool)CConfig.GetConfigBool(CConfig.CFG_USECUST), !(bool)CConfig.GetConfigBool(CConfig.CFG_IMGSCAN), true);
			}

			return !parseError;
		}

		/// <summary>
		/// Export game data from memory to the games json file
		/// NOTE: At the moment, the program will pretty pretty much create a brand new JSON file and override all of the content...
		/// ... I need to find a nice workaround as JsonDocument class is read-only.
		/// </summary>
		/// <returns>True is successful, otherwise false</returns>
		public static bool ExportGames(List<CGame> gameList)
		{
			CLogger.LogInfo("Saving {0} games to JSON...", gameList.Count);
			var options = new JsonWriterOptions
			{
				Indented = true
			};

			try
			{
                using MemoryStream stream = new();
                using (Utf8JsonWriter writer = new(stream, options))
                {
                    writer.WriteStartObject();
                    writer.WriteString(JSON_VERSION, CDock.version);
                    writer.WriteStartArray(GAMES_ARRAY);
                    for (int i = 0; i < gameList.Count; i++)
                    {
                        WriteGame(writer, gameList[i]); //, stream, options,
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                string strJsonData = Encoding.UTF8.GetString(stream.ToArray());
                byte[] bytes = new UTF8Encoding(true).GetBytes(strJsonData);

                using FileStream fs = File.Create(_gamesPath);
                fs.Write(bytes, 0, bytes.Length);
            }
			catch(Exception e)
			{
				CLogger.LogError(e);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Export search matches from memory to the search json file
		/// </summary>
		/// <returns>True is successful, otherwise false</returns>
		public static bool ExportSearch(List<CMatch> matchList)
		{
			CLogger.LogInfo("Saving search data to JSON...");
			var options = new JsonWriterOptions
			{
				Indented = true
			};

			try
			{
                using MemoryStream stream = new();
                using (Utf8JsonWriter writer = new(stream, options))
                {
                    writer.WriteStartObject();
                    writer.WriteString(JSON_VERSION, CDock.version);
                    writer.WriteStartArray(LAST_ARRAY);
                    for (int i = 0; i < matchList.Count; i++)
                    {
                        WriteSearch(writer, matchList[i]); //, stream, options,
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                string strJsonData = Encoding.UTF8.GetString(stream.ToArray());
                byte[] bytes = new UTF8Encoding(true).GetBytes(strJsonData);

                using FileStream fs = File.Create(_searchPath);
                fs.Write(bytes, 0, bytes.Length);

            }
			catch (Exception e)
			{
				CLogger.LogError(e);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Export configuration data from memory to the config json file
		/// </summary>
		/// <returns>True is successful, otherwise false</returns>
		public static bool ExportConfig()
		{
			CLogger.LogInfo("Saving configuration data to INI...");

			try
			{
				CIniParser ini = new(_configPath);

				ini.Write(INI_VERSION, CDock.version, VERSION_SECTION);
				foreach (KeyValuePair<string, string> setting in CConfig.config)
				{
					ini.Write(setting.Key, setting.Value, CONFIG_SECTION);
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Create empty games json file with the empty array
		/// </summary>
		/// <returns>True if file created, otherwise false</returns>
		private static bool CreateNewGamesFile(string file)
		{
			var options = new JsonWriterOptions
			{
				Indented = true
			};

			try
			{
                using MemoryStream stream = new();
                using (Utf8JsonWriter writer = new(stream, options))
                {
                    writer.WriteStartObject();
                    writer.WriteString(JSON_VERSION, CDock.version);
                    writer.WriteStartArray(GAMES_ARRAY);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                string strJsonData = Encoding.UTF8.GetString(stream.ToArray());
                byte[] bytes = new UTF8Encoding(true).GetBytes(strJsonData);

                using FileStream fs = File.Create(file);
                fs.Write(bytes, 0, bytes.Length);
            }
			catch (Exception e)
			{
				CLogger.LogError(e);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Create empty config json file
		/// </summary>
		/// <returns>True if file created, otherwise false</returns>
		private static bool CreateNewConfigFile(string file)
		{
			try
			{
				if (File.Exists(file))
					File.Delete(file);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}

			try
			{
				CIniParser ini = new(file);

				ini.Write(INI_VERSION, CDock.version, VERSION_SECTION);
				ini.WriteComment(" <https://docs.microsoft.com/dotnet/api/system.consolekey>", " Valid key_* entries :");
				ini.WriteComment(" <https://docs.microsoft.com/dotnet/api/system.consolecolor>", " Valid colour_* entries :");
				foreach (KeyValuePair<string, string> setting in CConfig.config)
				{
					ini.Write(setting.Key, setting.Value, CONFIG_SECTION);
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Import games from the json file and add them to the global game dictionary.
		/// <returns>True if successful, otherwise false</returns>
		/// </summary>
		private static bool ImportGames(string file, ref int nGameCount, ref Version version, CConsoleHelper.SortMethod sortMethod, bool faveSort, bool instSort, bool ignoreArticle = false)
		{
			CLogger.LogInfo("Importing games from JSON...");

			string strDocumentData = File.ReadAllText(file);

			if (string.IsNullOrEmpty(strDocumentData))
				return false;

			try
			{
                using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
                if (Version.TryParse(GetStringProperty(document.RootElement, JSON_VERSION), out version) && version < _min_game_version)
					return true;

                if (!document.RootElement.TryGetProperty(GAMES_ARRAY, out JsonElement jArrGames)) // 'games' array does not exist
                    return false;

                foreach (JsonElement jElement in jArrGames.EnumerateArray())
                {
                    string strID = GetStringProperty(jElement, GAMES_ARRAY_ID);
                    string strTitle = GetStringProperty(jElement, GAMES_ARRAY_TITLE);
                    if (string.IsNullOrEmpty(strTitle))
                        continue;

                    string strLaunch = GetStringProperty(jElement, GAMES_ARRAY_LAUNCH);
                    string strIconPath = GetStringProperty(jElement, GAMES_ARRAY_ICON);
                    string strUninstall = GetStringProperty(jElement, GAMES_ARRAY_UNINSTALLER);
                    bool bIsInstalled = GetBoolProperty(jElement, GAMES_ARRAY_INSTALLED);
                    bool bIsFavourite = GetBoolProperty(jElement, GAMES_ARRAY_FAVOURITE);
                    bool bIsNew = GetBoolProperty(jElement, GAMES_ARRAY_NEW);
                    bool bIsHidden = GetBoolProperty(jElement, GAMES_ARRAY_HIDDEN);
                    string strAlias = GetStringProperty(jElement, GAMES_ARRAY_ALIAS);
                    string strPlatform = GetStringProperty(jElement, GAMES_ARRAY_PLATFORM);
                    DateTime dateLastRun = GetDateTimeProperty(jElement, GAMES_ARRAY_LASTRUN);
					int numRuns = GetIntProperty(jElement, GAMES_ARRAY_NUMRUNS);
                    ushort rating = GetUShortProperty(jElement, GAMES_ARRAY_RATING);
                    double fOccurCount = GetDoubleProperty(jElement, GAMES_ARRAY_FREQUENCY);

                    AddGame(strID, strTitle, strLaunch, strIconPath, strUninstall, bIsInstalled, bIsFavourite, bIsNew, bIsHidden, strAlias, strPlatform, new List<string>(), dateLastRun, rating, (uint)numRuns, fOccurCount);
                    nGameCount++;
                }
                SortGames(sortMethod, faveSort, instSort, ignoreArticle);
            }
			catch (Exception e)
			{
				CLogger.LogError(e, $"Malformed {file} file!");
				Console.WriteLine($"ERROR: Malformed {file} file!");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Import games from the json file and add them to the global game dictionary.
		/// <returns>True if successful, otherwise false</returns>
		/// </summary>
		private static bool ImportSearch(string file, ref Version version, ref List<CMatch> matches)
		{
			CLogger.LogInfo("Importing search results from JSON...");

			string strDocumentData = File.ReadAllText(file);

			if (string.IsNullOrEmpty(strDocumentData))
				return false;

			try
			{
                using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
				if (!Version.TryParse(GetStringProperty(document.RootElement, JSON_VERSION), out version))
					version = Version.Parse("0.0");

                if (!document.RootElement.TryGetProperty(LAST_ARRAY, out JsonElement jArrSearch)) // 'matches' array does not exist
                    return false;

                foreach (JsonElement jElement in jArrSearch.EnumerateArray())
                {
                    string strTitle = GetStringProperty(jElement, LAST_ARRAY_TITLE);
                    if (string.IsNullOrEmpty(strTitle))
                        continue;

                    int nIndex = GetIntProperty(jElement, LAST_ARRAY_INDEX);
                    int nPercent = GetUShortProperty(jElement, LAST_ARRAY_PERCENT);
                    matches.Add(new CMatch(strTitle, nIndex, nPercent));
                }
            }
			catch (Exception e)
			{
				CLogger.LogError(e, $"Malformed {file} file!");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Import configuration items from the ini file and add them to the global configuration dictionary.
		/// <returns>True if successful, otherwise false</returns>
		/// </summary>
		private static bool ImportConfig(string file, ref CConfig.ConfigVolatile configv, ref CConfig.Hotkeys hotkeys, ref CConfig.Colours colours)
		{
			bool importError = false;
			bool translateError = false;
			Dictionary<string, string> configNew = new();
			//Version version = Version.Parse("0.0");

			CLogger.LogInfo("Importing configuration data from INI...");

			try
			{
				CIniParser ini = new(file);

				if (Version.TryParse(ini.Read(INI_VERSION, VERSION_SECTION), out Version version) && (version >= _min_cfg_version))
				{
					foreach (KeyValuePair<string, string> setting in CConfig.config)
					{
						string strVal = ini.Read(setting.Key, CONFIG_SECTION);
						//CLogger.LogDebug("{0}={1}", setting.Key, strVal);
						if (string.IsNullOrEmpty(strVal))
							configNew.Add(setting.Key, CConfig.GetConfigDefault(setting.Key));
						else
							configNew.Add(setting.Key, strVal);
					}
					CConfig.config = new Dictionary<string, string>(configNew);
				}
				else
				{
					CLogger.LogWarn("Outdated configuration file. Resetting defaults...");
					Console.WriteLine($"ERROR: Outdated {file} file. Resetting defaults...");
					CreateNewConfigFile(file);
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
				Console.WriteLine($"ERROR: Bad data in {file} file. Resetting defaults...");
				importError = true;
				SetConfigDefaults(true);
			}

			SetConfigVolatile(ref configv);
			translateError = !(TranslateConfig(ref hotkeys, ref colours));
			return !(importError || translateError);
		}

		private static bool SetConfigVolatile(ref CConfig.ConfigVolatile configv)
        {
			configv.dontSaveChanges = (bool)CConfig.GetConfigBool(CConfig.CFG_USEFILE);
			configv.typingInput = (bool)CConfig.GetConfigBool(CConfig.CFG_USETYPE);
			configv.listView = (bool)CConfig.GetConfigBool(CConfig.CFG_USELIST);
			configv.imageBorder = (bool)CConfig.GetConfigBool(CConfig.CFG_IMGBORD);
			configv.imageSize = (ushort)CConfig.GetConfigNum(CConfig.CFG_IMGSIZE);
			configv.iconSize = (ushort)CConfig.GetConfigNum(CConfig.CFG_ICONSIZE);
			return true;
        }

		/// <summary>
		/// Translate configuration strings into ConsoleKey and ConsoleColor enums
		/// <returns>True if successful, otherwise false</returns>
		/// </summary>
		private static bool TranslateConfig(ref CConfig.Hotkeys hotkeys, ref CConfig.Colours colours)
		{
			bool parseError = false;

			try
			{
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLBG1), true, out colours.bgCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLBG2), true, out colours.bgLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLTITLE1), true, out colours.titleCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLTITLE2), true, out colours.titleLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLSUB1), true, out colours.subCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLSUB2), true, out colours.subLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLENTRY1), true, out colours.entryCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLENTRY2), true, out colours.entryLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLUNIN1), true, out colours.uninstCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLUNIN2), true, out colours.uninstLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLHIBG1), true, out colours.highbgCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLHIBG2), true, out colours.highbgLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLHILITE1), true, out colours.highlightCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLHILITE2), true, out colours.highlightLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLINPBG1), true, out colours.inputbgCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLINPBG2), true, out colours.inputbgLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLINPUT1), true, out colours.inputCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLINPUT2), true, out colours.inputLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLERRBG1), true, out colours.errorCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLERRBG2), true, out colours.errorLtCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLERROR1), true, out colours.errorCC);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_COLERROR2), true, out colours.errorLtCC);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
				Console.WriteLine($"ERROR: Bad colour value. Resetting defaults...");
				parseError = true;
				SetConfigDefaults(false, false, false, false, false, true, false, false);
			}
			try
			{
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYLT1), true, out hotkeys.leftCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYLT2), true, out hotkeys.leftCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYUP1), true, out hotkeys.upCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYUP2), true, out hotkeys.upCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYRT1), true, out hotkeys.rightCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYRT2), true, out hotkeys.rightCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYDN1), true, out hotkeys.downCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYDN2), true, out hotkeys.downCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYSEL1), true, out hotkeys.selectCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYSEL2), true, out hotkeys.selectCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYQUIT1), true, out hotkeys.quitCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYQUIT2), true, out hotkeys.quitCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYSCAN1), true, out hotkeys.scanCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYSCAN2), true, out hotkeys.scanCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYHELP1), true, out hotkeys.helpCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYHELP2), true, out hotkeys.helpCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYBACK1), true, out hotkeys.backCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYBACK2), true, out hotkeys.backCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYPGUP1), true, out hotkeys.pageUpCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYPGUP2), true, out hotkeys.pageUpCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYPGDN1), true, out hotkeys.pageDownCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYPGDN2), true, out hotkeys.pageDownCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYHOME1), true, out hotkeys.firstCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYHOME2), true, out hotkeys.firstCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYEND1), true, out hotkeys.lastCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYEND2), true, out hotkeys.lastCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYPLAT1), true, out hotkeys.launcherCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYPLAT2), true, out hotkeys.launcherCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYCFG1), true, out hotkeys.settingsCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYCFG2), true, out hotkeys.settingsCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYFIND1), true, out hotkeys.searchCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYFIND2), true, out hotkeys.searchCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYTAB1), true, out hotkeys.completeCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYTAB2), true, out hotkeys.completeCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYESC1), true, out hotkeys.cancelCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYESC2), true, out hotkeys.cancelCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYNEW1), true, out hotkeys.newCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYNEW2), true, out hotkeys.newCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYDEL1), true, out hotkeys.deleteCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYDEL2), true, out hotkeys.deleteCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYUNIN1), true, out hotkeys.uninstCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYUNIN2), true, out hotkeys.uninstCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYCUT1), true, out hotkeys.shortcutCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYCUT2), true, out hotkeys.shortcutCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYHIDE1), true, out hotkeys.hideCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYHIDE2), true, out hotkeys.hideCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYFAVE1), true, out hotkeys.faveCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYFAVE2), true, out hotkeys.faveCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYALIAS1), true, out hotkeys.aliasCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYALIAS2), true, out hotkeys.aliasCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYTYPE1), true, out hotkeys.typeCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYTYPE2), true, out hotkeys.typeCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYVIEW1), true, out hotkeys.viewCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYVIEW2), true, out hotkeys.viewCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYMODE1), true, out hotkeys.modeCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYMODE2), true, out hotkeys.modeCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYIMG1), true, out hotkeys.imageCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYIMG2), true, out hotkeys.imageCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYSORT1), true, out hotkeys.sortCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYSORT2), true, out hotkeys.sortCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYTAGS1), true, out hotkeys.tagsCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYTAGS2), true, out hotkeys.tagsCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYRATEUP1), true, out hotkeys.ratingUpCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYRATEDN2), true, out hotkeys.ratingUpCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYRATEDN1), true, out hotkeys.ratingDownCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYRATEDN2), true, out hotkeys.ratingDownCK2);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYDLIMG1), true, out hotkeys.downloadCK1);
				Enum.TryParse(CConfig.GetConfigString(CConfig.CFG_KEYDLIMG2), true, out hotkeys.downloadCK2);
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
				Console.WriteLine($"ERROR: Bad hotkey value. Resetting defaults...");
				parseError = true;
				SetConfigDefaults(false, false, false, false, false, false, true, false);
			}
			return !parseError;
		}

		/// <summary>
		/// Create a game JSON object and add write it with JsonWriter
		/// </summary>
		/// <param name="writer">JsonWriter object</param>
		/// <param name="stream">MemoryStream object</param>
		/// <param name="options">JsonWriter options struct</param>
		/// <param name="data">Game data</param>
		private static void WriteGame(Utf8JsonWriter writer, CGame data) //, MemoryStream stream, JsonWriterOptions options, 
		{
			writer.WriteStartObject();
			writer.WriteString(GAMES_ARRAY_ID			, data.ID);
			writer.WriteString(GAMES_ARRAY_TITLE		, data.Title);
			writer.WriteString(GAMES_ARRAY_LAUNCH		, data.Launch);
			writer.WriteString(GAMES_ARRAY_ICON			, data.Icon);
			writer.WriteString(GAMES_ARRAY_UNINSTALLER	, data.Uninstaller);
			writer.WriteString(GAMES_ARRAY_PLATFORM		, data.PlatformString);
			writer.WriteBoolean(GAMES_ARRAY_INSTALLED	, data.IsInstalled);
			writer.WriteBoolean(GAMES_ARRAY_FAVOURITE	, data.IsFavourite);
			writer.WriteBoolean(GAMES_ARRAY_NEW			, data.IsNew);
			writer.WriteBoolean(GAMES_ARRAY_HIDDEN		, data.IsHidden);
			writer.WriteString(GAMES_ARRAY_ALIAS		, data.Alias);
			writer.WriteString(GAMES_ARRAY_LASTRUN		, data.LastRunDate);
			writer.WriteNumber(GAMES_ARRAY_NUMRUNS		, data.NumRuns);
			writer.WriteNumber(GAMES_ARRAY_RATING		, data.Rating);
			writer.WriteNumber(GAMES_ARRAY_FREQUENCY	, data.Frequency);
			writer.WriteEndObject();
		}

		/// <summary>
		/// Create a search JSON object and add write it with JsonWriter
		/// </summary>
		/// <param name="writer">JsonWriter object</param>
		/// <param name="stream">MemoryStream object</param>
		/// <param name="options">JsonWriter options struct</param>
		/// <param name="data">Match data</param>
		private static void WriteSearch(Utf8JsonWriter writer, CMatch data) //, MemoryStream stream, JsonWriterOptions options, 
		{
			writer.WriteStartObject();
			writer.WriteString(LAST_ARRAY_TITLE		, data.m_strTitle);
			writer.WriteNumber(LAST_ARRAY_INDEX		, data.m_nIndex);
			writer.WriteNumber(LAST_ARRAY_PERCENT	, data.m_nPercent);
			writer.WriteEndObject();
		}

		/// <summary>
		/// Retrieve string representing a ConsoleColor value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a string</returns>
		private static string GetColourProperty(JsonElement jElement, string strPropertyName)
		{
			string strElement = "";
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
					strElement = CConfig.ToConsoleColorFormat(jValue.GetString());
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return strElement;
		}

		/// <summary>
		/// Retrieve string representing a ConsoleKey value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a string</returns>
		private static string GetHotkeyProperty(JsonElement jElement, string strPropertyName)
		{
			string strElement = "";
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
					strElement = CConfig.ToConsoleKeyFormat(jValue.GetString());
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return strElement;
		}

		/// <summary>
		/// Retrieve string value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a string or empty string if not found</returns>
		public static string GetStringProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					if (jValue.ValueKind.Equals(JsonValueKind.String))
						return jValue.GetString();
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return "";
		}

		/// <summary>
		/// Retrieve boolean value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a boolean or false if not found</returns>
		public static bool GetBoolProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					return jValue.GetBoolean();
					/*
					if (jValue.GetString() == "1" ||
						jValue.GetString()[0].ToString().Equals("t", CDock.IGNORE_CASE) ||
						jValue.GetString()[0].ToString().Equals("y", CDock.IGNORE_CASE))
					{
						return true;
					}
					*/
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return false;
		}

		/// <summary>
		/// Retrieve ISO 8601-1:2019 or Unix epoch date/time value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a DateTime or null if not found</returns>
		public static DateTime GetDateTimeProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					if (jValue.ValueKind.Equals(JsonValueKind.String))
					{
						// Assume ISO 8601-1:2019
						string strVal = jValue.GetString();
						if (!string.IsNullOrEmpty(strVal))
						{
							return JsonSerializer.Deserialize<DateTime>("\"" + strVal + "\"");
						}
					}
					else if (jValue.ValueKind.Equals(JsonValueKind.Number))
					{
						// Assume Unix Epoch
						if (jValue.TryGetInt64(out long dateTime))
							return DateTimeOffset.FromUnixTimeSeconds(dateTime).UtcDateTime;
					}
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return DateTime.MinValue;
		}

		/// <summary>
		/// Retrieve integer value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as an int or 0 if not found</returns>
		public static int GetIntProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					if (jValue.TryGetInt32(out int nOut)) return (int)nOut;
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return 0;
		}

		/// <summary>
		/// Retrieve unsigned long value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a ulong or 0 if not found</returns>
		public static ulong GetULongProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					if (jValue.TryGetUInt64(out ulong nOut)) return nOut;
				}
			}
			catch (Exception e)
            {
				CLogger.LogError(e);
            }
			return 0;
		}

		/// <summary>
		/// Retrieve unsigned short value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as an unsigned short or 0 if not found</returns>
		public static ushort GetUShortProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					if (jValue.TryGetUInt16(out ushort nOut)) return nOut;
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return 0;
		}

		/// <summary>
		/// Retrieve double value from the JSON element
		/// </summary>
		/// <param name="strPropertyName">Name of the property</param>
		/// <param name="jElement">Source JSON element</param>
		/// <returns>Value of the property as a double or 0f if not found</returns>
		public static double GetDoubleProperty(JsonElement jElement, string strPropertyName)
		{
			try
			{
				if (jElement.TryGetProperty(strPropertyName, out JsonElement jValue))
				{
					if (jValue.TryGetDouble(out double fOut)) return (double)fOut;
				}
			}
			catch (Exception e)
            {
				CLogger.LogError(e);
            }
			return 0f;
		}

		/// <summary>
		/// Set the default configuration values
		/// </summary>
		/// <param name="boolOnly">Only affects boolean values, and override with defaults</param>
		/// <param name="listOnly">Only affects list values, and override with defaults</param>
		/// <param name="numOnly">Only affects number values, and override with defaults</param>
		/// <param name="colourOnly">Only affects colour values, and override with defaults</param>
		/// <param name="keyOnly">Only affects hotkey values, and override with defaults</param>
		/// <param name="textOnly">Only affects text values, and override with defaults</param>
		private static void SetConfigDefaults(bool forceAll, bool boolOnly, bool listOnly, bool numOnly, bool longOnly, bool colourOnly, bool keyOnly, bool textOnly)
		{
			if (forceAll)
			{
				SetBoolDefaults(true);
				SetListDefaults(true);
				SetNumberDefaults(true);
				SetLongDefaults(true);
				SetColourDefaults(true);
				SetKeyDefaults(true);
				SetTextDefaults(true);
			}
			else if (boolOnly)
				SetBoolDefaults(true);
			else if (listOnly)
				SetListDefaults(true);
			else if (numOnly)
				SetNumberDefaults(true);
			else if (longOnly)
				SetLongDefaults(true);
			else if (colourOnly)
				SetColourDefaults(true);
			else if (keyOnly)
				SetKeyDefaults(true);
			else if (textOnly)
				SetTextDefaults(true);
			else
			{
				SetBoolDefaults(false);
				SetNumberDefaults(false);
				SetLongDefaults(false);
				SetColourDefaults(false);
				SetKeyDefaults(false);
				SetTextDefaults(false);
			}
		}

		/// <summary>
		/// Set the default configuration values
		/// </summary>
		private static void SetConfigDefaults(bool forceAll)
		{
			SetConfigDefaults(forceAll, false, false, false, false, false, false, false);
        }

		/// <summary>
		/// Set the default boolean configuration values
		/// </summary>
		private static void SetBoolDefaults(bool force)
        {
			SetDefaultVal(CConfig.CFG_DEBUG, force);
			SetDefaultVal(CConfig.CFG_NOQUIT, force);
			SetDefaultVal(CConfig.CFG_USEFILE, force);
			SetDefaultVal(CConfig.CFG_USESCAN, force);
			SetDefaultVal(CConfig.CFG_USECMD, force);
			SetDefaultVal(CConfig.CFG_USETYPE, force);
			SetDefaultVal(CConfig.CFG_USELIST, force);
			SetDefaultVal(CConfig.CFG_NOPAGE, force);
			SetDefaultVal(CConfig.CFG_USESIZE, force);
			SetDefaultVal(CConfig.CFG_USEALPH, force);
			SetDefaultVal(CConfig.CFG_USEFREQ, force);
			SetDefaultVal(CConfig.CFG_USERATE, force);
			SetDefaultVal(CConfig.CFG_USEFAVE, force);
			SetDefaultVal(CConfig.CFG_USEINST, force);
			SetDefaultVal(CConfig.CFG_USELITE, force);
			SetDefaultVal(CConfig.CFG_USEALL, force);
			SetDefaultVal(CConfig.CFG_NOCFG, force);
			SetDefaultVal(CConfig.CFG_INSTONLY, force);
			SetDefaultVal(CConfig.CFG_USECUST, force);
			SetDefaultVal(CConfig.CFG_USETEXT, force);
			SetDefaultVal(CConfig.CFG_IMGBORD, force);
			SetDefaultVal(CConfig.CFG_IMGCUST, force);
			SetDefaultVal(CConfig.CFG_IMGDOWN, force);
			SetDefaultVal(CConfig.CFG_IMGRTIO, force);
			SetDefaultVal(CConfig.CFG_IMGBGLEG, force);
			SetDefaultVal(CConfig.CFG_IMGSCAN, force);
			SetDefaultVal(CConfig.CFG_SYNCLEG, force);
			SetDefaultVal(CConfig.CFG_USEEGL, force);
			SetDefaultVal(CConfig.CFG_USEGAL, force);
			SetDefaultVal(CConfig.CFG_USEHUM, force);
			SetDefaultVal(CConfig.CFG_USELEG, force);
		}

		/// <summary>
		/// Set the default list configuration values
		/// </summary>
		private static void SetListDefaults(bool force)
		{
#if DEBUG
			SetDefaultVal(CConfig.CFG_UWPLIST, force);
#endif
		}

		/// <summary>
		/// Set the default number configuration values
		/// </summary>
		private static void SetNumberDefaults(bool force)
		{
			SetDefaultVal(CConfig.CFG_ALIASLEN, force);
			SetDefaultVal(CConfig.CFG_ICONSIZE, force);
			SetDefaultVal(CConfig.CFG_ICONRES, force);
			SetDefaultVal(CConfig.CFG_IMGSIZE, force);
			SetDefaultVal(CConfig.CFG_IMGRES, force);
			SetDefaultVal(CConfig.CFG_IMGPOS, force);
			SetDefaultVal(CConfig.CFG_COLSIZE, force);
		}

		/// <summary>
		/// Set the default unsigned long integer configuration values
		/// </summary>
		private static void SetLongDefaults(bool force)
		{
			SetDefaultVal(CConfig.CFG_STEAMID, force);
		}

		/// <summary>
		/// Set the default colour configuration values
		/// </summary>
		private static void SetColourDefaults(bool force)
		{
			SetDefaultVal(CConfig.CFG_COLBG1, force);
			SetDefaultVal(CConfig.CFG_COLBG2, force);
			SetDefaultVal(CConfig.CFG_COLTITLE1, force);
			SetDefaultVal(CConfig.CFG_COLTITLE2, force);
			SetDefaultVal(CConfig.CFG_COLSUB1, force);
			SetDefaultVal(CConfig.CFG_COLSUB2, force);
			SetDefaultVal(CConfig.CFG_COLENTRY1, force);
			SetDefaultVal(CConfig.CFG_COLENTRY2, force);
			SetDefaultVal(CConfig.CFG_COLUNIN1, force);
			SetDefaultVal(CConfig.CFG_COLUNIN2, force);
			SetDefaultVal(CConfig.CFG_COLHIBG1, force);
			SetDefaultVal(CConfig.CFG_COLHIBG2, force);
			SetDefaultVal(CConfig.CFG_COLHILITE1, force);
			SetDefaultVal(CConfig.CFG_COLHILITE2, force);
			SetDefaultVal(CConfig.CFG_COLINPBG1, force);
			SetDefaultVal(CConfig.CFG_COLINPBG2, force);
			SetDefaultVal(CConfig.CFG_COLINPUT1, force);
			SetDefaultVal(CConfig.CFG_COLINPUT2, force);
			SetDefaultVal(CConfig.CFG_COLERRBG1, force);
			SetDefaultVal(CConfig.CFG_COLERRBG2, force);
			SetDefaultVal(CConfig.CFG_COLERROR1, force);
			SetDefaultVal(CConfig.CFG_COLERROR2, force);
		}

		/// <summary>
		/// Set the default key configuration values
		/// </summary>
		private static void SetKeyDefaults(bool force)
		{
			SetDefaultVal(CConfig.CFG_KEYLT1, force);
			SetDefaultVal(CConfig.CFG_KEYLT2, force);
			SetDefaultVal(CConfig.CFG_KEYUP1, force);
			SetDefaultVal(CConfig.CFG_KEYUP2, force);
			SetDefaultVal(CConfig.CFG_KEYRT1, force);
			SetDefaultVal(CConfig.CFG_KEYRT2, force);
			SetDefaultVal(CConfig.CFG_KEYDN1, force);
			SetDefaultVal(CConfig.CFG_KEYDN2, force);
			SetDefaultVal(CConfig.CFG_KEYSEL1, force);
			SetDefaultVal(CConfig.CFG_KEYSEL2, force);
			SetDefaultVal(CConfig.CFG_KEYQUIT1, force);
			SetDefaultVal(CConfig.CFG_KEYQUIT2, force);
			SetDefaultVal(CConfig.CFG_KEYSCAN1, force);
			SetDefaultVal(CConfig.CFG_KEYSCAN2, force);
			SetDefaultVal(CConfig.CFG_KEYHELP1, force);
			SetDefaultVal(CConfig.CFG_KEYHELP2, force);
			SetDefaultVal(CConfig.CFG_KEYBACK1, force);
			SetDefaultVal(CConfig.CFG_KEYBACK2, force);
			SetDefaultVal(CConfig.CFG_KEYPGUP1, force);
			SetDefaultVal(CConfig.CFG_KEYPGUP2, force);
			SetDefaultVal(CConfig.CFG_KEYPGDN1, force);
			SetDefaultVal(CConfig.CFG_KEYPGDN2, force);
			SetDefaultVal(CConfig.CFG_KEYHOME1, force);
			SetDefaultVal(CConfig.CFG_KEYHOME2, force);
			SetDefaultVal(CConfig.CFG_KEYEND1, force);
			SetDefaultVal(CConfig.CFG_KEYEND2, force);
			SetDefaultVal(CConfig.CFG_KEYCFG1, force);
			SetDefaultVal(CConfig.CFG_KEYCFG2, force);
			SetDefaultVal(CConfig.CFG_KEYFIND1, force);
			SetDefaultVal(CConfig.CFG_KEYFIND2, force);
			SetDefaultVal(CConfig.CFG_KEYTAB1, force);
			SetDefaultVal(CConfig.CFG_KEYTAB2, force);
			SetDefaultVal(CConfig.CFG_KEYESC1, force);
			SetDefaultVal(CConfig.CFG_KEYESC2, force);
			SetDefaultVal(CConfig.CFG_KEYNEW1, force);
			SetDefaultVal(CConfig.CFG_KEYNEW2, force);
			SetDefaultVal(CConfig.CFG_KEYDEL1, force);
			SetDefaultVal(CConfig.CFG_KEYDEL2, force);
			SetDefaultVal(CConfig.CFG_KEYUNIN1, force);
			SetDefaultVal(CConfig.CFG_KEYUNIN2, force);
			SetDefaultVal(CConfig.CFG_KEYCUT1, force);
			SetDefaultVal(CConfig.CFG_KEYCUT2, force);
			SetDefaultVal(CConfig.CFG_KEYHIDE1, force);
			SetDefaultVal(CConfig.CFG_KEYHIDE2, force);
			SetDefaultVal(CConfig.CFG_KEYFAVE1, force);
			SetDefaultVal(CConfig.CFG_KEYFAVE2, force);
			SetDefaultVal(CConfig.CFG_KEYALIAS1, force);
			SetDefaultVal(CConfig.CFG_KEYALIAS2, force);
			SetDefaultVal(CConfig.CFG_KEYTYPE1, force);
			SetDefaultVal(CConfig.CFG_KEYTYPE2, force);
			SetDefaultVal(CConfig.CFG_KEYVIEW1, force);
			SetDefaultVal(CConfig.CFG_KEYVIEW2, force);
			SetDefaultVal(CConfig.CFG_KEYMODE1, force);
			SetDefaultVal(CConfig.CFG_KEYMODE2, force);
			SetDefaultVal(CConfig.CFG_KEYIMG1, force);
			SetDefaultVal(CConfig.CFG_KEYIMG2, force);
			SetDefaultVal(CConfig.CFG_KEYSORT1, force);
			SetDefaultVal(CConfig.CFG_KEYSORT2, force);
			SetDefaultVal(CConfig.CFG_KEYTAGS1, force);
			SetDefaultVal(CConfig.CFG_KEYTAGS2, force);
			SetDefaultVal(CConfig.CFG_KEYRATEUP1, force);
			SetDefaultVal(CConfig.CFG_KEYRATEUP2, force);
			SetDefaultVal(CConfig.CFG_KEYRATEDN1, force);
			SetDefaultVal(CConfig.CFG_KEYRATEDN2, force);
			SetDefaultVal(CConfig.CFG_KEYDLIMG1, force);
			SetDefaultVal(CConfig.CFG_KEYDLIMG2, force);
		}

		/// <summary>
		/// Set the default text configuration values
		/// </summary>
		private static void SetTextDefaults(bool force)
		{
			SetDefaultVal(CConfig.CFG_PATHLEG, force);
			SetDefaultVal(CConfig.CFG_OCULUSID, force);
			SetDefaultVal(CConfig.CFG_ORIGINID, force);
			SetDefaultVal(CConfig.CFG_ORIGINPW, force);
			SetDefaultVal(CConfig.CFG_TXTMAINT, force);
			SetDefaultVal(CConfig.CFG_TXTCFGT, force);
			SetDefaultVal(CConfig.CFG_TXTFILET, force);
			SetDefaultVal(CConfig.CFG_TXTROOT, force);
			SetDefaultVal(CConfig.CFG_TXTSELECT, force);
			SetDefaultVal(CConfig.CFG_TXTCREATE, force);
			SetDefaultVal(CConfig.CFG_TXTNMAIN1, force);
			SetDefaultVal(CConfig.CFG_TXTNMAIN2, force);
			SetDefaultVal(CConfig.CFG_TXTNSUB1, force);
			SetDefaultVal(CConfig.CFG_TXTNSUB2, force);
			SetDefaultVal(CConfig.CFG_TXTIMAIN1, force);
			SetDefaultVal(CConfig.CFG_TXTIMAIN2, force);
			SetDefaultVal(CConfig.CFG_TXTISUB1, force);
			SetDefaultVal(CConfig.CFG_TXTISUB2, force);
		}

		/// <summary>
		/// Set the default configuration value if a value doesn't already exist
		/// </summary>
		/// <param name="value">The current value</param>
		/// <param name="default">The default</param>
		private static void SetDefaultVal(string property, bool force)
        {
            if (force)
                CConfig.SetConfigDefault(property);
            else if (CConfig.config.TryGetValue(property, out string value))
            {
                if (string.IsNullOrEmpty(value))
                    CConfig.SetConfigDefault(property);
            }
            return;
		}
	}
}
