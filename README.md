# Real-Time FSR Signal Processing for Finger-Pressure-Controlled Two-Player Pong Game Using Unity

## Description

Acquire force signals from four FSRs, two per player, using an Arduino. Identify noise (mains interference, ADC quantization, drift, channel crosstalk), then design and compare digital filters. The filtered difference between each player's "up" and "down" sensors controls paddle velocity in a Unity Pong game. We can evaluate how filter type and cutoff affect paddle smoothness, response time and game performance.

---

## Table of Contents

0. [Prerequisites](#prerequisites)
1. [Run by adding the GitHub repository](#run-by-adding-the-github-repository)
2. [Run from a cloned repository](#run-from-a-cloned-repository)
3. [Quick Start: run the game](#quick-start-run-the-game)
4. [Project Status](#project-status)
5. [Project Overview](#project-overview)
6. [Repository Structure](#repository-structure)
7. [Troubleshooting](#troubleshooting)

---

## Prerequisites

Complete these prerequisites before opening the project.

### Required for keyboard gameplay

1. **Unity Hub**
   - Install Unity Hub from <https://unity.com/download>.
   - A Unity account and sign-in may be required for license activation.

2. **Unity Editor 6000.5.10f1**
   - Install the exact Editor version listed in `ProjectSettings/ProjectVersion.txt`.
   - Unity Hub: **Installs > Install Editor**, select **6000.5.10f1**, then install the required modules.
   - Do not open the project with a substantially different Unity version. Unity can change serialized scene and package data when versions do not match.

3. **Internet access on the first import**
   - Unity Package Manager needs internet access to restore the packages in `Packages/manifest.json`.
   - The first import may take several minutes and will generate the local `Library/` and `Temp/` folders.

4. **A supported desktop platform**
   - Windows, macOS, or Linux can open the project as an Editor project.
   - A keyboard and display are sufficient for normal keyboard gameplay.
   - About 5 GB of free disk space is recommended for the Editor, package cache, project library, and temporary files.

5. **A C# editor is optional**
   - Visual Studio, VS Code, or Rider is useful for viewing or changing C# code.
   - It is not required simply to run the game.

### Optional for physical FSR input

The Arduino hardware is not required for keyboard gameplay. To use physical sensors:

- Arduino Uno or a compatible board
- USB data cable
- Four FSR sensors
- Four 10 kΩ resistors
- Breadboard, jumper wires, mounting pads, and strain relief
- Arduino IDE for flashing [arduino/ssrp_fsr/ssrp_fsr.ino](arduino/ssrp_fsr/ssrp_fsr.ino)
- Wiring that follows [docs/hardware/wiring.md](docs/hardware/wiring.md)

Only one application may own the Arduino USB serial port at a time. Close the Arduino Serial Monitor/Plotter, Python serial logger, or other serial application before selecting **Connect** in Unity.

### Optional for Python tools

Python is not required for normal Unity gameplay. It is only required for serial logging or the UDP bridge tools.

- Python 3
- Install the Python dependency:
  ```powershell
  pip install -r python/requirements.txt
  ```

The `pyserial` package is required by the serial and Python filter modes. The UDP sender also supports a synthetic sine-signal mode that does not use Python serial hardware.

### Optional for standalone builds

Running in the Unity Editor does not require a build target module. To export an executable, install the matching Unity Build Support module in Unity Hub:

- Windows Build Support
- macOS Build Support
- Linux Build Support

---

## Run by adding the GitHub repository

Use this method to download the repository and open it in Unity without creating a local Git clone.

### A. Download and add a ZIP

1. Open the GitHub repository:
   <https://github.com/SoureenIITGN/SSRP-Project-Game-main>
2. Select **Code > Download ZIP**.
3. Extract the full ZIP into a normal writable folder.
4. Open Unity Hub and go to **Projects**.
5. Select **Add > Add project from disk**.
6. Select the extracted folder that contains `Assets/`, `Packages/`, and `ProjectSettings/`.
7. If Unity Hub does not find Unity 6000.5.10f1, install it and choose it for the project.
8. Open the project and wait for package resolution and asset import.

### B. Add it through a Git repository link

If your Unity Hub version provides version-control/project-link support:

1. Open Unity Hub and go to **Projects**.
2. Select **Add > Add project from version control** or **Clone repository**, if shown.
3. Paste:
   ```text
   https://github.com/SoureenIITGN/SSRP-Project-Game-main.git
   ```
4. Choose a local destination folder.
5. Open the cloned project with Unity 6000.5.10f1.

Unity Hub can also show **Open** inside GitHub Desktop or similar Git applications. In that case, clone or open the repository first, then use **Add project from disk** in Unity Hub.

---

## Run from a cloned repository

### 1. Install Git

Install Git from <https://git-scm.com/downloads>. GitHub Desktop is also suitable if command-line Git is not wanted.

### 2. Clone the repository

Command line:

```powershell
git clone https://github.com/SoureenIITGN/SSRP-Project-Game-main.git
cd SSRP-Project-Game-main
```

In GitHub Desktop: **File > Clone Repository**, paste the repository URL, and choose a local folder.

### 3. Install the Unity Editor

Open `ProjectSettings/ProjectVersion.txt` and confirm that it contains Unity `6000.5.10f1`. Install that exact version through Unity Hub.

### 4. Add and open the project

1. In Unity Hub, open **Projects**.
2. Select **Add > Add project from disk**.
3. Select the cloned repository folder—the folder containing `Assets/`, `Packages/`, and `ProjectSettings/`.
4. Open it with Unity 6000.5.10f1.
5. Wait for the first package restore and asset import. Unity recreates ignored folders such as `Library/` automatically.

### 5. Generate or refresh project assets

On the first Editor launch, if Unity asks **"Run SSRP > Build Project now?"**, select **Build now**. Otherwise, use **SSRP > Build Project** from the Unity menu.

This menu operation generates the scenes, prefabs, arena assets, and runtime configuration under `Assets/`. Do not remove the generated `.meta` files.

---

## Quick Start: run the game

1. Open the project with the exact Unity version in `ProjectSettings/ProjectVersion.txt` (6000.5.10f1).
2. **First open only:** the editor asks *"Run SSRP > Build Project now?"*. Click **Build now**, or use the
   menu **SSRP > Build Project**. This generates every scene, prefab, tile, arena and config asset from
   code (see `Assets/Editor/Build/`). One person runs it, then commits the new files under `Assets/`
   (including the `.meta` files), so everyone shares the same GUIDs.
3. Open `Assets/Scenes/Boot.unity` (**SSRP > Open Boot Scene**) and press **Play**.

| Action | Keys |
|---|---|
| Player 1 (left) | **W** = up sensor, **S** = down sensor |
| Player 2 (right) | **Up arrow** / **Down arrow** |
| Parry | both of your keys (or both FSRs) held firmly |
| Pause | **Esc** or **P** |
| Live sensor debug overlay | **F1** |

**With the Arduino:** flash `arduino/ssrp_fsr/ssrp_fsr.ino` and wire it as in `docs/hardware/wiring.md`.
Then go to **Match Setup**, pick the port, press **Connect**, then **Calibrate sensors...**, and set a
player's input to **Arduino (USB)**.

**Tests:** **Window > General > Test Runner**, then *EditMode* > Run All and *PlayMode* > Run All (`docs/physics.md`).

**Dev scenes** (open directly, no menu needed): `Scenes/Gameplay/Dev_PhysicsSandbox`,
`Scenes/Gameplay/Dev_TileArena`, `Scenes/Input/Dev_InputPipeline` (keyboard vs noisy keyboard, with the F1
overlay on) and `Scenes/Input/Dev_SerialPanel` (Arduino, no menu).

## Project Status

All 26 issues in `docs/GITHUB_ISSUES.md` are implemented. The git history has one commit per issue.
The items below **need people or hardware** and cannot be done in code:

| Issue | Remaining manual step |
|---|---|
| [A1] | Measure the interval and jitter on the real board (`python/serial_logger.py`) and fill in the table in `docs/serial-protocol.md` |
| [A5] | Wire, mount, photograph, record `data/samples/raw_1min.csv`, play a full match on FSRs (`docs/hardware/bringup.md`) |
| [C5] | Build the circuit in TinkerCAD, save screenshots and the share link in `docs/hardware/` (`docs/hardware/wiring.md`) |
| [D7] | Capture the screenshots for the "Add an arena" guide (`docs/EXTENDING.md`) |
| all | Review and approve the PRs as a team (rules in `docs/GITHUB_ISSUES.md`) |

Design notes: `docs/ARCHITECTURE.md` (how it fits together), `docs/EXTENDING.md` (adding tiles,
power-ups, abilities, arenas, inputs, filters), `docs/physics.md`, `docs/serial-protocol.md`, `docs/CREDITS.md`.

---

## Project Overview

Signal path (planned):

```
FSR x4  ->  Arduino (ADC)  ->  USB serial  ->  Python (filtering, analysis)  ->  Unity (paddle control)
```

Each player has two FSRs. The paddle velocity is proportional to the filtered difference between the player's "up" and "down" sensors:

```
v_paddle = K * ( F_up_filtered - F_down_filtered )
```

We compare filters (e.g. median, moving average, IIR low-pass) at different cutoffs and measure paddle smoothness, response time and game performance.

---

## Repository Structure

Unity only looks inside `Assets/`, `Packages/` and `ProjectSettings/`. Folders outside these (such as `python/`) are ignored by Unity, so they are safe places for non-Unity work.

```
SSRP-Project-Game-main/
├── Assets/
│   ├── Art/           # Sprites and visual assets
│   ├── Audio/         # Music and sound assets
│   ├── Editor/        # Unity Editor and project-generation tools
│   ├── Prefabs/       # Paddle, ball, UI, and gameplay prefabs
│   ├── Resources/     # Runtime-loaded resources
│   ├── Scenes/        # Boot, menu, game, arena, and development scenes
│   ├── ScriptableObjects/
│   ├── Scripts/       # C# runtime and UI code
│   │   ├── Arena/
│   │   ├── Audio/
│   │   ├── Core/
│   │   ├── Flow/
│   │   ├── Gameplay/
│   │   ├── Input/
│   │   └── UI/
│   ├── Settings/      # Render pipeline and project settings
│   └── Tests/         # EditMode and PlayMode tests
├── Packages/          # Unity packages (auto-managed)
├── ProjectSettings/   # Unity settings (committed, shared by everyone)
├── arduino/           # Arduino sketch(es) for reading the FSRs
├── python/            # Filtering, FFT/PSD, plots, serial/UDP bridge
├── data/              # Small recorded datasets (CSV) for analysis
├── docs/              # Photos, figures, notes for the presentation
├── tools/             # Asset and sound generation utilities
├── .gitignore
└── README.md
```

## Troubleshooting

**Unity says "Project is from a different Unity version."**
Install the version listed in `ProjectSettings/ProjectVersion.txt` through Unity Hub. Don't upgrade just to make the message go away.

**Pink or missing objects, or "missing script" warnings after a pull.**
Close Unity and make sure the pull finished. Reopen and let it finish importing. If it persists, check that the `.meta` files were committed by the teammate who added the asset.

**Merge conflict in a `.unity` or `.prefab` file.**
Don't try to fix this alone. Tell the team and agree on which version to keep. This is the reason for rule 5.

**`git push` is rejected.**
Run `git fetch origin`, then `git merge origin/main`, then push again.

**I committed to `main` by mistake.**
Don't push. Tell a teammate and ask for help.

**I forgot which branch I'm on.**
Run `git branch` (the current branch is marked with `*`).

**The SSRP menu is missing.**
The scripts did not compile. Open the Console (*Window > General > Console*), fix the first red error
(or send it to the team), and the menu appears after recompiling.

**`System.IO.Ports` / `SerialPort` could not be found.**
Go to *Edit > Project Settings > Player > Other Settings > Api Compatibility Level* and set it to **.NET Framework**.

**Serial: "Access denied" or "port in use".**
Another program has the port open: the Arduino IDE Serial Monitor/Plotter or `serial_logger.py`. Close it,
then press **Connect** again. On macOS use the `/dev/cu.usbmodem...` port.

**Text is invisible or the UI looks empty.**
The UI is built at runtime from code (`Assets/Scripts/UI`), so check the Console for errors first.
