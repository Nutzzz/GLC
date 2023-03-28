//using HtmlAgilityPack;
using Logger;
//using Microsoft.Web.WebView2;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
//using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
/*
using System.Management.Automation;		// PowerShell Reference Assemblies
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
*/
using System.Xml;
/*
//using Windows.Management.Deployment;	// PackageManager [won't work unless this is a UWP or MSIX-packaged app?]
using XboxWebApi.Authentication;
using XboxWebApi.Authentication.Model;
*/
using static GameLauncher_Console.CGameData;
//using static GameLauncher_Console.CJsonWrapper;
using static GameLauncher_Console.CRegScanner;
//using static GameLauncher_Console.CWebStuff;

namespace GameLauncher_Console
{
	public class PlatformMicrosoft : IPlatform
	{
		// Microsoft Store/Xbox Game Pass
		
		// [experimental, DEBUG only]

		public const GamePlatform ENUM			= GamePlatform.Microsoft;
		public const string PROTOCOL			= "msxbox://";
		//public const string LAUNCH_SUFFIX		= @":\\";
		private const string MSSTORE_APP		= "Microsoft.WindowsStore_8wekyb3d8bbwe!App";
		private const string MSSTORE_MSPREFIX	= "xboxliveapp-";

		private static readonly string _name = Enum.GetName(typeof(GamePlatform), ENUM);

		private readonly List<string> manifestNamespaces = new()
		{
			"http://schemas.microsoft.com/appx/manifest/foundation/windows10",
			"http://schemas.microsoft.com/appx/2010/manifest"
		};

		GamePlatform IPlatform.Enum => ENUM;

		string IPlatform.Name => _name;

		string IPlatform.Description => GetPlatformString(ENUM);

		[SupportedOSPlatform("windows")]
		public static void Launch() => Process.Start($"explorer.exe shell:AppsFolder\\{MSSTORE_APP}"); // Microsoft Store
		//public static void Launch() => CDock.StartShellExecute(PROTOCOL); // Xbox app

		// return value
		// -1 = not implemented
		// 0 = failure
		// 1 = success
		[SupportedOSPlatform("windows")]
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
				CDock.StartShellExecute(game.Launch);
			else
				Process.Start(game.Launch);
		}

		[SupportedOSPlatform("windows")]
		public void GetGames(List<ImportGameData> gameDataList, bool expensiveIcons = false)
		{
			string strPlatform = GetPlatformString(ENUM);

			// Registry + URI method
			List<RegistryKey> appList = new();

			// Any app that can be launched with an "xboxliveapp-*" URL is automatically collected (these seem to be just games from Microsoft Studios)
			// For the moment, additional games can be gathered by title added to glc.ini

#if DEBUG
			List<string> msStoreWhiteList = CConfig.GetConfigList(CConfig.CFG_UWPLIST);
#endif

			using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"ActivatableClasses\Package", RegistryKeyPermissionCheck.ReadSubTree))
			{
				if (key == null)
				{
					CLogger.LogInfo("{0} client not found in the registry.", _name.ToUpper());
					return;
				}
				appList = FindGameFolders(key, "");
			}

			foreach (var app in appList)
			{
				bool found = false;
				string strID = Path.GetFileName(app.Name);
				string strTitle = "";
				string strLaunch = "";
				string strIconPath = "";
				string strAlias = "";
				List<RegistryKey> classList = new();

				using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(Path.Combine(@"Extensions\ContractId\Windows.Protocol\PackageId", strID, "ActivatableClassId"), RegistryKeyPermissionCheck.ReadSubTree))
				{
					if (key != null)
					{
						classList = FindGameFolders(key, "");
						foreach (var appClass in classList)
						{
                            using RegistryKey key2 = Registry.ClassesRoot.OpenSubKey(Path.Combine(appClass.Name, "CustomProperties")[18..], RegistryKeyPermissionCheck.ReadSubTree);
                            if (key2 != null)
                            {
                                string protocol = "";
                                try
                                {
                                    strTitle = appClass.GetValue(GAME_DISPLAY_NAME).ToString();
                                    strAlias = GetAlias(appClass.GetValue("Description").ToString());
                                    protocol = key2.GetValue("Name").ToString();
                                }
                                catch (Exception e)
                                {
                                    CLogger.LogError(e);
                                }

                                if (protocol.StartsWith(MSSTORE_MSPREFIX))
                                {
                                    found = true;
                                    CLogger.LogDebug($"- {strTitle}");
                                    strLaunch = protocol + "://";
                                    break;
                                }
#if DEBUG
								else
                                {
                                    foreach (string item in msStoreWhiteList)
                                    {
                                        if (strTitle.Equals(item))
                                        {
                                            found = true;
                                            CLogger.LogDebug($"- {strTitle}");
                                            strLaunch = protocol + "://";
                                            break;
                                        }
                                    }
                                    if (found)
                                        break;
                                }
#endif
							}
                        }
					}
				}

				if (!found)
				{
                    using RegistryKey key = Registry.ClassesRoot.OpenSubKey(
						Path.Combine(@"Extensions\ContractId\Windows.Launch\PackageId", strID, "ActivatableClassId", "App"), 
						RegistryKeyPermissionCheck.ReadSubTree);
                    if (key != null)
                    {
                        try
                        {
                            strTitle = key.GetValue(GAME_DISPLAY_NAME).ToString();
                            strAlias = GetAlias(key.GetValue("Description").ToString());
                            // strLaunch will be found using AppxManifest.xml below
                        }
                        catch (Exception e)
                        {
                            CLogger.LogError(e);
                        }
                    }
                }

				using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
					Path.Combine(@"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages", strID)))
				{
					if (key != null)
					{
						try
						{
							string basePath = key.GetValue("PackageRootFolder").ToString();
							if (strTitle.StartsWith("@"))
							{
								strTitle = key.GetValue(GAME_DISPLAY_NAME).ToString();
								if (!found)
								{
#if DEBUG
									foreach (string item in msStoreWhiteList)
									{
										if (strTitle.Equals(item))
										{
											found = true;
											CLogger.LogDebug($"- {strTitle}");
											strAlias = GetAlias(strTitle);
										}
									}
#endif
								}
							}

							if (found)
							{
								// get icon, and launch if not already found
								if (!string.IsNullOrEmpty(basePath))
								{
									string xmlPath = Path.Combine(basePath, "AppxManifest.xml");
									if (File.Exists(xmlPath))
									{
										XmlDocument doc = new();
										doc.Load(xmlPath);
										XmlNamespaceManager nsMgr = new(doc.NameTable);
										foreach (var ns in manifestNamespaces)
										{
											nsMgr.AddNamespace("schema", ns);

											string id = "";
											string exe = "";
											XmlNode appNode = doc.SelectSingleNode("//schema:Application", nsMgr);

											if (appNode != null)
											{
												XmlAttribute idAttr = appNode.Attributes["Id"];
												if (idAttr != null)
													id = idAttr.Value;
												XmlAttribute exeAttr = appNode.Attributes["Executable"];
												if (exeAttr != null)
													exe = exeAttr.Value;

												if (string.IsNullOrEmpty(strLaunch))
												{
													if (!string.IsNullOrEmpty(id))
														strLaunch = $"explorer.exe shell:appsFolder\\{strID}!{id}";
													else if (!string.IsNullOrEmpty(exe))
														strLaunch = Path.Combine(basePath, exe);
												}
											}

											if (!string.IsNullOrEmpty(strLaunch))
											{
												if (expensiveIcons)
												{
													XmlNode logoNode = doc.SelectSingleNode("//schema:Logo", nsMgr);
													if (logoNode != null)
													{
														string iconBase = Path.Combine(basePath, logoNode.InnerText);
														string iconDir = Path.GetDirectoryName(iconBase);
														string iconFile = Path.GetFileNameWithoutExtension(iconBase);
														string iconExt = Path.GetExtension(iconBase);

														if (!Directory.Exists(iconDir))
															iconDir = iconDir.Replace(@"Assets\PackageVisuals\", @"Assets\PackageVisuals\en-US\"); // TODO: Figure out how to do this right; I've only noticed this with Minesweeper so far

														foreach (string suffix in new List<string> { ".scale-400", ".scale-200", ".scale-150", ".scale-125", ".scale-100", "" })
														{
															string iconTest = Path.Combine(iconDir, iconFile + suffix + iconExt);
															if (File.Exists(iconTest))
															{
																strIconPath = iconTest;
																break;
															}
														}
														break;
													}
												}
												else
													strIconPath = Path.Combine(basePath, exe);
											}
										}
									}
								}
							}
						}
						catch (Exception e)
						{
							CLogger.LogError(e);
						}
					}
				}

				if (found && !string.IsNullOrEmpty(strLaunch))
				{
					if (strAlias.Length > strTitle.Length)
						strAlias = GetAlias(strTitle);
					if (strAlias.Equals(strTitle))
						strAlias = "";
					gameDataList.Add(new ImportGameData(strID, strTitle, strLaunch, strIconPath, null, strAlias, true, strPlatform));
				}
			}

			// TODO?: PowerShell Reference Assemblies method
			/*
			PowerShell ps = PowerShell.Create();
			ps.AddCommand("Find-Package");

			//or maybe: ps.AddCommand("Get-StartApps | where{$_.AppID -like '*!*'}")

			foreach (PSObject result in ps.Invoke())
			{
				CLogger.LogDebug(result);
				string strID = "";
				string strTitle = "";
				string strLaunch = "";
				string strIconPath = "";
				string strUninstall = "";
				string strAlias = "";
				try
				{
					strID = ""; //package.Id.Name;
					strTitle = strID;
					CLogger.LogDebug($"- {strTitle}");
					strLaunch = strID;
					strIconPath = strID;
					strUninstall = strID;
					strAlias = strID;
					if (strAlias.Equals(strTitle))
						strAlias = "";
				}
				catch (Exception e)
				{
					CLogger.LogError(e);
				}
				if (!(string.IsNullOrEmpty(strLaunch)))
					gameDataList.Add(
						new ImportGameData(strID, strTitle, strLaunch, strIconPath, strUninstall, strAlias, true, strPlatform));
			}
			*/

			// TODO?: Internet method
			/*
			string xboxUsername = "";
			string msUsername = "";
			string msPassword = "";
			string url = $"https://account.xbox.com/en-us/profile?gamertag={xboxUsername}&activetab=main:mainTab2";
			HtmlWeb web = new();
			web.UseCookies = true;
			web.PreRequest += (request) => {
				request.Credentials = new NetworkCredential(msUsername, msPassword);
				return true;
			};
			*/
			/*
#if DEBUG
			string tmpfile = $"tmp_{_name}.html";

			try
			{
				if (!File.Exists(tmpfile))
				{
					using (WebClient client = new());
					client.DownloadFile(url, tmpfile);
				}
				HtmlDocument doc = new HtmlDocument
				{
					OptionUseIdAttribute = true
				};
				doc.Load(tmpfile);
#else
			*/
			/*
				HtmlDocument doc = web.Load(url);
				doc.OptionUseIdAttribute = true;
			*/
			//#endif
			// div#gamesList.All
			// <ul class="recentProgressList"><li style><xbox-title-item><div class="recentProgressInfoWrapper"><a class="recentProgressLinkWrapper" ...
			// data-m="{\"cN\":\"Microsoft Minesweeper\",\"bhvr\":281,\"tags\":{\"titleName\":\"Microsoft Minesweeper\",\"titleType\":\"XboxOne\"}}"

			//HtmlNode gameList = doc.DocumentNode.SelectNodes("recentProgressList");

			/*
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			*/

			// TODO?: PackageManager method
			/*
			if (System.Environment.OSVersion.Version.Major < 10 || System.Environment.OSVersion.Version.Build < 18917)
			{
				CLogger.LogInfo("Microsoft Store support requires Windows 10, build 18917 or newer.");
				return;
			}

			PackageManager packageManager = new PackageManager();

			try
			{
				var packages = packageManager.FindProvisionedPackages();

				if (packages.Count == 0)
				{
					Console.WriteLine("No packages were found.");
				}
				else
				{
					foreach (var package in packages)
					{
						DisplayPackageInfo(package);
						Console.WriteLine();
					}
				}

			}
			catch (UnauthorizedAccessException)
			{
				Console.WriteLine("packageManager.FindProvisionedPackages() failed because access was denied. This program must be run from an elevated command prompt.");
			}
			catch (Exception ex)
			{
				Console.WriteLine("packageManager.FindProvisionedPackages() failed, error message: {0}", ex.Message);
				Console.WriteLine("Full Stacktrace: {0}", ex.ToString());
			}
			*/

			CLogger.LogDebug("----------------------");
		}

		public static string GetIconUrl(CGame _) => throw new NotImplementedException();

		public static string GetGameID(string key) => key;

		/*
		private static void DisplayPackageInfo(Windows.ApplicationModel.Package package)
		{
			Console.WriteLine("Name: {0}", package.Id.Name);

			Console.WriteLine("FullName: {0}", package.Id.FullName);

			Console.WriteLine("Version: {0}.{1}.{2}.{3}", package.Id.Version.Major, package.Id.Version.Minor,
				package.Id.Version.Build, package.Id.Version.Revision);

			Console.WriteLine("Publisher: {0}", package.Id.Publisher);

			Console.WriteLine("PublisherId: {0}", package.Id.PublisherId);
		}
		*/

		/*
		public static async Task SignInAsync()
        {
			
			string requestUrl = AuthenticationService.GetWindowsLiveAuthenticationUrl();

			// Call requestUrl via WebWidget or manually and authenticate

			// Generates state and PKCE values.
			string state = randomDataBase64url(32);
			string code_verifier = randomDataBase64url(32);
			//string code_challenge = base64urlencodeNoPadding(sha256(code_verifier));
			//const string code_challenge_method = "S256";

			// Creates a redirect URI using an available port on the loopback address.
			string redirectURI = string.Format("http://{0}:{1}/", IPAddress.Loopback, GetRandomUnusedPort());
			CLogger.LogDebug("redirect URI: " + redirectURI);

			// Creates an HttpListener to listen for requests on that redirect URI.
			var http = new HttpListener();
			http.Prefixes.Add(redirectURI);
			CLogger.LogDebug("Listening..");
			http.Start();

			Process.Start(requestUrl);
			var context = await http.GetContextAsync();

			WindowsLiveResponse response = AuthenticationService.ParseWindowsLiveResponse(
				"<Received Redirection URL>");

			AuthenticationService authenticator = new AuthenticationService(response);

			FileStream fs = new FileStream("glc-tokens.json", FileMode.Create);
			bool success = authenticator.Authenticate();
			if (!success)
			{
				CLogger.LogWarn("Authentication failed!");
				return;
			}
			else
			{
				authenticator.DumpToFile(fs);
				CLogger.LogDebug("Tokens saved:");
				CLogger.LogDebug(authenticator.XToken.ToString());
				CLogger.LogDebug(authenticator.UserInformation.ToString());
			}

		*/
			/*
			if (!authenticator.XToken.Valid)
			{
				Console.WriteLine("Token expired, please refresh / reauthenticate");
				return;
			}

			XblConfiguration xblConfig = new XblConfiguration(authenticator.XToken, XblLanguage.United_States);

			PresenceService presenceService = new PresenceService(xblConfig);
			PeopleService peopleService = new PeopleService(xblConfig);
			MessageService messageService = new MessageService(xblConfig);
			// ... more services

			var friends = peopleService.GetFriendsAsync();
			var presenceBatch = presenceService.GetPresenceBatchAsync(friends.GetXuids());
			for (int i = 0; i < friends.TotalCount; i++)
			{
				Console.WriteLine($"{presenceBatch[i].Xuid} is {presenceBatch[i].State}");
			}
			*/
		/*

			// GET INVENTORY: https://inventory.xboxlive.com/users/me/inventory

			fs.Close();
		}
		*/
	}

	/*
	static class NativeMethods
	{
		[DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
		private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

		static internal string ExtractStringFromPRIFile(string pathToPRI, string resourceKey)
		{
			string sWin8ManifestString = string.Format("@{{{0}? {1}}}", pathToPRI, resourceKey);
			var outBuff = new StringBuilder(1024);
			int result = SHLoadIndirectString(sWin8ManifestString, outBuff, outBuff.Capacity, IntPtr.Zero);
			return outBuff.ToString();
		}

		public enum ActivateOptions
		{
			None = 0x00000000,  // No flags set
			DesignMode = 0x00000001,  // The application is being activated for design mode, and thus will not be able to
			// to create an immersive window. Window creation must be done by design tools which
			// load the necessary components by communicating with a designer-specified service on
			// the site chain established on the activation manager.  The splash screen normally
			// shown when an application is activated will also not appear.  Most activations
			// will not use this flag.
			NoErrorUI = 0x00000002,  // Do not show an error dialog if the app fails to activate.
			NoSplashScreen = 0x00000004,  // Do not show the splash screen when activating the app.
		}

		[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		public interface IApplicationActivationManager
		{
			// Activates the specified immersive application for the "Launch" contract, passing the provided arguments
			// string into the application.  Callers can obtain the process Id of the application instance fulfilling this contract.
			IntPtr ActivateApplication([In] String appUserModelId, [In] String arguments, [In] ActivateOptions options, [Out] out UInt32 processId);
	*/
	//IntPtr ActivateForFile([In] String appUserModelId, [In] IntPtr /*IShellItemArray* */ itemArray, [In] String verb, [Out] out UInt32 processId);
	//IntPtr ActivateForProtocol([In] String appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out UInt32 processId);
	/*
		}

		[ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]//Application Activation Manager
		public class ApplicationActivationManager : IApplicationActivationManager
		{
	*/
	//[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)/*, PreserveSig*/]
	//public extern IntPtr ActivateApplication([In] String appUserModelId, [In] String arguments, [In] ActivateOptions options, [Out] out UInt32 processId);
	//[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	//public extern IntPtr ActivateForFile([In] String appUserModelId, [In] IntPtr /*IShellItemArray* */ itemArray, [In] String verb, [Out] out UInt32 processId);
	//[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	//public extern IntPtr ActivateForProtocol([In] String appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out UInt32 processId);
	/*
		}
	}
	*/
}