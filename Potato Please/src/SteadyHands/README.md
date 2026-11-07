# Steady Hands (Potato Please)

Your hands stop shaking while you peel. The peeler goes exactly where you aim.

The game deliberately adds a fast hand tremor and a slow hand drift to the peeler, plus a shake
when your peeling stamina runs out. This mod turns all three off. Everything else, including
stamina draining and refilling, works as normal. The cough event's shake is left alone.

## Settings

`BepInEx\config\dev.potatoplease.steadyhands.cfg`, written on first launch:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RemoveTremor` | `true` | No fast hand tremor. |
| `RemoveDrift` | `true` | No slow drift away from where you aim. |
| `RemoveExhaustShake` | `true` | No shake when stamina runs out. |

## Install

- **BepInEx 6 IL2CPP already installed:** extract `SteadyHands.zip` into the game folder.
- **Clean game:** extract `SteadyHands-Pack.zip` into the game folder (BepInEx included).
  The first launch takes a minute or two longer while BepInEx sets itself up.

Uninstall: delete `BepInEx\plugins\SteadyHands.dll`.

## Requirements

Potato Please (Unity 6000.3, IL2CPP, win-x64) with BepInEx 6.0.0-be.781 IL2CPP x64.

## Building

`build.ps1` builds against the interop assemblies BepInEx generates in the game folder
(`..\..\Potato Please\BepInEx\interop`) and copies the DLL into `BepInEx\plugins`.
