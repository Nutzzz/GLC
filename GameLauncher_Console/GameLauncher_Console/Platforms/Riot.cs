using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CGameFinder;
using static GameLauncher_Console.CJsonWrapper;
using static System.Environment;

namespace GameLauncher_Console
{
	// Riot Games
	// [installed games only]
	public class PlatformRiot : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Riot;
		public const string PROTOCOL			= "riotclient://";
		private const string RIOT_FOLDER		= "Riot Games";            // ProgramData
		private const string RIOT_METADATA		= "Metadata";
		private const string RIOT_CLIENT_FILE	= "RiotClientInstalls.json";
		//private const string RIOT_UNREG			= "Riot Game [*].live";		// HKCU64 Uninstall

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
			string dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), RIOT_FOLDER);

			try
			{
				if (Directory.Exists(dataPath))
				{
					// Should we use YamlDotNet parser?

					string strClientPath = "";

					var clientFile = Path.Combine(dataPath, RIOT_CLIENT_FILE);
					if (File.Exists(clientFile))
					{
						string strDocumentData = File.ReadAllText(clientFile);

						if (!string.IsNullOrEmpty(strDocumentData))
						{
							using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
							strClientPath = GetStringProperty(document.RootElement, "rc_live");
						}
					}

					string metaPath = Path.Combine(dataPath, "Metadata");
					if (Directory.Exists(metaPath))
					{
						foreach (var dir in Directory.EnumerateDirectories(metaPath))
						{
							string path = "";
							string strID = Path.GetFileName(dir);
							string strTitle = "";
							string strLaunch = "";
							string strIconPath = "";
							string strUninstall = "";
							string strAlias = "";

							foreach (var settingsFile in Directory.EnumerateFiles(dir, "*.yaml"))
							{
								foreach (string line in File.ReadLines(settingsFile))
								{
									if (line.StartsWith("product_install_full_path"))
										path = line[line.IndexOf('"')..].Trim().Trim('"');
									else if (line.StartsWith("shortcut_name"))
										strTitle = Path.GetFileNameWithoutExtension(line[line.IndexOf('"')..].Trim().Trim('"'));
								}
								break;
							}
							foreach (var iconFile in Directory.EnumerateFiles(dir, "*.ico"))
							{
								strIconPath = iconFile;
								break;
							}
							if (!string.IsNullOrEmpty(strTitle))
							{
								strLaunch = FindGameBinaryFile(path, strTitle);
								if (!string.IsNullOrEmpty(strLaunch))
								{
									if (string.IsNullOrEmpty(strIconPath))
										strIconPath = strLaunch;
									if (!string.IsNullOrEmpty(strClientPath))
										strUninstall = "\"" + strClientPath + "\" --uninstall-product=" + Path.GetFileNameWithoutExtension(strID) + " --uninstall-patchline=live";
									strAlias = GetAlias(strTitle);
									if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
										strAlias = "";
									gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strIconPath, strUninstall, strAlias, true, strPlatform));
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
			CLogger.LogDebug("------------------------");
		}

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		public static string GetGameID(string key) => key;
	}
}