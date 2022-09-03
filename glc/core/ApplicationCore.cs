using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using Logger;
using core.Platform;
using SqlDB;
using core.DataAccess;

namespace core
{
    /// <summary>
    /// Main entry point into the glc application
    /// Contains all core initialisation logic for database, logging, etc.
    /// Should be inherited by the implementing app class
    /// </summary>
    public abstract class CApplicationCore
    {
        private const string DATA_SCHEMA_PATH = "CreateDB.sql";

        protected List<CBasicPlatform> m_platforms;

        /// <summary>
        /// Constructor.
        /// </summary>
        protected CApplicationCore()
        {
            m_platforms = new List<CBasicPlatform>();
        }

        /// <summary>
        /// Initialise the library's core functions
        /// </summary>
        /// <returns>True on initialise success</returns>
        public virtual bool Initialise()
        {
            if(!InitialiseLogger())
            {
                return false;
            }
            if(!InitialiseDatabase())
            {
                CLogger.LogWarn("Error initialising the database: {0}", CSqlDB.Instance.Conn.LastError);
                return false;
            }
            LoadConfig();
            if(!InitialisePlatforms())
            {
                CLogger.LogWarn("Errors initialising platforms");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Initialise the logger
        /// </summary>
        /// <returns>True on initialise success</returns>
        private bool InitialiseLogger()
        {
#if DEBUG
            // Log unhandled exceptions in debug only
            System.AppDomain.CurrentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(CLogger.ExceptionHandleEvent);
#endif // DEBUG

            string currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            CLogger.Configure(Path.Combine(currentPath,
                Path.GetFileNameWithoutExtension(System.Environment.GetCommandLineArgs()[0]) + ".log")); // Create a log file

            CLogger.LogInfo("{0} version {1}",
                Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title,
                Assembly.GetEntryAssembly().GetName().Version.ToString());
            CLogger.LogInfo("*************************");

            return true;
        }

        /// <summary>
        /// Initialise the database connection
        /// If database does not exist, create new and apply data schema
        /// </summary>
        /// <returns>True on initialise success</returns>
        private bool InitialiseDatabase()
        {
            SQLiteErrorCode err = CSqlDB.Instance.Open(true);
            if(err == SQLiteErrorCode.Ok)
            {
                // Success - no op.
            }
            else if(err == SQLiteErrorCode.Schema)
            {
                // New database, apply schema
                string script = "";
                try
                {
                    script = File.ReadAllText(DATA_SCHEMA_PATH);
                }
                catch(IOException e)
                {
                    CLogger.LogError(e);
                    err = SQLiteErrorCode.Unknown; // Use unknown for non-sql issues
                }
                if(script.Length > 0)
                {
                    err = CSqlDB.Instance.Conn.Execute(script);
                }
            }
            return err == SQLiteErrorCode.Ok;
        }

        /// <summary>
        /// Load the plugins found in the "platforms" folder.
        /// Either load the platforms from the database (if exist) or
        /// create default instance and insert into the database
        /// </summary>
        /// <returns>True on initialise success</returns>
        private bool InitialisePlatforms()
        {
            var pluginLoader = new PluginLoader<CPlatformFactory<CPlatform>>();
            //var plugins = pluginLoader.LoadAll(@"C:\dev\GameHub\glc\glc\bin\Debug\net6.0\platforms"); // TODO: path
            var plugins = pluginLoader.LoadAll(@Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "platforms"));
            CLogger.LogInfo($"Loaded {plugins.Count} plugin(s)");

            foreach(var plugin in plugins)
            {
                CLogger.LogInfo($"Loaded plugin: {plugin.GetPlatformName()}");
                CPlatform platform = null;
                if(!CPlatformSQL.LoadPlatform(plugin, out platform))
                {
                    platform = plugin.CreateDefault();
                    CPlatformSQL.InsertPlatform(platform);
                    CLogger.LogInfo($"Added new platform: {platform.Name}");
                }
                m_platforms.Add(platform);
            }

            // We've loaded all data, no longer need to keep the DLLs loaded.
            pluginLoader.UnloadAll();
            return true;
        }

        protected abstract void LoadConfig();
    }

    /// <summary>
    /// Generic plugin loader class
    /// </summary>
    /// <typeparam name="T">Generic type T</typeparam>
    internal class PluginLoader<T> where T : class
    {
        private readonly List<PluginAssemblyLoadContext<T>> loadContexts = new List<PluginAssemblyLoadContext<T>>();

        /// <summary>
        /// Load all available plugins found in the specified folder
        /// Plugins must match the generic type T
        /// </summary>
        /// <param name="pluginFolder">The plugin folder absolute path</param>
        /// <returns>List of objects of generic type T</returns>
        public List<T> LoadAll(string pluginFolder)
        {
            List<T> plugins = new List<T>();
            foreach(var filePath in Directory.EnumerateFiles(pluginFolder, "*.dll", SearchOption.AllDirectories))
            {
                // TEMP
                if(filePath.Contains("BasePlatform.dll"))
                {
                    Assembly.LoadFrom(filePath);
                    continue;
                }

                T plugin = Load(filePath);
                if(plugin != null)
                {
                    plugins.Add(plugin);
                }
            }
            return plugins;
        }

        /// <summary>
        /// Load plugin from dll file, adding its context to the internal list
        /// </summary>
        /// <param name="pluginPath">The absolute path to DLL file</param>
        /// <returns>Generic instance of type T</returns>
        private T Load(string pluginPath)
        {
            PluginAssemblyLoadContext<T> loadContext = new PluginAssemblyLoadContext<T>(pluginPath);
            loadContexts.Add(loadContext);

            Assembly assembly = loadContext.LoadFromAssemblyPath(pluginPath);
            var type = assembly.GetTypes().FirstOrDefault(t => typeof(T).IsAssignableFrom(t));
            if(type == null)
            {
                return null;
            }
            return (T)Activator.CreateInstance(type);
        }

        /// <summary>
        /// Unload all plugins from internal list
        /// </summary>
        public void UnloadAll()
        {
            foreach(PluginAssemblyLoadContext<T> loadContext in loadContexts)
            {
                loadContext.Unload();
            }
        }
    }

    /// <summary>
    /// Generic plugin context class
    /// </summary>
    /// <typeparam name="T">Generic type T</typeparam>
    internal class PluginAssemblyLoadContext<T> : AssemblyLoadContext where T : class
    {
        private AssemblyDependencyResolver resolver;
        private HashSet<string> assembliesToNotLoadIntoContext;

        /// <summary>
        /// Constructor.
        /// Creatre plugin context from plugin file
        /// </summary>
        /// <param name="pluginPath">The absolute path to the plugin DLL file</param>
        public PluginAssemblyLoadContext(string pluginPath)
            : base(isCollectible: true)
        {
            var pluginInterfaceAssembly = typeof(T).Assembly.FullName;
            assembliesToNotLoadIntoContext = GetReferencedAssemblyFullNames(pluginInterfaceAssembly);
            assembliesToNotLoadIntoContext.Add(pluginInterfaceAssembly);

            resolver = new AssemblyDependencyResolver(pluginPath);
        }

        /// <summary>
        /// Get the hashset of referenced assembly names
        /// </summary>
        /// <param name="ReferencedBy">The string describing the referer of the target assemblies</param>
        /// <returns>String hashset</returns>
        private HashSet<string> GetReferencedAssemblyFullNames(string ReferencedBy)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies().FirstOrDefault(t => t.FullName == ReferencedBy)
                .GetReferencedAssemblies()
                .Select(t => t.FullName)
                .ToHashSet();
        }
    }
}
