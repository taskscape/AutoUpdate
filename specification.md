# Universal Application Updater Specification

## 1. Objective

Implement a universal application updater as a **.NET 8 C# library** compatible with any .NET 8 Windows WinForms, ASP.NET Core WebApp, or console application. The system ensures seamless, background updates of deployed applications via GitHub releases, guaranteeing the software is always on the latest version.

The library is distributed as a **NuGet package** that consuming applications reference and initialize at startup.

### 1.1 Target Environment

- **Runtime:** .NET 8 (Windows).
- **OS:** Windows (x64). The Windows Service and temporary runner are Windows-specific.
- **Application types supported:** WinForms (desktop), Console, and ASP.NET Core WebApps (hosted in IIS).

## 2. Architecture and Privileges

### 2.1 Components

1. **Updater Library (NuGet):** Referenced by the host application. Performs the startup update check, downloads installers, and signals the Windows Service to perform the privileged install.
2. **Windows Service (per-application):** A dedicated service is installed **per application**, running under the **SYSTEM** account to obtain administrative privileges for writing to protected directories. Each updatable application ships and registers its own service instance.
3. **Temporary Runner:** Because Windows locks running `.exe` and `.dll` files, the service extracts a temporary "runner" executable to the `%TEMP%` directory. The runner orchestrates the target application's shutdown, the installation process, and the subsequent restart. The runner **self-deletes** after completing (or failing out) an update cycle.

### 2.2 Host ↔ Service Communication

The host application communicates with its SYSTEM service via **named pipes (local IPC)**. The host requests an update (passing the detected release tag, installer path, target install directory, app type, and restart arguments); the service acknowledges and drives the runner.

### 2.3 Service Installation

The application's own **InnoSetup installer registers the per-app Windows Service** during the initial installation (and during subsequent updates if needed). Installing the service is therefore handled by the elevated installer, not by the library at runtime.

### 2.4 Release Mode Lock

Auto-update functionality must be **strictly disabled when the host software is running in Debug mode** and activate only when compiled and deployed in **Release mode**. (Determined via the build configuration of the host assembly.)

## 3. Update Discovery and Polling

### 3.1 Trigger Timing

The application checks for updates **strictly upon application startup**, to minimize network traffic and comply with GitHub API limits.

### 3.2 Check Behavior

The startup check (the GitHub API call and download) runs **asynchronously / non-blocking** so it does not delay application startup. However, once an update is confirmed available, the updater **hands off immediately** on this same startup — triggering the shutdown/install flow right away rather than deferring to a later session.

### 3.3 Authentication

The library uses an authenticated GitHub token (provided via the application's configuration file) to bypass the unauthenticated 60-requests/hour rate limit and to securely fetch releases from private repositories. The token is stored **in plaintext** in the configuration file for the current iteration (see §6).

### 3.4 Version Identification

- The library identifies the correct release by reading the repository's **"Latest" release** on GitHub. **Pre-releases and drafts are ignored.**
- The exact tag string (regardless of its formatting scheme) is recorded locally to track whether that specific release has already been deployed.
- Each release is assumed to contain **exactly one `.exe` asset** (the InnoSetup installer), which is selected automatically.

### 3.5 Update Mode (Silent vs. Prompt)

A per-application configuration option (`updateMode`) controls whether a detected update is applied automatically or only after explicit user consent:

- **`Silent` (default):** The update is downloaded and installed in the background with no user interaction, following the immediate-handoff behavior in §3.2 and the force-close behavior in §4.3.
- **`Prompt`:** Intended for **interactive desktop/WinForms applications**. At startup, when an update is detected, a **single modal dialog** is shown asking the user *"a new version is available — close the application and update now?"*:
  - **Yes:** The update proceeds exactly as in Silent mode (download → shutdown → install → restart).
  - **No:** The update is **deferred** to a later startup. The dialog is offered again on the next launch. A declined prompt does **not** consume one of the three install attempts (§5.3).
  - The prompt is shown **before** any download or shutdown, so declining costs no bandwidth and never closes the app unexpectedly.

Constraints and behavior:

- `Prompt` mode is **only valid for `Desktop`/`Console` application types**, not `WebApps` (which have no interactive UI); this is rejected at configuration validation time.
- The confirmation UI is **host-supplied** so the core library stays UI-agnostic. A ready-made WinForms implementation (a single Yes/No `MessageBox`) ships separately and is wired in by the host on the UI thread.
- **Fail-safe:** If `Prompt` mode is configured but the host does not supply a prompt UI, the update is **deferred rather than applied**, so the application is never closed without asking.

## 4. Download and Installation

### 4.1 Background Processing

Once a new release tag is detected, the associated `.exe` installer is downloaded silently in the background. No integrity verification (signature/checksum) is performed beyond a successful download in this iteration.

### 4.2 InnoSetup Execution

The downloaded installer is assumed to be compiled via InnoSetup. The `%TEMP%` runner executes it in **silent mode** using appropriate command-line parameters (e.g. `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`), explicitly **overriding the installation directory** (`/DIR=`) to match the target application's currently deployed location.

### 4.3 Target-Specific Shutdowns

- **WebApps (IIS / ASP.NET Core hosted in IIS):** The updater drops an `app_offline.htm` file into the **web root directory** (path provided explicitly in config). This gracefully unloads the .NET application and releases file locks without requiring administrative IIS resets.
- **Desktop / Console Apps:** In `Silent` mode the updater **forcefully closes** the running executable without warning the user. In `Prompt` mode (§3.5) the executable is only closed after the user has explicitly consented at startup.

## 5. Post-Installation and Recovery

### 5.1 Automatic Restart

- **WebApps:** The updater deletes the `app_offline.htm` file, automatically bringing the updated website back online.
- **Desktop / Console Apps:** The updater restarts the application automatically, **preserving and passing all original startup arguments** to the new executable. Because the service runs as SYSTEM, the runner **relaunches the application into the active interactive user session** (CreateProcessAsUser-style) so the restarted app runs in the correct user context.

### 5.2 Failure Handling and Retries

If the InnoSetup installer fails halfway (e.g. power loss, full disk), the application may be left in a corrupted state. The updater **automatically retries the installation**.

- There is **no backup/rollback** of the prior version; recovery is via retry only.

### 5.3 Retry Limits

- The system persistently tracks how many installation attempts have been made for a specific release tag.
- Attempts are spread **one per application startup** (across reboots) — not looped immediately within a single run.
- A specific release is attempted up to **three (3) times**. If it fails on the third attempt, the deployment for that tag is marked **"completed" (ignored)** to prevent infinite boot loops.

## 6. Configuration

Each updatable application ships with the updater library (via NuGet) and a **JSON configuration file**.

### 6.1 Configuration File (author-provided, JSON)

Stores essential parameters:

- `repositoryUrl` — target GitHub repository URL.
- `githubToken` — GitHub authentication token (plaintext, current iteration).
- `applicationType` — explicitly declared by the app author: `WebApp`, `Desktop`, or `Console`.
- `updateMode` — `Silent` (default) or `Prompt` (§3.5). `Prompt` is valid only for `Desktop`/`Console`.
- `installDirectory` — the target application's deployed location (used for the InnoSetup `/DIR=` override).
- `webRootPath` — (WebApps only) directory where `app_offline.htm` is created/deleted.

### 6.2 Deployment State (machine-wide, separate file)

Local deployment tracking metadata is stored in a **separate machine-wide state file** (e.g. under `%ProgramData%`), distinct from the author-provided config:

- `currentTag` — the currently deployed release tag.
- `retryCounts` — per-tag installation attempt counts.
- `completedTags` — tags marked completed/ignored (succeeded or exhausted 3 retries).

## 7. Logging and Observability

The updater library, the Windows Service, and the temporary runner each write to:

- A **rolling log file**, and
- The **Windows Event Log**.

Logs cover: update checks, detected tags, download progress/results, shutdown/install/restart steps, retry attempts, and failures.
