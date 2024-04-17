using GameCollector.StoreHandlers.IGClient;
using GameFinder.Common;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	// Indiegala Client
	// [owned and installed games]
	public class PlatformIGClient : IPlatform
	{
		public const GamePlatform ENUM		= GamePlatform.IGClient;
		public const string PROTOCOL		= "";
		private const string IG_REG			= @"SOFTWARE\6f4f090a-db12-53b6-ac44-9ecdb7703b4a"; // HKLM64
		private const string IG_OWN_JSON	= @"IGClient\config.json"; // AppData\Roaming
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64).OpenSubKey(IG_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM64
                Process igcProcess = new();
                string launcherPath = Path.Combine(GetRegStrVal(key, GAME_INSTALL_LOCATION), "IGClient.exe");
                if (File.Exists(launcherPath))
                    CDock.StartAndRedirect(launcherPath);
                else
                {
                    //SetFgColour(cols.errorCC, cols.errorLtCC);
                    CLogger.LogWarn("Cannot start {0} launcher.", _name.ToUpper());
                    Console.WriteLine("ERROR: Launcher couldn't start. Is it installed properly?");
                    //Console.ResetColor();
                }
            }
		}

        // return value
        // -1 = not implemented
        // 0 = failure
        // 1 = success
        public static int InstallGame(CGame game)
		{
			CDock.DeleteCustomImage(game.Title, justBackups: false);
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

        //[SupportedOSPlatform("windows")]
        public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
        {
            string strPlatform = GetPlatformString(ENUM);

            IGClientHandler handler = new(FileSystem.Shared, null); // WindowsRegistry.Shared);
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
            string file = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), IG_OWN_JSON);
            if (!File.Exists(file))
            {
                CLogger.LogInfo("{0} file not found in AppData", _name.ToUpper());
                return "";
            }

            string strDocumentData = File.ReadAllText(file);

            if (string.IsNullOrEmpty(strDocumentData))
            {
                CLogger.LogWarn(string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
                return "";
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
                bool exists = false;
                JsonElement coll = new();
                document.RootElement.TryGetProperty("gala_data", out JsonElement gData);
                if (!gData.Equals(null))
                {
                    gData.TryGetProperty("data", out JsonElement data);
                    if (!data.Equals(null))
                    {
                        data.TryGetProperty("showcase_content", out JsonElement sContent);
                        if (!sContent.Equals(null))
                        {
                            sContent.TryGetProperty("content", out JsonElement content);
                            if (!content.Equals(null))
                            {
                                content.TryGetProperty("user_collection", out coll);
                                if (!coll.Equals(null))
                                    exists = true;
                            }
                        }
                    }
                }

                if (exists)
                {
                    foreach (JsonElement prod in coll.EnumerateArray())
                    {
                        if (game.ID.Equals(GetStringProperty(prod, "prod_id_key_name")))
                        {
                            string devName = GetStringProperty(prod, "prod_dev_namespace");
                            /*
                            string cover = GetStringProperty(prod, "prod_dev_cover");
                            string iconWideUrl = $"https://www.indiegalacdn.com/imgs/devs/{devName}/products/{strID}/prodcover/{cover}";
                            */
                            string image = GetStringProperty(prod, "prod_dev_image");
                            if (!string.IsNullOrEmpty(devName) && !string.IsNullOrEmpty(image))
                                return $"https://www.indiegalacdn.com/imgs/devs/{devName}/products/{game.ID}/prodmain/{image}";
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
            }

            CLogger.LogInfo("Icon for {0} game \"{1}\" not found in file.", _name.ToUpper(), game.Title);
            return "";
        }

        public static string GetGameID(string key) => key;
    }
}