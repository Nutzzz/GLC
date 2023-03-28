using HtmlAgilityPack;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Net;
using System.Text;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;

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
		private const int STEAM_MAX_LIBS		= 64;
		private const string STEAM_GAME_PREFIX	= "Steam App ";
		private const string STEAM_PATH			= "steamapps";
		private const string STEAM_LIBFILE		= "libraryfolders.vdf";
		private const string STEAM_APPFILE		= "SteamAppData.vdf";
		private const string STEAM_USRFILE		= "loginusers.vdf";
		private const string STEAM_LIBARR		= "LibraryFolders";
		private const string STEAM_REG			= @"SOFTWARE\Valve\Steam"; // HKLM32
		//private const string STEAM_UNREG		= "Steam"; // HKLM32 Uninstall

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
			CDock.DeleteCustomImage(game.Title, false);
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
		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
			string strInstallPath = "";
			string strClientPath = "";
			string strPlatform = GetPlatformString(ENUM);

			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(STEAM_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				if (key == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}

				strInstallPath = GetRegStrVal(key, GAME_INSTALL_PATH);
				strClientPath = Path.Combine(strInstallPath, STEAM_PATH);
			}

			if (!Directory.Exists(strClientPath))
			{
				CLogger.LogInfo("{0} library not found: {1}", _name.ToUpper(), strClientPath);
				return;
			}

			string libFile = Path.Combine(strClientPath, STEAM_LIBFILE);
			List<string> libs = new()
            {
				strClientPath
			};
			int nLibs = 1;

			try
			{
				if (File.Exists(libFile))
				{
					SteamWrapper document = new(libFile);
					ACF_Struct documentData = document.ACFFileToStruct();
					ACF_Struct folders = new();
					if (documentData.SubACF.ContainsKey(STEAM_LIBARR))
						folders = documentData.SubACF[STEAM_LIBARR];
					else if (documentData.SubACF.ContainsKey(STEAM_LIBARR.ToLower()))
						folders = documentData.SubACF[STEAM_LIBARR.ToLower()];
					for (; nLibs <= STEAM_MAX_LIBS; ++nLibs)
					{
						folders.SubItems.TryGetValue(nLibs.ToString(), out string library);
						if (string.IsNullOrEmpty(library))
						{
							if (folders.SubACF.ContainsKey(nLibs.ToString()))
								folders.SubACF[nLibs.ToString()].SubItems.TryGetValue("path", out library);
							if (string.IsNullOrEmpty(library))
							{
								nLibs--;
								break;
							}
						}
						library = Path.Combine(library, STEAM_PATH);
						if (!library.Equals(strClientPath) && Directory.Exists(library))
							libs.Add(library);
					}
				}
			}
			catch (Exception e)
			{
				CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), libFile));
				nLibs--;
			}

			int i = 0;
			List<string> allFiles = new();
			foreach (string lib in libs)
			{
				List<string> libFiles = new();
				try
				{
					libFiles = Directory.GetFiles(lib, "appmanifest_*.acf", SearchOption.TopDirectoryOnly).ToList();
					allFiles.AddRange(libFiles);
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
						SteamWrapper document = new(file);
						ACF_Struct documentData = document.ACFFileToStruct();
						ACF_Struct app = documentData.SubACF["AppState"];

						string id = app.SubItems["appid"];
						if (id.Equals("228980"))  // Steamworks Common Redistributables
							continue;

						string strID = Path.GetFileName(file);
						string strTitle = app.SubItems["name"];
						CLogger.LogDebug($"- {strTitle}");
						string strLaunch = START_GAME + id;
						string strIconPath = "";
						string strUninstall = "";
						string strAlias = "";

						strAlias = GetAlias(strTitle);
						if (!string.IsNullOrEmpty(strLaunch))
						{
							using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
											       RegistryView.Registry64).OpenSubKey(Path.Combine(UNINSTALL_REG, STEAM_GAME_PREFIX + id), 
												   RegistryKeyPermissionCheck.ReadSubTree),  // HKLM64
											   key2 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
											       RegistryView.Registry32).OpenSubKey(Path.Combine(UNINSTALL_REG, STEAM_GAME_PREFIX + id), 
												   RegistryKeyPermissionCheck.ReadSubTree))  // HKLM32
							{
								if (key != null)
									strIconPath = GetRegStrVal(key, GAME_DISPLAY_ICON).Trim(new char[] { ' ', '"' });
								else if (key2 != null)
									strIconPath = GetRegStrVal(key2, GAME_DISPLAY_ICON).Trim(new char[] { ' ', '"' });
							}
							if (string.IsNullOrEmpty(strIconPath) && expensiveIcons)
							{
								bool success = false;

								// Search for an .exe to use as icon
								strIconPath = CGameFinder.FindGameBinaryFile(Path.Combine(lib, "common", app.SubItems["installdir"]), strTitle);
								if (!string.IsNullOrEmpty(strIconPath))
								{
									success = true;
									strAlias = GetAlias(Path.GetFileNameWithoutExtension(strIconPath));
									if (strAlias.Length > strTitle.Length)
										strAlias = GetAlias(strTitle);
								}

								if (!success && !(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
								{
									// Download missing icons
									string iconUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/capsule_184x69.jpg";
									if (CDock.DownloadCustomImage(strTitle, iconUrl))
										success = true;
								}
							}
							if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
								strAlias = "";
							strUninstall = UNINST_GAME + id;
							gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strIconPath, strUninstall, strAlias, true, strPlatform));
						}
					}
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
					}
				}
				i++;
				/*
				if (i > nLibs)
					CLogger.LogDebug("---------------------");
				*/
			}

			// Get not-installed games
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY))
			{
				// First get Steam user ID
				ulong userId = (ulong)CConfig.GetConfigULong(CConfig.CFG_STEAMID);

				if (userId < 1)
				{
					try
					{
						ulong userIdTmp = 0;
						string userName = "";
						string userNameTmp = "";
						string strConfigPath = Path.Combine(strInstallPath, "config");
						string appFile = Path.Combine(strConfigPath, STEAM_APPFILE);

						if (File.Exists(appFile))
						{
							SteamWrapper appDoc = new(appFile);
							ACF_Struct appDocData = appDoc.ACFFileToStruct();
							ACF_Struct appData = appDocData.SubACF["SteamAppData"];

							appData.SubItems.TryGetValue("AutoLoginUser", out userName);

							SteamWrapper usrDoc = new(Path.Combine(strConfigPath, STEAM_USRFILE));
							ACF_Struct usrDocData = usrDoc.ACFFileToStruct();
							ACF_Struct usrData = usrDocData.SubACF["users"];

							foreach (KeyValuePair<string, ACF_Struct> user in usrData.SubACF)
							{
								if (!ulong.TryParse(user.Key, out userIdTmp))
									userIdTmp = 0;

								foreach (KeyValuePair<string, string> userVal in user.Value.SubItems)
								{
									if (userVal.Key.Equals("AccountName"))
									{
										userNameTmp = userVal.Value;
										if (userNameTmp.Equals(userName))
										{
											if (!ulong.TryParse(user.Key, out userId))
												userId = 0;
										}
									}
									if (userVal.Key.Equals("MostRecent") && userVal.Value.Equals("1") && string.IsNullOrEmpty(userName))
									{
										userId = userIdTmp;
										userName = userNameTmp;
										break;
									}
								}
							}
							if (userId < 1)
							{
								userId = userIdTmp;
								userName = userNameTmp;
							}
						}
						if (userId > 0)
						{
							CLogger.LogInfo("Setting default {0} user to {1} #{2}", _name.ToUpper(), userName, userId);
							CConfig.SetConfigValue(CConfig.CFG_STEAMID, userId);
							ExportConfig();
						}
					}
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("Malformed {0} file: {1} or {2}", _name.ToUpper(), STEAM_APPFILE, STEAM_USRFILE));
					}
				}

				if (userId > 0)
				{
					// Download game list from public user profile
					try
					{
						string url = string.Format("https://steamcommunity.com/profiles/{0}/games/?tab=all", userId);
						/*
#if DEBUG
						// Don't re-download if file exists
						string tmpfile = $"tmp_{_name}.html";
						if (!File.Exists(tmpfile))
						{
							using WebClient client = new();
							client.DownloadFile(url, tmpfile);
						}
						HtmlDocument doc = new()
						{
							OptionUseIdAttribute = true
						};
						doc.Load(tmpfile);
#else
						*/
                        HtmlWeb web = new()
                        {
                            UseCookies = true
                        };
                        HtmlDocument doc = web.Load(url);
						doc.OptionUseIdAttribute = true;
//#endif
						HtmlNode gameList = doc.DocumentNode.SelectSingleNode("//script[@language='javascript']");
						if (gameList == null)
                        {
							CLogger.LogInfo("Can't get not-installed {0} games. Profile may not be public.\n" +
											"To change this, go to <https://steamcommunity.com/my/edit/settings>.",
								_name.ToUpper());
						}
						else
						{
							CLogger.LogDebug("{0} not-installed games (user #{1}):", _name.ToUpper(), userId);

							var options = new JsonDocumentOptions
							{
								AllowTrailingCommas = true
							};
							string rgGames = gameList.InnerText.Remove(0, gameList.InnerText.IndexOf('['));
							rgGames = rgGames.Remove(rgGames.IndexOf(';'));
#if DEBUG
							File.WriteAllText($"tmp_{_name}.json", rgGames);
#endif

							using JsonDocument document = JsonDocument.Parse(@rgGames, options);
                            foreach (JsonElement game in document.RootElement.EnumerateArray())
                            {
                                ulong id = GetULongProperty(game, "appid");
                                if (id > 0)
                                {
                                    // Check if game is already installed
                                    string strID = $"appmanifest_{id}.acf";
                                    bool found = false;
                                    foreach (string file in allFiles)
                                    {
                                        if (file.EndsWith(strID))
                                            found = true;
                                    }
                                    if (!found)
                                    {
                                        string strTitle = GetStringProperty(game, "name");

                                        // Add not-installed games
                                        CLogger.LogDebug($"- *{strTitle}");
                                        gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", "", false, strPlatform));

                                        // Use logo to download not-installed icons
                                        if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
                                        {
                                            string iconUrl = GetStringProperty(game, "logo");
                                            CDock.DownloadCustomImage(strTitle, iconUrl);
                                        }
                                    }
                                }
                            }
                        }
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}

					CLogger.LogDebug("---------------------");
				}
			}
		}

		/*
//using Steam.Models.SteamCommunity;
//using SteamWebAPI2.Interfaces;
//using SteamWebAPI2.Utilities;
//using System.Net.Http;
//using System.Threading.Tasks;

					try
                    {
                        Task<OwnedGamesResultModel> t = GetOwnedGames(0);
						t.Wait();
						foreach (OwnedGameModel game in t.Result.OwnedGames)
						{
							uint strID = game.AppId;
							string iconUrl = game.ImgIconUrl;
							CLogger.LogDebug($"- *{game.Name} <{iconUrl}>");
						}
					}
					catch (Exception e)
                    {
						CLogger.LogError(e);
                    }
        */

		// Using SteamWebAPI2
		/*
		static async Task<OwnedGamesResultModel> GetOwnedGames(ulong userId)
		{
			string apiKey = CConfig.GetConfigString(CConfig.CFG_STEAMAPI);
			if (string.IsNullOrEmpty(apiKey))
				CLogger.LogInfo("Can't get {0} not-installed games. API key must be added to glc.ini.\n" +
								"To get your key, go to <https://steamcommunity.com/dev/apikey>.",
					_name.ToUpper());
			
			if (userId <= 0)
				userId = (ulong)CConfig.GetConfigULong(CConfig.CFG_STEAMID);
			SteamWebInterfaceFactory apiFactory = new(apiKey);

			SteamUser userInterface = apiFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());
			var userResponse = await userInterface.GetPlayerSummaryAsync(userId);
			//DateTimeOffset? userLastModified = userResponse.LastModified;
			PlayerSummaryModel userData = userResponse.Data;
			ProfileVisibility visibility = userData.ProfileVisibility;
			CLogger.LogDebug("{0} Profile {1}, URL: <{2}>", _name, visibility, userData.ProfileUrl);

			if (visibility != ProfileVisibility.Public)
				CLogger.LogInfo("Can't get {0} not-installed games. Profile is not public.\n" +
								"To change this, go to <https://steamcommunity.com/my/edit/settings>.",
					_name.ToUpper());
			else
			{
				PlayerService playerInterface = apiFactory.CreateSteamWebInterface<PlayerService>();
				ISteamWebResponse<OwnedGamesResultModel> ownedGames = await playerInterface.GetOwnedGamesAsync(userId, true, true);
				return ownedGames.Data;
			}
			return null;
		}
*/

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
						var options = new JsonDocumentOptions
						{
							AllowTrailingCommas = true
						};
						string rgGames = gameList.InnerText.Remove(0, gameList.InnerText.IndexOf('['));
						rgGames = rgGames.Remove(rgGames.IndexOf(';'));

						using JsonDocument document = JsonDocument.Parse(@rgGames, options);
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

	/// <summary>
	/// Steam acf File Reader
	/// https://stackoverflow.com/a/42876399/6754996
	/// </<summary>
	class SteamWrapper
	{
		public string FileLocation { get; private set; }

		public SteamWrapper(string FileLocation)
		{
			if (File.Exists(FileLocation))
				this.FileLocation = FileLocation;
			else
				throw new FileNotFoundException("Error", FileLocation);
		}

		public bool CheckIntegrity()
		{
			string Content = File.ReadAllText(FileLocation);
			int quote = Content.Count(x => x == '"');
			int braceleft = Content.Count(x => x == '{');
			int braceright = Content.Count(x => x == '}');

			return ((braceleft == braceright) && (quote % 2 == 0));
		}

		public ACF_Struct ACFFileToStruct()
		{
			return ACFFileToStruct(File.ReadAllText(FileLocation));
		}

		private ACF_Struct ACFFileToStruct(string RegionToReadIn)
		{
			ACF_Struct ACF = new();
			int LengthOfRegion = RegionToReadIn.Length;
			int CurrentPos = 0;
			while (LengthOfRegion > CurrentPos)
			{
				int FirstItemStart = RegionToReadIn.IndexOf('"', CurrentPos);
				if (FirstItemStart == -1)
					break;
				int FirstItemEnd = RegionToReadIn.IndexOf('"', FirstItemStart + 1);
				CurrentPos = FirstItemEnd + 1;
				string FirstItem = "";
				if (FirstItemEnd - FirstItemStart - 1 >= 0)
					FirstItem = RegionToReadIn.Substring(FirstItemStart + 1, FirstItemEnd - FirstItemStart - 1);

				int SecondItemStartQuote = RegionToReadIn.IndexOf('"', CurrentPos);
				int SecondItemStartBraceleft = RegionToReadIn.IndexOf('{', CurrentPos);
				if (SecondItemStartBraceleft == -1 || SecondItemStartQuote < SecondItemStartBraceleft)
				{
					int SecondItemEndQuote = RegionToReadIn.IndexOf('"', SecondItemStartQuote + 1);
					string SecondItem = "";
					if (SecondItemEndQuote - SecondItemStartQuote - 1 >= 0)
						SecondItem = RegionToReadIn.Substring(SecondItemStartQuote + 1, SecondItemEndQuote - SecondItemStartQuote - 1);
					CurrentPos = SecondItemEndQuote + 1;
					if (!ACF.SubItems.ContainsKey(FirstItem))
						ACF.SubItems.Add(FirstItem, SecondItem);
					else
						break;
				}
				else
				{
					int SecondItemEndBraceright = RegionToReadIn.NextEndOf('{', '}', SecondItemStartBraceleft + 1);
					ACF_Struct ACFS = null;
					if (SecondItemEndBraceright - SecondItemStartBraceleft - 1 >= 0)
						ACFS = ACFFileToStruct(RegionToReadIn.Substring(SecondItemStartBraceleft + 1, SecondItemEndBraceright - SecondItemStartBraceleft - 1));
					CurrentPos = SecondItemEndBraceright + 1;
					if (!ACF.SubACF.ContainsKey(FirstItem))
						ACF.SubACF.Add(FirstItem, ACFS);
				}
			}

			return ACF;
		}
	}

	class ACF_Struct
	{
		public Dictionary<string, ACF_Struct> SubACF { get; private set; }
		public Dictionary<string, string> SubItems { get; private set; }

		public ACF_Struct()
		{
			SubACF = new Dictionary<string, ACF_Struct>();
			SubItems = new Dictionary<string, string>();
		}

		public override string ToString()
		{
			return ToString(0);
		}

		private string ToString(int Depth)
		{
			StringBuilder SB = new();
			foreach (KeyValuePair<string, string> item in SubItems)
			{
				SB.Append('\t', Depth);
				SB.AppendFormat("\"{0}\"\t\t\"{1}\"\r\n", item.Key, item.Value);
			}
			foreach (KeyValuePair<string, ACF_Struct> item in SubACF)
			{
				SB.Append('\t', Depth);
				SB.AppendFormat("\"{0}\"\n", item.Key);
				SB.Append('\t', Depth);
				SB.AppendLine("{");
				SB.Append(item.Value.ToString(Depth + 1));
				SB.Append('\t', Depth);
				SB.AppendLine("}");
			}
			return SB.ToString();
		}
	}

	static class Extension
	{
		public static int NextEndOf(this string str, char Open, char Close, int startIndex)
		{
			if (Open == Close)
				throw new Exception("\"Open\" and \"Close\" char are equivalent!");

			int OpenItem = 0;
			int CloseItem = 0;
			for (int i = startIndex; i < str.Length; i++)
			{
				if (str[i] == Open)
				{
					OpenItem++;
				}
				if (str[i] == Close)
				{
					CloseItem++;
					if (CloseItem > OpenItem)
						return i;
				}
			}
			throw new Exception("Not enough closing characters!");
		}
	}
}