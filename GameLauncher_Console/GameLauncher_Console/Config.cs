//using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
//using System.Runtime.InteropServices;
//using System.Text;
using System.Windows.Forms;
//using System.Windows.Input;

namespace GameLauncher_Console
{
	/// <summary>
	/// Contains the definition of the configuration data
	/// </summary>
	public class CConfig
	{
        // TODO: After switch to .NET 5+, move App.config to appsettings.json?
        // [Requires copying to output directory?]
        //public static ConfigurationBuilder builder;

		public static Dictionary<string, string> config;

		// Config JSON field defaults
		public const string KEY_NONE		= "NoName";
		public const string COLBG_UNK1		= "Black";
		public const string COLBG_UNK2		= "White";
		public const string COLFG_UNK1		= "Gray";
		public const string COLFG_UNK2		= "Black";


		public struct ConfigVolatile
        {
			public bool dontSaveChanges;
			public bool typingInput;
			public bool listView;
			public ushort iconSize;
			public ushort imageSize;
			public bool imageBorder;
        }

		public struct Hotkeys
        {
			public ConsoleKey leftCK1;
			public ConsoleKey leftCK2;
			public ConsoleKey upCK1;
			public ConsoleKey upCK2;
			public ConsoleKey rightCK1;
			public ConsoleKey rightCK2;
			public ConsoleKey downCK1;
			public ConsoleKey downCK2;
			public ConsoleKey quitCK1;
			public ConsoleKey quitCK2;
			public ConsoleKey selectCK1;
			public ConsoleKey selectCK2;
			public ConsoleKey scanCK1;
			public ConsoleKey scanCK2;
			public ConsoleKey helpCK1;
			public ConsoleKey helpCK2;
			public ConsoleKey backCK1;
			public ConsoleKey backCK2;
			public ConsoleKey pageUpCK1;
			public ConsoleKey pageUpCK2;
			public ConsoleKey pageDownCK1;
			public ConsoleKey pageDownCK2;
			public ConsoleKey firstCK1;
			public ConsoleKey firstCK2;
			public ConsoleKey lastCK1;
			public ConsoleKey lastCK2;
			public ConsoleKey launcherCK1;
			public ConsoleKey launcherCK2;
			public ConsoleKey settingsCK1;
			public ConsoleKey settingsCK2;
			public ConsoleKey searchCK1;
			public ConsoleKey searchCK2;
			public ConsoleKey completeCK1;
			public ConsoleKey completeCK2;
			public ConsoleKey cancelCK1;
			public ConsoleKey cancelCK2;
			public ConsoleKey newCK1;
			public ConsoleKey newCK2;
			public ConsoleKey deleteCK1;
			public ConsoleKey deleteCK2;
			public ConsoleKey uninstCK1;
			public ConsoleKey uninstCK2;
			public ConsoleKey shortcutCK1;
			public ConsoleKey shortcutCK2;
			public ConsoleKey hideCK1;
			public ConsoleKey hideCK2;
			public ConsoleKey faveCK1;
			public ConsoleKey faveCK2;
			public ConsoleKey aliasCK1;
			public ConsoleKey aliasCK2;
			public ConsoleKey typeCK1;
			public ConsoleKey typeCK2;
			public ConsoleKey viewCK1;
			public ConsoleKey viewCK2;
			public ConsoleKey modeCK1;
			public ConsoleKey modeCK2;
			public ConsoleKey imageCK1;
			public ConsoleKey imageCK2;
			public ConsoleKey sortCK1;
			public ConsoleKey sortCK2;
			public ConsoleKey ratingUpCK1;
			public ConsoleKey ratingUpCK2;
			public ConsoleKey ratingDownCK1;
			public ConsoleKey ratingDownCK2;
			public ConsoleKey tagsCK1;
			public ConsoleKey tagsCK2;
			public ConsoleKey downloadCK1;
			public ConsoleKey downloadCK2;
		}
		public struct Colours
        {
			public ConsoleColor bgCC;
			public ConsoleColor bgLtCC;
			public ConsoleColor titleCC;
			public ConsoleColor titleLtCC;
			public ConsoleColor subCC;
			public ConsoleColor subLtCC;
			public ConsoleColor entryCC;
			public ConsoleColor entryLtCC;
			public ConsoleColor uninstCC;
			public ConsoleColor uninstLtCC;
			public ConsoleColor highlightCC;
			public ConsoleColor highlightLtCC;
			public ConsoleColor highbgCC;
			public ConsoleColor highbgLtCC;
			public ConsoleColor inputbgCC;
			public ConsoleColor inputbgLtCC;
			public ConsoleColor inputCC;
			public ConsoleColor inputLtCC;
			public ConsoleColor errbgCC;
			public ConsoleColor errbgLtCC;
			public ConsoleColor errorCC;
			public ConsoleColor errorLtCC;
		}

		public const string CFG_DEBUG		= "flag_debug_logging";
		public const string CFG_NOQUIT		= "flag_prevent_exit";
		public const string CFG_USEFILE		= "flag_do_not_save_interface_changes";
		public const string CFG_USESCAN		= "flag_always_scan_for_new_games";
		public const string CFG_USECMD		= "flag_only_allow_command_line";
		public const string CFG_USETYPE		= "flag_typing_input_is_default";
		public const string CFG_USELIST		= "flag_single_column_list_is_default";
		public const string CFG_NOPAGE		= "flag_do_not_split_over_pages";
		public const string CFG_USESIZE		= "flag_windows_enlarge_height_to_fit";
		public const string CFG_USEALPH		= "flag_sort_alphabetically";
		public const string CFG_USEFREQ		= "flag_sort_by_frequency";
		public const string CFG_USERATE		= "flag_sort_by_user_rating";
		public const string CFG_USEFAVE		= "flag_sort_favourites_on_top";
		public const string CFG_USEINST		= "flag_sort_not_installed_on_bottom";
		public const string CFG_USELITE		= "flag_colour_light_mode_is_default";
		public const string CFG_USEALL		= "flag_always_show_all_games";
		public const string CFG_NOCFG		= "flag_do_not_show_settings_in_platform_list";			// TODO
		public const string CFG_INSTONLY	= "flag_do_not_scan_not_installed_games";				// TODO
		public const string CFG_USECUST		= "flag_only_scan_custom_games";
		public const string CFG_USETEXT		= "flag_text_use_custom_text_values";
		public const string CFG_IMGBORD		= "flag_image_draw_border_characters";
		public const string CFG_IMGCUST		= "flag_image_do_not_use_custom";
		public const string CFG_IMGRTIO		= "flag_image_ignore_custom_aspect_ratio";
		public const string CFG_IMGBGLEG	= "flag_image_use_legacy_background_colours";
		public const string CFG_IMGSCAN		= "flag_image_do_not_do_deep_scan_for_icons";
		public const string CFG_IMGDOWN		= "flag_image_do_not_download_missing";
		public const string CFG_SYNCLEG		= "flag_sync_legendary_saves";
		public const string CFG_USEEGL		= "flag_use_epic_launcher";
		public const string CFG_USEGAL		= "flag_use_gog_galaxy_launcher";
		public const string CFG_USEHUM		= "flag_use_humble_launcher";
		public const string CFG_USELEG		= "flag_use_legendary_for_epic";
#if DEBUG
		public const string CFG_UWPLIST		= "list_show_uwp_titles";
#endif
		// images only work in conhost (cmd or PowerShell and some 3rd party shells), but not in others, e.g., Windows Terminal, TCC, etc.
		public const string CFG_ALIASLEN	= "num_alias_max_length";
		public const string CFG_ICONSIZE	= "num_list_icons_max_size_in_characters";		// only in list mode, icons for all games on left; set to 0 to disable
		public const string CFG_ICONRES		= "num_list_icons_resolution";					// up to 256, but setting higher than 48 causes icons with 32x32 max size to have a border and become smaller by comparison
		public const string CFG_IMGSIZE		= "num_selected_image_max_size_in_characters";	// the icon for the selected game on right; set to 0 to disable
		public const string CFG_IMGRES		= "num_selected_image_icon_resolution";			// up to 256 [same as above]
		public const string CFG_IMGPOS		= "num_selected_image_y_location_percent";
		public const string CFG_COLSIZE		= "num_grid_text_min_column_characters";
		public const string CFG_STEAMID		= "num_steam_id";
		public const string CFG_OCULUSID	= "text_oculus_username";
		public const string CFG_ORIGINID	= "text_ea_email";
		public const string CFG_ORIGINPW	= "base64_ea_password";
		public const string CFG_COLBG1		= "colour_background";
		public const string CFG_COLBG2		= "colour_background_lightmode";
		public const string CFG_COLTITLE1	= "colour_title";
		public const string CFG_COLTITLE2	= "colour_title_lightmode";
		public const string CFG_COLSUB1		= "colour_subtitle";
		public const string CFG_COLSUB2		= "colour_subtitle_lightmode";
		public const string CFG_COLENTRY1	= "colour_entries";
		public const string CFG_COLENTRY2	= "colour_entries_lightmode";
		public const string CFG_COLUNIN1	= "colour_entries_not_installed";
		public const string CFG_COLUNIN2	= "colour_entries_not_installed_lightmode";
		public const string CFG_COLHIBG1	= "colour_highlight_background";
		public const string CFG_COLHIBG2	= "colour_highlight_background_lightmode";
		public const string CFG_COLHILITE1	= "colour_highlight_text";
		public const string CFG_COLHILITE2	= "colour_highlight_text_lightmode";
		public const string CFG_COLINPBG1	= "colour_input_background";
		public const string CFG_COLINPBG2	= "colour_input_background_lightmode";
		public const string CFG_COLINPUT1	= "colour_input_prompt";
		public const string CFG_COLINPUT2	= "colour_input_prompt_lightmode";
		public const string CFG_COLERRBG1	= "colour_error_background";
		public const string CFG_COLERRBG2	= "colour_error_background_lightmode";
		public const string CFG_COLERROR1	= "colour_error_text";
		public const string CFG_COLERROR2	= "colour_error_text_lightmode";
		public const string CFG_KEYLT1		= "key_left_1";
		public const string CFG_KEYLT2		= "key_left_2";
		public const string CFG_KEYUP1		= "key_up_1";
		public const string CFG_KEYUP2		= "key_up_2";
		public const string CFG_KEYRT1		= "key_right_1";
		public const string CFG_KEYRT2		= "key_right_2";
		public const string CFG_KEYDN1		= "key_down_1";
		public const string CFG_KEYDN2		= "key_down_2";
		public const string CFG_KEYSEL1		= "key_select_1";
		public const string CFG_KEYSEL2		= "key_select_2";
		public const string CFG_KEYQUIT1	= "key_exit_1";
		public const string CFG_KEYQUIT2	= "key_exit_2";
		public const string CFG_KEYSCAN1	= "key_rescan_1";
		public const string CFG_KEYSCAN2	= "key_rescan_2";
		public const string CFG_KEYHELP1	= "key_help_1";
		public const string CFG_KEYHELP2	= "key_help_2";
		public const string CFG_KEYBACK1	= "key_back_1";
		public const string CFG_KEYBACK2	= "key_back_2";
		public const string CFG_KEYPGUP1	= "key_pageup_1";
		public const string CFG_KEYPGUP2	= "key_pageup_2";
		public const string CFG_KEYPGDN1	= "key_pagedown_1";
		public const string CFG_KEYPGDN2	= "key_pagedown_2";
		public const string CFG_KEYHOME1	= "key_first_1";
		public const string CFG_KEYHOME2	= "key_first_2";
		public const string CFG_KEYEND1		= "key_last_1";
		public const string CFG_KEYEND2		= "key_last_2";
		public const string CFG_KEYPLAT1	= "key_launcher_1";								// TODO
		public const string CFG_KEYPLAT2	= "key_launcher_2";
		public const string CFG_KEYCFG1		= "key_settings_1";								// TODO
		public const string CFG_KEYCFG2		= "key_settings_2";
		public const string CFG_KEYFIND1	= "key_search_1";
		public const string CFG_KEYFIND2	= "key_search_2";
		public const string CFG_KEYTAB1		= "key_autocomplete_1";							// TODO
		public const string CFG_KEYTAB2		= "key_autocomplete_2";
		public const string CFG_KEYESC1		= "key_cancel_1";								// TODO
		public const string CFG_KEYESC2		= "key_cancel_2";
		public const string CFG_KEYNEW1		= "key_new_game_1";								// TODO
		public const string CFG_KEYNEW2		= "key_new_game_2";
		public const string CFG_KEYDEL1		= "key_delete_1";								// TODO
		public const string CFG_KEYDEL2		= "key_delete_2";
		public const string CFG_KEYUNIN1	= "key_uninstall_1";
		public const string CFG_KEYUNIN2	= "key_uninstall_2";
		public const string CFG_KEYCUT1		= "key_make_shortcuts_1";
		public const string CFG_KEYCUT2		= "key_make_shortcuts_2";
		public const string CFG_KEYHIDE1	= "key_hide_game_1";							// TODO
		public const string CFG_KEYHIDE2	= "key_hide_game_2";
		public const string CFG_KEYFAVE1	= "key_favourite_1";
		public const string CFG_KEYFAVE2	= "key_favourite_2";
		public const string CFG_KEYALIAS1	= "key_set_alias_1";
		public const string CFG_KEYALIAS2	= "key_set_alias_2";
		public const string CFG_KEYTYPE1	= "key_input_type_1";							// toggle Navigate/Insert
		public const string CFG_KEYTYPE2	= "key_input_type_2";
		public const string CFG_KEYVIEW1	= "key_grid_view_1";							// toggle Grid/List
		public const string CFG_KEYVIEW2	= "key_grid_view_2";
		public const string CFG_KEYMODE1	= "key_light_mode_1";							// toggle Dark/Light
		public const string CFG_KEYMODE2	= "key_light_mode_2";
		public const string CFG_KEYIMG1		= "key_image_display_1";						// toggle Enabled/Disabled
		public const string CFG_KEYIMG2		= "key_image_display_2";
		public const string CFG_KEYSORT1	= "key_sort_method_1";							// toggle LastRun/Freq/Rating/Alpha
		public const string CFG_KEYSORT2	= "key_sort_method_2";
		public const string CFG_KEYTAGS1	= "key_edit_tags_1";	                        // TODO
		public const string CFG_KEYTAGS2	= "key_edit_tags_2";
		public const string CFG_KEYRATEUP1	= "key_rating_up_1";
		public const string CFG_KEYRATEUP2	= "key_rating_up_2";
		public const string CFG_KEYRATEDN1	= "key_rating_down_1";
		public const string CFG_KEYRATEDN2	= "key_rating_down_2";
		public const string CFG_KEYDLIMG1	= "key_download_image_1";
		public const string CFG_KEYDLIMG2	= "key_download_image_2";
		public const string CFG_PATHLEG		= "path_legendary_exe";
		public const string CFG_TXTMAINT	= "text_main_menu_title";
		public const string CFG_TXTCFGT		= "text_settings_title";
		public const string CFG_TXTFILET	= "text_browse_title";
		public const string CFG_TXTROOT		= "text_root_folder";
		public const string CFG_TXTSELECT	= "text_select_folder";
		public const string CFG_TXTCREATE	= "text_create_folder";
		public const string CFG_TXTNMAIN1	= "text_instruct1_platforms_nav_custom_msg";
		public const string CFG_TXTNMAIN2	= "text_instruct2_platforms_nav_custom_msg";
		public const string CFG_TXTNSUB1	= "text_instruct1_games_nav_custom_msg";
		public const string CFG_TXTNSUB2	= "text_instruct2_games_nav_custom_msg";
		public const string CFG_TXTIMAIN1	= "text_instruct1_platforms_type_custom_msg";
		public const string CFG_TXTIMAIN2	= "text_instruct2_platforms_type_custom_msg";
		public const string CFG_TXTISUB1	= "text_instruct1_games_type_custom_msg";
		public const string CFG_TXTISUB2	= "text_instruct2_games_type_custom_msg";

		public static string GetConfigDefault(string property)
		{
			foreach (SettingsProperty setting in Properties.Settings.Default.Properties)
			{
				if (setting.Name.Equals(property))
					return setting.DefaultValue.ToString();
			}
			return null;
		}

		public static string GetConfigString(string property, ref Dictionary<string, string> dict)
		{
			if (dict.TryGetValue(property, out string strVal))
				return strVal;
			else
				return GetConfigDefault(property);
		}
		public static string GetConfigString(string property)
        {
			return GetConfigString(property, ref config);
        }

		public static List<string> GetConfigList(string property)
		{
			if (!config.TryGetValue(property, out string strVal))
				strVal = GetConfigDefault(property);

			return new List<string>(
				from part in strVal.Split('|')
				select part.Trim());
		}

		public static bool? GetConfigBool(string property)
		{
			bool bVal;
			if (config.TryGetValue(property, out string strVal))
			{
				if (bool.TryParse(strVal, out bVal))
					return bVal;
			}
			if (bool.TryParse(GetConfigDefault(property), out bVal))
				return bVal;
			return null;
		}

		public static int? GetConfigInt(string property)
		{
			int iVal;
			if (config.TryGetValue(property, out string strVal))
			{
				if (int.TryParse(strVal, out iVal))
					return iVal;
			}
			if (int.TryParse(GetConfigDefault(property), out iVal))
				return iVal;
			return null;
		}

		public static ushort? GetConfigNum(string property)
		{
			ushort nVal;
			if (config.TryGetValue(property, out string strVal))
			{
				if (ushort.TryParse(strVal, out nVal))
					return nVal;
			}
			if (ushort.TryParse(GetConfigDefault(property), out nVal))
				return nVal;
			return null;
		}

		public static ulong? GetConfigULong(string property)
		{
			ulong nVal;
			if (config.TryGetValue(property, out string strVal))
			{
				if (ulong.TryParse(strVal, out nVal))
					return nVal;
			}
			if (ulong.TryParse(GetConfigDefault(property), out nVal))
				return nVal;
			return null;
		}

		public static bool SetConfigDefault(string property)
		{
			return SetConfigDefault(property, ref config);
		}

		public static bool SetConfigDefault(string property, ref Dictionary<string, string> dict)
        {
			if (dict.ContainsKey(property))
            {
				foreach (SettingsProperty setting in Properties.Settings.Default.Properties)
				{
					if (setting.Name.Equals(property))
					{
						dict[property] = setting.DefaultValue.ToString();
						return true;
					}
				}
			}
			return false;
        }

		public static bool SetConfigValue(string property, string strVal, ref Dictionary<string, string> dict)
		{
			if (dict.ContainsKey(property))
			{
				dict[property] = strVal;
				return true;
			}
			return false;
		}
		public static bool SetConfigValue(string property, string strVal)
        {
			return SetConfigValue(property, strVal, ref config);
        }
		public static bool SetConfigValue(string property, bool bVal)
        {
			return SetConfigValue(property, bVal.ToString(), ref config);
		}
		public static bool SetConfigValue(string property, int iVal)
		{
			return SetConfigValue(property, iVal.ToString(), ref config);
		}
		public static bool SetConfigValue(string property, ushort nVal)
		{
			return SetConfigValue(property, nVal.ToString(), ref config);
		}
		public static bool SetConfigValue(string property, ulong nVal)
		{
			return SetConfigValue(property, nVal.ToString(), ref config);
		}
		public static bool SetConfigValue(string property, byte[] byteVal)
        {
			return SetConfigValue(property, byteVal.ToString(), ref config);
        }

		/// <summary>
		/// Convert user-inputted entries for colour names to appropriate ConsoleColor name
		/// </summary>
		/// <param name="strKeyName">Name of the user-inputted colour</param>
		/// <returns>Value of the translated colour or original colour if not found</returns>
		public static string ToConsoleColorFormat(string strCCName)
		{
			switch (strCCName)
            {
				case "DarkBlack":
					return "Black";
				case "BrightBlue":
				case "LightBlue":
				case "DeepBlue":
				case "RoyalBlue":
					return "Blue";
				case "BlueGreen":
				case "BrightBlueGreen":
				case "LightBlueGreen":
				case "BrightCyan":
				case "LightCyan":
				case "GreenBlue":
				case "BrightGreenBlue":
				case "LightGreenBlue":
				case "Teal":
				case "LightTeal":
				case "BrightTeal":
				case "Turquoise":
				case "BrightTurquoise":
				case "LightTurquoise":
					return "Cyan";
				case "Navy":
				case "NavyBlue":
					return "DarkBlue";
				case "DarkBlueGreen":
				case "DarkGreenBlue":
				case "DarkTurquoise":
					return "DarkCyan";
				case "BrightBlack":
				case "LightBlack":
				case "DarkGrey":
					return "DarkGray";
				case "ForestGreen":
					return "DarkGreen";
				case "Purple":
				case "DarkPurple":
				case "DarkPurpleRed":
				case "DarkRedPurple":
				case "DarkViolet":
					return "DarkMagenta";
				case "Maroon":
				case "DarkMaroon":
					return "DarkRed";
				case "Brown":
				case "BrightBrown":
				case "DarkBrown":
				case "LightBrown":
				case "Gold":
				case "Ochre":
				case "Tan":
				case "DarkTan":
					return "DarkYellow";
				case "BrightGray":
				case "LightGray":
				case "Grey":
				case "BrightGrey":
				case "LightGrey":
				case "DarkWhite":
					return "Gray";
				case "BrightGreen":
				case "LightGreen":
					return "Green";
				case "Fuchsia":
				case "BrightMagenta":
				case "LightMagenta":
				case "BrightPurple":
				case "LightPurple":
				case "PurpleRed":
				case "BrightPurpleRed":
				case "LightPurpleRed":
				case "RedPurple":
				case "BrightRedPurple":
				case "LightRedPurple":
					return "Magenta";
				case "BrightRed":
				case "LightRed":
				case "Orange":
				case "Pink":
				case "Salmon":
					return "Red";
				case "BrightWhite":
				case "LightWhite":
					return "White";
				case "BrightYellow":
				case "LightYellow":
					return "Yellow";
				default:
					break;
			}
			return strCCName;
		}

		/*
		// https://stackoverflow.com/questions/31154596/represent-an-special-keyboard-key-in-the-current-culture/31204719#31204719
		static string GetCharsFromKeys(Keys keys, bool shift, bool altGr)
		{
			var buf = new StringBuilder(256);
			var keyboardState = new byte[256];
			if (shift)
				keyboardState[(int)Keys.ShiftKey] = 0xff;
			if (altGr)
			{
				keyboardState[(int)Keys.ControlKey] = 0xff;
				keyboardState[(int)Keys.Menu] = 0xff;
			}
			ToUnicode((uint)KeyInterop.VirtualKeyFromKey((Key)keys), 0, keyboardState, buf, 256, 0);
			return buf.ToString();
		}
		*/

		/// <summary>
		/// Convert "Oem" console keys to user-readable form (US-only)
		/// </summary>
		/// <param name="strCKeyName">Name of the console key name</param>
		/// <returns>Value of the translated key or original key if not found</returns>
		public static string KeyToUSFormat(string strCKeyName)
		{
			if (InputLanguage.CurrentInputLanguage.LayoutName == "US")
			{
				switch (strCKeyName)
				{
					case "Oem1":
						return ";";
					case "OemPlus":
						return "=";
					case "OemComma":
						return ",";
					case "OemMinus":
						return "-";
					case "OemPeriod":
						return ".";
					case "Oem2":
						return "/";
					case "Oem3":
						return "`";
					case "Oem4":
						return "[";
					case "Oem5":
						return "\\";
					case "Oem6":
						return "]";
					case "Oem7":
						return "'";
					default:
						break;
				}
			}
			return strCKeyName;
		}

		/// <summary>
		/// Abbreviate console keys descriptions
		/// </summary>
		/// <param name="strCKeyName">Name of the console key name</param>
		/// <returns>Value of the translated key or original key if not found</returns>
		public static string ShortenKeyName(string strCKeyName)
		{
			if (InputLanguage.CurrentInputLanguage.LayoutName == "US")
				strCKeyName = KeyToUSFormat(strCKeyName);
			switch (strCKeyName)
			{
				case "Backspace":
					return "Bksp";
				//case "Control":
				//	return "Ctrl";
				//case "CapsLock":
				//	return "Caps";
				case "Escape":
					return "Esc";
				case "Spacebar":
					return "Space";
				case "PageUp":
					return "PgUp";
				case "PageDown":
					return "PgDn";
				case "LeftArrow":
					return "←";
				case "UpArrow":
					return "↑";
				case "RightArrow":
					return "→";
				case "DownArrow":
					return "↓";
				case "PrintScreen":
					return "PrtScn";
				case "Insert":
					return "Ins";
				case "Delete":
					return "Del";
				case "D0":
					return "0";
				case "D1":
					return "1";
				case "D2":
					return "2";
				case "D3":
					return "3";
				case "D4":
					return "4";
				case "D5":
					return "5";
				case "D6":
					return "6";
				case "D7":
					return "7";
				case "D8":
					return "8";
				case "D9":
					return "9";
				case "LeftWindows":
					return "LWin";
				case "RightWindows":
					return "RWin";
				case "Applications":
					return "Menu";
				case "NumPad0":
					return "Pad0";
				case "NumPad1":
					return "Pad1";
				case "NumPad2":
					return "Pad2";
				case "NumPad3":
					return "Pad3";
				case "NumPad4":
					return "Pad4";
				case "NumPad5":
					return "Pad5";
				case "NumPad6":
					return "Pad6";
				case "NumPad7":
					return "Pad7";
				case "NumPad8":
					return "Pad8";
				case "NumPad9":
					return "Pad9";
				case "Multiply":
					return "Pad*";
				case "Add":
					return "Pad+";
				case "Subtract":
					return "Pad-";
				case "Decimal":
					return "Pad.";
				case "Divide":
					return "Pad/";
				case "NumberLock":
					return "NumLock";
				case "ScrollLock":
					return "Scroll";
				/*
				case "LeftShift":
					return "LShift";
				case "RightShift":
					return "RShift";
				case "LeftControl":
					return "LCtrl";
				case "RightControl":
					return "RCtrl";
				case "LeftAlt":
					return "LAlt";
				case "RightAlt":
					return "RAlt";
				*/
				//case "BrowserHome":
				//	return "Browser⌂";
				case "VolumeMute":
					return "Mute";
				case "VolumeDown":
					return "Vol-";
				case "VolumeUp":
					return "Vol+";
				case "MediaNext":
					return "►│";
				case "MediaPrevious":
					return "│◄";
				case "MediaStop":
					return "■";
				case "MediaPlay":
					return "►";
				// These are renamed by KeyToUSFormat() if applicable
				case "OemPlus":
					return "Oem+";
				case "OemComma":
					return "Oem,";
				case "OemMinus":
					return "Oem-";
				case "OemPeriod":
					return "Oem.";
				default:
					break;
			}
			return strCKeyName;
		}

		/// <summary>
		/// Convert user-inputted entries for key names to appropriate ConsoleKey name
		/// </summary>
		/// <param name="strKeyName">Name of the user-inputted key name</param>
		/// <returns>Value of the translated key or original key if not found</returns>
		public static string ToConsoleKeyFormat(string strKeyName)
		{
			switch (strKeyName.ToUpper())
			{
				case "RETURN":
				case "NEWLINE":
					return "Enter";
				case "BREAK":
					return "Pause";
				case "ESC":
					return "Escape";
				case " ":
				case "SPACE":
					return "Spacebar";
				case "PGUP":
					return "PageUp";
				case "PGDN":
					return "PageDown";
				case "LEFT":
					return "LeftArrow";
				case "UP":
					return "UpArrow";
				case "RIGHT":
					return "RightArrow";
				case "DOWN":
					return "DownArrow";
				case "INS":
					return "Insert";
				case "DEL":
					return "Delete";
				case "0":
					return "D0";
				case "1":
					return "D1";
				case "2":
					return "D2";
				case "3":
					return "D3";
				case "4":
					return "D4";
				case "5":
					return "D5";
				case "6":
					return "D6";
				case "7":
					return "D7";
				case "8":
					return "D8";
				case "9":
					return "D9";
				case ";":
				case ":":
				case "SEMICOLON":
				case "COLON":
				case "OEMCOLON":
				case "OEMSEMICOLON":
					return "Oem1";
				//return "OemSemicolon";
				case "PAD0":
				case "NUM0":
					return "NumPad0";
				case "PAD1":
				case "NUM1":
					return "NumPad1";
				case "PAD2":
				case "NUM2":
					return "NumPad2";
				case "PAD3":
				case "NUM3":
					return "NumPad3";
				case "PAD4":
				case "NUM4":
					return "NumPad4";
				case "PAD5":
				case "NUM5":
					return "NumPad5";
				case "PAD6":
				case "NUM6":
					return "NumPad6";
				case "PAD7":
				case "NUM7":
					return "NumPad7";
				case "PAD8":
				case "NUM8":
					return "NumPad8";
				case "PAD9":
				case "NUM9":
					return "NumPad9";
				case "ASTERISK":
				case "STAR":
				case "SPLAT":
				case "NUM*":
				case "NUMASTERISK":
				case "NUMSPLAT":
				case "NUMSTAR":
				case "NUMMULTIPLY":
				case "PAD*":
				case "PADASTERISK":
				case "PADSPLAT":
				case "PADSTAR":
				case "PADMULTIPLY":
				case "NUMPAD*":
				case "NUMPADASTERISK":
				case "NUMPADSPLAT":
				case "NUMPADSTAR":
				case "NUMPADMULTIPLY":
					return "Multiply";
				case "NUM+":
				case "NUMPLUS":
				case "NUMSUM":
				case "NUMADD":
				case "PAD+":
				case "PADPLUS":
				case "PADSUM":
				case "PADADD":
				case "NUMPAD+":
				case "NUMPADPLUS":
				case "NUMPADSUM":
				case "NUMPADADD":
					return "Add";
				case "NUM-":
				case "NUMHYPHEN":
				case "NUMDASH":
				case "NUMMINUS":
				case "NUMSUBTRACT":
				case "PAD-":
				case "PADHYPHEN":
				case "PADDASH":
				case "PADMINUS":
				case "PADSUBTRACT":
				case "NUMPAD-":
				case "NUMPADHYPHEN":
				case "NUMPADDASH":
				case "NUMPADMINUS":
				case "NUMPADSUBTRACT":
					return "Subtract";
				case "NUM.":
				case "NUMPERIOD":
				case "NUMDOT":
				case "NUMPOINT":
				case "NUMDECIMAL":
				case "PAD.":
				case "PADPERIOD":
				case "PADDOT":
				case "PADPOINT":
				case "PADDECIMAL":
				case "NUMPAD.":
				case "NUMPADPERIOD":
				case "NUMPADDOT":
				case "NUMPADPOINT":
				case "NUMPADDECIMAL":
					return "Decimal";
				case "NUM/":
				case "NUMSLASH":
				case "NUMFWDSLASH":
				case "NUMFORWARDSLASH":
				case "NUMDIVIDE":
				case "PAD/":
				case "PADSLASH":
				case "PADFWDSLASH":
				case "PADFORWARDSLASH":
				case "PADDIVIDE":
				case "NUMPAD/":
				case "NUMPADSLASH":
				case "NUMPADFWDSLASH":
				case "NUMPADFORWARDSLASH":
				case "NUMPADDIVIDE":
					return "Divide";
				case "=":						// US standard
				case "+":						// US standard
				case "SUM":
				case "PLUS":
					return "OemPlus";
				case ",":						// US standard
				case "<":						// US standard
				case "COMMA":
				case "ANGLE":
				case "ANGLEBRACKET":
				case "WAKA":
				case "WACA":
				case "LESS":
				case "LESSTHAN":
				case "LEFTWAKA":
				case "LEFTWACA":
				case "LEFTANGLE":
				case "LEFTANGLEBRACKET":
				case "OPENWAKA":
				case "OPENWACA":
				case "OPENANGLE":
				case "OPENANGLEBRACKET":
					return "OemComma";
				case "-":						// US standard
				case "_":						// US standard
				case "HYPHEN":
				case "DASH":
				case "UNDERSCORE":
				case "UNDERLINE":
				case "MINUS":
					return "OemMinus";
				case ".":						// US standard
				case ">":						// US standard
				case "DOT":
				case "POINT":
				case "PERIOD":
				case "GREATER":
				case "GREATERTHAN":
				case "RIGHTWAKA":
				case "RIGHTWACA":
				case "RIGHTANGLE":
				case "RIGHTANGLEBRACKET":
				case "RIGHTANGLEBRACKETS":
				case "CLOSEWAKA":
				case "CLOSEWACA":
				case "CLOSEANGLE":
				case "CLOSEANGLEBRACKET":
				case "CLOSEANGLEBRACKETS":
					return "OemPeriod";
				case "/":						// US standard
				case "?":						// US standard
				case "SLASH":
				case "FWDSLASH":
				case "FORWARDSLASH":
				case "QUESTION":
				case "QUESTIONMARK":
				case "OEMQUESTION":
					return "Oem2";
				//return "OemQuestion";
				case "`":						// US standard
				case "~":						// US standard
				case "BACKTICK":
				case "TILDE":
				case "OEMTILDE":
					return "Oem3";
				//return "Oemtilde";
				case "[":						// US standard
				case "{":						// US standard
				case "BRACKET":
				case "BRACKETS":
				case "SQUAREBRACKET":
				case "SQUAREBRACKETS":
				case "BRACE":
				case "BRACES":
				case "CURLY":
				case "CURLYS":
				case "CURLIES":
				case "CURLYBRACE:":
				case "CURLYBRACES:":
				case "CURLYBRACKET:":
				case "CURLYBRACKETS:":
				case "LEFTBRACKET":
				case "LEFTBRACKETS":
				case "LEFTSQUAREBRACKET":
				case "LEFTSQUAREBRACKETS":
				case "LEFTBRACE":
				case "LEFTBRACES":
				case "LEFTCURLY":
				case "LEFTCURLYS":
				case "LEFTCURLIES":
				case "LEFTCURLYBRACE":
				case "LEFTCURLYBRACES":
				case "LEFTCURLYBRACKET":
				case "LEFTCURLYBRACKETS":
				case "OPENBRACKET":
				case "OPENBRACKETS":
				case "OPENSQUAREBRACKET":
				case "OPENSQUAREBRACKETS":
				case "OPENBRACE":
				case "OPENBRACES":
				case "OPENCURLY":
				case "OPENCURLYS":
				case "OPENCURLYBRACE":
				case "OPENCURLYBRACES":
				case "OPENCURLYBRACKET":
				case "OPENCURLYBRACKETS":
				case "OEMOPENBRACKET":
				case "OEMOPENBRACKETS":
					return "Oem4";
				//return "OemOpenBrackets";
				case "\\":						// US standard
				case "|":						// US standard
				case "BACKSLASH":
				case "BKSLASH":
				case "WHACK":
				case "PIPE":
				case "BAR":
				case "STICK":
				case "VBAR":
				case "VERTBAR":
				case "VERTICAL":
				case "VERTICALBAR":
				case "OEMPIPE":
					return "Oem5";
				//return "OemPipe";
				case "OEMBACKSLASH":
					return "Oem102";
				case "]":						// US standard
				case "}":						// US standard
				case "RIGHTBRACKET":
				case "RIGHTBRACKETS":
				case "RIGHTSQUAREBRACKET":
				case "RIGHTSQUAREBRACKETS":
				case "RIGHTBRACE":
				case "RIGHTBRACES":
				case "RIGHTCURLY":
				case "RIGHTCURLYS":
				case "RIGHTCURLIES":
				case "RIGHTCURLYBRACE":
				case "RIGHTCURLYBRACES":
				case "RIGHTCURLYBRACKET":
				case "RIGHTCURLYBRACKETS":
				case "CLOSEBRACKET":
				case "CLOSEBRACKETS":
				case "CLOSESQUAREBRACKET":
				case "CLOSESQUAREBRACKETS":
				case "CLOSEBRACE":
				case "CLOSEBRACES":
				case "CLOSECURLY":
				case "CLOSECURLYS":
				case "CLOSECURLIES":
				case "CLOSECURLYBRACE":
				case "CLOSECURLYBRACES":
				case "CLOSECURLYBRACKET":
				case "CLOSECURLYBRACKETS":
				case "OEMCLOSEBRACKET":
				case "OEMCLOSEBRACKETS":
					return "Oem6";
				//return "OemCloseBrackets";
				case "'":						// US standard
				case "\"":						// US standard
				case "APOSTROPHE":
				case "APOSTROPHES":
				case "TICK":
				case "QUOTE":
				case "QUOTES":
				case "DOUBLEQUOTE":
				case "DOUBLEQUOTES":
				case "QUOTEMARK":
				case "QUOTEMARKS":
				case "QUOTATIONMARK":
				case "QUOTATIONMARKS":
				case "OEMQUOTE:":
				case "OEMQUOTES:":
					return "Oem7";
				//return "OemQuotes";
				case "":
					return "NoName";
				default:
					break;
			}
			return strKeyName;
		}

		// https://stackoverflow.com/questions/31154596/represent-an-special-keyboard-key-in-the-current-culture/31204719#31204719
		/*
		[DllImport("user32.dll")]
		public static extern int ToUnicode(uint virtualKeyCode, uint scanCode,
			byte[] keyboardState,
			[Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
			StringBuilder receivingBuffer,
			int bufferSize, uint flags);
		*/
	}
}