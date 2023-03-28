// This application entry point is based on ASP.NET Core new project templates and is included
// as a starting point for app host configuration.
// This file may need updated according to the specific scenario of the application being upgraded.
// For more information on ASP.NET Core hosting, see https://docs.microsoft.com/aspnet/core/fundamentals/host/web-host

using Logger;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
using System;
//using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
//using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameLauncher_Console
{
	/// <summary>
	/// Entry point class.
	/// Configure logging and go further into the app
	/// </summary>
	public class Program
    {
		private const string IMAGE_FOLDER_NAME = "CustomImages";
		private const string GAME_FOLDER_NAME = "CustomGames";
		private const string MIN_WINDOWS_VER = "10.0.17763.0";
		private const string MIN_WINDOWS_NAME = "Windows 10 1809";

        [STAThread] // Requirement for Shell32.Shell [and Microsoft.Web.WebView2] COM objects
		public static void Main(string[] args)
        {
#if DEBUG
			// Log unhandled exceptions
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CLogger.ExceptionHandleEvent);
#endif
			SetProcessDpiAwarenessContext(GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) + 
				(int)DpiAwarenessContext.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
			Application.EnableVisualStyles();
			//Application.SetCompatibleTextRenderingDefault(false);

			string imgFolder = Path.Combine(CDock.currentPath, IMAGE_FOLDER_NAME);
			string gameFolder = Path.Combine(CDock.currentPath, GAME_FOLDER_NAME);

			CLogger.Configure(Path.Combine(CDock.currentPath,
				Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]) + ".log")); // Create a log file
			if (!Directory.Exists(imgFolder)) Directory.CreateDirectory(imgFolder);
			if (!Directory.Exists(gameFolder)) Directory.CreateDirectory(gameFolder);

			CLogger.LogInfo("{0} version {1}", CDock.title, CDock.version);
			CLogger.LogInfo("*************************");

			if (Console.IsOutputRedirected)
				Console.WriteLine("redir");
				
			// Allow for unicode characters, such as trademark symbol
			Console.OutputEncoding = System.Text.Encoding.UTF8;  // 'The handle is invalid.'

            if (OperatingSystem.IsWindows() && Environment.OSVersion.Version < Version.Parse(MIN_WINDOWS_VER))
            {
                CLogger.LogInfo(MIN_WINDOWS_NAME + " or greater required.");
				Console.WriteLine(MIN_WINDOWS_NAME + " or greater required.");
				Environment.Exit(-1);
            }

			CDock gameDock = new();
			gameDock.MainLoop(args);
		}

		enum ProcessDpiAwareness
		{
			PROCESS_DPI_UNAWARE = 0,
			PROCESS_SYSTEM_DPI_AWARE = 1,
			PROCESS_PER_MONITOR_DPI_AWARE = 2
		}

		enum DpiAwareness
		{
			DPI_AWARENESS_INVALID = -1,
			DPI_AWARENESS_UNAWARE = 0,
			DPI_AWARENESS_SYSTEM_AWARE = 1,
			DPI_AWARENESS_PER_MONITOR_AWARE = 2
		}

		enum DpiAwarenessContext
		{
			DPI_AWARENESS_CONTEXT_UNAWARE = -1,
			DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2,
			DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3,
			DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4,
			DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = -5
		}

		[DllImport("user32.dll")]
		static extern IntPtr SetProcessDPIAware();

		[DllImport("user32.dll")]
		static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hWnd);

		[DllImport("shcore.dll")]
		static extern int GetProcessDpiAwareness(IntPtr hProcess, out ProcessDpiAwareness value);

		[DllImport("user32.dll")]
		static extern IntPtr GetThreadDpiAwarenessContext();

		[DllImport("user32.dll")]
		static extern int GetAwarenessFromDpiAwarenessContext(IntPtr dpiContext);

		[DllImport("user32.dll")]
		static extern bool SetProcessDpiAwarenessContext(int value);
	}
}
