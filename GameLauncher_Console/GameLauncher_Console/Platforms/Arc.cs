using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CRegScanner;
//using static System.Environment;

namespace GameLauncher_Console
{
	// Arc
	// [installed games only]
	public class PlatformArc : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.Arc;
		public const string PROTOCOL			= "arc://";
		private const string ARC_REG			= @"SOFTWARE\Perfect World Entertainment"; // HKLM32
		//private const string ARC_UNREG		= "{CED8E25B-122A-4E80-B612-7F99B93284B3}"; // HKLM32 Uninstall
		private const string ARC_GAMES			= "Core";
		private const string ARC_ID				= "APP_ABBR";
		private const string ARC_PATH			= "INSTALL_PATH";
		private const string ARC_EXEPATH		= "CLIENT_PATH";
		private const string ARC_INST			= "installed";
		private const string ARC_LANGDEF		= "en";
		//[strLaunch] CLIENT_PATH in e.g., HKLM32\SOFTWARE\Perfect World Entertainment\14000en
		//[strId?] APP_ABBR
		//[installed] installed

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
			//CDock.DeleteCustomImage(game.Title, false);
			Launch();
			return -1;
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
			List<RegistryKey> keyList = new();
			string strPlatform = GetPlatformString(ENUM);
			//string arcFolder = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "Arc"); // AppData\Roaming
			/*
			string launcherPath = "";

			using (RegistryKey launcherKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, 
				RegistryView.Registry32).OpenSubKey(Path.Combine(ARC_REG, "Arc"), RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				if (launcherKey == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}
				launcherPath = GetRegStrVal(launcherKey, "client");
			}
			*/

            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(Path.Combine(ARC_REG, ARC_GAMES), RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				if(key == null)
                {
					return;
                }
				foreach (string subKey in key.GetSubKeyNames()) // Add subkeys to search list
				{
					try
					{
						if (subKey.IndexOf(ARC_LANGDEF) > -1)
							keyList.Add(key.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadSubTree));
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}
				}
				CLogger.LogInfo("{0} {1} games found", keyList.Count, _name.ToUpper());
				foreach (var data in keyList)
                {
					string id = GetRegStrVal(data, ARC_ID);
					if (string.IsNullOrEmpty(id))
					{
						id = Path.GetFileName(data.Name);
						int idIndex = id.IndexOf(ARC_LANGDEF);
						if (idIndex > -1)
							id = id.Substring(0, idIndex);
					}
					string name = Path.GetFileName(GetRegStrVal(data, ARC_PATH).Trim(new char[] { '"', '\\', '/' }));
					int nameIndex = name.IndexOf("_" + ARC_LANGDEF);
					if (nameIndex > -1)
						name = name.Substring(0, nameIndex);
					string strID = "";
					string strTitle = "";
					string strLaunch = "";
					string strAlias = "";
					bool bInstalled = true;

					try
					{
						strID = "arc_" + id;
						if (!string.IsNullOrEmpty(name))
							strTitle = name;
						else
							strTitle = id;
						CLogger.LogDebug($"- {strTitle}");
						strLaunch = GetRegStrVal(data, ARC_EXEPATH);
						strAlias = GetAlias(Path.GetFileNameWithoutExtension(strLaunch));
						if (strAlias.Length > strTitle.Length)
							strAlias = GetAlias(strTitle);
						if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
							strAlias = "";
						int? installed = GetRegDWORDVal(data, ARC_INST);
						if (installed != null && installed == 0)
							bInstalled = false;
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}
					if (!(string.IsNullOrEmpty(strLaunch)))
						gameDataList.Add(
							new ImportGameData(strID, strTitle, strLaunch, strLaunch, "", strAlias, bInstalled, strPlatform));
				}
			}
			CLogger.LogDebug("------------------------");
		}

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		/// <summary>
		/// Scan the key name and extract the Arc game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Arc game ID as string</returns>
		public static string GetGameID(string key)
		{
			if (key.StartsWith("arc_"))
				return key[4..];
			return key;
		}
	}
}