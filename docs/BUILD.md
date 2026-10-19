# Build Guide — NHI Inventory Management System

How to build, run and package the application from source.

## Prerequisites

- **Windows 10/11 (64-bit)**
- [**.NET 8 SDK**](https://dotnet.microsoft.com/download/dotnet/8.0) — install the SDK
  (the "Desktop development with .NET" workload in Visual Studio also works)
- Optional, only for building the installer:
  [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

Verify the SDK:

```bat
dotnet --version
```

## Build and run

From the **repository root**:

```bat
dotnet restore src\NHi.Inventory\NHi.Inventory.csproj
dotnet build   src\NHi.Inventory\NHi.Inventory.csproj -c Release
dotnet run     --project src\NHi.Inventory\NHi.Inventory.csproj
```

Or use the convenience script:

```bat
scripts\build.bat
```

The executable is produced at:

```
src\NHi.Inventory\bin\Release\net8.0-windows\NHi.Inventory.exe
```

### Opening in Visual Studio

Open `NHi.Inventory.sln` at the repository root. The project references resolve to
`src\NHi.Inventory\NHi.Inventory.csproj`.

## Debug vs. Release

- **Debug** builds seed sample/test data on first run (`Data/TestDataSeeder.cs`) to make
  development easier.
- **Release** builds start with a clean database containing only the default admin/shop
  accounts and reference data (`Data/SeedData.cs`).

## Building the Windows installer

```powershell
pwsh -File installer\build-installer.ps1
# or, to override the version for a release:
pwsh -File installer\build-installer.ps1 -Version 1.1.0
```

The script:

1. Publishes the app self-contained for `win-x64` into `installer\publish\`.
2. Compiles `installer\NHiInventory.iss` with Inno Setup.
3. Produces `installer\Output\NHI-Inventory-Setup.exe` and copies it to the Desktop.

The `publish\`, `Output\` and `*.exe` outputs are **git-ignored** and are rebuilt on demand
(the setup executable exceeds GitHub's per-file size limit and is not stored in the repo).

## Verifying a build

The solution currently has **no automated test project**. To verify a change:

1. Build in `Release` with no warnings/errors:
   `dotnet build src\NHi.Inventory\NHi.Inventory.csproj -c Release`
2. Run the app and exercise the affected screen(s) manually:
   `dotnet run --project src\NHi.Inventory\NHi.Inventory.csproj`

## Troubleshooting

**`dotnet` is not recognised**

- The .NET 8 SDK is not installed or not on `PATH`. Reinstall from the link above.

**Package restore fails**

- Check your internet connection (NuGet restore needs it the first time), then retry
  `dotnet restore`.

**Build fails after moving files**

- Rebuild the solution from `NHi.Inventory.sln` at the repository root so project paths
  resolve correctly, and delete `bin\`/`obj\` if stale output is interfering.

**Urdu text prints as boxes**

- The Urdu Nastaleeq font is embedded from
  `src\NHi.Inventory\Resources\Fonts\` and must be present. Do not rename or remove it.
