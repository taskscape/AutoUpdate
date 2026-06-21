# Distributing the Sample App with InnoSetup

This folder shows how to package and ship the `SampleApp` WinForms host so it can auto-update from
GitHub releases. The compiled installer is **both** the first-time installer and the update payload.

## Prerequisites

- .NET 8 SDK
- [InnoSetup 6](https://jrsoftware.org/isinfo.php) (`iscc.exe` on your PATH)
- A GitHub repository with releases enabled (private repos need a token in `updater.json`)

## One-time setup

Edit [`SampleApp.iss`](SampleApp.iss) and confirm these line up:

| Value | Where | Must equal |
|-------|-------|------------|
| `MyAppId` (`SampleApp`) | `SampleApp.iss` | `applicationId` in `samples/SampleApp/updater.json` |
| `{app}` install dir | `SampleApp.iss` `DefaultDirName` | `installDirectory` in `updater.json` |
| `MyAppVersion` | `SampleApp.iss` | the GitHub release **tag** and SampleApp `<Version>` |
| `repositoryUrl` | `updater.json` | your GitHub repo |

Also generate your own `AppId` GUID in `[Setup]` (keep it stable across versions).

## Build & package

```powershell
# 1. Publish app + service + runner into one merged folder (publish/SampleApp)
pwsh installer/publish.ps1 -Version 1.0.0

# 2. Compile the installer (produces installer/Output/SampleApp-Setup-1.0.0.exe)
iscc installer/SampleApp.iss
```

## Publish an update

1. Bump the version everywhere (e.g. `1.0.1`): `publish.ps1 -Version 1.0.1` and `MyAppVersion`.
2. Recompile with `iscc`.
3. Create a GitHub **Release** with tag `1.0.1` and attach `SampleApp-Setup-1.0.1.exe` as the
   single `.exe` asset.

That's it. Installed copies will, on next startup (spec §3.1):

1. Detect the new "Latest" tag (spec §3.4).
2. In `Prompt` mode, ask the user to close and update now (spec §3.5).
3. Download the installer and hand off to the SYSTEM service (spec §2.2).
4. The temp runner force-closes the app, runs the installer silently with `/DIR=` (spec §4.2),
   then relaunches the app preserving its startup arguments (spec §5.1).

## What the installer does

- Copies `SampleApp.exe`, `AutoUpdater.Service.exe`, `updater.json` and dependencies to `{app}`.
  (The runner is **embedded inside `AutoUpdater.Service.exe`** as a single-file resource and
  extracted to `%TEMP%` at runtime, spec §2.1 — it is not shipped as a separate file.)
- Registers and starts the per-app SYSTEM service via
  `AutoUpdater.Service.exe install --application-id SampleApp` (idempotent, spec §2.3).
- On upgrades, stops the service before copying files (so its locked `.exe` can be replaced), then
  restarts it.
- On uninstall, removes the service.

## Notes / caveats

- The installer requires admin (Program Files + service creation).
- Auto-update is disabled for Debug builds (spec §2.4) — always publish Release.
- Token is stored in plaintext in `updater.json` for this iteration (spec §3.3); do not commit a
  real token.
