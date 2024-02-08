using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Logger;

namespace GameLauncher_Console
{
	/// <summary>
	/// Class for finding game binary (.exe) and link/shortcus (.lnk) files inside directories.
	/// </summary>
	public static class CGameFinder
	{
		/// <summary>
		/// Find the game's binary executable file
		/// </summary>
		/// <param name="strPath">Path to search</param>
		/// <param name="strTitle">Title of the game</param>
		/// <returns></returns>
		public static string FindGameBinaryFile(string strPath, string strTitle)
		{
			/* function flow
			 * Find all exe files in the directory. (Exclude files with words: "unins", "redist")
			 * Compare exe files with gameTitle
			 * If failed, compare with name of root directory
			 * Return "" if failed
			 */

			// Get our list of exe files in the game folder + all subfolders
			if (!Directory.Exists(strPath))
				return "";
			
			List<string> exeFiles = Directory.EnumerateFiles(strPath, "*", SearchOption.AllDirectories).Where(s => s.EndsWith(".exe")).ToList();
			
			// Big Fish Games may use .bfg for executables
			//exeFiles.AddRange(Directory.EnumerateFiles(strPath, "*", SearchOption.AllDirectories).Where(s => s.EndsWith(".bfg")).ToList());

			// If only 1 file has been found, return it.
			if (exeFiles.Count == 1)
			{
				return exeFiles[0];
			}

			// First, compare the files against the game title
			{
				string[] words = strTitle.Split(new char[] { ' ', ':', '-', '_' });
				string letters = "";

				foreach (string word in words)
				{
					if (!string.IsNullOrEmpty(word))
						letters += word[0];
				}

				foreach (string file in exeFiles)
				{
					//CLogger.LogInfo("*** {0}", file);
					if (file.ToLower().Contains("redist")) // A lot of steam games contain C++ redistributables. Ignore them
						continue;
					if (file.ToLower().Contains("unins"))
						continue;
					if (file.ToLower().Contains("setup"))
						continue;


					string description = FileVersionInfo.GetVersionInfo(file).FileDescription ?? "";
					description = description.ToLower();

					FileInfo info = new(file);
					string name = info.Name[..info.Name.IndexOf('.')].ToLower();

					// Perform a check against the acronym
					if (letters.Length > 2)
					{
						if (letters.ToLower().Contains(name) || name.Contains(letters.ToLower()))
							return file;

						else if (!string.IsNullOrEmpty(description) &&
							(letters.ToLower().Contains(description) || description.Contains(letters.ToLower())))
							return file;
					}

					foreach (string word in words)
					{
						if (string.IsNullOrEmpty(word) || word.Length < 3)
							continue;

						if (name.Contains(word.ToLower()))
							return file;

						if (!string.IsNullOrEmpty(description) && description.Contains(word.ToLower()))
							return file;
					}
				}
			}

			// If search failed, we need to compare the exe files against the name of root directory
			{
				string[] words = { "" };
				string letters = "";

				int pathLen = strPath.LastIndexOf('\\');
				if (pathLen < 0)
				{
					pathLen = strPath.LastIndexOf('/');
					if (pathLen < 0)
					{
						pathLen = 0;
					}
					else
						words = strPath[strPath.LastIndexOf('/')..].Split(new char[] { ' ', '-', '_', ':' });
				}
				else
					words = strPath[strPath.LastIndexOf('\\')..].Split(new char[] { ' ', '-', '_', ':' });
				
				foreach (string word in words)
				{
					if (!string.IsNullOrEmpty(word))
						letters += word[0];
				}

				foreach (string file in exeFiles)
				{
					//CLogger.LogInfo("*** {0}", file);
					if (file.ToLower().Contains("redist")) // A lot of steam games contain C++ redistributables. Ignore them
						continue;
					if (file.ToLower().Contains("unins"))
						continue;

					string description = FileVersionInfo.GetVersionInfo(file).FileDescription ?? "";
					description = description.ToLower();

					FileInfo info = new(file);
					string name = info.Name[..info.Name.IndexOf('.')].ToLower();

					// Perform a check against the acronym
					if (letters.Length > 2)
					{
						if (letters.ToLower().Contains(name) || name.Contains(letters.ToLower()))
							return file;

						else if (!string.IsNullOrEmpty(description) &&
							(letters.ToLower().Contains(description) || description.Contains(letters.ToLower())))
							return file;
					}

					foreach (string word in words)
					{
						if (string.IsNullOrEmpty(word) || word.Length < 3)
							continue;

						if (name.Contains(word.ToLower()))
							return file;

						if (!string.IsNullOrEmpty(description) && description.Contains(word.ToLower()))
							return file;
					}
				}
			}
			return "";
		}
	}
}
