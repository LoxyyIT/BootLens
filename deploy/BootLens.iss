#define AppName "BootLens"
#define AppExeName "BootLens.exe"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PortableDir
  #define PortableDir "..\artifacts\BootLens-0.1.0-win-x64"
#endif
[Setup]
AppId={{F4A11A8E-0D37-4A70-9C06-BOOTLENS0001}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\BootLens
DefaultGroupName=BootLens
OutputDir=..\artifacts
OutputBaseFilename=BootLens-Setup-{#AppVersion}
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
[Files]
Source: "{#PortableDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
[Icons]
Name: "{autoprograms}\BootLens"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\BootLens"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
