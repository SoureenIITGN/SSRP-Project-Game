# Real-Time FSR Signal Processing for Finger-Pressure-Controlled Two-Player Pong Game Using Unity

## Description

Acquire force signals from four FSRs, two per player, using an Arduino. Identify noise (mains interference, ADC quantization, drift, channel crosstalk), then design and compare digital filters. The filtered difference between each player's "up" and "down" sensors controls paddle velocity in a Unity Pong game. We can evaluate how filter type and cutoff affect paddle smoothness, response time and game performance.

---

## Table of Contents

0. [Quick Start: run the game](#quick-start-run-the-game) and [Project Status](#project-status)
1. [Project Overview](#project-overview)
2. [Repository Structure](#repository-structure)
3. [One-Time Setup](#one-time-setup)
4. [Team Workflow](#team-workflow)
5. [Golden Rules for Unity + Git](#golden-rules-for-unity--git)
6. [Git Cheat Sheet](#git-cheat-sheet)
7. [Troubleshooting](#troubleshooting)

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

The layout below is the **proposed** convention. Create folders as you need them and keep to it.

```
SSRP-Project-game/
├── Assets/
│   ├── Scenes/        # Game scenes (see the scene rule below)
│   ├── Scripts/       # C# code (Input/, Gameplay/, UI/)
│   ├── Prefabs/       # Paddle, Ball, Walls, etc.
│   ├── Art/           # Sprites, materials
│   └── Audio/         # Sound effects
├── Packages/          # Unity packages (auto-managed)
├── ProjectSettings/   # Unity settings (committed, shared by everyone)
├── arduino/           # Arduino sketch(es) for reading the FSRs
├── python/            # Filtering, FFT/PSD, plots, serial/UDP bridge
├── data/              # Small recorded datasets (CSV) for analysis
├── docs/              # Photos, figures, notes for the presentation
├── .gitignore
└── README.md
```

---

## One-Time Setup

Everyone does this once.

1. **Install Git** from <https://git-scm.com/downloads>.
   *Optional but recommended for beginners:* install [GitHub Desktop](https://desktop.github.com/), a graphical app that does everything in this guide with buttons.
2. **Install [Unity Hub](https://unity.com/download)** and sign in.
3. **Install the correct Unity Editor version.** Open `ProjectSettings/ProjectVersion.txt` after cloning to see the exact version. **Everyone must use the same version.** Mixed versions corrupt scenes and settings. Never upgrade the version unless the whole team agrees.
4. **Install a C# editor** (Visual Studio, VS Code or Rider). Unity Hub can install Visual Studio with the Editor.
5. **Check your Git identity** (only needed for command-line Git):
   ```bash
   git config --global user.name  "Your Name"
   git config --global user.email "you@example.com"
   ```

---

## Team Workflow

This is the professional "feature branch" workflow, in short: **`main` always works, and all changes arrive through reviewed Pull Requests.**

### Step 1: Clone the repo (once)

```bash
git clone https://github.com/SoureenIITGN/SSRP-Project-game.git
```

(In GitHub Desktop: *File -> Clone repository*, choose `SSRP-Project-game`.)

### Step 2: Open the project in Unity Hub (once per clone)

1. Open **Unity Hub**, go to **Projects**, then **Add -> Add project from disk**.
2. Select the cloned folder `SSRP-Project-game` (the folder that contains `Assets/`).
3. Open it with the correct Editor version.

The first open will take several minutes. Unity is rebuilding the `Library/` folder, which is not stored in Git. This is normal.

### Step 3: Feature branching (never commit to `main`)

Always work in your own branch, one branch per task. Start from an up-to-date `main`:

```bash
git checkout main
git pull
git checkout -b feature/paddle-movement
```

**Branch naming:**

| Prefix | Use for | Example |
|---|---|---|
| `feature/` | New gameplay or code | `feature/paddle-movement` |
| `art/` | Sprites, UI, sounds | `art/ui-sprites` |
| `fix/` | Bug fixes | `fix/ball-stuck-on-wall` |
| `analysis/` | Python filters, plots, data | `analysis/butterworth-filter` |
| `docs/` | README, figures, notes | `docs/setup-photos` |

Keep branches small and short-lived (a day or two). Long-lived branches cause painful merges.

### Step 4: The Pull-Push Rule

> **Before you open Unity, always pull the latest updates. When a feature is ready, push it and open a Pull Request.**

**Start of every work session (Unity closed):**

```bash
git checkout feature/paddle-movement    # your branch
git fetch origin
git merge origin/main                   # bring in your teammates' merged work
```

Then open the project in Unity Hub.

**While working:**

1. Save in Unity (**Ctrl+S** / **Cmd+S** for the scene).
2. Commit small and often, with a clear message:
   ```bash
   git add .
   git commit -m "Add paddle velocity control from input axis"
   ```
   Good messages say *what* changed (`Add ball bounce off paddles`). Bad messages say nothing (`update`, `stuff`, `fix`).
3. Push your branch (first push needs `-u`):
   ```bash
   git push -u origin feature/paddle-movement
   ```
   Later pushes are just `git push`.

### Step 5: Open a Pull Request (PR)

1. On GitHub, open the repo and click **Compare & pull request** (or go to **Pull requests -> New pull request**).
2. Set **base: `main`** and **compare: your branch**.
3. Write a short description: what you changed, how to test it, and any scene or prefab you touched.
4. Request a review from at least one teammate.

### Step 6: Review and merge

- A teammate pulls your branch, opens it in Unity and checks that it works. Check for console errors and that scenes still open.
- **Do not merge your own PR.** Another team member approves it.
- Merge on GitHub (**Merge pull request**), then click **Delete branch**.

### Step 7: Update your local copy

```bash
git checkout main
git pull
```

Now start the next task from Step 3 with a new branch.

---

## Golden Rules for Unity + Git

1. **Never commit directly to `main`.** Branch, push, PR, review and merge.
2. **Always pull before opening Unity.**
3. **Always commit `.meta` files.** Unity uses them to link assets, scripts and prefabs. A missing `.meta` file breaks references for everyone.
4. **Never commit generated folders:** `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Builds/`. Check that `.gitignore` covers them. If these appear in `git status`, stop and tell the team.
5. **One person per scene at a time.** Scene and prefab files are text but merge very badly. Announce in the group chat before editing `Assets/Scenes/*.unity`. To avoid conflicts, build objects as **Prefabs** and keep personal test scenes (e.g. `Test_Alex.unity`) for experiments.
6. **Same Unity version for everyone.** See [One-Time Setup](#one-time-setup).
7. **Keep commits focused.** One idea per commit, and no commits of half-broken code to shared branches.
8. **Don't commit secrets or huge files.** Keep large raw recordings, videos and builds out of the repo. Share them through a drive link instead.
9. **Open Unity only after Git operations finish.** Don't switch branches while Unity is open. Close it, switch, then reopen.
10. **Ask before force-pushing or deleting anything.** If Git shows a scary message, stop and ask the team.

---

## Git Cheat Sheet

| Task | Command |
|---|---|
| See what changed | `git status` |
| See current branch | `git branch` |
| Switch branch | `git checkout <branch>` |
| Create and switch to new branch | `git checkout -b <branch>` |
| Download latest remote info | `git fetch origin` |
| Bring `main` into your branch | `git merge origin/main` |
| Stage all changes | `git add .` |
| Commit | `git commit -m "message"` |
| Push | `git push` |
| Undo uncommitted changes to one file | `git checkout -- <file>` (this permanently discards your edits) |

---

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
