using GameCollector.StoreHandlers.BigFish;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using HtmlAgilityPack;
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
	// Big Fish Games
	// [owned and installed games]
	public class PlatformBigFish : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.BigFish;
		private const string BIGFISH_REG		= @"SOFTWARE\Big Fish Games\Client"; // HKLM32
		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
			{
				using RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry32).OpenSubKey(BIGFISH_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM32
				string launcherPath = Path.Combine(GetRegStrVal(key, "InstallationPath"), "bfgclient.exe");
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
			CDock.DeleteCustomImage(game.Title, justBackups: false);
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
		public void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons = false)
		{
            string strPlatform = GetPlatformString(ENUM);

            BigFishHandler handler = new(WindowsRegistry.Shared, FileSystem.Shared);
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

			CLogger.LogDebug("------------------------");
		}

		public static string GetIconUrl(CGame game)
		{
			return GetIconUrl(GetGameID(game.ID), game.Title);
		}
		
		public static string GetIconUrl(string id, string title)
		{
			if (!string.IsNullOrEmpty(id))
			{
				string url = $"https://www.bigfishgames.com/games/{id}/";
				/*
#if DEBUG
				// Don't re-download if file exists
				string tmpfile = $"tmp_{_name}_{id}.html";
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
				HtmlNode node = doc.DocumentNode.SelectSingleNode("//div[@class='rr-game-image']");
				foreach (HtmlNode child in node.ChildNodes)
				{
					foreach (HtmlAttribute attr in child.Attributes)
					{
						if (attr.Name.Equals("src", CDock.IGNORE_CASE))
						{
							string strIcon = attr.Value;
							if (!string.IsNullOrEmpty(strIcon))
							{
								return strIcon;
							}
						}
					}
				}
			}

			CLogger.LogInfo("Icon for {0} game \"{1}\" not found on website.", _name.ToUpper(), title);
			return "";
		}

		/// <summary>
		/// Scan the key name and extract the Big Fish game id
		/// </summary>
		/// <param name="key">The game string</param>
		/// <returns>Big Fish game ID as string</returns>
		public static string GetGameID(string key)
		{
			int index = 0;
			if (key.StartsWith("bfg_"))
				index = 5;
			if (int.TryParse(key.AsSpan(index, key.IndexOf('T') - 1), out int num) && num > 0)
				return num.ToString();
			else
				return key;
		}

		public DateTime RegToDateTime(byte[] bytes)
		{
			// Note this only accounts for the first 4 bytes of a 16 byte span; not sure what the rest specifies
			long date = ((((
			(long)bytes[0]) * 256 +
			bytes[1]) * 256 +
			bytes[2]) * 256 +
			bytes[3]);
			return DateTimeOffset.FromUnixTimeSeconds(date - 2209032000).UtcDateTime; // This date is seconds from 1900 rather than 1970 epoch
		}
	}
}