using HtmlAgilityPack;
using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Versioning;
using System.Xml;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CRegScanner;

namespace GameLauncher_Console
{
	// Big Fish Games
	// [owned and installed games]
	public class PlatformBigFish : IPlatform
	{
		public const GamePlatform ENUM			= GamePlatform.BigFish;
		//public const string START_GAME		= "LaunchGame.bfg";
		private const string BIGFISH_PREFIX		= "BFG-";
		private const string BIGFISH_REG		= @"SOFTWARE\Big Fish Games\Client"; // HKLM32
		private const string BIGFISH_GAMES		= @"SOFTWARE\Big Fish Games\Persistence\GameDB"; // HKLM32
		private const string BIGFISH_ID			= "WrapID";
		private const string BIGFISH_PATH		= "ExecutablePath";
		private const string BIGFISH_CASINO_ID	= "F7315T1L1";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		public static void Launch()
		{
			if (OperatingSystem.IsWindows())
			{
				using RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
					RegistryView.Registry32).OpenSubKey(BIGFISH_REG, RegistryKeyPermissionCheck.ReadSubTree); // HKLM32
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
			CDock.DeleteCustomImage(game.Title, false);
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
			List<RegistryKey> keyList;
			string strPlatform = GetPlatformString(ENUM);

			using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Registry32).OpenSubKey(BIGFISH_GAMES, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
			{
				if (key == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}

				keyList = FindGameFolders(key, "");

				CLogger.LogInfo("{0} {1} games found", keyList.Count > 1 ? keyList.Count - 1 : keyList.Count, _name.ToUpper());
				foreach (var data in keyList)
				{
					string id = Path.GetFileName(data.Name);
					if (id.Equals(BIGFISH_CASINO_ID))  // hide Big Fish Casino Activator
						continue;

					string strID = "bfg_" + id;
					string strTitle = "";
					string strLaunch = "";
					string strIconPath = "";
					string strUninstall = "";
					string strAlias = "";
					try
					{
						//found = true;
						bool isInstalled = false;
						strTitle = GetRegStrVal(data, "Name");

						// If this is an expired trial, count it as not-installed
						int activated = (int)GetRegDWORDVal(data, "Activated");
						int daysLeft = (int)GetRegDWORDVal(data, "DaysLeft");
						int timeLeft = (int)GetRegDWORDVal(data, "TimeLeft");
						if (activated > 0 || timeLeft > 0 || daysLeft > 0)
						{
							isInstalled = true;
							CLogger.LogDebug($"- {strTitle}");
						}
						else
							CLogger.LogDebug($"- *{strTitle}");

						strLaunch = GetRegStrVal(data, BIGFISH_PATH);
						strAlias = GetAlias(strTitle);
						if (strAlias.Equals(strTitle, CDock.IGNORE_CASE))
							strAlias = "";
						//strIconPath = GetRegStrVal(data, "Icon");			// 60x40
						//strIconPath = GetRegStrVal(data, "Thumbnail");	// 80x80
						strIconPath = GetRegStrVal(data, "feature");		// 175x150

						//ushort userRating = (ushort)GetRegDWORDVal(data, "Rating"); // Not used?
						uint numberRuns = (uint)GetRegDWORDVal(data, "PlayCount");
						byte[] dateData = GetRegBinaryVal(data, "LastActionTime");
						DateTime lastRun = RegToDateTime(dateData);
						//CLogger.LogDebug("    LastActionTime: " + BitConverter.ToString(dateData) + " -> " + lastRun.ToShortDateString());

						List<RegistryKey> unKeyList;
						using (RegistryKey key2 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
							RegistryView.Registry32).OpenSubKey(UNINSTALL_REG, RegistryKeyPermissionCheck.ReadSubTree)) // HKLM32
						{
							if (key2 != null)
							{
								unKeyList = FindGameFolders(key2, BIGFISH_PREFIX);
								foreach (var data2 in unKeyList)
								{
									if (GetRegStrVal(data2, BIGFISH_ID).Equals(id))
									{
										if (string.IsNullOrEmpty(strIconPath))
											strIconPath = GetRegStrVal(data2, GAME_DISPLAY_ICON).Trim(new char[] { ' ', '"' });
										strUninstall = GetRegStrVal(data2, GAME_UNINSTALL_STRING).Trim(new char[] { ' ', '"' });
									}
								}
							}
						}
						if (string.IsNullOrEmpty(strIconPath) && expensiveIcons)
						{
							bool success = false;
							string dir = Path.GetDirectoryName(strLaunch);
							string xmlPath = Path.Combine(dir, "bfgstate.xml");
							try
							{
								if (File.Exists(xmlPath))
								{
									XmlDocument doc = new();
									doc.Load(xmlPath);
									
									XmlNode node = doc.DocumentElement.SelectSingleNode("feature");			// 175x150
									if (node != null)
									{
										strIconPath = node.InnerText;
										if (File.Exists(strIconPath))
											success = true;
										else
										{
											// Use website to download missing icons
											if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
											{
												if (CDock.DownloadCustomImage(strTitle, GetIconUrl(id, strTitle)))
													success = true;
											}
										}
									}
									else
                                    {
										XmlNode node2 = doc.DocumentElement.SelectSingleNode("thumbnail");	// 80x80 [or "icon" = 60x40]
										strIconPath = node2.InnerText;
										if (File.Exists(strIconPath))
											success = true;
										else
										{
											// Use website to download missing icons
											if (!(bool)(CConfig.GetConfigBool(CConfig.CFG_IMGDOWN)))
											{
												if (CDock.DownloadCustomImage(strTitle, GetIconUrl(id, strTitle)))
													success = true;
											}
										}
									}
								}
							}
							catch (Exception e)
							{
								CLogger.LogError(e);
							}
							if (!success)
							{
								strIconPath = CGameFinder.FindGameBinaryFile(dir, strTitle);
							}
						}
						if (!(string.IsNullOrEmpty(strLaunch)))
							gameDataList.Add(
								new ImportGameData(strID, strTitle, strLaunch, strIconPath, strUninstall, strAlias, isInstalled, strPlatform, dateLastRun:lastRun, numRuns:numberRuns));
					}
					catch (Exception e)
					{
						CLogger.LogError(e);
					}
				}
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
			if (int.TryParse(key.Substring(index, key.IndexOf('T') - 1), out int num) && num > 0)
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