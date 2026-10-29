# ArmorStandEnchant — Valheim 1.0.21 release

## Goal
- Verify Release build against Valheim 1.0.21, update BepInExPack to 5.4.2360, changelog, Thunderstore `/dist` zip, git tag `v1.0.1`.

## Status
- Completed

## Changes
- Version 1.0.1 in csproj, Plugin.cs, manifest.
- BepInEx: `denikson-BepInExPack_Valheim-5.4.2360`.
- CHANGELOG (root + Package).
- `pack.ps1` and `dist/` Thunderstore artifacts.

## Commands
- `dotnet build -c Release` — succeeded (0 warnings, 0 errors).

## Output
- DLL: `D:\AI-Agent-Projects\cjaycraft\Cjayride.ArmorStandEnchant\bin\Release\net481\Cjayride.ArmorStandEnchant.dll`
- Zip: `D:\AI-Agent-Projects\cjaycraft\Cjayride.ArmorStandEnchant\dist\cjayride-ArmorStandEnchant-1.0.1.zip`
