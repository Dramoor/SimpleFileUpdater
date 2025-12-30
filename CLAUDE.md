# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SimpleFileUpdater is a cross-platform file synchronization system consisting of:
- **Client**: Avalonia-based .NET 9.0 desktop application that checks local files against a server and downloads updates
- **Server**: Python HTTP server that serves file listings with MD5 hashes and handles file downloads

The client downloads only files that differ (by MD5 hash) or are missing, avoiding full re-downloads.

## Directory Structure

```
SimpleFileUpdater/
├── src/
│   ├── Client/          # C# .NET client application
│   │   ├── *.cs         # C# source files
│   │   ├── *.axaml      # Avalonia XAML UI files
│   │   ├── *.csproj     # Project file
│   │   ├── *.sln        # Solution file
│   │   └── resources/   # Visual assets (background.png, icon.ico)
│   └── Server/          # Python server
│       ├── serv.py      # HTTP server implementation
│       └── start.sh     # Server startup script
├── README.md
├── LICENSE
└── CLAUDE.md
```

## Build Commands

### Client (C# .NET)

Basic build:
```bash
cd src/Client
dotnet build -c Release
```

Platform-specific builds (creates self-contained single-file executables):
```bash
cd src/Client

# Windows
dotnet publish -c Release -r win-x64

# Linux
dotnet publish -c Release -r linux-x64

# macOS (may need to run on Mac hardware)
dotnet publish -c Release -r osx-x64
```

Output location: `src/Client/bin/Release/net9.0/{runtime}/publish/`

### Server (Python)

Start the server:
```bash
cd src/Server
python3 serv.py
```

Requirements: Python 3.11+

## Architecture

### Client Architecture

The client uses Avalonia UI framework with MVVM pattern:

- **Entry Point**: `src/Client/Program.cs` → bootstraps Avalonia application
- **Application Lifecycle**: `src/Client/MainWindow.cs` (Application class) → creates main window (`Main.cs`)
- **Main Window**: `src/Client/Main.axaml.cs` + `Main.axaml` → initializes `MainViewModel` and triggers `UpdateHandler.HandleUpdates()`
- **View Model**: `src/Client/MainViewModel.cs` → implements `INotifyPropertyChanged` for data binding (progress, status text, error messages)
- **Update Logic**: `src/Client/UpdateHandler.cs` → core file synchronization logic
- **Configuration**: `src/Client/Settings.cs` → all customizable strings, colors, and server URL

### UpdateHandler Flow

`UpdateHandler.cs` orchestrates the update process in three phases:

1. **GetFileList()**: Fetches JSON array of `{name, md5}` from server root endpoint
2. **StartComparingFiles()**: Spawns `WORKER_COUNT` (2) workers that compare local file MD5s against server MD5s, queuing mismatches/missing files for download
3. **StartDownloading()**: Spawns `WORKER_COUNT` (2) workers that download queued files with retry logic (max 5 retries per file)

All phases use `ConcurrentQueue` for thread-safe work distribution and `Dispatcher.UIThread.Post()` to update UI from background threads.

### Server Architecture

`src/Server/serv.py` is a simple HTTP server with two endpoints:

- `GET /`: Returns JSON array of all files in `files/` directory with their MD5 hashes (cached in `jsoncache.json`, regenerated hourly)
- `GET /file/{path}`: Serves the actual file content as `application/octet-stream`

File cache regeneration runs in a background daemon thread every hour.

## Customization Points

All branding/configuration is in `src/Client/Settings.cs`:
- `Title`, `Subtitle`: Header text
- `TitleColor`, `SubtitleColor`: Hex color strings for text
- `DefaultTextColor`, `ProgressBarBackground`, `ProgressBarForeground`: Brush colors
- `UpdateUrl`: Server endpoint (must include trailing slash if using path segments)
- `Finished`, `ReqFileList`, `ComparingFiles`, `DownloadingFiles`: Status messages (support `string.Format` placeholders)
- Error messages: `ConError`, `BadData`, `UnknownError`, `FileFailedError`

Visual assets:
- `src/Client/resources/background.png`: 800x450 background image
- `src/Client/resources/icon.ico`: Application icon

## Key Implementation Details

### Threading and Concurrency
- Uses `WORKER_COUNT = 2` parallel workers for both comparing and downloading
- `ConcurrentQueue<FileEntry>` for thread-safe file queuing
- UI updates via `Dispatcher.UIThread.Post()` to marshal to UI thread
- Cancellation support via `CancellationToken`

### File Download Strategy
- 81920-byte buffer size for streaming downloads
- Retry logic with max 5 attempts per file (tracked in `retryMap`)
- Download speed calculation based on cumulative bytes/time across all workers
- UI updates throttled to 0.5-second intervals during downloads
- Automatic directory creation for nested paths

### HTTP Client Configuration
- Initial connection timeout: 5 seconds (for file list retrieval)
- Download timeout: 15 minutes (client recreated between phases)

### MD5 Comparison
- Server computes MD5 on startup and caches in `jsoncache.json`
- Client computes MD5 for each local file during comparison phase
- Files are queued for download if MD5 differs or file doesn't exist

## Common Development Scenarios

### Changing Server URL
Edit `Settings.UpdateUrl` in `src/Client/Settings.cs`. Ensure URL includes protocol and port if non-standard.

### Adjusting Worker Count
Modify `WORKER_COUNT` constant in `src/Client/UpdateHandler.cs` (line 13). Higher values increase parallelism but may stress server.

### Modifying UI Text
All user-facing strings are in `src/Client/Settings.cs`. Messages using format placeholders (`{0}`, `{1}`) correspond to:
- `ComparingFiles`: `{0}` = current file count, `{1}` = total files
- `DownloadingFiles`: `{0}` = current file count, `{1}` = total files, `{2}` = download speed

### Testing Server Changes
The Python server automatically creates `files/` directory on first run. Place test files there and restart server (or wait 1 hour for cache refresh).

## Deployment Notes

- Client executable should be placed in the **same directory** where downloaded files will be stored (not a subdirectory)
- Client creates subdirectories automatically if server provides paths like `maps/map0.mul`
- Server `files/` directory structure is mirrored on client side
