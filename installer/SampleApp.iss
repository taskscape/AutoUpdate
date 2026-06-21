; ============================================================================
;  AutoUpdater Sample App - InnoSetup distribution script
; ============================================================================
;
;  This script packages the WinForms sample host together with the updater
;  service + runner, and registers the per-app SYSTEM Windows Service that the
;  Universal Application Updater requires (specification.md §2.1, §2.3).
;
;  The COMPILED OUTPUT of this script (SampleApp-Setup-<version>.exe) is exactly
;  the single .exe asset you upload to the GitHub "Latest" release (spec §3.4,
;  §4.1). The updater downloads it and runs it silently with a /DIR= override
;  (spec §4.2), so the same script serves both first-time installs and updates.
;
;  ----------------------------------------------------------------------------
;  BUILD / DISTRIBUTION WORKFLOW
;  ----------------------------------------------------------------------------
;  1. Publish the app + service into ONE folder (they share dependencies). The runner is embedded
;     into the service via -p:EmbedRunner=true (no separate runner publish). The helper script
;     installer/publish.ps1 does exactly this:
;
;       dotnet publish samples/SampleApp/SampleApp.csproj                -c Release -r win-x64 --self-contained false -o publish/SampleApp
;       dotnet publish src/AutoUpdater.Service/AutoUpdater.Service.csproj -c Release -r win-x64 --self-contained false -o publish/SampleApp -p:EmbedRunner=true
;
;     The merged publish/SampleApp folder ends up containing:
;       SampleApp.exe, AutoUpdater.Service.exe, updater.json, and supporting *.dll files.
;     The runner is EMBEDDED inside AutoUpdater.Service.exe (single-file resource) and extracted
;     to %TEMP% at runtime (spec §2.1), so it is not a separate file here.
;
;     NOTE: auto-update only activates for RELEASE builds (spec §2.4), so always
;           publish in Release.
;
;  2. Set MyAppVersion below to match BOTH the SampleApp <Version> and the
;     GitHub release tag you intend to publish (spec §3.4 records the tag).
;
;  3. Compile this script with InnoSetup (iscc):
;
;       iscc installer/SampleApp.iss
;
;     Output: installer/Output/SampleApp-Setup-<version>.exe
;
;  4. Create a GitHub Release whose tag matches MyAppVersion and attach the
;     compiled SampleApp-Setup-<version>.exe as its single .exe asset.
;
;  IMPORTANT: {app} below MUST equal "installDirectory" in updater.json so the
;  /DIR= override the updater passes during silent upgrades lands in the same
;  place. Here both are: C:\Program Files\AutoUpdater Sample App.
; ============================================================================

#define MyAppName       "AutoUpdater Sample App"
#define MyAppId         "SampleApp"            ; MUST match applicationId in updater.json
#define MyAppExeName    "SampleApp.exe"
#define MyAppPublisher  "Your Company"
#define MyAppVersion    "1.0.0"                ; MUST match the GitHub release tag + SampleApp <Version>
#define ServiceExeName  "AutoUpdater.Service.exe"
#define ServiceName     "AutoUpdater.SampleApp" ; = "AutoUpdater." + MyAppId
#define SourceDir       "..\publish\SampleApp"  ; merged publish output from step 1

[Setup]
; A stable AppId GUID keeps upgrades recognized as the same product. Generate your own once.
AppId={{8B2F1E54-9C3A-4D7E-AB10-7F2C6E4D9A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Install location MUST match updater.json "installDirectory" (spec §4.2 /DIR= override).
DefaultDirName={commonpf}\{#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=yes
; Writing to Program Files and creating a SYSTEM service both require elevation.
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=Output
OutputBaseFilename=SampleApp-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Let InnoSetup close the running app during interactive upgrades. During silent updates the
; updater's runner has already shut the app down (spec §4.3).
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Package everything from the merged publish folder (app + service + runner + updater.json + dlls).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Run]
; Register (idempotent) and start the per-app SYSTEM updater service (spec §2.1, §2.3).
; The service runs as LocalSystem so it can write to protected directories during updates.
Filename: "{app}\{#ServiceExeName}"; Parameters: "install --application-id {#MyAppId}"; \
    Flags: runhidden waituntilterminated; StatusMsg: "Installing the auto-update service..."

; Offer to launch the app after an interactive install (skipped during silent updates).
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the updater service when the app is uninstalled.
Filename: "{app}\{#ServiceExeName}"; Parameters: "uninstall --application-id {#MyAppId}"; \
    Flags: runhidden waituntilterminated; RunOnceId: "RemoveUpdaterService"

[Code]
{ -----------------------------------------------------------------------------
  On an in-place UPGRADE (including the updater's silent run), AutoUpdater.Service.exe
  is locked because the service is running. We stop the service BEFORE files are
  copied (ssInstall fires prior to the [Files] step) so it can be overwritten, then
  the [Run] "install" verb starts it again afterwards.
  ----------------------------------------------------------------------------- }
procedure StopUpdaterService;
var
  ResultCode: Integer;
begin
  { sc.exe is always present; ignore failures (service may not exist on a fresh install). }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { Give the Service Control Manager a moment to release the file handle. }
  Sleep(2000);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopUpdaterService;
end;
