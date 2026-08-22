# Native System Update Orchestrator (Topgrade Alternative)

A modular, high-performance system update orchestrator inspired by [Topgrade](https://github.com/topgrade-rs/topgrade), built using **native libraries and languages for each OS**:
- **Windows**: High-performance native **C# (.NET 10)** engine featuring a standalone **WPF / Fluent UI Desktop App (`WinUpdate.App.exe`)** and a fast **CLI Console Runner (`WinUpdate.exe`)** with COM Windows Update integration and parallel Git scanner.
- **Linux**: Multi-distribution Bash engine supporting `paru`, `yay`, `pacman`, `nala`, `apt`, `dnf`, `zypper`, `apk`, `emerge`, `flatpak`, `distrobox`, `mise`, `pipx`, and RAM cache flushing.

---

## 🪟 Windows: `WinUpdate` (.NET 10 Native)

### Supported Modules & Categories

| Category | Module | Action Performed |
| :--- | :--- | :--- |
| **System** | `windows-update` | Queries & installs official Windows Updates via `Microsoft.Update.Session` COM interface |
| **System** | `windows-defender` | Updates antivirus & antimalware definitions via `MpCmdRun.exe` |
| **System** | `wsl` | Updates Windows Subsystem for Linux (`wsl --update`) |
| **Package Managers** | `winget` | Upgrades all desktop apps (`winget upgrade --all --include-unknown`) |
| **Package Managers** | `choco` | Upgrades Chocolatey packages (`choco upgrade all -y`) |
| **Package Managers** | `scoop` | Updates Scoop buckets & installed apps (`scoop update *`) |
| **Package Managers** | `vcpkg` | Upgrades C++ libraries (`vcpkg upgrade`) |
| **Dev Runtimes** | `mise` | Updates `mise` binary, all managed tools/runtimes (`mise upgrade`), and plugins |
| **Dev Runtimes** | `git` | Parallel multi-drive & user folder scan for `.git` repos with auto-pull & dirty tree detection |
| **Dev Runtimes** | `dotnet` | Updates global tools (`dotnet tool update -g`) and SDK workloads |
| **Dev Runtimes** | `python` | Updates outdated `pip` packages, `pipx` apps, and `uv` tools |
| **Dev Runtimes** | `node` | Updates global packages across `npm`, `yarn`, `pnpm`, `bun`, and `deno` |
| **Dev Runtimes** | `rust` | Updates Rust toolchains (`rustup`) and cargo binaries (`cargo-install-update` / `cargo-binstall`) |
| **Dev Runtimes** | `go` | Checks & updates Go environment and toolchain |
| **Dev Runtimes** | `powershell-modules` | Updates installed modules from PSGallery (`Update-Module`) |
| **Containers** | `docker` / `podman` | Pulls latest container images and prunes dangling resources |
| **Maintenance** | `cleanup` | Safely flushes `%TEMP%`, `C:\Windows\Temp`, Delivery Optimization cache & Recycle Bin |

---

### 🎬 Windows Demo Showcase

https://github.com/user-attachments/assets/winupdate-demo.mp4

<video src="assets/winupdate-demo.mp4" controls="controls" width="100%"></video>

---

### How to Run (Windows)

#### 1. Native Windows Desktop App (`WinUpdate.App.exe`) - GUI
Run or double-click the portable standalone desktop application:
```powershell
# Double-click or run from terminal:
.\win_script\WinUpdate.App.exe

# Or via dotnet CLI:
dotnet run --project win_script/WinUpdate.App
```

#### 2. Console CLI Mode (`WinUpdate.exe`)
Run all system and developer updates directly from your terminal:
```powershell
# Run all available update steps:
.\win_script\WinUpdate.exe

# Or via dotnet CLI:
dotnet run --project win_script/WinUpdate
```

#### 3. CLI Flags & Selective Execution
```powershell
# Preview changes without modifying the system
.\win_script\WinUpdate.exe --dry-run

# List all detected update modules on your machine
.\win_script\WinUpdate.exe --list

# Run only specific steps (e.g. winget and git)
.\win_script\WinUpdate.exe --only winget,git

# Skip specific steps (e.g. skip windows-update)
.\win_script\WinUpdate.exe --skip windows-update

# Run an entire category (System, PackageManagers, DevRuntimes, Containers, Maintenance)
.\win_script\WinUpdate.exe --category DevRuntimes

# Run only temporary file & cache cleanup
.\win_script\WinUpdate.exe --cleanup-only
```

---

### Configuration (`win_script/WinUpdate/config.json`)

You can customize scan paths, ignore rules, concurrency, timeouts, and toggles:
```json
{
  "dryRun": false,
  "parallelGit": true,
  "maxGitParallelism": 8,
  "stepTimeoutMinutes": 15,
  "git": {
    "enabled": true,
    "scanAllDrives": true,
    "ignoredDirectories": ["node_modules", "bin", "obj", ".vs", "AppData", "vendor"]
  },
  "cleanup": {
    "clearUserTemp": true,
    "clearSystemTemp": true,
    "clearDeliveryOptimization": true,
    "emptyRecycleBin": false
  }
}
```

---

## 🐧 Linux: `linux_script/update.sh` (Bash)

### Features:
- Auto-detects privilege escalation tool (`sudo`, `doas`, or `su`).
- Detects distro and package manager (`paru`, `yay`, `pacman`, `nala`, `apt-get`, `dnf`, `yum`, `zypper`, `apk`, `emerge`, `nix-env`).
- Clones and installs AUR helpers (`yay-git`) if missing on Arch.
- Locates and updates Git repositories across the user's home directory.
- Upgrades Flatpak apps, Python virtualenvs / `pipx`, and Distrobox containers.
- Flushes RAM caches via `sync` and drop caches.

### How to Run (Linux):
```bash
chmod +x ./linux_script/update.sh
./linux_script/update.sh
```

---

## 🛠 Prerequisites

- **Windows**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or ASP.NET Core runtime.
- **Linux**: Bash, standard coreutils, `jq`, `mlocate`.
