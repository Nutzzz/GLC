using GameFinder.RegistryUtils;
using GameCollector.StoreHandlers.EGS;
using Logger;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;
using GameFinder.Common;

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
			CDock.DeleteCustomImage(game.Title, justBackups: false);
			//bool useEGL = (bool)CConfig.GetConfigBool(CConfig.CFG_USEEGL);
			bool useLeg = (bool)CConfig.GetConfigBool(CConfig.CFG_USELEG);
			string pathLeg = CConfig.GetConfigString(CConfig.CFG_PATHLEG);
			if (string.IsNullOrEmpty(pathLeg))
				useLeg = false;
			if (!pathLeg.Contains('\\') && !pathLeg.Contains('/')) // legendary.exe in current directory
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
			if (!pathLeg.Contains('\\') && !pathLeg.Contains('/')) // legendary.exe in current directory
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
			if (!pathLeg.Contains('\\') && !pathLeg.Contains('/')) // legendary.exe in current directory
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

        [SupportedOSPlatform("windows")]
        public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
		{
            string strPlatform = GetPlatformString(ENUM);

			EGSHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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
					byteSpan = byteSpan[..numBytes];
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