using GameCollector.StoreHandlers.Paradox;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CRegScanner;
using FileSystem = NexusMods.Paths.FileSystem;

namespace GameLauncher_Console
{
	// Paradox Launcher
	// [owned and installed games]
	public class PlatformParadox : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Paradox;
		public const string PROTOCOL			= "";
		private const string PARADOX_REG		= @"SOFTWARE\Paradox Interactive\Paradox Launcher\LauncherPath"; // HKLM32
        private const string PARADOX_PATH		= "Path";
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
        {
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry32).OpenSubKey(PARADOX_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM32
                string launcherPath = Path.Combine(GetRegStrVal(key, PARADOX_PATH), "\\Paradox Launcher.exe");
                if (File.Exists(launcherPath))
                    Process.Start(launcherPath);
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

            ParadoxHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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

        public static string GetIconUrl(CGame _) => throw new NotImplementedException();

        public static string GetGameID(string key) => key;
    }
}