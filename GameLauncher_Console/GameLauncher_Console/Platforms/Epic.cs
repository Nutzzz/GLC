using Logger;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// Epic Games Launcher
	// [owned and installed games]
	// [also with support for Legendary https://legendary.gl/github]
	public class PlatformEpic : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Epic;
		public const string PROTOCOL			= "com.epicgames.launcher://";
		private const string START_GAME			= PROTOCOL + @"apps/";
		private const string START_GAME_ARGS	= "?action=launch&silent=true";
		private const string INSTALL_GAME_ARGS	= "?action=install";
		//private const string EPIC_GAMES_UNREG	= "{A2FB1E1A-55D9-4511-A0BF-DEAD0493FBBC}"; // HKLM32 Uninstall
		//private const string EPIC_GAMES_UNREG	= "{A7BBC0A6-3DB0-41CC-BCED-DDFC5D4F3060}"; // HKLM32 Uninstall
		private const string EPIC_ITEMS 		= @"Epic\EpicGamesLauncher\Data\Manifests"; // ProgramData
		private const string EPIC_CATALOG		= @"Epic\EpicGamesLauncher\Data\Catalog\catcache.bin";  // ProgramData

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
			//bool useEGL = (bool)CConfig.GetConfigBool(CConfig.CFG_USEEGL);
			bool useLeg = (bool)CConfig.GetConfigBool(CConfig.CFG_USELEG);
			string pathLeg = CConfig.GetConfigString(CConfig.CFG_PATHLEG);
			if (string.IsNullOrEmpty(pathLeg))
				useLeg = false;
			if (!pathLeg.Contains(@"\") && !pathLeg.Contains("/")) // legendary.exe in current directory
				pathLeg = Path.Combine(Directory.GetCurrentDirectory(), pathLeg);

			string id = GetGameID(game.ID);
			if (useLeg && File.Exists(pathLeg))
			{
				if (OperatingSystem.IsWindows())
				{
					CLogger.LogInfo($"Launch: cmd.exe /c \"" + pathLeg + "\" -y install " + id);
					CDock.StartAndRedirect("cmd.exe", "/c '\"" + pathLeg + "\" -y install " + id);
				}
				else
				{
					CLogger.LogInfo($"Launch: " + pathLeg + " -y install " + id);
					Process.Start(pathLeg, "-y install " + id);
				}
				return 1;
			}
			else //if (useEGL)
			{
				if (OperatingSystem.IsWindows())
					_ = CDock.StartShellExecute(START_GAME + id + INSTALL_GAME_ARGS);
				else
					_ = Process.Start(START_GAME + id + INSTALL_GAME_ARGS);
				return 1;
			}
			//return 0;
		}

		public static void StartGame(CGame game)
        {
			bool useEGL = (bool)CConfig.GetConfigBool(CConfig.CFG_USEEGL);
			bool useLeg = (bool)CConfig.GetConfigBool(CConfig.CFG_USELEG);
			bool syncLeg = (bool)CConfig.GetConfigBool(CConfig.CFG_SYNCLEG);
			string pathLeg = CConfig.GetConfigString(CConfig.CFG_PATHLEG);
			if (string.IsNullOrEmpty(pathLeg))
				useLeg = false;
			if (!pathLeg.Contains(@"\") && !pathLeg.Contains("/")) // legendary.exe in current directory
				pathLeg = Path.Combine(Directory.GetCurrentDirectory(), pathLeg);

			string id = GetGameID(game.ID);
			if (useLeg && File.Exists(pathLeg))
			{
				if (OperatingSystem.IsWindows())
				{
					string cmdLine = "\"" + pathLeg + "\" -y launch " + id;
					CLogger.LogInfo($"Launch: cmd.exe /c " + cmdLine);
					if (syncLeg)
						cmdLine = "\"" + pathLeg + "\" -y sync-saves " + id + " & " + cmdLine + " & \"" + pathLeg + "\" -y sync-saves " + id;
					CDock.StartAndRedirect("cmd.exe", "/c '" + cmdLine + " '");
				}
				else
				{
					CLogger.LogInfo($"Launch: " + pathLeg + " -y launch " + id);
					if (syncLeg)
						Process.Start(pathLeg, "-y sync-saves " + id);
					Process.Start(pathLeg, "-y launch " + id);
					if (syncLeg)
						Process.Start(pathLeg, "-y sync-saves " + id);
				}
			}
			else if (useEGL)
            {
				CLogger.LogInfo($"Launch: {0}", PROTOCOL + START_GAME + id + START_GAME_ARGS);
				if (OperatingSystem.IsWindows())
					_ = CDock.StartShellExecute(START_GAME + id + START_GAME_ARGS);
				else
					_ = Process.Start(PROTOCOL + START_GAME + id + START_GAME_ARGS);
            }
			else
			{
				CLogger.LogInfo($"Launch: {game.Launch}");
				if (OperatingSystem.IsWindows())
					_ = CDock.StartShellExecute(game.Launch);
				else
					_ = Process.Start(game.Launch);
			}
		}

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		public static int UninstallGame(CGame game)
        {
			//bool useEGL = (bool)CConfig.GetConfigBool(CConfig.CFG_USEEGL);
			bool useLeg = (bool)CConfig.GetConfigBool(CConfig.CFG_USELEG);
			string pathLeg = CConfig.GetConfigString(CConfig.CFG_PATHLEG);
			if (string.IsNullOrEmpty(pathLeg))
				useLeg = false;
			if (!pathLeg.Contains(@"\") && !pathLeg.Contains("/")) // legendary.exe in current directory
				pathLeg = Path.Combine(Directory.GetCurrentDirectory(), pathLeg);
			if (useLeg && File.Exists(pathLeg))
			{
				string id = GetGameID(game.ID);
				//Process ps;
				if (OperatingSystem.IsWindows())
				{
					CLogger.LogInfo("Launch: cmd.exe /c \"" + pathLeg + "\" -y uninstall " + id);
					CDock.StartAndRedirect("cmd.exe", "/c \"" + pathLeg + "\" -y uninstall " + id);
				}
				else
				{
					CLogger.LogInfo("Launch: " + pathLeg + " -y uninstall " + id);
					Process.Start(pathLeg, "-y uninstall " + id);
				}
				/*
				ps.WaitForExit(30000);
				if (ps.ExitCode == 0)
				*/
					return 1;
			}
			/*
			else if (useEGL)
            {
				Launch();
				return -1;
			}
			*/
			else if (!string.IsNullOrEmpty(game.Uninstaller))
            {
				// delete Desktop icon
				File.Delete(Path.Combine(GetFolderPath(SpecialFolder.Desktop), game.Title + ".lnk"));
				string[] un = game.Uninstaller.Split(';');

				// delete manifest file
				if (un.Length > 1 && !string.IsNullOrEmpty(un[1]) && un[1].EndsWith(".item"))
					File.Delete(Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), EPIC_ITEMS, un[1]));

				if (un.Length > 0 && !string.IsNullOrEmpty(un[0]) && !un[0].EndsWith(".item"))
				{
					DirectoryInfo rootDir = new(un[0]);
					if (rootDir.Exists)
					{
						/*
						foreach (DirectoryInfo dir in rootDir.EnumerateDirectories())
							dir.Delete(true);
						foreach (FileInfo file in rootDir.EnumerateFiles())
							file.Delete();
						*/
						rootDir.Delete(true);
						return 1;
					}
				}
            }
			return 0;
		}

		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
			List<string> epicIds = new();
			string strPlatform = GetPlatformString(ENUM);
			string dir = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), EPIC_ITEMS);
			if (!Directory.Exists(dir))
			{
				CLogger.LogInfo("{0} games not found in ProgramData.", _name.ToUpper());
				return;
			}

			string[] files = Directory.GetFiles(dir, "*.item", SearchOption.TopDirectoryOnly);
			CLogger.LogInfo("{0} {1} games found", files.Length, _name.ToUpper());

			foreach (string file in files)
			{
				string strDocumentData = File.ReadAllText(file);

				if (string.IsNullOrEmpty(strDocumentData))
					continue;

				try
				{
					using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
					string id = GetStringProperty(document.RootElement, "AppName");
					if (string.IsNullOrEmpty(id))
						id = Path.GetFileName(file);
					string strID = $"epic_{id}";
					string strTitle = GetStringProperty(document.RootElement, "DisplayName");
					CLogger.LogDebug($"- {strTitle}");
					string strLaunch = GetStringProperty(document.RootElement, "LaunchExecutable"); // DLCs won't have this set
					string strUninstall = "";
					string strAlias = "";

					if (!string.IsNullOrEmpty(strLaunch))
					{
						epicIds.Add(id);
						strUninstall = GetStringProperty(document.RootElement, "InstallLocation");
						strLaunch = Path.Combine(strUninstall, strLaunch);
						strUninstall += ";" + Path.GetFileName(file);
						// rather than an uninstaller like most platforms, for Epic, strUninstall will hold two fields: the install location and the manifest file
						strAlias = GetAlias(GetStringProperty(document.RootElement, "MandatoryAppFolderName"));
						if (strAlias.Length > strTitle.Length)
							strAlias = GetAlias(strTitle);
						if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
							strAlias = "";
						gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strLaunch, strUninstall, strAlias, true, strPlatform));
					}
				}
				catch (Exception e)
				{
					CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
				}

			}

			// Get not-installed games
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY))
			{
				bool useLeg = (bool)CConfig.GetConfigBool(CConfig.CFG_USELEG);
				string pathLeg = CConfig.GetConfigString(CConfig.CFG_PATHLEG);
				if (string.IsNullOrEmpty(pathLeg))
					useLeg = false;
				if (!pathLeg.Contains(@"\") && !pathLeg.Contains("/")) // legendary.exe in current directory
					pathLeg = Path.Combine(Directory.GetCurrentDirectory(), pathLeg);
				if (!File.Exists(pathLeg))
					useLeg = false;

				Dictionary<string, List<string>> epicDict = new();
				bool success = false;

				if (!(useLeg && (bool)CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
				{
					string catalogPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), EPIC_CATALOG);

					if (!File.Exists(catalogPath))
						CLogger.LogInfo("{0} catalog not found in ProgramData.", _name.ToUpper());
					else
					{
						try
						{
							// Decode catalog file
							Span<byte> byteSpan = File.ReadAllBytes(catalogPath);
							/*
#if DEBUG
							File.WriteAllBytes($"tmp_{_name}_catalog.txt", byteSpan.ToArray());
#endif
							*/
							OperationStatus status = Base64.DecodeFromUtf8InPlace(byteSpan, out int numBytes);
							if (status == OperationStatus.Done)
							{
								byteSpan = byteSpan.Slice(0, numBytes);
#if DEBUG
								File.WriteAllBytes($"tmp_{_name}_catalog.json", byteSpan.ToArray());
#endif
								string strCatalogData = Encoding.UTF8.GetString(byteSpan);
								using JsonDocument document = JsonDocument.Parse(strCatalogData, jsonTrailingCommas);
								foreach (JsonElement element in document.RootElement.EnumerateArray())
								{
									// Skip if this is a DLC
									if (element.TryGetProperty("mainGameItem", out JsonElement dlc) &&
										!string.IsNullOrEmpty(GetStringProperty(dlc, "namespace")))
										continue;
									string id = "";
									string iconUrl = "";
									string iconWideUrl = "";
									// File seems to be encoded to Base64 improperly, so we need to remove Unicode replacement character
									string strTitle = GetStringProperty(element, "title").Replace("?", "");
									if (string.IsNullOrEmpty(strTitle))
										continue;
									// TODO: metadata developer
									//string strDeveloper = GetStringProperty(element, "developer");
									if (element.TryGetProperty("keyImages", out JsonElement imageArray))
									{
										foreach (JsonElement image in imageArray.EnumerateArray())
										{
											if (GetStringProperty(image, "type").Equals("DieselGameBox"))
											{
												iconWideUrl = GetStringProperty(image, "url");
											}
											if (GetStringProperty(image, "type").Equals("DieselGameBoxTall"))
											{
												iconUrl = GetStringProperty(image, "url");
												break;
											}
										}
									}
									if (string.IsNullOrEmpty(iconUrl))
									{
										if (string.IsNullOrEmpty(iconWideUrl))
											continue;
										else
											iconUrl = iconWideUrl;
									}
									if (element.TryGetProperty("releaseInfo", out JsonElement releaseArray))
									{
										foreach (JsonElement release in releaseArray.EnumerateArray())
										{
											id = GetStringProperty(release, "appId");
											break;
										}
									}
									if (string.IsNullOrEmpty(id))
										continue;
									epicDict.Add(id, new() { strTitle, iconUrl });
									//CLogger.LogDebug($"{strTitle} [{id}]: {iconUrl}");
								}
							}
						}
						catch (Exception e)
                        {
							CLogger.LogError(e, string.Format("Malformed {0} catalog: {1}", _name.ToUpper(), catalogPath));
						}
					}
				}
				if (epicDict.Count > 1)
					success = true;

				bool successleg = false;
				string tmpfile = $"tmp_{_name}.json";
				string errfile = $"tmp_{_name}.err";
				if (!useLeg)
				{
					if (!success)
					{
						CLogger.LogInfo("Can't get not-installed {0} games. Run Epic Games Launcher or install Legendary: " +
							"<https://legendary.gl/release/latest>",
							_name.ToUpper());
					}
				}
				else
				{
					try
					{
						Process ps;
						if (OperatingSystem.IsWindows())
						{
							CLogger.LogInfo("Launch: cmd.exe /c \"" + pathLeg + "\" list --json 1> " + tmpfile + " 2> " + errfile);
							ps = CDock.StartAndRedirect("cmd.exe", "/c \"" + pathLeg + "\" list --json 1> " + tmpfile + " 2> " + errfile);
						}
						else
						{
							CLogger.LogInfo("Launch: " + pathLeg + " list 1> " + tmpfile + " 2> " + errfile);
							ps = Process.Start(pathLeg, "list 1> " + tmpfile + " 2> " + errfile);
						}
						ps.WaitForExit(30000);
						if (File.Exists(tmpfile))
							CLogger.LogInfo("Error getting not-installed {0} games from Legendary.", _name.ToUpper());
						else
						{
							if (ps.ExitCode == 0)
								successleg = true;
							else
							{
								if (File.Exists(errfile))
								{
									string strErrorData = File.ReadAllText(errfile);
									if (!string.IsNullOrEmpty(strErrorData))
									{
										using StreamReader reader = new(errfile);
										string line;
										while ((line = reader.ReadLine()) != null)
										{
											if (line.Trim().EndsWith("No saved credentials"))
											{
												CLogger.LogInfo("Error getting {0} games from Legendary. Is Legendary authenticated?\n" +
																$"To login, enter: \"{pathLeg}\" auth",
													_name.ToUpper());
											}
										}
									}
								}
							}
						}
					}
					catch (Exception e)
					{
						CLogger.LogError(e, string.Format("Failure getting not-installed {0} games from Legendary.", _name.ToUpper()));
					}
				}
#if !DEBUG
				File.Delete(errfile);
				File.Delete(tmpfile);
#endif
				if (successleg)
				{
					string strDocumentData = File.ReadAllText(tmpfile);
					if (!string.IsNullOrEmpty(strDocumentData))
					{
						CLogger.LogDebug("{0} not-installed games:", _name.ToUpper());
						using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
						foreach (JsonElement element in document.RootElement.EnumerateArray())
						{
							string id = GetStringProperty(element, "app_name");
							if (epicIds.Contains(id)) // Check if game is already installed
								continue;
							string strID = $"epic_{id}";
							string strTitle = GetStringProperty(element, "app_title");
							CLogger.LogDebug($"- *{strTitle}");
							string strAlias = GetAlias(strTitle);
							if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
								strAlias = "";

							if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
							{
								if (success && epicDict.TryGetValue(id, out List<string> epicGame))
								{
									if (epicGame.Count > 1 && !string.IsNullOrEmpty(epicGame[1]))
										CDock.DownloadCustomImage(strTitle, epicGame[1]);
								}
							}
							gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", strAlias, false, strPlatform));
						}
					}
				}
				else if (success)
                {
					CLogger.LogDebug("{0} not-installed games:", _name.ToUpper());
					foreach (KeyValuePair<string, List<string>> epicGame in epicDict)
					{
						string id = epicGame.Key;
						string strID = $"epic_{id}";
						string strTitle = "";
						if (epicIds.Contains(id)) // Check if game is already installed
							continue;
						if (epicGame.Value.Count < 1)
							continue;
						strTitle = epicGame.Value[0];
						CLogger.LogDebug($"- *{strTitle}");
						string strAlias = GetAlias(strTitle);
						if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
							strAlias = "";

						if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
						{
							if (epicGame.Value.Count > 1)
							{
								string iconUrl = epicGame.Value[1];
								if (!string.IsNullOrEmpty(iconUrl))
									CDock.DownloadCustomImage(strTitle, iconUrl);
							}
						}
						gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", strAlias, false, strPlatform));
					}
				}
			}

			CLogger.LogDebug("--------------------");
		}

		public static string GetIconUrl(CGame game)
		{
			string catalogPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), EPIC_CATALOG);
			if (!File.Exists(catalogPath))
			{
				CLogger.LogInfo("{0} catalog not found in ProgramData.", _name.ToUpper());
				return "";
			}

			try
			{
				// Decode catalog file
				Span<byte> byteSpan = File.ReadAllBytes(catalogPath);
				OperationStatus os = Base64.DecodeFromUtf8InPlace(byteSpan, out int numBytes);
				if (os == OperationStatus.Done)
				{
					byteSpan = byteSpan.Slice(0, numBytes);
					string strCatalogData = Encoding.UTF8.GetString(byteSpan);
					using JsonDocument document = JsonDocument.Parse(strCatalogData, jsonTrailingCommas);
					foreach (JsonElement element in document.RootElement.EnumerateArray())
					{
						string id = "";
						if (element.TryGetProperty("releaseInfo", out JsonElement releaseArray))
						{
							foreach (JsonElement release in releaseArray.EnumerateArray())
							{
								id = GetStringProperty(release, "appId");
								break;
							}
						}
						if (!id.Equals(GetGameID(game.ID)))
							continue;
						
						string iconUrl = "";
						string iconWideUrl = "";
						if (element.TryGetProperty("keyImages", out JsonElement imageArray))
						{
							foreach (JsonElement image in imageArray.EnumerateArray())
							{
								if (GetStringProperty(image, "type").Equals("DieselGameBox"))
									iconWideUrl = GetStringProperty(image, "url");

								if (GetStringProperty(image, "type").Equals("DieselGameBoxTall"))
								{
									iconUrl = GetStringProperty(image, "url");
									break;
								}
							}
						}
						if (string.IsNullOrEmpty(iconUrl))
						{
							if (string.IsNullOrEmpty(iconWideUrl))
								continue;
							else
								iconUrl = iconWideUrl;
						}

						return iconUrl;
						//break;
					}
				}
			}
			catch (Exception e)
            {
				CLogger.LogError(e, string.Format("Malformed {0} catalog: {1}", _name.ToUpper(), catalogPath));
			}

			CLogger.LogInfo("Icon for {0} game \"{1}\" not found in catalog.", _name.ToUpper(), game.Title);
			return "";
		}

		/// <summary>
		/// Scan the key name and extract the Epic game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Epic game ID as string</returns>
		public static string GetGameID(string key)
		{
			if (key.StartsWith("epic_"))
				return key[5..];
			return key;
		}
	}
}