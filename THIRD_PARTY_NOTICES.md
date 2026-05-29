# Third-party notices

Date: 2026-05-30

This launcher is a compatibility/patch launcher for legally installed copies of Final Tactics. The distributable launcher does not include Final Tactics game data, movies, music, models, save data, or original game executables.

## Included in FinalTacticsLauncher.exe

### DDrawCompat

- Component: `ddraw.dll`
- Version: `v0.7.1`
- Project: `https://github.com/narzoul/DDrawCompat`
- Release checked: `https://github.com/narzoul/DDrawCompat/releases/tag/v0.7.1`
- License checked: `https://github.com/narzoul/DDrawCompat/blob/master/LICENSE.txt`
- License: 0BSD / Zero-Clause BSD style license
- Embedded file SHA256: `F75F0AC48D2782F225C483DC2F1142303A513E8DD8A60793891ADE89F64755EA`

DDrawCompat's license permits use, copy, modification, and distribution with or without fee, and includes an as-is/no-warranty disclaimer. The license does not require a separate notice file, but this notice is kept for transparency.

### DSETUP.DLL compatibility stub

- Component: `DSETUP.DLL`
- Embedded file SHA256: `8B803E2C0F2BA613F61BF4C79EE4B4A020CD2045CC8B5055CBCDE64933EFD9B9`
- Origin: project-local compatibility stub created for this launcher
- Purpose: provides the `DirectXSetupGetVersion` ordinal used by `FT.exe` so the old DirectSetup loader path does not fail on modern Windows

This is not Microsoft's original DirectX `DSETUP.DLL`. The original bundled DirectX DLL is not included in the launcher distribution.

## Not included

- Final Tactics / Elemental Arts game files
- Original `FT.exe`
- Original game icon `ft.ico`
- Original game music, movies, models, save data, or data archives
- Microsoft's original DirectX setup DLL
- Python or PowerShell helper scripts

## Distribution note

The release artifact is intended to be the zip generated under `dist/`. `FinalTacticsLauncher.ini` may be generated next to the exe after first run. The launcher writes compatibility files into the selected local game folder during application.

Final Tactics, Elemental Arts, InterSoft, and any related names/assets remain the property of their respective rightsholders. This project is not affiliated with or endorsed by those rightsholders.
