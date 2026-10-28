# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A maze-chase game in the style of Namco's 1980 arcade game Pac-Man, in C# on .NET 10 and Avalonia 12, drawn with
Avalonia's `DrawingContext`. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM).
Everything in it is original: the graphics are drawn in code, and the sounds and music are synthesised by
`tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAPacMan.slnx         # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAPacMan.slnx          # every test runs headless: no window opens
dotnet run --project src/AVAPacMan  # play it; --no-audio for silence
PACMAN_SHOTS=$PWD/Images dotnet test tests/AVAPacMan.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py        # rebuild src/AVAPacMan/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAPacMan/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, demonstration, play, pause, high scores) and steps the game;
    `GameEngine.Play.cs` plays a level: dots, energizers, fruit, ghosts and their scatter-and-chase timetable.
  - `Entities.cs` has the player and the ghosts (`Mover`, `Player`, `Ghost`) and effects.
  - `AttractPilot.cs` is the computer player, used by the demonstration and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAPacMan/Models/`: the maze and the rules' tables.
  - `Field.cs` has `Vec`, the tiles and `Maze` (the layout and the fixed step).
  - `Levels.cs` has each level's speeds, fright times, timetable and fruit.
  - `Sprites.cs` is the pixel art, drawn for this game.
- `src/AVAPacMan/Views/`: `GameCanvas` (split over `GameCanvas.cs`, `.Draw.cs` and `.Screens.cs`) draws the frame
  and runs the 60 Hz timer.
- `src/AVAPacMan/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVAPacMan/Sounds/`: the WAV files that `tools/make_sounds.py` writes.
- `tests/AVAPacMan.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`.
  - `GameplayTests` play levels from a seed; `ScreenshotTests` render frames. There is no probe.
- `Images/`: the README's screenshots.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` feeds the frame time into fixed 1/60 s ticks (`Maze.Step`),
  independent of the frame rate, so tests can play whole games deterministically from a seed. Draw
  random numbers from the engine's `_rng` (a `Random` made from the game's seed), never from
  `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** `Controls` holds a `Dir` (the way the player wants to go), not a `dy`:
  keep `Dir.Up` meaning up the screen on keys and pad alike. Test new controls through `InputHandler`, not by
  setting `Controls` directly.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAPacMan`); tests load
  the table from a temporary folder and set `Silent` on the engine.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
