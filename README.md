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
  - Produces a bootstrap command from embedded static scripts in the current development build.
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

The current build embeds the files in `scripts\` into the EXE and generates
base64-backed `bash` commands, so the first-time takeover path does not depend
on placeholder GitHub URLs. Before a public release, the same script files can be
published to a fixed GitHub Release/tag and the command builder can be switched
back to URL download mode.
