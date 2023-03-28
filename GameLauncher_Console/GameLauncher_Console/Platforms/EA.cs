using Logger;
using Microsoft.Win32;
using PureOrigin.API;
using SHA3.Net;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// EA (formerly Origin)
	// [installed games + owned games; accurate titles only if login is provided]
	public class PlatformEA : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.EA;
		public const string PROTOCOL			= "origin2://"; //"eadm://" and "ealink://" added after move to EA branding, but "origin://" or "origin2://" still seem to be the correct ones
        public const string LAUNCH				= PROTOCOL + "library/open";
		//public const string INSTALL_GAME		= PROTOCOL + "";
		public const string START_GAME			= PROTOCOL + "game/launch?offerIds=";
		public const string EA_REG				= "EA Desktop";
		//private const string EA_CONTENT			= @"EA Desktop\InstallData"; // ProgramData
		private const string EA_DB				= @"EA Desktop\530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e\IS"; // ProgramData
        private const string EA_KEY_PREFIX		= "allUsersGenericIdIS";
        //private const string EA_IV				= "84efc4b836119c20419398c3f3f2bcef6fc52f9d86c6e4e8756aec5a8279e492";
		
        private const string EA_LANGDEF			= "en_US";
        private const string ORIGIN_CTRYDEF		= "US";
        private const string ORIGIN_CONTENT		= @"Origin\LocalContent"; // ProgramData
        private const string ORIGIN_PATH		= "dipinstallpath=";
		/*
		private const string ORIGIN_GAMES		= "Origin Games";
		private const string EA_GAMES			= "EA Games";
		private const string ORIGIN_UNREG		= "Origin"; // HKLM32 Uninstall
		private const string EA_UNREG			= "{9d365a2c-801c-4d99-a902-f17f2dc03510}"; // HKLM32 Uninstall
		private const string EA_REG				= @"SOFTWARE\Electronic Arts\EA Desktop"; // HKLM32
		*/

        private const string WMI_CLASS_MOBO		= "Win32_BaseBoard";
        private const string WMI_CLASS_BIOS		= "Win32_BIOS";
        private const string WMI_CLASS_VID		= "Win32_VideoController";
        private const string WMI_CLASS_PROC		= "Win32_Processor";
        private const string WMI_PROP_MFG		= "Manufacturer";
        private const string WMI_PROP_SERIAL	= "SerialNumber";
        private const string WMI_PROP_PNPID		= "PNPDeviceID";
        private const string WMI_PROP_NAME		= "Name";
        private const string WMI_PROP_PROCID	= "ProcessorID";

        private static readonly string _name	= Enum.GetName(typeof(GamePlatform), ENUM);
        private static readonly string _hwfile	= string.Format("{0}_hwinfo.txt", _name);
        private readonly static byte[] _ea_iv	= { 0x84, 0xef, 0xc4, 0xb8, 0x36, 0x11, 0x9c, 0x20, 0x41, 0x93, 0x98, 0xc3, 0xf3, 0xf2, 0xbc, 0xef, };

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
			Launch();
			/*
			if (OperatingSystem.IsWindows())
                _ = CDock.StartShellExecute(INSTALL_GAME + GetGameID(game.ID));
			else
				_ = Process.Start(INSTALL_GAME + GetGameID(game.ID));
			*/
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
			bool dbGameFound = false;
			List<string[]> ownedGames = new();
            string strPlatform = GetPlatformString(ENUM);

            // Get all owned games via API

            if (!(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY))
            {
                if (GetLogin(out string email, out SecureString password) && (!email.Equals("skipped") && !email.Equals("invalid") && !password.Equals(null) && password.Length > 0))
                {
                    ownedGames = GetOwnedGames(email, password).Result;
                    password.Dispose();
                    if (ownedGames.Count <= 0)
                    {
                        CLogger.LogInfo("Can't get {0} game data from website. Profile may not be public.\n" +
                                        "To change this, go to <https://myaccount.ea.com/cp-ui/privacy/index>.",
                            _name.ToUpper());
                    }
					else
					{
                        CLogger.LogInfo("{0} {1} games found", ownedGames.Count, _name.ToUpper());
                    }
                }
            }


            // Get games installed by EA Desktop client

            string dbFile = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), EA_DB);
			if (!File.Exists(dbFile))
				CLogger.LogInfo("{0} installed game database not found.", _name.ToUpper());
			else
			{
				bool hwFail = false;
				string hwInfo = "";

				if (!GetHardwareInfo())
					hwFail = true;
				else
				{
					try
					{
						hwInfo = File.ReadAllText(_hwfile);
						if (string.IsNullOrEmpty(hwInfo))
                            hwFail = true;
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
						hwFail = true;
					}
				}

				if (hwFail)
                    CLogger.LogInfo("Could not get hardware info for {0} installed game database decryption.", _name.ToUpper());
				else
				{

					if (!DecryptISFile(dbFile, hwInfo, out string strDocumentData) && !string.IsNullOrEmpty(strDocumentData))
					{
						CLogger.LogInfo("Could not decrypt {0} database. You may need to open the {1} launcher to update it.", _name.ToUpper(), _name);
					}
					else
					{
						try
						{
							using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
							if (document.RootElement.TryGetProperty("installInfos", out JsonElement gameArray))
							{
								foreach (JsonElement game in gameArray.EnumerateArray())
								{
									bool dlc = false;
									bool apiFound = true;
									string slug = "";
									string regKey = "";
									string instPath = "";
									string exeFile = "";
									string imgUrl = "";
									string titleTmp = "";

									bool bInstalled = false;
									string strID = "";
									string strTitle = "";
									string strLaunch = "";
									string strUninstall = "";
									string strAlias = "";
									DateTime lastRun = DateTime.MinValue;

									instPath = GetStringProperty(game, "baseInstallPath");
									if (!string.IsNullOrEmpty(instPath))
										bInstalled = true;
									slug = GetStringProperty(game, "baseSlug");
									dlc = string.IsNullOrEmpty(GetStringProperty(game, "dlcSubPath")) ? false : true;
									if (dlc)
										continue;
									strID = GetStringProperty(game, "softwareId");

									foreach (string[] ownedGame in ownedGames)
									{
										if (ownedGame.Length > 1 && ownedGame[0].Equals(strID))
										{
											apiFound = true;
											strID = ownedGame[0];
											strTitle = ownedGame[1];
											// TODO: metadata description
											//if (ownedGame.Length > 2)
											//	string strDescription = ownedGame[2];
											if (!(ownedGame.Length > 3 && DateTime.TryParse(ownedGame[3], out lastRun)))
												lastRun = DateTime.MinValue;
											if (ownedGame.Length > 4 && !(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
												imgUrl = ownedGame[4];
										}
									}
									if (string.IsNullOrEmpty(strTitle))
									{
										TextInfo ti = new CultureInfo("en-US", false).TextInfo;
										strTitle = ti.ToTitleCase(slug.Replace("-", " "));
									}

									if (bInstalled)
									{
										dbGameFound = true;
										string exeCheck = GetStringProperty(game, "executableCheck");
										if (exeCheck.StartsWith('['))
										{
											int i = exeCheck.IndexOf(']');
											if (i > 1)
												regKey = exeCheck.Substring(1, i);
											exeFile = exeCheck[(i + 1)..];
											strLaunch = Path.Combine(instPath, exeFile);

											if (!apiFound)
											{
												if (!string.IsNullOrEmpty(regKey))
													titleTmp = GetRegStrVal(ToRegKey(ref regKey, out string regVal).OpenSubKey(regKey), GAME_DISPLAY_NAME);
												if (!string.IsNullOrEmpty(titleTmp))
													strTitle = titleTmp;
												else
												{
													string instFile = Path.Combine(instPath, @"__Installer\installerdata.xml");
													if (File.Exists(instFile))
													{
														XmlDocument doc = new();
														doc.Load(instFile);
														foreach (XmlNode gameTitle in doc.SelectNodes("//DiPManifest/gameTitles/gameTitle"))
														{
															if (gameTitle.Attributes["locale"] != null && gameTitle.Attributes["locale"].Value == EA_LANGDEF)
															{
																titleTmp = gameTitle.InnerText;
																if (!string.IsNullOrEmpty(titleTmp))
																	strTitle = titleTmp;
																break;
															}
														}
													}
												}
											}
										}

										game.TryGetProperty("localUninstallProperties", out JsonElement uninst);
										strUninstall = "\"" + GetStringProperty(uninst, "uninstallCommand") + "\"";
										string uninstParam = GetStringProperty(uninst, "uninstallParameters");
										if (!string.IsNullOrEmpty(uninstParam))
											strUninstall += " " + uninstParam;

										CLogger.LogDebug($"- {strTitle}");
										slug = slug.Replace("-", "");
										strAlias = GetAlias(Path.GetFileNameWithoutExtension(exeFile));
										if (strAlias.Length > slug.Length)
											strAlias = slug;
										if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
											strAlias = "";
										gameDataList.Add(
											new ImportGameData(strID, strTitle, strLaunch, strLaunch, strUninstall, strAlias, true, strPlatform, dateLastRun: lastRun));
									}
									else // not installed
									{
										CLogger.LogDebug($"- *{strTitle}");
										gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", "", false, strPlatform, dateLastRun: lastRun));
										// Use website to download missing icons
										if (!(string.IsNullOrEmpty(imgUrl) || (bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN))))
											CDock.DownloadCustomImage(strTitle, imgUrl);
									}
								}
							}
						}
						catch (Exception e)
						{
							CLogger.LogError(e, string.Format("Malformed {0} file.", _name.ToUpper()));
						}
					}
				}
            }

			if (!dbGameFound)
			{
				// If no installed EA Desktop games are found, check for games installed by Origin client

				List<RegistryKey> keyList = new();
				List<string> dirs = new();
				string path = "";
				try
				{
					path = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), ORIGIN_CONTENT);
					if (Directory.Exists(path))
					{
						dirs.AddRange(Directory.GetDirectories(path, "*.*", SearchOption.TopDirectoryOnly));
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e, string.Format("{0} directory read error: {1}", _name.ToUpper(), path));
				}

				CLogger.LogInfo("{0} {1} Origin games found", dirs.Count, _name.ToUpper());
				foreach (string dir in dirs)
				{
					string[] files = Array.Empty<string>();
					string install = "";

					string strID = "";
					string strTitle = Path.GetFileName(dir);
					string strLaunch = "";
					//string strIconPath = "";
					string strUninstall = "";
					string strAlias = "";

					try
					{
						files = Directory.GetFiles(dir, "*.mfst", SearchOption.TopDirectoryOnly);
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}

					foreach (string file in files)
					{
						strID = Path.GetFileName(file);
						try
						{
							string strDocumentData = File.ReadAllText(file);
							string[] subs = strDocumentData.Split('&');
							foreach (string sub in subs)
							{
								if (sub.StartsWith(ORIGIN_PATH))
								{
									install = sub[15..];
									break;
								}
							}
						}
						catch (Exception e)
						{
							CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
						}
					}

					if (!string.IsNullOrEmpty(install))
					{
						install = Uri.UnescapeDataString(install);

						using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
							RegistryView.Registry32).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
						{
							if (key != null)
							{	
								keyList = FindGameKeys(key, install, GAME_INSTALL_LOCATION, new string[] { EA_REG });
								foreach (var subKey in keyList)
								{
									strID = subKey.Name;
									strTitle = GetRegStrVal(subKey, GAME_DISPLAY_NAME);
									strLaunch = GetRegStrVal(subKey, GAME_DISPLAY_ICON).Trim(new char[] { ' ', '"' });
									strUninstall = GetRegStrVal(subKey, GAME_UNINSTALL_STRING); //.Trim(new char[] { ' ', '"' });
								}
							}
						}

						CLogger.LogDebug($"- {strTitle}");
						if (string.IsNullOrEmpty(strLaunch))
							strLaunch = CGameFinder.FindGameBinaryFile(install, strTitle);
						strAlias = GetAlias(Path.GetFileNameWithoutExtension(install));
						if (strAlias.Length > strTitle.Length)
							strAlias = GetAlias(strTitle);
						if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
							strAlias = "";

						if (!(string.IsNullOrEmpty(strLaunch)))
						{
							gameDataList.Add(
								new ImportGameData(strID, strTitle, strLaunch, strLaunch, strUninstall, strAlias, true, strPlatform));
						}
					}
				}
			}

			CLogger.LogDebug("----------------------");
		}

		public static string GetIconUrl(CGame game)
		{
			if (GetLogin(out string email, out SecureString password) && (!email.Equals("skipped") && !email.Equals("invalid") && !password.Equals(null) && password.Length > 0))
			{
				List<string[]> games = GetOwnedGames(email, password, GetGameID(game.ID)).Result;
				password.Dispose();
				if (games.Count <= 0)
				{
					CLogger.LogInfo("Can't get {0} game list. Profile may not be public.\n" +
									"To change this, go to <https://myaccount.ea.com/cp-ui/privacy/index>.",
						_name.ToUpper());
				}
				else
				{
					foreach (string[] gameArray in games)
					{
						if (gameArray.Length > 4 && !(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
							CDock.DownloadCustomImage(game.Title, gameArray[4]);
						break;
					}
				}
			}

			return "";
		}

		/// <summary>
		/// Scan the key name and extract the Origin game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Origin game ID as string</returns>
		public static string GetGameID(string key)
		{
			if (key.EndsWith(".mfst"))
			{
				return Path.GetFileNameWithoutExtension(key);
			}
			return key;
		}

		private static bool GetLogin(out string email, out SecureString password)
		{
			email = CConfig.GetConfigString(CConfig.CFG_ORIGINID);
			password = new();
			try
			{
				string encrypted = CConfig.GetConfigString(CConfig.CFG_ORIGINPW);
				if (!string.IsNullOrEmpty(encrypted))
				{
					Span<byte> byteSpan = Convert.FromBase64String(encrypted);
					if (OperatingSystem.IsWindows())
					{
						Array.ForEach(Encoding.UTF8.GetString(ProtectedData.Unprotect(byteSpan.ToArray(), null, DataProtectionScope.CurrentUser)).ToArray(), password.AppendChar);
					}
					else  // If we ever support other OSes, we will need to have a POSIX method to encrypt the password
						Array.ForEach(Encoding.UTF8.GetString(byteSpan).ToArray(), password.AppendChar);
					password.MakeReadOnly();
				}

				if (string.IsNullOrEmpty(email))
				{
					email = CDock.InputPrompt(_name + " e-mail >>> ", new());
					CDock.ClearInputLine(new());
				}

				if (string.IsNullOrEmpty(email))
					CConfig.SetConfigValue(CConfig.CFG_ORIGINID, "skipped");
				else if (!email.Contains('@') || !email.Contains('.'))
					CConfig.SetConfigValue(CConfig.CFG_ORIGINID, "invalid");
				else
				{
					CConfig.SetConfigValue(CConfig.CFG_ORIGINID, email);

					if (string.IsNullOrEmpty(encrypted))
					{
						string strPwd = CDock.InputPassword(_name + " password >>> ", new());
						if (!string.IsNullOrEmpty(strPwd))
						{
							Span<byte> byteSpan = new();
							if (OperatingSystem.IsWindows())
								byteSpan = ProtectedData.Protect(Encoding.UTF8.GetBytes(strPwd), null, DataProtectionScope.CurrentUser);
							else  // If we ever support other OSes, we will need to have a POSIX method to encrypt the password
								byteSpan = Encoding.UTF8.GetBytes(strPwd);
							password = new NetworkCredential("", strPwd).SecurePassword;
							password.MakeReadOnly();
							encrypted = Convert.ToBase64String(byteSpan);
							CConfig.SetConfigValue(CConfig.CFG_ORIGINPW, encrypted);
							byteSpan = null;
						}
						strPwd = null;
						CDock.ClearInputLine(new());
					}
				}
				encrypted = null;
				if (!string.IsNullOrEmpty(email) && password.Length > 0)
					return true;
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}

			return false;
		}

		[SupportedOSPlatform("windows")]
		private static string GetWMIValue(string property, string path)
		{
            ManagementObjectSearcher mos = new(new SelectQuery(string.Format("SELECT {0} FROM {1}", property, path)));
			foreach (ManagementObject result in mos.Get())
			{
				return result.GetPropertyValue(property).ToString();
				//break;
			}
            return "";
		}

		[SupportedOSPlatform("windows")]
		private static bool GetHardwareInfo()
		{
            StringBuilder sb = new();

			try
			{
				sb.Append(GetWMIValue(WMI_PROP_MFG, WMI_CLASS_MOBO));
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_SERIAL, WMI_CLASS_MOBO));
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_MFG, WMI_CLASS_BIOS));
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_SERIAL, WMI_CLASS_BIOS));
                sb.Append(';');
				sb.Append(GetVolumeInformationW(@"C:\", null!, 0, out uint drvsn, out _, out _, null!, 0) ? 
					drvsn.ToString("X", CultureInfo.InvariantCulture) : 
					"");
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_PNPID, WMI_CLASS_VID));
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_MFG, WMI_CLASS_PROC));
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_PROCID, WMI_CLASS_PROC));
                sb.Append(';');
                sb.Append(GetWMIValue(WMI_PROP_NAME, WMI_CLASS_PROC));
                sb.Append(';');

				string hwInfo = sb.ToString();
                CLogger.LogInfo("{0} Hardware Info: {1}", _name.ToUpper(), hwInfo);
				if (!File.Exists(_hwfile))
					File.WriteAllText(_hwfile, hwInfo);
				return true;
			}
			catch (Exception e)
			{
				CLogger.LogError(e, e.Message);
			}
			return false;
		}

		private static byte[] CalculateKey(string hwInfo)
		{
			int byteCount = 0;

            // Calculate SHA1 Hash of hardware string
            ReadOnlySpan<char> hwInfoSpan = hwInfo.AsSpan();
            byteCount = Encoding.ASCII.GetByteCount(hwInfoSpan);
            Span<byte> hwHashSpan = byteCount < 1024 ? stackalloc byte[byteCount] : new byte[byteCount];
            Encoding.ASCII.GetBytes(hwInfoSpan, hwHashSpan);
            Span<byte> hwBuff = stackalloc byte[20];
            SHA1.HashData(hwHashSpan, hwBuff);

            string hash = EA_KEY_PREFIX + Convert.ToHexString(hwBuff).ToLower(CultureInfo.InvariantCulture);

            // Calculate SHA3 256 Hash of full string
            byteCount = Encoding.ASCII.GetByteCount(hash);
            byte[] keyBuff = new byte[byteCount];
            Encoding.ASCII.GetBytes(hash.AsSpan(), keyBuff.AsSpan());

            using Sha3 sha3 = Sha3.Sha3256();
			return sha3.ComputeHash(keyBuff);
        }

        private static bool DecryptISFile(string dbFile, string hwInfo, out string decryptedText)
		{
            try
			{
                byte[] key = CalculateKey(hwInfo);
                byte[] encryptedText = File.ReadAllBytes(dbFile);

                // skips the first 64 bytes, because they contain a hash we don't need
                using MemoryStream ms = new(encryptedText, 64, encryptedText.Length - 64, writable: false);
				using Aes aes = Aes.Create();
				aes.Mode = CipherMode.CBC;
				aes.Key = key;
				aes.IV = _ea_iv;
                
                ICryptoTransform dec = aes.CreateDecryptor(aes.Key, aes.IV);
				using CryptoStream cs = new(ms, dec, CryptoStreamMode.Read);
				using StreamReader sr = new(cs);
				decryptedText = sr.ReadToEnd();
#if DEBUG
				File.WriteAllText($"tmp_{_name}.json", decryptedText);
#endif
				return true;
			}
			catch (Exception e)
			{
				CLogger.LogError(e, string.Format("Malformed {0} file.", _name.ToUpper()));
			}
			decryptedText = "";
            return false;
		}

		private static async Task<List<string[]>> GetOwnedGames(string email, SecureString password, string id = "")
		{
			List<string[]> games = new();
			if (!email.Equals("skipped") && !email.Equals("invalid") && !password.Equals(null) && password.Length > 0)
			{
				bool success = false;
				XmlDocument doc = new();
				ulong userid = 0;
				string country = "";
				//string language = "";
				string locale = "";
				try
				{
					OriginAPI originApi = new(email, new NetworkCredential("", password).Password);
					bool result = await originApi.LoginAsync();
					if (!result)
					{
						CLogger.LogWarn("Login error for {0} owned games.", _name.ToUpper());
					}
					else
					{
						userid = originApi.InternalUser.UserId;
						country = originApi.InternalUser.Country;       // "US"
						if (string.IsNullOrEmpty(country))
							country = ORIGIN_CTRYDEF;
						//language = originApi.InternalUser.Language;	// "en"
						locale = originApi.InternalUser.Locale;         // "en_US"
						if (string.IsNullOrEmpty(locale))
							locale = EA_LANGDEF;

						/*
						OriginUser user = await originApi.GetUserAsync(userid);
						CLogger.LogDebug("persona: {0}, name: {1}, avatar: {2}", user.PersonaId.ToString(), user.Username, await user.GetAvatarUrlAsync());
						*/
						/*
						//Random rand = new Random();
						//rand.Next(1, 5);
						//HttpRequestMessage request4 = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api1.origin.com/atom/users?userIds={userid}");
						//HttpRequestMessage request = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api1.origin.com/atom/users/{userid}/appSettings");
						HttpRequestMessage request = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api4.origin.com/atom/users/{userid}/privacySettings");
						HttpResponseMessage response = await BaseAPIManager.SendAsync(request);
						if (response.StatusCode != HttpStatusCode.OK)
							CLogger.LogWarn("Communication error for {0} privacy settings: {1}", _name.ToUpper(), response.StatusCode);
						else
						{
							string content = await response.Content.ReadAsStringAsync();
							CLogger.LogDebug("  {0} 1 content:\n{1}", _name, content);
						}
						*/
#if DEBUG
						// Don't re-download if file exists
						string tmpfile = $"tmp_{_name}_api.xml";
						if (!File.Exists(tmpfile))
						{
#endif
							//rand.Next(1, 5);
							HttpRequestMessage request2 = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api2.origin.com/ecommerce2/consolidatedentitlements/{userid}?machine_hash=1");
							request2.Headers.Add("accept", new List<string> { "application/vnd.origin.v3+json", "x-cache/force-write" });
							HttpResponseMessage response2 = await BaseAPIManager.SendAsync(request2);
							if (response2.StatusCode != HttpStatusCode.OK)
								CLogger.LogWarn("Communication error for {0} owned games: {1}", _name.ToUpper(), response2.StatusCode);
							else
							{
								success = true;
								StreamReader content2stream = new(await response2.Content.ReadAsStreamAsync()); //Transfer-Encoding: chunked
								byte[] content2byte = Encoding.UTF8.GetBytes(content2stream.ReadToEnd());
								//CLogger.LogDebug("  {0} 2 content:\n{1}", _name, Encoding.UTF8.GetString(content2byte.ToArray()));
								doc.LoadXml(Encoding.UTF8.GetString(content2byte.ToArray()));
#if DEBUG
								File.WriteAllBytes(tmpfile, content2byte);
							}
						}
						else
						{
							success = true;
							doc.Load(tmpfile);
#endif
						}
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e, string.Format("API error for {0} owned games.", _name.ToUpper()));
				}

				if (!success)
					return new();

				/*
				//rand.Next(1, 5);
				HttpRequestMessage request3 = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api1.origin.com/ecommerce2/entitlements/{userid}/associations/pending");
				HttpResponseMessage response3 = await BaseAPIManager.SendAsync(request3);
				if (response3.StatusCode != HttpStatusCode.OK)
					CLogger.LogWarn("Communication error for {0} owned games: {1}", _name.ToUpper(), response3.StatusCode);
				else
				{
					string content3 = await response3.Content.ReadAsStringAsync();
					CLogger.LogDebug("  {0} 3 content:\n{1}", _name, content3);
				}
				*/

				XmlNodeList gameList = doc.DocumentElement.SelectNodes("/entitlements/entitlement");
				if (gameList == null || gameList.Count <= 0)
					return new();

				//CLogger.LogDebug("{0} owned games (user #{1}):", _name.ToUpper(), userid);

				string strID = "";
				string strTitle = "";
				string strDescription = "";
				string iconUrl = "";
				string mdmId = "";
				string lastRun = "";

				foreach (XmlNode game in gameList)
				{
					try
					{
						strID = game.SelectSingleNode("offerId").InnerText; // if found later, we'll use softwareId instead of offerId
						/*
						// we're now looking for softwareId instead of offerId
						if (!string.IsNullOrEmpty(id) && !strID.Equals(id))  // used when looking for a particular game ID
							continue;
						*/

						// skip DLCs
						//if (!game.SelectSingleNode("originDisplayType").InnerText.Equals("Full Game"))	// DLCs will be "Expansion" or "Addon"
						if (!game.SelectSingleNode("entitlementTag").InnerText.Equals("ORIGIN_DOWNLOAD"))   // DLCs will be XPACK{#}_ACCESS
							continue;

						iconUrl = game.SelectSingleNode("offer/customAttributes/imageServer").InnerText;

						XmlNode node3 = game.SelectSingleNode("offer/localizableAttributes");
						if (node3 == null)
							continue;

						XmlNode descNode = node3.SelectSingleNode("shortDescription");
						if (descNode != null)
							strDescription = descNode.InnerText;
						XmlNode titleNode = node3.SelectSingleNode("displayName");
						if (titleNode != null)
							strTitle = titleNode.InnerText;
						XmlNode imageNode = node3.SelectSingleNode("packArtLarge");
						if (imageNode != null)
							iconUrl += imageNode.InnerText;

                        XmlNode node4 = game.SelectSingleNode("offer/publishing/softwareList");
                        XmlNodeList swList = node4.SelectNodes("software");
						foreach (XmlNode sw in node4)
						{
							foreach (XmlAttribute swAttrib in sw.Attributes)
							{
								if (swAttrib.LocalName.Equals("softwarePlatform") && swAttrib.Value.Equals("PCWIN"))
								{
									strID = sw.SelectSingleNode("softwareId").InnerText;
                                    if (!string.IsNullOrEmpty(id) && !strID.Equals(id))  // used when looking for a particular game ID
                                        continue;
                                }
                            }
						}

						// Get lastRun timestamp
                        XmlNode node5 = game.SelectSingleNode("offer/mdmHierarchies");
						XmlNodeList mdmList = node5.SelectNodes("mdmHierarchy");
						foreach (XmlNode mdm in node5)
						{
							foreach (XmlAttribute mdmAttrib in mdm.Attributes)
							{
								if (mdmAttrib.LocalName.Equals("type") && mdmAttrib.Value.Equals("Primary"))
								{
									foreach (XmlAttribute titleAttrib in mdm.SelectSingleNode("mdmMasterTitle").Attributes)
									{
										if (titleAttrib.LocalName.Equals("masterTitleId"))
										{
											mdmId = titleAttrib.Value;

											//rand.Next(1, 5);
											HttpRequestMessage request4 = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api1.origin.com/atom/users/{userid}/games/{mdmId}/usage");
											request4.Headers.Add("X-Origin-Platform", "PCWIN");
											HttpResponseMessage response4 = await BaseAPIManager.SendAsync(request4);
											if (response4.StatusCode != HttpStatusCode.OK)
												CLogger.LogWarn("Communication error for {0} game usage: {1}", _name.ToUpper(), response4.StatusCode);
											else
											{
												string content4 = await response4.Content.ReadAsStringAsync();
												//CLogger.LogDebug("  {0} 4 content:\n{1}", _name, content4);
												XmlDocument doc2 = new();
												doc2.LoadXml(content4);
												XmlNode usage = doc2.DocumentElement.SelectSingleNode("/usage");
												if (!usage.SelectSingleNode("gameId").InnerText.Equals(mdmId))
													continue;
#if DEBUG
												//File.WriteAllText($"tmp_{_name}_usage{mdmId}.xml", content4);
#endif
												lastRun = usage.SelectSingleNode("lastSessionEndTimeStamp").InnerText;
												//CLogger.LogDebug($"lastRun: {lastRun}");
											}
											break;
										}
									}
								}
							}
						}

						/*
						//rand.Next(1, 5);
						HttpRequestMessage request5 = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api4.origin.com/ecommerce2/offerUpdatedDate?offerIds={game}");
						HttpResponseMessage response5 = await BaseAPIManager.SendAsync(request5);
						if (response5.StatusCode != HttpStatusCode.OK)
							CLogger.LogWarn("Communication error for {0} owned games: {1}", _name.ToUpper(), response5.StatusCode);
						else
						{
							string content5 = await response5.Content.ReadAsStringAsync();
							//CLogger.LogDebug("  {0} 5 content:\n{1}", _name, content5);
						}
						*/

                        //CLogger.LogDebug($"- id:{strID} | title:{strTitle} | lastRun:{lastRun}");
                        games.Add(new string[] { strID, strTitle, strDescription, lastRun, iconUrl });
					}
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("API error for {0} owned games.", _name.ToUpper()));
					}
				}

				/*
				//rand.Next(1, 5);
				string file = $"supercat-PCWIN_MAC-{country}-{locale}.json.gz";
				if (!File.Exists(file))
				{
					HttpRequestMessage request6 = BaseAPIManager.CreateRequest(HttpMethod.Get, $"https://api2.origin.com/supercat/${country}/{locale}/{file}");
					HttpResponseMessage response6 = await BaseAPIManager.SendAsync(request6);
					if (response6.StatusCode != HttpStatusCode.OK)
						CLogger.LogWarn("Communication error for {0} owned games: {1}", _name.ToUpper(), response6.StatusCode);
					else
					{
						string content6 = await response6.Content.ReadAsStringAsync();
						//CLogger.LogDebug("  {0} 6 content:\n{1}", _name, content6);
					}
				}
				*/

				return games;
			}

			return new();
		}
#nullable enable
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern bool GetVolumeInformationW(
			string lpRootPathName,
			StringBuilder? lpVolumeNameBuffer,
			int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
			out uint lpFileSystemFlags,
            StringBuilder? lpFileSystemNameBuffer,
            int nFileSystemNameSize);
    }
#nullable restore

	public class OriginResponse
	{
		public class Entitlement
		{
			public class OriginEntitlement
			{
				public long entitlementId;
				public string offerId;
				public string offerPath;
				public string status;
				public string offerType;
				public string originDisplayType;
				public string masterTitleId;
				public string gameDistributionSubType;
			}

			public string error;
			public List<OriginEntitlement> entitlements;
		}
	}
}