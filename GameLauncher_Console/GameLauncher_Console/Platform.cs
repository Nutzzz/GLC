using GameFinder.Common;
using Logger;
using SqlDB;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using static GameLauncher_Console.CGameData;
using static GameLauncher_Console.CJsonWrapper;
using static SqlDB.CSqlField;

namespace GameLauncher_Console
{
	public interface IPlatform
	{
		GamePlatform Enum { get; }
		string Name { get; }
        string Description { get; }
		//void Launch();
		//int InstallGame(CGame game);
		//void StartGame(CGame game);
		/*
		void Login();
		void IsLoggedIn();
		void Auth(string token);
		void TokenLoad();
		void TokenRefresh();
		void GetEntitlements();
		*/

		void GetGames(List<ImportGameData> gameDataList, Settings settings, bool expensiveIcons);
		//string GetIconUrl(CGame game);
		//string GetGameID(string id);
	}

	/// <summary>
	/// Class to handle platform object and platform database table
	/// </summary>
	public class CPlatform
	{
		private readonly List<IPlatform> _platforms;

        /*
		/// <summary>
		/// Enumerator containing currently supported game platforms
		/// [Unlike CGameData.GamePlatform, this does not include Custom, All, Hidden, Search, New, NotInstalled categories]
		/// </summary>
		public enum Platform
        {
			[Description("Unknown")]
			Unknown = -1,
			[Description("Steam")]
			Steam = 0,
			[Description("GOG Galaxy")]
			GOG = 1,
			[Description("Ubisoft Connect")]
			Ubisoft = 2,
			[Description("EA")]
			EA = 3,
			[Description("Epic")]
			Epic = 4,
			[Description("Bethesda.net")]		// deprecated
			Bethesda = 5,
			[Description("Battle.net")]
			Battlenet = 6,
			[Description("Rockstar")]
			Rockstar = 7, 
			[Description("Amazon")]
			Amazon = 8, 
			[Description("Big Fish")]
			BigFish = 9, 
			[Description("Arc")]
			Arc = 10,
			[Description("itch")]
			Itch = 11,
			[Description("Paradox")]
			Paradox = 12,
			[Description("Plarium Play")]
			Plarium = 13,
			[Description("Twitch")]				// deprecated
			Twitch = 14,
			[Description("Wargaming.net")]
			Wargaming = 15,
			[Description("Indiegala Client")]
			IGClient = 16,
			[Description("Microsoft Store")]
			Microsoft = 17,
			[Description("Oculus")]
			Oculus = 18,
			[Description("Legacy")]
			Legacy = 19,
			[Description("Riot Client")]
			Riot = 20,
			[Description("Game Jolt Client")]
			GameJolt = 21,
			[Description("Humble App")]
			Humble = 22,
			[Description("RobotCache")]
			RobotCache = 23,
			//[Description("Miscellaneous")]
			//Misc = 24,
		}
		*/

        #region Query definitions

        /// <summary>
        /// Retrieve the platform information from the database
        /// Also returns the game count for each platform
        /// </summary>
        public class CQryReadPlatforms : CSqlQry
        {
			public CQryReadPlatforms()
				: base(
				"Platform " + 
				"LEFT JOIN Game G on PlatformID = G.PlatformFK", 
				"", " GROUP BY PlatformID")
			{
				m_sqlRow["PlatformID"]	= new CSqlFieldInteger("PlatformID", QryFlag.cSelWhere);
				m_sqlRow["Name"]		= new CSqlFieldString("Name",		 QryFlag.cSelRead);
				m_sqlRow["GameCount"]	= new CSqlFieldString("COUNT(G.GameID) as GameCount", QryFlag.cSelRead);
			}
			public int PlatformID
			{
				get { return m_sqlRow["PlatformID"].Integer; }
				set { m_sqlRow["PlatformID"].Integer = value; }
			}
			public string Name
			{
				get { return m_sqlRow["Name"].String; }
				set { m_sqlRow["Name"].String = value; }
			}
			public int GameCount
			{
				get { return m_sqlRow["GameCount"].Integer; }
				set { m_sqlRow["GameCount"].Integer = value; }
			}
		}

		/// <summary>
		/// Query for writing to the platform table
		/// </summary>
		public class CQryWritePlatforms : CSqlQry
        {
			public CQryWritePlatforms()
				: base("Platform", "", "")
			{
				m_sqlRow["PlatformID"]	= new CSqlFieldInteger("PlatformID" , QryFlag.cUpdWhere | QryFlag.cDelWhere);
				m_sqlRow["Name"]		= new CSqlFieldString("Name"		, QryFlag.cUpdWrite | QryFlag.cInsWrite);
				m_sqlRow["Description"]	= new CSqlFieldString("Description" , QryFlag.cUpdWrite | QryFlag.cInsWrite);
			}
			public int PlatformID
			{
				get { return m_sqlRow["PlatformID"].Integer; }
				set { m_sqlRow["PlatformID"].Integer = value; }
			}
			public string Name
			{
				get { return m_sqlRow["Name"].String; }
				set { m_sqlRow["Name"].String = value; }
			}
			public string Description
			{
				get { return m_sqlRow["Description"].String; }
				set { m_sqlRow["Description"].String = value; }
			}
		}

		#endregion // Query definitions

		private static CQryReadPlatforms m_qryRead = new();
		private static CQryWritePlatforms m_qryWrite = new();

		/// <summary>
		/// Container for a single platform
		/// </summary>
		public struct PlatformObject
		{
			public PlatformObject(int platformID, string name, int gameCount, string description)
			{
				PlatformID	= platformID;
				Name		= name;
				GameCount	= gameCount;
				Description = description;
			}

			/// <summary>
			/// Constructor overload.
			/// Populate using query
			/// </summary>
			/// <param name="qryRead"></param>
			public PlatformObject(CQryReadPlatforms qryRead)
			{
				PlatformID	= qryRead.PlatformID;
				Name		= qryRead.Name;
				GameCount	= qryRead.GameCount;
				Description = "";
			}

			// Properties
			public int PlatformID { get; }
			public string Name { get; }
			public int GameCount { get; private set; }
			public string Description { get; }
		}

		public CPlatform()
		{
			_platforms = new List<IPlatform>();
		}

		public void AddSupportedPlatform(IPlatform platform)
		{
			_platforms.Add(platform);
		}

		/// <summary>
		/// Scan the registry and filesystem for games, add new games to memory and export into JSON document
		/// </summary>
		public void ScanGames(bool bOnlyCustom, bool bExpensiveIcons, bool bFirstScan)
		{
			CTempGameSet tempGameSet = new();
			CLogger.LogDebug("-----------------------");
			CLogger.LogInfo("Scanning for games...");
			//if (bFirstScan)
				Console.Write("Scanning for games");  // add dots for each platform

			List<ImportGameData> gameDataList = new();
			var cursor = Console.CursorLeft;
			if (!bOnlyCustom)
			{
				if (!bool.TryParse(CConfig.CFG_BASEONLY, out var baseOnly))
					baseOnly = true;
				if (!bool.TryParse(CConfig.CFG_GAMEONLY, out var gamesOnly))
					gamesOnly = true;
				if (!bool.TryParse(CConfig.CFG_INSTONLY, out var installedOnly))
					installedOnly = false;
				if (!bool.TryParse(CConfig.CFG_OWNONLY, out var ownedOnly))
					ownedOnly = true;

				Settings gcSettings = new()
				{
					BaseOnly = baseOnly,
					GamesOnly = gamesOnly,
					InstalledOnly = installedOnly,
					OwnedOnly = ownedOnly,
				};
				foreach (IPlatform platform in _platforms)
				{
					//if (bFirstScan)
					{
						Console.Write($". [{platform.Description}]");
						cursor++;
					}
					//else
					//	Console.Write(".");
					CLogger.LogInfo("Looking for {0} games...", platform.Description);

					platform.GetGames(gameDataList, gcSettings, bExpensiveIcons);

					//if (bFirstScan)
					{
						Console.SetCursorPosition(cursor, Console.CursorTop);
						Console.Write(new string(' ', 3 + platform.Description.Length));
						Console.SetCursorPosition(cursor, Console.CursorTop);
					}
				}
				foreach (var data in gameDataList)
				{
					tempGameSet.InsertGame(data.m_gameData.GameId,
						data.m_gameData.GameName,
						data.m_gameData.Launch == default ? data.m_gameData.LaunchUrl : (string.IsNullOrEmpty(data.m_gameData.LaunchArgs) ? data.m_gameData.Launch.GetFullPath() : data.m_gameData.Launch.GetFullPath() + " " + data.m_gameData.LaunchArgs),
						data.m_gameData.LaunchUrl,
						data.m_gameData.Icon == default ? "" : data.m_gameData.Icon.GetFullPath(),
						data.m_gameData.Metadata == default ? "" : data.m_gameData.Metadata.TryGetValue("IconUrl", out var urls) && urls.Count > 0 ? urls[0] : (data.m_gameData.Metadata.TryGetValue("ImageUrl", out urls) && urls.Count > 0 ? urls[0] : ""),
						data.m_gameData.Uninstall == default ? data.m_gameData.UninstallUrl : (string.IsNullOrEmpty(data.m_gameData.UninstallArgs) ? data.m_gameData.Uninstall.GetFullPath() : data.m_gameData.Uninstall.GetFullPath() + " " + data.m_gameData.UninstallArgs),
						data.m_gameData.IsInstalled,
						bIsFavourite: false, bIsNew: true, bIsHidden: false,
						GetAlias(data.m_gameData.GameName),
						data.m_strPlatform,
						new List<string>(), DateTime.MinValue, 0, 0, 0f);
				}
			}

			//if (bFirstScan)
			{
				Console.Write($". [{GetPlatformString(GamePlatform.Custom)}]");
				Console.SetCursorPosition(0, Console.CursorTop);
			}
			//else
			//	Console.Write(".");

			CLogger.LogInfo("Looking for {0}...", GetPlatformString(GamePlatform.Custom));

			PlatformCustom custom = new();
			custom.GetGames(ref tempGameSet);
			MergeGameSets(tempGameSet);
			if (bFirstScan)
				SortGames((int)CConsoleHelper.SortMethod.cSort_Alpha, faveSort: false, (bool)CConfig.GetConfigBool(CConfig.CFG_USEINST), ignoreArticle: true);
			CLogger.LogDebug("-----------------------");
			ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', 21 + _platforms.Count + GetPlatformString(GamePlatform.Custom).Length));

			if (!(bool)CConfig.GetConfigBool(CConfig.CFG_IMGDOWN))
			{
				Console.SetCursorPosition(0, Console.CursorTop);
				DownloadAllImages(bFirstScan);
			}
			Console.WriteLine();
		}

		/// <summary>
		/// Download all images for games
		/// </summary>
		public void DownloadAllImages(bool bFirstScan = false)
		{
			CLogger.LogDebug("-----------------------");
			CLogger.LogInfo("Downloading images...");
			//if (!bFirstScan)
				Console.Write("Downloading images");  // add dots for each platform

			var cursor = Console.CursorLeft;
			foreach (IPlatform platform in _platforms)
			{
				//if (bFirstScan)
				{
					Console.Write($". [{platform.Description}]");
					cursor++;
				}
				//else
				//	Console.Write(".");

				CLogger.LogInfo("Looking for {0} images...", platform.Description);
				foreach (var game in GetPlatformGameList(platform.Enum))
				{
					CDock.DownloadCustomImage(game.Title, game.IconUrl, overwrite: false);
				}

				//if (bFirstScan)
				{
					Console.SetCursorPosition(cursor, Console.CursorTop);
					Console.Write(new string(' ', 3 + platform.Description.Length));
					Console.SetCursorPosition(cursor, Console.CursorTop);
				}
			}
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', 21 + _platforms.Count + _platforms.Last().Description.Length));
			CLogger.LogDebug("-----------------------");
		}

		/// <summary>
		/// Get list of platforms from the database
		/// </summary>
		/// <returns>List of PlatformObjects</returns>
		public static Dictionary<string, PlatformObject>GetPlatforms()
		{
			Dictionary<string, PlatformObject> platforms = new();
			m_qryRead.MakeFieldsNull();
			if(m_qryRead.Select() == SQLiteErrorCode.Ok)
			{
				do
				{
					platforms[m_qryRead.Name] = new PlatformObject(m_qryRead);
				} while(m_qryRead.Fetch());
			}
			return platforms;
		}

		/// <summary>
		/// Insert specified platform into the database
		/// </summary>
		/// <param name="platform">The PlatformObject to insert</param>
		/// <returns>True on insert success, otherwise false</returns>
		public static bool InsertPlatform(PlatformObject platform)
		{
			m_qryWrite.MakeFieldsNull();
			m_qryWrite.Name			= platform.Name;
			m_qryWrite.Description	= platform.Description;
			return m_qryWrite.Insert() == SQLiteErrorCode.Ok;
		}

		/// <summary>
		/// Insert specified platform into the database
		/// </summary>
		/// <param name="title">Platform title</param>
		/// <param name="description">Platform description</param>
		/// <returns>True on insert success, otherwise false</returns>
		public static bool InsertPlatform(string title, string description)
		{
			m_qryWrite.MakeFieldsNull();
			m_qryWrite.Name = title;
			m_qryWrite.Description = description;
			return m_qryWrite.Insert() == SQLiteErrorCode.Ok;
		}

		/// <summary>
		/// Update specified platform
		/// </summary>
		/// <param name="platform">The PlatformObject to write</param>
		/// <returns>True on update success, otherwise false</returns>
		public static bool UpdatePlatform(PlatformObject platform)
		{
			m_qryWrite.MakeFieldsNull();
			m_qryWrite.PlatformID	= platform.PlatformID;
			m_qryWrite.Name			= platform.Name;
			m_qryWrite.Description	= platform.Description;
			return m_qryWrite.Update() == SQLiteErrorCode.Ok;
		}

		/// <summary>
		/// Remove platform with selected PlatformID from the database
		/// </summary>
		/// <param name="platformID">The platformID to delete</param>
		/// <returns>True on delete success, otherwise false</returns>
		public static bool RemovePlatform(int platformID)
		{
			m_qryWrite.MakeFieldsNull();
			m_qryWrite.PlatformID = platformID;
			return m_qryWrite.Delete() == SQLiteErrorCode.Ok;
		}
	}
}