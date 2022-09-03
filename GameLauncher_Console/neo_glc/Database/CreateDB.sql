-- Create tables --

CREATE TABLE IF NOT EXISTS "SystemAttribute" (
	"AttributeIndex"	INTEGER NOT NULL UNIQUE,
	"AttributeName"		varchar(50) NOT NULL UNIQUE,
	"AttributeValue"	varchar(255),
	"AttributeDesc"		varchar(255),
	"AttributeType"		INTEGER,
	PRIMARY KEY("AttributeIndex" AUTOINCREMENT)
);

CREATE TABLE IF NOT EXISTS "Platform" (
	"PlatformID"	INTEGER NOT NULL UNIQUE,
	"Name"			VARCHAR(50) NOT NULL UNIQUE,
	"Description" 	VARCHAR(255),
	"Path"			VARCHAR(255),
	"IsActive"		BIT NOT NULL DEFAULT 0,
	PRIMARY KEY("PlatformID" AUTOINCREMENT)
);

CREATE TABLE IF NOT EXISTS "PlatformAttribute" (
	"PlatformFK"		INTEGER,
	"AttributeName"		varchar(50),
	"AttributeIndex"	INTEGER,
	"AttributeValue"	varchar(255),
	FOREIGN KEY("PlatformFK") REFERENCES "Platform"("PlatformID")
);

CREATE TABLE IF NOT EXISTS "Game" (
	"GameID"		INTEGER NOT NULL UNIQUE,
	"PlatformFK"	INTEGER,
	"Identifier"	varchar(50) NOT NULL UNIQUE,
	"Title"			varchar(50) NOT NULL,
	"Alias"			varchar(50) NOT NULL UNIQUE,
	"Launch"		varchar(255) NOT NULL,
	"Frequency"		NUMERIC NOT NULL DEFAULT 0.0,
	"IsFavourite"	bit NOT NULL DEFAULT 0,
	"IsHidden"		bit NOT NULL DEFAULT 0,
	"Group"			varchar(255),
	"Icon" 			VARCHAR(255),
	"Tag"			INTEGER,
	PRIMARY KEY("GameID" AUTOINCREMENT),
	CONSTRAINT "Platform_Title" UNIQUE("PlatformFK", "Title")
);

CREATE TABLE IF NOT EXISTS "GameAttribute" (
	"GameFK"			INTEGER,
	"AttributeName"		varchar(50),
	"AttributeIndex"	INTEGER,
	"AttributeValue"	varchar(255),
	FOREIGN KEY("GameFK") REFERENCES "Game"("GameID")
);

CREATE TABLE IF NOT EXISTS "Extension" (
	"ExtensionID"		INTEGER NOT NULL UNIQUE,
	"Name"				varchar(255) NOT NULL UNIQUE,
	"IsActive"			BIT NOT NULL DEFAULT 0,
	"DllPath"			varchar(255) NOT NULL UNIQUE,
	PRIMARY KEY("ExtensionID" AUTOINCREMENT)
);

CREATE TABLE IF NOT EXISTS "ColourScheme" (
	"ColourSchemeID"	INTEGER NOT NULL UNIQUE,
	"Name"				varchar(50) NOT NULL UNIQUE,
	"Description"		varchar(255) NOT NULL,
	"IsActive"			bit NOT NULL DEFAULT 0,
	"IsSystem"			bit NOT NULL DEFAULT 0,
	PRIMARY KEY("ColourSchemeID" AUTOINCREMENT)
);

CREATE TABLE IF NOT EXISTS "Tag" (
	"TagID"				INTEGER NOT NULL UNIQUE,
	"Name"				varchar(50) NOT NULL UNIQUE,
	"Description"		varchar(255) NOT NULL,
	"IsActive"			bit NOT NULL DEFAULT 0,
	"IsInternal"		bit NOT NULL DEFAULT 0,
	PRIMARY KEY("TagID" AUTOINCREMENT)
);

/*
CREATE TABLE "Player" (
	"PlayerID"		INTEGER NOT NULL UNIQUE,
	"Title"			varchar(50) NOT NULL UNIQUE,
	"Launch"		varchar(255) NOT NULL,
	"Param"			varchar(255),
	"Filepath"		varchar(255),
	"Filemask"		varchar(255),
	"FileIconPath"	varchar(255),
	"Description" 	VARCHAR(255),
	PRIMARY KEY("PlayerID" AUTOINCREMENT)
);

CREATE TABLE "PlayerAttribute" (
	"PlayerFK"			INTEGER,
	"AttributeName"		varchar(50),
	"AttributeIndex"	INTEGER,
	"AttributeValue"	varchar(255),
	FOREIGN KEY("PlayerFK") REFERENCES "Player"("PlayerID")
);
*/
-- Add any data --
INSERT INTO Game(PlatformFK, Identifier, Title, Alias, Launch, Frequency, IsFavourite, IsHidden) VALUES
	(1, 'test1.01',  'P1 - Test #1', 'test1.01', 'test', 0, 0, 0),
	(1, 'test1.02',  'P1 - Test #2', 'test1.02', 'test', 0, 0, 0),
	(1, 'test1.03',  'P1 - Test #3', 'test1.03', 'test', 0, 0, 0),
	(1, 'test1.04',  'P1 - Test #4', 'test1.04', 'test', 0, 0, 0),
	(1, 'test1.05',  'P1 - Test #5', 'test1.05', 'test', 0, 0, 0),
	(1, 'test1.06',  'P1 - Test #6', 'test1.06', 'test', 0, 0, 0),
	(1, 'test1.07',  'P1 - Test #7', 'test1.07', 'test', 0, 0, 0),
	(1, 'test1.08',  'P1 - Test #8', 'test1.08', 'test', 0, 0, 0),
	(1, 'test1.09',  'P1 - Test #9', 'test1.09', 'test', 0, 0, 0),
	(1, 'test1.10', 'P1 - Test #10', 'test1.10', 'test', 0, 1, 0),
	(1, 'test1.11', 'P1 - Test #11', 'test1.11', 'test', 0, 0, 0),
	(1, 'test1.12', 'P1 - Test #12', 'test1.12', 'test', 0, 0, 0),
	(1, 'test1.13', 'P1 - Test #13', 'test1.13', 'test', 0, 0, 0),
	(1, 'test1.14', 'P1 - Test #14', 'test1.14', 'test', 0, 0, 0),
	(1, 'test1.15', 'P1 - Test #15', 'test1.15', 'test', 0, 0, 0),
	(1, 'test1.16', 'P1 - Test #16', 'test1.16', 'test', 0, 0, 0),
	(1, 'test1.17', 'P1 - Test #17', 'test1.17', 'test', 0, 0, 0),
	(1, 'test1.18', 'P1 - Test #18', 'test1.18', 'test', 0, 0, 0),
	(1, 'test1.19', 'P1 - Test #19', 'test1.19', 'test', 0, 0, 0),
	(1, 'test1.20', 'P1 - Test #20', 'test1.20', 'test', 0, 1, 0),
	(1, 'test1.21', 'P1 - Test #21', 'test1.21', 'test', 0, 0, 1),
	(1, 'test1.22', 'P1 - Test #22', 'test1.22', 'test', 0, 0, 0),
	(1, 'test1.23', 'P1 - Test #23', 'test1.23', 'test', 0, 0, 0),
	(1, 'test1.24', 'P1 - Test #24', 'test1.24', 'test', 0, 0, 0),
	(1, 'test1.25', 'P1 - Test #25', 'test1.25', 'test', 0, 0, 0),
	(1, 'test1.26', 'P1 - Test #26', 'test1.26', 'test', 0, 0, 0),
	(1, 'test1.27', 'P1 - Test #27', 'test1.27', 'test', 0, 0, 0),
	(1, 'test1.28', 'P1 - Test #28', 'test1.28', 'test', 0, 0, 0),
	(1, 'test1.29', 'P1 - Test #29', 'test1.29', 'test', 0, 0, 0),
	(1, 'test1.30', 'P1 - Test #30', 'test1.30', 'test', 0, 1, 0),
	(1, 'test1.31', 'P1 - Test #31', 'test1.31', 'test', 0, 1, 0),
	(2, 'test2.01', 'P2 - Test #1', 'test2.01', 'test', 0, 0, 0),
	(2, 'test2.02', 'P2 - Test #2', 'test2.02', 'test', 0, 0, 0),
	(2, 'test2.03', 'P2 - Test #3', 'test2.03', 'test', 0, 0, 0),
	(2, 'test2.04', 'P2 - Test #4', 'test2.04', 'test', 0, 0, 0),
	(2, 'test2.05', 'P2 - Test #5', 'test2.05', 'test', 0, 0, 0);


-- Setting that controls if the app should show inactive platforms
IF NOT EXISTS (SELECT AttributeValue FROM SystemAttribute WHERE AttributeName = 'SHOW_INACTIVE_PLATFORMS')
BEGIN
	INSERT INTO SystemAttribute(AttributeName, AttributeType, AttributeValue) VALUES('SHOW_INACTIVE_PLATFORMS', 0, 'N');
END ;

-- Setting controlling if the app should close when a game is selected
IF NOT EXISTS (SELECT AttributeValue FROM SystemAttribute WHERE AttributeName = 'CLOSE_ON_LAUNCH')
BEGIN
	INSERT INTO SystemAttribute(AttributeName, AttributeType, AttributeValue) VALUES('CLOSE_ON_LAUNCH', 0, 'Y');
END ;