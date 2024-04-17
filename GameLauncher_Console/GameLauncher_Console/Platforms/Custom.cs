using GameFinder.Common;
using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using static GameLauncher_Console.CGameData;
//using static GameLauncher_Console.CRegScanner;

namespace GameLauncher_Console
{
	// .lnk and .exe files in .\CustomGames
	// [maybe this shouldn't derive from IPlatform interface?]
	public class PlatformCustom : IPlatform
    {
		public const GamePlatform ENUM			= GamePlatform.Custom;
		public const string PROTOCOL			= "";
		private const string CUSTOM_GAME_FOLDER	= "CustomGames";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

        string IPlatform.Description => GetPlatformString(ENUM);

        public static void Launch() => throw new NotImplementedException();

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		public static int InstallGame(CGame _) => throw new NotImplementedException();

		public static void StartGame(CGame game)
		{
			CLogger.LogInfo($"Launch: {game.Launch}");
			if (OperatingSystem.IsWindows())
				_ = CDock.StartShellExecute(game.Launch);
			else
				_ = Process.Start(game.Launch);
		}

		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false) => throw new NotImplementedException();

		public void GetGames(ref CTempGameSet tempGameSet)
        {
			if (OperatingSystem.IsWindows())
				FindCustomLinkFiles(ref tempGameSet);
			FindCustomBinaries(ref tempGameSet);
		}

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		public static string GetGameID(string key) => key;

		/// <summary>
		/// Search the "CustomGames" folder for file shortcuts (.lnk) to import.
		/// </summary>
		/// http://www.saunalahti.fi/janij/blog/2006-12.html#d6d9c7ee-82f9-4781-8594-152efecddae2
		[SupportedOSPlatform("windows")]
		private static void FindCustomLinkFiles(ref CTempGameSet tempGameSet)
		{
            string strPlatform = GetPlatformString(ENUM);

            List<string> fileList = Directory.EnumerateFiles(Path.Combine(CDock.currentPath, CUSTOM_GAME_FOLDER), "*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".lnk")).ToList();
			
			foreach (string file in fileList)
			{
				string strPathOnly = Path.GetDirectoryName(file);
				strPathOnly = Path.GetFullPath(strPathOnly);
				string strFilenameOnly = Path.GetFileName(file);

				Shell32.Shell shell = new();
				Shell32.Folder folder = shell.NameSpace(strPathOnly);
				Shell32.FolderItem folderItem = folder.ParseName(strFilenameOnly);
				if (folderItem != null)
				{
					Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;
					string strID = Path.GetFileNameWithoutExtension(file);
					string strTitle = strID;
					CLogger.LogDebug($"- {strTitle}");
					string strLaunch = link.Path;
					string strUninstall = "";  // N/A
					string strAlias = GetAlias(strTitle);
					if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
						strAlias = "";
					tempGameSet.InsertGame(strID, strTitle, strLaunch, "", strLaunch, "", strUninstall, bIsInstalled: true, bIsFavourite: false, bIsNew: true, bIsHidden: false, strAlias, strPlatform, new List<string>(), DateTime.MinValue, 0, 0, 0f);
				}
			}
		}

		/// <summary>
		/// Search the "CustomGames" folder for binaries (.exe) files to import
		/// </summary>
		private static void FindCustomBinaries(ref CTempGameSet tempGameSet)
		{
			string strPlatform = GetPlatformString(ENUM);

			List<string> fileList = Directory.EnumerateFiles(Path.Combine(CDock.currentPath, CUSTOM_GAME_FOLDER), "*", SearchOption.AllDirectories).Where(s => s.EndsWith(".exe")).ToList();

			// Big Fish Games may use .bfg for executables
			//fileList.AddRange(Directory.EnumerateFiles(Path.Combine(CDock.currentPath, CUSTOM_GAME_FOLDER), "*", SearchOption.AllDirectories).Where(s => s.EndsWith(".bfg")).ToList());

			foreach (string file in fileList)
			{
				string strID = Path.GetFileNameWithoutExtension(file);
				string strTitle = strID;
				CLogger.LogDebug($"- {strTitle}");
				string strLaunch = Path.GetFullPath(file);
				string strUninstall = ""; // N/A
				string strAlias = GetAlias(strTitle);
				if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
					strAlias = "";
				tempGameSet.InsertGame(strID, strTitle, strLaunch, "", strLaunch, "", strUninstall, bIsInstalled: true, bIsFavourite: false, bIsNew: true, bIsHidden: false, strAlias, strPlatform, new List<string>(), DateTime.MinValue, 0, 0, 0f);
			}
		}
	}
}