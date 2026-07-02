# No Decoy Death — a MECCHA CHAMELEON mod

Shoot the clone, lose the clone — not your life.

In [MECCHA CHAMELEON](https://store.steampowered.com/app/2995940/) (dev: PenguinHotel), a Survivor's decoy is supposed to be a distraction. Instead, if a Hunter shoots it, the game kills the Survivor and converts them into a Hunter anyway — the decoy never even disappears. This mod fixes that: when a Hunter shoots your decoy, the decoy pops (same as if you'd deleted it yourself with X) and you stay a Survivor. A real body shot still kills you normally.

This repo exists so anyone — not just people who trust a random download — can read exactly what the mod does before running it. There's no compiled-and-hidden logic here: the entire fix is [one Lua file](mod/NoDecoyDeath/Scripts/main.lua), about 170 lines, and the installer is a small C# program you can read start to finish in a couple of minutes.

## Install

1. Download [`NoDecoyDeath-Installer.zip`](NoDecoyDeath-Installer.zip) from this repo and unzip it.
2. Close the game.
3. Run `Install NoDecoyDeath.exe`. It locates your MECCHA CHAMELEON install automatically and sets everything up.
4. Launch the game and **host** a match as a Survivor.

In the lobby, press **Shift+F7** to toggle protection on/off for that round (it locks once the countdown starts, so it can't be flipped mid-match). You'll see a "Decoy Protection: ON/OFF" message and hear a click.

**Host-only.** The fix runs on the host's copy of the game, because that's the machine that's authoritative for hit results. Nobody else in the lobby needs to install anything.

## How it works

The mod runs on top of [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS), an open-source Unreal Engine script loader, which lets Lua register hooks on the game's own Blueprint functions without modifying any game files.

When a Hunter shoots a decoy, the game's own code resolves "which Survivor owns this decoy" and asks the host to validate the hit by calling a function named `AntiChatTrace(End, Target)` — `End` is the reported hit location, `Target` is the Survivor to kill if the hit checks out. The mod hooks that call on the host:

1. It checks whether the reported hit point is closer to one of the target's own decoys than to the target's own body. If so, this was a decoy hit.
2. It destroys that decoy — calling the same `K2_DestroyActor` and `DestroyDecoy()` functions the game itself uses when you delete a decoy with X.
3. It rewrites the `End` vector to a point far away, so the host's own existing hit-validation logic fails the shot and the kill never lands.

That's the entire fix. No packet interception, no memory patching, no anti-cheat bypass (the game has none) — it detects one specific case and redirects one parameter using the game's own functions, on your own machine, for your own hosted matches.

The rest of the file is UI: an on-screen toggle indicator (a small `UserWidget` + `TextBlock` created at runtime, set to `HitTestInvisible` so it never blocks clicks) and lobby-state detection so the toggle only works before a match starts.

## Why this is safe to run

- **No network activity.** The mod doesn't open a socket, phone home, or fetch anything at runtime.
- **No credential or file access.** It doesn't read your Steam session, browser data, or any file outside the game's own UE4SS log.
- **Host-only, no advantage over other players.** It only changes what happens on the machine hosting the match, and only for decoy hits specifically — a direct body shot still kills you.
- **The installer just copies files.** Read [`installer/Install.cs`](installer/Install.cs) — it locates your Steam library, copies the mod payload, and edits `mods.txt` to enable it. Nothing else.
- **Made with the developer's permission.** PenguinHotel gave permission to build and share this mod for free.

## Source layout

```
mod/NoDecoyDeath/Scripts/main.lua   the actual fix (UE4SS Lua mod)
installer/Install.cs                installer source
installer/Uninstall.cs              uninstaller source
NoDecoyDeath-Installer.zip          ready-to-run build (installer .exe + UE4SS + this mod)
```

## Building from source

- **The mod itself** is just `main.lua` — drop it into `<game>/Chameleon/Binaries/Win64/ue4ss/Mods/NoDecoyDeath/Scripts/main.lua` on a system with [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) installed, and enable it in `ue4ss/Mods/mods.txt`.
- **The installer** compiles with the .NET Framework compiler that ships with Windows:
  ```
  csc /optimize+ /r:System.dll /out:Install.exe installer/Install.cs
  csc /optimize+ /r:System.dll /out:Uninstall.exe installer/Uninstall.cs
  ```
  It expects a `payload/` folder next to the `.exe` containing `dwmapi.dll` and a `ue4ss/` folder (UE4SS itself, plus this mod under `ue4ss/Mods/NoDecoyDeath/`) — that's what's already assembled inside the release zip above.

## Uninstall

Run `Uninstall NoDecoyDeath.exe` from the same zip, or delete `dwmapi.dll` and the `ue4ss/` folder from the game's `Binaries/Win64` directory (or Steam → Properties → Installed Files → Verify integrity of game files).

## License

All rights reserved. This source is published for transparency so you can verify what the mod does before running it — it is not licensed for reuse, modification, or redistribution. See [LICENSE](LICENSE).
