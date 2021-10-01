﻿using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
using static System.Environment;

namespace GameLauncher_Console
{
	// Paradox Launcher
	// [owned and installed games]
	public class PlatformParadox : IPlatform
	{
		public const GamePlatform ENUM = GamePlatform.Paradox;
		public const string PROTOCOL			= "";
		public const string PARADOX_REG			= @"SOFTWARE\WOW6432Node\Paradox Interactive\Paradox Launcher\LauncherPath"; // HKLM32
		public const string PARADOX_PATH		= "Path";
		//private const string PARADOX_UNREG	= "{ED2CDA1D-39E4-4CBB-992C-5C1D08672128}"; //HKLM32
		private const string PARADOX_JSON_FOLDER = @"\Paradox Interactive\launcher";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
        {
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(PARADOX_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM32
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

		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
			List<string> dirs = new();

			// Get installed games
			if (OperatingSystem.IsWindows())
			{
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(PARADOX_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM32
                if (key == null)
                    CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
                else
                {
                    string path = GetRegStrVal(key, PARADOX_PATH);

                    try
                    {
                        if (!path.Equals(null) && Directory.Exists(path))
                        {
                            dirs.AddRange(Directory.GetDirectories(Directory.GetParent(Directory.GetParent(path).ToString()) + "\\games", "*.*", SearchOption.TopDirectoryOnly));
                            foreach (string dir in dirs)
                            {
                                CultureInfo ci = new("en-GB");
                                TextInfo ti = ci.TextInfo;

                                string strID = Path.GetFileName(dir);
                                string strTitle = "";
                                string strLaunch = "";
                                string strAlias = "";
                                string strPlatform = GetPlatformString(GamePlatform.Paradox);

                                strTitle = ti.ToTitleCase(strID.Replace('_', ' '));
                                CLogger.LogDebug($"- {strTitle}");
                                strLaunch = CGameFinder.FindGameBinaryFile(dir, strTitle);
                                strAlias = GetAlias(strLaunch);
                                if (strAlias.Length > strTitle.Length)
                                    strAlias = GetAlias(strTitle);
                                if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
                                    strAlias = "";
                                if (!(string.IsNullOrEmpty(strLaunch)))
                                    gameDataList.Add(
                                        new ImportGameData(strID, strTitle, strLaunch, strLaunch, "", strAlias, true, strPlatform));
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        CLogger.LogError(e);
                    }
                }
            }

			// Get not-installed games
			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_INSTONLY))
			{
				string folder = GetFolderPath(SpecialFolder.LocalApplicationData) + PARADOX_JSON_FOLDER;
				if (!Directory.Exists(folder))
				{
					CLogger.LogInfo("{0} games not found in Local AppData.", _name.ToUpper());
				}
				else
				{
					string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);

					foreach (string file in files)
					{
						if (file.EndsWith("_installableGames.json") && !(file.StartsWith("_noUser")))
						{
							string strDocumentData = File.ReadAllText(file);

							if (string.IsNullOrEmpty(strDocumentData))
								continue;

							CLogger.LogDebug("{0} not-installed games:", _name.ToUpper());

							try
							{
                                using JsonDocument document = JsonDocument.Parse(@strDocumentData, jsonTrailingCommas);
                                document.RootElement.TryGetProperty("content", out JsonElement content);
                                if (!content.Equals(null))
                                {
                                    foreach (JsonElement game in content.EnumerateArray())
                                    {
                                        game.TryGetProperty("_name", out JsonElement id);

                                        // Check if game is already installed
                                        bool found = false;
                                        foreach (string dir in dirs)
                                        {
                                            if (id.ToString().Equals(Path.GetFileName(dir)))
                                                found = true;
                                        }
                                        if (!found)
                                        {
                                            game.TryGetProperty("_displayName", out JsonElement title);
                                            game.TryGetProperty("_owned", out JsonElement owned);
                                            if (!id.Equals(null) && !title.Equals(null) && owned.ToString().ToLower().Equals("true"))
                                            {
                                                string strID = id.ToString();
                                                string strTitle = title.ToString();
                                                CLogger.LogDebug($"- *{strTitle}");
                                                string strPlatform = GetPlatformString(GamePlatform.Paradox);
                                                gameDataList.Add(new ImportGameData(strID, strTitle, "", "", "", "", false, strPlatform));
                                            }
                                        }
                                    }
                                }
                            }
							catch (Exception e)
							{
								CLogger.LogError(e, string.Format("Malformed {0} file: {1}", _name.ToUpper(), file));
							}
						}
					}
				}
			}
			CLogger.LogDebug("--------------------");
		}
	}
}