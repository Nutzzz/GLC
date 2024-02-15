using System;
using System.IO;
using GameLauncher_Console;

namespace Logger
{
	/// <summary>
	/// Logger class
	/// Saves text to log files. Log files can be redefined, in runtime, but can only save to one file at a time.
	/// Currently supports the following logging levels:
	/// Info:	General purpose log
	/// Debug:	Same as Info, but only works in DEBUG configuration
	/// Warn:	Warning log.
	/// Error:	Log exceptions (must be called manually).
	/// Fatal:	Log unhandled exceptions. (called automatically at runtime)
	/// </summary>
    public static class CLogger
    {
		private static string m_strFilePath;

		/// <summary>
		/// Catch and log unhandled exceptions using LogFatal().
		/// In order to make this handler work, the following code must be used in the main function:
		/// AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Logger.CLogger.ExceptionHandleEvent);
		/// </summary>
		/// <param name="sender">Event sender object</param>
		/// <param name="args">Unhandled exception arguments</param>
		public static void ExceptionHandleEvent(object sender, UnhandledExceptionEventArgs args)
		{
			LogFatal((Exception)args.ExceptionObject);
		}

		/// <summary>
		/// Setup the initial logger configuration
		/// </summary>
		/// <param name="strFilePath">Path to the log file. If null (default), the logger will not write to file.</param>
		public static void Configure(string strPathToLog)
		{
			// Log file path cannot be empty
			if(strPathToLog.Length < 1)
			{
				//throw new LogUndefinedException("Log file cannot be empty");
			}
			
			// Check that the file exists or at least that the directory is accessible
			if(File.Exists(strPathToLog))
			{
				m_strFilePath = strPathToLog;
				try
				{
                    using StreamWriter writer = new(m_strFilePath, append: true);
                    writer.WriteLine("_____S_T_A_R_T_E_D_____");
                    writer.Close();
                }
				catch { }
				return;
			}

			// Try to create a file and write the starting line
			try
			{
                using StreamWriter writer = new(strPathToLog, append: true);
                writer.WriteLine("_____S_T_A_R_T_E_D_____");
                writer.Close();
            }
			catch(Exception)
			{
				//throw new LogUndefinedException("Log file definition is not valid: {0}");
			}
			m_strFilePath = strPathToLog;
		}

#if DEBUG
		/// <summary>
		/// Test function: only seen with DEBUG config
		/// Set the logPath member variable to null
		/// </summary>
		public static void ResetLogFile()
		{
			m_strFilePath = null;
		}
#endif

		/// <summary>
		/// Debug log. Works only in DEBUG configuration
		/// General purpose log to display information about the application behaviour.
		/// </summary>
		/// <param name="strMessage">Message</param>
		/// <param name="list">Optional list of parameters for string formatting</param>
		public static void LogDebug(string strMessage, params object[] list)
		{
#if !DEBUG
			if ((bool)CConfig.GetConfigBool(CConfig.CFG_DEBUG))
#endif
				LogInfo(strMessage, list);
		}

		/// <summary>
		/// Information log.
		/// Same as LogDebug(), but works outside of DEBUG configuration
		/// <param name="strMessage">Message</param>
		/// <param name="list">Optional list of parameters for string formatting</param>
		/// </summary>
		public static void LogInfo(string strMessage, params object[] list)
		{
			if (m_strFilePath == null || m_strFilePath.Length < 0)
			{
				//throw new LogUndefinedException("Log file not defined");
			}

			try
			{
                using StreamWriter writer = new(m_strFilePath, append: true);
                string strDateTime = DateTime.Now.ToString();
                string msg = string.Format(strMessage, list);
                string line = string.Format("{0} : {1}", strDateTime, msg);
                writer.WriteLine(line);
                writer.Close();
            }
			catch { }
		}

		/// <summary>
		/// Warning log.
		/// Display information about indications of problems
		/// The same functionality as LogInfo() but also prints "WARNING: " before the message
		/// <param name="strMessage">Message</param>
		/// <param name="list">Optional list of parameters for string formatting</param>
		/// </summary>
		public static void LogWarn(string strMessage, params object[] list)
		{
			string msg = string.Format("WARNING: {0}", strMessage);
			LogInfo(msg, list);
		}

		/// <summary>
		/// Error log.
		/// Display the optional argument message along with information about the exception
		/// </summary>
		/// <param name="ex">Exception</param>
		/// <param name="strMessage">Optional message</param>
		public static void LogError(Exception ex, string strMessage = null)
		{
			if (m_strFilePath == null || m_strFilePath.Length < 0)
			{
				//throw new LogUndefinedException("Log file not defined");
			}

			try
			{
                using StreamWriter writer = new(m_strFilePath, append: true);
                string strDateTime = DateTime.Now.ToString();
                string msg = string.Format("ERROR: {0} | {1} {2} | {3}", ex.Message, ex.Source, ex.StackTrace, strMessage ?? "");
                string line = string.Format("{0} : {1}", strDateTime, msg);
                writer.WriteLine(line);
                writer.Close();
            }
			catch { }
		}

		/// <summary>
		/// Fatal log.
		/// Called when the application meets an unexpected fatal error which results in termination
		/// <param name="ex">Exception</param>
		/// </summary>
		private static void LogFatal(Exception ex)
		{
			if (m_strFilePath == null || m_strFilePath.Length < 0)
			{
				//throw new LogUndefinedException("Log file not defined");
			}

			try
			{
                using StreamWriter writer = new(m_strFilePath, append: true);
                string strDateTime = DateTime.Now.ToString();
                string msg = string.Format("FATAL ERROR: {0} | {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                string line = string.Format("{0} : {1}", strDateTime, msg);
                writer.WriteLine(line);
                writer.Close();
            }
			catch { }
		}
    }

	/// <summary>
	/// Exception thrown when the logger log file is undefined
	/// </summary>
	public class LogUndefinedException : Exception
	{
		/// <summary>
		/// Default constructor
		/// </summary>
		public LogUndefinedException()
		{
			
		}

		/// <summary>
		/// Constructor overload
		/// </summary>
		/// <param name="strMessage">Exception message</param>
		public LogUndefinedException(string strMessage) : base(strMessage)
		{

		}

		/// <summary>
		/// Constructor overload:
		/// </summary>
		/// <param name="strMessage">Exception message</param>
		/// <param name="innerEx">Inner exception</param>
		public LogUndefinedException(string strMessage, Exception innerEx) : base(strMessage, innerEx)
		{

		}
	}
}
