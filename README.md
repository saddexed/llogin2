# llogin - LPU Wifi Autologin

A cross-platform .NET 10 console application for automatic login to the LPU captive portal.

## Quick Install

### Windows (PowerShell)
Run this command in an elevated PowerShell terminal:
```powershell
irm https://raw.githubusercontent.com/saddexed/llogin2/install-script/install.ps1 | iex
```

### Linux / macOS (Bash)
Run this command in your terminal:
```bash
curl -sSL https://raw.githubusercontent.com/saddexed/llogin2/install-script/install.sh | bash
```

## Basic Usage

| Command | Description |
| :--- | :--- |
| `llogin` | Login with default account |
| `llogin -s` | Check connection status |
| `llogin -l` | Logout current session |
| `llogin -ls` | List all saved accounts |
| `llogin -a <user> <pass>` | Add/Update an account |
| `llogin -d <user>` | Set default account |
| `llogin --task` | Setup auto-login (Windows) |

Run `llogin -h` for full documentation.
