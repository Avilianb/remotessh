# My SSH

My SSH is a Windows native server security tool built with .NET 10 and WPF.

## Current scope

- Server profiles stored at `%APPDATA%\My SSH\servers.json`.
- SSH config import from `%USERPROFILE%\.ssh\config`.
- Server table with search, tag filter, edit, delete, and key-based connection test.
- OpenSSH-backed multi-tab terminal surface.
- Host key fingerprint confirmation before first trusted connection.
- Key takeover and rotation workbench:
  - Generates ed25519 keys locally.
  - Produces a fixed GitHub Release/tag bootstrap command.
  - Probes the temporary SSH port.
  - Verifies temporary and official key login before cleanup.
  - Writes a My SSH managed block into the local SSH config after verification.
- Static Linux server scripts for bootstrap, cleanup, and disabling password login.

## Publish

Install the .NET 10 SDK, then run:

```powershell
.\build\Publish.ps1
```

The release artifacts are written to `dist\MySSH.exe` and `dist\MySSH.exe.sha256`.

## Script release

Server scripts are published to:

```text
https://github.com/Avilianb/remotessh/releases/tag/v0.1.0
```

The generated takeover command downloads the fixed tag assets instead of `main`
or `latest`.
