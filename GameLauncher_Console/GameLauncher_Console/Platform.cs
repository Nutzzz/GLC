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

		void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons);
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
			Misc = 21,
			[Description("Humble App")]
			Misc = 22,
			[Description("Miscellaneous")]
			Misc = 23
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
			List<ImportGameData> gameDataList = new();
			if (!bOnlyCustom)
			{
				foreach (IPlatform platform in _platforms)
				{
					Console.Write(".");
					CLogger.LogInfo("Looking for {0} games...", platform.Description);
					platform.GetGames(gameDataList, bExpensiveIcons);
				}
				foreach (ImportGameData data in gameDataList)
				{
					tempGameSet.InsertGame(data.m_strID, data.m_strTitle, data.m_strLaunch, data.m_strIcon, data.m_strUninstall, data.m_bInstalled, false, true, false, data.m_strAlias, data.m_strPlatform, new List<string>(), DateTime.MinValue, 0, 0, 0f);
				}
			}

			PlatformCustom custom = new();

			Console.Write(".");
			CLogger.LogInfo("Looking for {0} games...", GetPlatformString(GamePlatform.Custom));
			custom.GetGames(ref tempGameSet);
			MergeGameSets(tempGameSet);
			if (bFirstScan)
				SortGames((int)CConsoleHelper.SortMethod.cSort_Alpha, false, (bool)CConfig.GetConfigBool(CConfig.CFG_USEINST), true);
			CLogger.LogDebug("-----------------------");
			Console.WriteLine();
			ExportGames(GetPlatformGameList(GamePlatform.All).ToList());
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