# GLC (GameLauncher Console)

This is a simple Windows console program that will scan the system for video games, then allow the user to quickly launch any of these games from a single location, without having to store the icons on the desktop or to remember which client (Steam or Epic, etc.) to launch. The program currently supports the following platforms:
- Amazon Games;
- Battle&period;net;
- Big Fish Games;
- EA [formerly Origin];
- Epic Games Launcher;
- Game Jolt Client;
- GOG Galaxy;
- Humble App;
- Indiegala Client;
- itch;
- Legacy Games;
- Oculus;
- Paradox Launcher;
- Plarium Play;
- Riot Client;
- Rockstar Games Launcher;
- Steam;
- Ubisoft Connect [formerly Uplay];
- Wargaming.net Game Center.

[NOTE: Bethesda.net Launcher has been deprecated as of May 2022.]

You can also use it to launch custom programs (see [below](#Using) for instructions).

<br/>

![](GLConsole.gif)

<br/>

## Features
An interactive console allows the user to navigate via menus using the arrows and Enter (by default). An in-app help (H or F1 by default) provides a list of other keys for other functions, e.g.:
- Launch a game or platform client;
- Search for games;
- Flag favourites;
- Create a Desktop shortcut, or export a group of shortcuts to a given location;
- Uninstall a game;
  - Uninstall not currently available for some platforms (e.g., Indiagala, itch).
- Install owned games;
  - Non-installed games only supported for some platforms (for Steam, user profile must be set to public)
- Change between grid/list mode;
- Change between light/dark mode.
  - Colours can be further customised; see below.

## [GLC Wiki](../../wiki)
***See the [wiki](../../wiki) for [Known Issues](../../wiki/Known-Issues), [Changelog](../../wiki/Changelog), and more information.***

## Using
***[Click here to download the latest binary release.](../../releases/latest/download/glc.exe)*** The app is portable (does not require installation). However, we'd recommend moving it to a dedicated folder before running it because several support files and folders are automatically created in the same location.

The .NET 7 Windows x86 runtime is required. You can [download it here](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-7.0.0-windows-x86-installer), or using the command line run: `winget install Microsoft.DotNet.DesktopRuntime.7`

To change the default configuration options, you can edit glc.ini in v1.2 (or glc-cfg.json in v1.1), including custom keys, colours, text, input, layout, sorting, images, etc.

To manually add programs, place file shortcuts or .exe executables in the ".\CustomGames" folder found in the same directory as the application, and use the rescan feature to load them into the program. It is recommended to use shortcuts instead of executables, as .exe files often require external assets and therefore may not work properly.

By default, GLC will attempt tot download images for games that don't have icons (e.g., non-installed Steam games). To customise an image, place files with the same titles in the ".\CustomImages" folder (supports BMP, GIF, EPRT, EXIF, ICO, JPEG, PNG, and TIFF).

Legendary is supported as an alternative to the Epic Games Launcher. [Download legendary.exe](/derrod/legendary/releases/latest/download/legendary.exe) and place it in the same folder as glc.exe (or set a different path in glc.ini). First run `legendary auth` and login to Epic before scanning for games.

## Building from source
After cloning the repo, use the Publish feature in Visual Studio (right-click the GameLauncher_Console project and choose Publish), or run: `dotnet publish --configuration Release`

The program uses following NuGet packages:
- [HtmlAgilityPack](https://html-agility-pack.net/) for HTML parsing (Steam non-installed games);
- [protobuf-net](https://protobuf-net.github.io/protobuf-net/) for database parsing (Battle.net);
- [PureOrigin.API](https://github.com/JaydenMaalouf/PureOrigin.API) for the EA API;
- [System.Data.SQLite](https://system.data.sqlite.org/index.html/doc/trunk/www/index.wiki) for database parsing (v2 game database, Amazon, GOG, itch, Oculus).

## Contributing
You can support the project in the following ways:
- Go to [Issues](/Solaire/GLC/issues) to identify bugs or make suggestions;
- Fork the repo, make some changes, and submit a pull request.

## [License](LICENSE)
GNU General Public License v3.0

See [LICENSE](LICENSE) file.
