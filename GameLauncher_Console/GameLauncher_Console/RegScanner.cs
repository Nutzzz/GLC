using Logger;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Media.Animation;

namespace GameLauncher_Console
{

	/// <summary>
	/// Class used to scan the registry and retrieve the game data.
	/// </summary>
	[SupportedOSPlatform("windows")]
	public static class CRegScanner
	{
		public const string UNINSTALL_REG			= @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
		public const string GAME_DISPLAY_NAME		= "DisplayName";
		public const string GAME_DISPLAY_ICON		= "DisplayIcon";
		public const string GAME_INSTALL_PATH		= "InstallPath";
		public const string GAME_INSTALL_LOCATION	= "InstallLocation";
		public const string GAME_UNINSTALL_STRING	= "UninstallString";
		public const string INSTALLSHIELD			= "_is1";

		/// <summary>
		/// Find game keys in specified root
		/// Looks for a key-value pair inside the specified root.
		/// </summary>
		/// <param name="root">Root folder that will be scanned</param>
		/// <param name="strValData">A substring of the target value data in the subkey</param>
		/// <param name="strValName">The target value name that should contain the target value data</param>
		/// <param name="ignore">Function will ignore these subkey names (used to ignore things like launchers)</param>
		/// <returns>List of game registry keys</returns>
		public static List<RegistryKey> FindGameKeys(RegistryKey root, string strValData, string strValName, string[] ignoreKeys)
		{
			LinkedList<RegistryKey> toCheck = new();
			List<RegistryKey> gameKeys = new();

			toCheck.AddLast(root);

			while(toCheck.Count > 0)
			{
				root = toCheck.First.Value;
				toCheck.RemoveFirst();

				if(root != null)
				{
					foreach(var name in root.GetValueNames())
					{
						if(root.GetValueKind(name) == RegistryValueKind.String && name.Equals(strValName))
						{
							if(((string)root.GetValue(name)).Contains(strValData, CDock.IGNORE_CASE))
							{
								gameKeys.Add(root);
								break;
							}
						}
					}

					foreach(var sub in root.GetSubKeyNames()) // Add subkeys to search list
					{
						if(sub.Equals("Microsoft")) // Microsoft folder only contains system stuff and it doesn't need searching
							break;

						bool ignore = false;
						foreach (string entry in ignoreKeys)
						{
							if (sub.Equals(entry))
							{
								ignore = true;
								break;
							}
						}
						if (!ignore)
						{
							try
							{
								toCheck.AddLast(root.OpenSubKey(sub, RegistryKeyPermissionCheck.ReadSubTree));
							}
							catch (Exception e)
							{
								CLogger.LogError(e);
							}
						}
					}
				}
			}
			return gameKeys;
		}

		/// <summary>
		/// Find game folders in the registry.
		/// This method will look for and return folders that match the input string.
		/// </summary>
		/// <param name="root">Root directory that will be scanned</param>
		/// <param name="strFolder">Target game folder</param>
		/// <returns>List of Reg keys with game folders</returns>
		public static List<RegistryKey> FindGameFolders(RegistryKey root, string strFolder)
		{
			LinkedList<RegistryKey> toCheck = new();
			List<RegistryKey> gameKeys = new();

			toCheck.AddLast(root);

			while(toCheck.Count > 0)
			{
				root = toCheck.First.Value;
				toCheck.RemoveFirst();

				if(root != null)
				{
					foreach(var sub in root.GetSubKeyNames())
					{
						if(!(sub.Equals("Microsoft")))
						{
							if (string.IsNullOrEmpty(strFolder) || sub.Contains(strFolder, CDock.IGNORE_CASE))
								gameKeys.Add(root.OpenSubKey(sub, RegistryKeyPermissionCheck.ReadSubTree));
						}
					}
				}
			}
			return gameKeys;
		}

		/// <summary>
		/// Get a value from the registry if it exists
		/// </summary>
		/// <param name="key">The registry key</param>
		/// <param name="valName">The registry value name</param>
		/// <returns>the value's string data</returns>
		public static string GetRegStrVal(RegistryKey key, string valName)
		{
			try
			{
				object valData = key.GetValue(valName);
				if (valData != null)
					return valData.ToString();
			}
			catch (Exception e)
            {
				CLogger.LogError(e);
            }
			return string.Empty;
		}

		/// <summary>
		/// Get a value from the registry if it exists
		/// </summary>
		/// <param name="key">The registry key</param>
		/// <param name="valName">The registry value name</param>
		/// <returns>the value's DWORD (UInt32) data</returns>
		public static int? GetRegDWORDVal(RegistryKey key, string valName)
		{
			try
			{
				object valData = key.GetValue(valName);
				/*
				Type valType = valData.GetType();
				if (valData != null && valType == typeof(int))
				{
				*/
					if (int.TryParse(valData.ToString(), out int result))
						return result;
				//}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return null;
		}

		/// <summary>
		/// Get a value from the registry if it exists
		/// </summary>
		/// <param name="key">The registry key</param>
		/// <param name="valName">The registry value name</param>
		/// <returns>the value's data as a string</returns>
		public static byte[] GetRegBinaryVal(RegistryKey key, string valName)
		{
			try
			{
				object valData = key.GetValue(valName);
				/*
				Type valType = valData.GetType();
				if (valData != null && valType == typeof(byte))
				{
				*/
				//if (byte.TryParse(valData.ToString(), out byte result))
				return (byte[])valData;
				//}
			}
			catch (Exception e)
			{
				CLogger.LogError(e);
			}
			return null;
		}

        /// <summary>
        /// Extract hive, key, and value separately from a registry path
        /// </summary>
        /// <param name="key">The registry key</param>
        /// <param name="valName">The registry value name</param>
        /// <returns>the value's data as a string</returns>
        public static RegistryKey ToRegKey(ref string key, out string valName)
		{
			valName = Path.GetFileName(key);
			key = Path.GetDirectoryName(key);
			if (key.ToUpper().StartsWith(@"HKEY_CLASSES_ROOT\"))
			{
				key = key[18..];
				return Registry.ClassesRoot;
			}
            if (key.ToUpper().StartsWith(@"HKCR\"))
            {
                key = key[5..];
                return Registry.ClassesRoot;
            }
            else if (key.ToUpper().StartsWith(@"HKEY_CURRENT_CONFIG\"))
			{
				key = key[20..];
				return Registry.CurrentConfig;
			}
            else if (key.ToUpper().StartsWith(@"HKCC\"))
            {
                key = key[5..];
                return Registry.CurrentConfig;
            }
            else if (key.ToUpper().StartsWith(@"HKEY_CURRENT_USER\SOFTWARE\WOW6432NODE\"))
            {
                key = @"SOFTWARE\" + key[39..];
				return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
            }
            else if (key.ToUpper().StartsWith(@"HKCU\SOFTWARE\WOW6432NODE\"))
            {
                key = @"SOFTWARE\" + key[26..];
                return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
            }
            else if (key.ToUpper().StartsWith(@"HKCU64\"))
            {
                key = key[7..];
                return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            }
            else if (key.ToUpper().StartsWith(@"HKCU32\"))
			{
                key = key[7..];
                return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
            }
            else if (key.ToUpper().StartsWith(@"HKEY_CURRENT_USER\"))
            {
                key = key[18..];
                return Registry.CurrentUser;
            }
            else if (key.ToUpper().StartsWith(@"HKCU\"))
			{
				key = key[5..];
				return Registry.CurrentUser;
			}
            else if (key.ToUpper().StartsWith(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432NODE\"))
            {
                key = @"SOFTWARE\" + key[40..];
				return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }
            else if (key.ToUpper().StartsWith(@"HKLM\SOFTWARE\WOW6432NODE\"))
            {
                key = @"SOFTWARE\" + key[26..];
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }
            else if (key.ToUpper().StartsWith(@"HKLM64\"))
            {
                key = key[7..];
                return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            }
            else if (key.ToUpper().StartsWith(@"HKLM32\"))
            {
                key = key[7..];
                return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
            }
            else if (key.ToUpper().StartsWith(@"HKEY_LOCAL_MACHINE\"))
            {
                key = key[19..];
                return Registry.LocalMachine;
            }
            else if (key.ToUpper().StartsWith(@"HKLM\"))
			{
				key = key[5..];
				return Registry.LocalMachine;
			}
			else if (key.ToUpper().StartsWith(@"HKEY_PERFORMANCE_DATA\"))
			{
				key = key[22..];
				return Registry.PerformanceData;
			}
            else if (key.ToUpper().StartsWith(@"HKPD\"))
            {
                key = key[5..];
                return Registry.PerformanceData;
            }
            else if (key.ToUpper().StartsWith(@"HKEY_USERS\"))
			{
				key = key[11..];
				return Registry.Users;
			}
            else if (key.ToUpper().StartsWith(@"HKU\"))
            {
                key = key[4..];
                return Registry.Users;
            }
            return null;
		}
	}
}