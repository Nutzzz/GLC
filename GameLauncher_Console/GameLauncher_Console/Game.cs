using SqlDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Text.Json;
using static SqlDB.CSqlField;

namespace CGame_Test // TODO: GameLauncher_Console
{
    /// <summary>
    /// Handles any game-relaed logic and data structures
    /// </summary>
    public static class CGame
    {
        // AttributeName definitions
        private static readonly string _a_game_tag = "TAG";

        /// <summary>
        /// Game title articles.
        /// Removed from titles when creating default aliases
        /// Ignored during title searches
        /// </summary>
        private static readonly string[] _articles =
        {
            "The ",								// English definite
			"A ", "An ",						// English indefinite
			/*
			"El ", "La ", "Los ", "Las ",		// Spanish definite
			"Un ", "Una ", "Unos ", "Unas ",	// Spanish indefinite
			"Le ", "Les ", "L\'",				//, "La" [Spanish] // French definite
			"Une ", "De ", "Des ",				//, "Un" [Spanish] // French indefinite [many French sort with indefinite article]
			"Der", "Das",						//, "Die" [English word] // German definite
			"Ein", "Eine"						// German indefinite
			*/
        };

        // Due to C#'s limitations, every query will come with a lot of bloated boilerplate code
        // Best thing to do for now is to hide them away in a region and just use them. I will have to return to them one day
        #region Query definitions

        /// <summary>
        /// Main game query, handling all reading and writing
        /// TODO: Once the game class is done, it's probably worth seeing if this can be split into special-purpose queries to improve performance and avoid potential problems
        /// </summary>
        public class CQryGame : CSqlQry
        {
            public CQryGame() : base("Game", "", "")
            {
                m_sqlRow["GameID"]          = new CSqlFieldInteger("GameID"     , QryFlag.cSelRead | QryFlag.cWhere);
                m_sqlRow["PlatformFK"]      = new CSqlFieldInteger("PlatformFK" , QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cWhere);
                m_sqlRow["Identifier"]      = new CSqlFieldString("Identifier"  , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["Title"]           = new CSqlFieldString("Title"       , QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["Alias"]           = new CSqlFieldString("Alias"       , QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["Launch"]          = new CSqlFieldString("Launch"      , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["Uninstall"]       = new CSqlFieldString("Uninstall"   , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["IsInstalled"]     = new CSqlFieldBoolean("IsInstalled", QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["IsFavourite"]     = new CSqlFieldBoolean("IsFavourite", QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["IsNew"]           = new CSqlFieldBoolean("IsNew"      , QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["IsHidden"]        = new CSqlFieldBoolean("IsHidden"   , QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["Frequency"]       = new CSqlFieldDouble("Frequency"   , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["Rating"]          = new CSqlFieldInteger("Rating"     , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["NumRuns"]         = new CSqlFieldInteger("NumRuns"    , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["LastRun"]         = new CSqlFieldString("LastRun"     , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["Icon"]            = new CSqlFieldString("Icon"        , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["Tags"]            = new CSqlFieldString("Tags"        , QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["Description"]     = new CSqlFieldString("Description" , QryFlag.cInsWrite | QryFlag.cSelRead);
                m_sqlRow["IsMultiPlatform"] = new CSqlFieldBoolean("IsMultiPlatform", QryFlag.cInsWrite | QryFlag.cSelRead | QryFlag.cSelWhere);
            }
            public int GameID
            {
                get { return m_sqlRow["GameID"].Integer; }
                set { m_sqlRow["GameID"].Integer = value; }
            }
            public int PlatformFK
            {
                get { return m_sqlRow["PlatformFK"].Integer; }
                set { m_sqlRow["PlatformFK"].Integer = value; }
            }
            public string Identifier
            {
                get { return m_sqlRow["Identifier"].String; }
                set { m_sqlRow["Identifier"].String = value; }
            }
            public string Title
            {
                get { return m_sqlRow["Title"].String; }
                set { m_sqlRow["Title"].String = value; }
            }
            public string Alias
            {
                get { return m_sqlRow["Alias"].String; }
                set { m_sqlRow["Alias"].String = value; }
            }
            public string Launch
            {
                get { return m_sqlRow["Launch"].String; }
                set { m_sqlRow["Launch"].String = value; }
            }
            public string Uninstall
            {
                get { return m_sqlRow["Uninstall"].String; }
                set { m_sqlRow["Uninstall"].String = value; }
            }
            public bool IsInstalled
            {
                get { return m_sqlRow["IsInstalled"].Bool; }
                set { m_sqlRow["IsInstalled"].Bool = value; }
            }
            public bool IsFavourite
            {
                get { return m_sqlRow["IsFavourite"].Bool; }
                set { m_sqlRow["IsFavourite"].Bool = value; }
            }
            public bool IsNew
            {
                get { return m_sqlRow["IsNew"].Bool; }
                set { m_sqlRow["IsNew"].Bool = value; }
            }
            public bool IsHidden
            {
                get { return m_sqlRow["IsHidden"].Bool; }
                set { m_sqlRow["IsHidden"].Bool = value; }
            }
            public double Frequency
            {
                get { return m_sqlRow["Frequency"].Double; }
                set { m_sqlRow["Frequency"].Double = value; }
            }
            public int Rating
            {
                get { return m_sqlRow["Rating"].Integer; }
                set { m_sqlRow["Rating"].Integer = value; }
            }
            public int NumRuns
            {
                get { return m_sqlRow["NumRuns"].Integer; }
                set { m_sqlRow["NumRuns"].Integer = value; }
            }
            public string LastRun
            {
                get { return m_sqlRow["LastRun"].String; }
                set { m_sqlRow["LastRun"].String = value; }
            }
            public string Icon
            {
                get { return m_sqlRow["Icon"].String; }
                set { m_sqlRow["Icon"].String = value; }
            }
            public List<string> Tags
            {
                get {
                    return new List<string>(
                        from part in m_sqlRow["Tags"].String.Split('|')
                        select part.Trim());
                    }
                set { m_sqlRow["Tags"].String += "|" + value; } // This is really an Add rather than Set
            }
            public string Description
            {
                get { return m_sqlRow["Description"].String; }
                set { m_sqlRow["Description"].String = value; }
            }
            public bool IsMultiPlatform
            {
                get { return m_sqlRow["IsMultiPlatform"].Bool; }
                set { m_sqlRow["IsMultiPlatform"].Bool = value; }
            }
        }

        /// <summary>
        /// Query for game sorting
        /// </summary>
        private class CQryGameSort : CSqlQry
        {
            public CQryGameSort() : base("Game", "", "")
            {
                m_sqlRow["GameID"] = new CSqlFieldInteger("GameID", QryFlag.cSelRead);
                m_sqlRow["PlatformFK"] = new CSqlFieldInteger("PlatformFK", QryFlag.cSelWhere);
            }

            public int GameID
            {
                get { return m_sqlRow["GameID"].Integer; }
                set { m_sqlRow["GameID"].Integer = value; }
            }
            public int PlatformFK
            {
                get { return m_sqlRow["PlatformFK"].Integer; }
                set { m_sqlRow["PlatformFK"].Integer = value; }
            }
        }

        /// <summary>
        /// Class for performing a fuzzy search 
        /// </summary>
        private class CQryGameFuzzySearch : CSqlQry
        {
            public CQryGameFuzzySearch() : base("Game", "(& = ?) AND (& LIKE '%?%')", "")
            {
                m_sqlRow["PlatformFK"] = new CSqlFieldInteger("PlatformFK", QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["Title"] = new CSqlFieldString("Title", QryFlag.cSelRead | QryFlag.cSelWhere);
                m_sqlRow["GameID"] = new CSqlFieldInteger("GameID", QryFlag.cSelRead);
            }
            public int GameID
            {
                get { return m_sqlRow["GameID"].Integer; }
                set { m_sqlRow["GameID"].Integer = value; }
            }
            public int PlatformFK
            {
                get { return m_sqlRow["PlatformFK"].Integer; }
                set { m_sqlRow["PlatformFK"].Integer = value; }
            }
            public string Title
            {
                get { return m_sqlRow["Title"].String; }
                set { m_sqlRow["Title"].String = value; }
            }
        }
        
        #endregion // Query definitions

        private static CQryGame m_qryGame = new();
        private static CDbAttribute m_gameAttribute = new("Game");
        private static GameSet m_currentGames = new();

        /// <summary>
        /// Dictionary collection implementation for the game object
        /// Encapsulates all sorting and direct manipulation of the collection
        /// </summary>
        public class GameSet : Dictionary<string, GameObject>
        {
            /// <summary>
            /// Enum used when returning sorted lists of games
            /// The flags can be added together to support multi-parameter sorting
            /// The lower flag values take precedence as they are easier to sort
            /// Performance should be considered before sorting with all flags
            /// </summary>
            public enum SortFlag
            {
                // When adding new sorting flags, add the easiest ones (such as binary flags)
                // to the top and more difficult ones (alphabetical) to the bottom.
                [Description("IsFavourite")]
                cSortFavourite  = 0x01,
                [Description("IsInstalled")]
                cSortInstalled  = 0x02,
                [Description("Rating")]
                cSortRating     = 0x04,
                [Description("Frequency")]
                cSortFrequency  = 0x08,
                [Description("LastRun")]
                cSortDate       = 0x10,
                [Description("Title")]
                cSortAlpha      = 0x20,
            }

            private CQryGameSort m_qryGameSort;

            public GameSet()
            {
                m_qryGameSort = new CQryGameSort();
            }

            /// <summary>
            /// The current platform of the games
            /// </summary>
            public int Platform { get; set; }

            /// <summary>
            /// Sort the current GameSet
            /// </summary>
            /// <param name="flag">Sorting flags</param>
            /// <param name="ascending">If true, sort by ascending order, otherwise sort by descending</param>
            /// <param name="noArticle">If true and doing alpha sort, ignore articles in the names</param>
            /// <returns>List of sorted games</returns>
            public List<int> SortGames(SortFlag flag, bool ascending, bool noArticle)
            {
                // TODO. all, fav, new, hidden games
                // TODO. ignore article
                List<int> outGameIDs = new();

                m_qryGameSort.MakeFieldsNull();
                m_qryGameSort.PlatformFK = Platform; // Using current platform

                // Create the order by condition
                string orderByString = " ORDER BY ";
                int used = 0;
                foreach(SortFlag i in Enum.GetValues(typeof(SortFlag)))
                {
                    if((flag & i) > 0)
                    {
                        orderByString += GameLauncher_Console.CExtensions.GetDescription(i);
                        if(!ascending)
                        {
                            orderByString += " DESC, ";
                        }
                        used++;
                    }
                }
                if(used == 1) // Remove trailing comma
                {
                    orderByString = orderByString.Remove(',');
                }
                else if(used == 0) // Clear if no flags (just in case)
                {
                    orderByString = "";
                }
                m_qryGameSort.SelectExtraCondition = orderByString;
                if (m_qryGameSort.Select() == SQLiteErrorCode.Ok)
                {
                    do
                    {
                        outGameIDs.Add(m_qryGameSort.GameID);
                    } while (m_qryGameSort.Fetch());
                }
                return outGameIDs;
            }
        }

        /// <summary>
        /// Struct which actually holds the game information
        /// </summary>
        public struct GameObject
        {
            /// <summary>
            /// Constructor.
            /// Set game values using a CQryGame object
            /// </summary>
            /// <param name="qryGame">SQL query object for the game data</param>
            public GameObject(CQryGame qryGame)
            {
                GameID          = qryGame.GameID;
                PlatformFK      = qryGame.PlatformFK;
                Identifier      = qryGame.Identifier;
                Title           = qryGame.Title;
                Alias           = qryGame.Alias;
                Launch          = qryGame.Launch;
                Uninstall       = qryGame.Uninstall;
                Icon            = qryGame.Icon;
                Tags            = qryGame.Tags;
                IsInstalled     = qryGame.IsInstalled;
                IsFavourite     = qryGame.IsFavourite;
                IsNew           = qryGame.IsNew;
                IsHidden        = qryGame.IsHidden;
                LastRun         = JsonSerializer.Deserialize<DateTime>(qryGame.LastRun);
                NumRuns         = qryGame.NumRuns;
                Rating          = qryGame.Rating;
                Frequency       = qryGame.Frequency;
                IsMultiPlatform = qryGame.IsMultiPlatform;
            }

            /// <summary>
            /// Smaller constructor.
            /// Set up all necessary values, and set all optional values to zero
            /// </summary>
            /// <param name="platformFK">PlatformFK database field</param>
            /// <param name="identifier">Identifier used for launching the game (eg. steamapp id)</param>
            /// <param name="title">Game title</param>
            /// <param name="alias">Game alias</param>
            /// <param name="launch">Game executable path or launch string</param>
            /// <param name="uninstall">Game uninstaller path or uninstall string</param>
            public GameObject(int platformFK, string identifier, string title, string alias, string launch, string uninstall)
            {
                PlatformFK  = platformFK;
                Identifier  = identifier;
                Title       = title;
                Alias       = alias;
                Launch      = launch;
                Uninstall   = uninstall;

                // Default rest
                GameID          = 0;
                Icon            = "";
                Tags            = new List<string>() { "" };
                IsInstalled     = false;
                IsFavourite     = false;
                IsNew           = false;
                IsHidden        = false;
                LastRun         = DateTime.MinValue;
                NumRuns         = 0;
                Rating          = 0;
                Frequency       = 0.0f;
                IsMultiPlatform = false;
            }

            // Properties
            public int GameID           { get; }
            public int PlatformFK       { get; } // TODO, change to platform enum
            public string Identifier    { get; }
            public string Title         { get; }
            public string Alias         { get; set; }
            public string Launch        { get; }
            public string Uninstall     { get; }
            public string Icon          { get; set; }
            public List<string> Tags    { get; set; }
            public bool IsInstalled     { get; set; }
            public bool IsFavourite     { get; set; }
            public bool IsNew           { get; private set; }
            public bool IsHidden        { get; set; }
            public DateTime LastRun     { get; private set; }
            public int NumRuns          { get; private set; }
            public double Rating        { get; set; }
            public double Frequency     { get; private set; }
            public bool IsMultiPlatform { get; set; }

            /// <summary>
            /// Equals override for HashSet comparison.
            /// </summary>
            /// <param name="other">Object to compare against</param>
            /// <returns>True is other is not null and the titles are matching</returns>
            public override bool Equals(object other)
            {
                // We're only interested in comparing the titles
                return (other is GameObject game && this.Title == game.Title);
            }

            /// <summary>
            /// Return the hash code of this object's title variable
            /// </summary>
            /// <returns>Hash code</returns>
            public override int GetHashCode()
            {
                return this.Title.GetHashCode();
            }

            public static bool operator ==(GameObject left, GameObject right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(GameObject left, GameObject right)
            {
                return !(left == right);
            }
        }

        /// <summary>
        /// Return games for the selected platform
        /// </summary>
        /// <param name="platformFK">Database platform FK</param>
        /// <returns>Hashset with games for the specified platform</returns>
        public static List<GameObject> GetPlatformGames(int platformFK)
        {
            // Check the current platform and hashset
            // No point running the query if we want the same information
            if(m_currentGames.Platform == platformFK && m_currentGames.Count > 0)
            {
                return m_currentGames.Values.ToList();
            }
            m_currentGames.Clear();
            m_currentGames.Platform = platformFK;

            // Get new games and return
            m_qryGame.MakeFieldsNull();
            m_qryGame.PlatformFK = platformFK;
            if(m_qryGame.Select() == SQLiteErrorCode.Ok)
            {
                do
                {
                    m_currentGames[m_qryGame.Title] = new GameObject(m_qryGame);
                } while(m_qryGame.Fetch());
            }
            return m_currentGames.Values.ToList();
        }

        public static HashSet<GameObject> GetAllGames()
        {
            HashSet<GameObject> outHashSet = new();
            m_qryGame.MakeFieldsNull();
            if(m_qryGame.Select() == SQLiteErrorCode.Ok)
            {
                do
                {
                    outHashSet.Add(new GameObject(m_qryGame));
                } while(m_qryGame.Fetch());
            }
            return outHashSet;
        }

        /// <summary>
        /// Insert game into the database.
        /// </summary>
        /// <param name="game">Game object to insert</param>
        /// <returns>True on insert success, otherwise false</returns>
        public static bool InsertGame(GameObject game)
        {
            m_qryGame.MakeFieldsNull();
            if(game.PlatformFK > 0)
            {
                m_qryGame.PlatformFK = game.PlatformFK;
            }
            m_qryGame.Identifier = game.Identifier;
            m_qryGame.Title      = game.Title;
            m_qryGame.Alias      = (game.Alias.Length > 0) ? game.Alias : game.Title;
            m_qryGame.Launch     = game.Launch;
            m_qryGame.Uninstall  = game.Uninstall;
            if(m_qryGame.Insert() == SQLiteErrorCode.Ok)
            {
                // If the same platform as last query, add to the hashset.
                if(m_currentGames.Platform == game.PlatformFK)
                {
                    m_currentGames[game.Title] = game;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove selected game from the database
        /// </summary>
        /// <param name="gameID">The GameID primary key</param>
        /// <returns>true on delete success, otherwise false</returns>
        public static bool RemoveGame(int gameID)
        {
            m_qryGame.MakeFieldsNull();
            m_qryGame.GameID = gameID;
            return m_qryGame.Delete() == SQLiteErrorCode.Ok;
        }

        /// <summary>
        /// Toggle the game's installed flag and update the DB row
        /// </summary>
        /// <param name="game">The game object</param>
        /// <param name="isInstalled">Installed flag</param>
        /// <returns>True on update success, otherwise false</returns>
        public static bool ToggleInstalled(GameObject game, bool isInstalled)
        {
            m_qryGame.MakeFieldsNull();
            m_qryGame.GameID = game.GameID;
            m_qryGame.IsInstalled = isInstalled;
            if (m_qryGame.Update() == SQLiteErrorCode.Ok)
            {
                // If the same platform as last query, switch object
                if (m_currentGames.Platform == game.PlatformFK)
                {
                    m_currentGames[game.Title] = game;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Toggle the game's favourite flag and update the DB row
        /// </summary>
        /// <param name="game">The game object</param>
        /// <param name="isFavourite">Favourite flag</param>
        /// <returns>True on update success, otherwise false</returns>
        public static bool ToggleFavourite(GameObject game, bool isFavourite)
        {
            m_qryGame.MakeFieldsNull();
            m_qryGame.GameID = game.GameID;
            m_qryGame.IsFavourite = isFavourite;
            if(m_qryGame.Update() == SQLiteErrorCode.Ok)
            {
                // If the same platform as last query, switch object
                if(m_currentGames.Platform == game.PlatformFK)
                {
                    m_currentGames[game.Title] = game;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Toggle the game's new flag and update the DB row
        /// </summary>
        /// <param name="game">The game object</param>
        /// <param name="isNew">New flag</param>
        /// <returns>True on update success, otherwise false</returns>
        public static bool ToggleNew(GameObject game, bool isNew)
        {
            m_qryGame.MakeFieldsNull();
            m_qryGame.GameID = game.GameID;
            m_qryGame.IsNew = isNew;
            if (m_qryGame.Update() == SQLiteErrorCode.Ok)
            {
                // If the same platform as last query, switch object
                if (m_currentGames.Platform == game.PlatformFK)
                {
                    m_currentGames[game.Title] = game;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Toggle the game's hidden flag and update the DB row
        /// </summary>
        /// <param name="game">The game object</param>
        /// <param name="isHidden">Hidden flag</param>
        /// <returns>True on update success, otherwise false</returns>
        public static bool ToggleHidden(GameObject game, bool isHidden)
        {
            m_qryGame.MakeFieldsNull();
            m_qryGame.GameID = game.GameID;
            m_qryGame.IsHidden = isHidden;
            if (m_qryGame.Update() == SQLiteErrorCode.Ok)
            {
                // If the same platform as last query, switch object
                if (m_currentGames.Platform == game.PlatformFK)
                {
                    m_currentGames[game.Title] = game;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Merge game set with all games in the database.
        /// Add any new and remove any missing games from the database
        /// </summary>
        /// <param name="games">Game set to merge</param>
        public static void MergeGameSets(GameSet games)
        {
            HashSet<GameObject> newGames = games.Values.ToHashSet();
            HashSet<GameObject> allGames = GetAllGames().ToHashSet();

            HashSet<GameObject> toAdd = new(newGames);
            toAdd.ExceptWith(allGames);
            HashSet<GameObject> toRemove = new(allGames);
            toRemove.ExceptWith(newGames);

            foreach(GameObject game in toAdd)
            {
                InsertGame(game);
            }
            foreach(GameObject game in toRemove)
            {
                RemoveGame(game.GameID);
            }
        }

        /// <summary>
        /// Update all game frequencies.
        /// Increment selected game's frequency by 5
        /// Decrement all other frequencies by 10%
        /// </summary>
        /// <param name="gameID">gameID of the game to increment</param>
        public static void NormaliseFrequencies(int gameID)
        {
            CSqlDB.Instance.Execute("UPDATE Game SET Frequency = (CASE GameID WHEN " + gameID + " THEN Frequency + 5 ELSE Frequency * 0.9 END)");
        }

        /// <summary>
        /// Perform a fuzzy match on the current gameset or all games
        /// </summary>
        /// <param name="match">The search input string</param>
        /// <param name="allGames">If true search all games, otherwise search current gameset only</param>
        /// <returns>Dictionary of titles with confidence level</returns>
        public static Dictionary<string, int> FuzzyMatch(string match, bool allGames)
        {
            return null; // TODO: query and confidence check
        }
    }
}
