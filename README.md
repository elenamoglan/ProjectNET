# Void Runner

A small arcade-style survival game: dodge enemies, survive waves, and chase a high score. Built with **C#** and **Silk.NET** (SDL windowing, OpenGL rendering, OpenAL audio).

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) (LTS)
- **Windows** is the primary target (OpenGL 3.3 core, SDL2 native dependencies via NuGet)

## Run

From the repository root:

```bash
dotnet run
```

Release build:

```bash
dotnet run -c Release
```

Or build and run the executable:

```bash
dotnet build -c Release
.\bin\Release\net10.0\VoidRunner.exe
```

## How to play

- **Move**: **WASD** or **arrow keys**
- **Pause**: **P** or **Escape** (when not in a game-over dialog)
- **Goal**: Stay alive as long as you can. Enemies spawn from the edges; difficulty ramps up in **waves**. You have limited **lives**; when they reach zero, it’s **game over**
- **Score**: Increases over time and with wave progression

### Main menu

- **Play / High Scores / Quit**: click the buttons (mouse)
- **Language**: **←** / **→** to cycle supported languages (UI text updates when translations load)
- **Exit menu**: **Escape** closes the window from the main menu

### After game over

- If you set a **top-10** score, you can enter a name (**Enter** to confirm, **Escape** to cancel and use a default name)
- **Play again?**: **Y** for yes, **N** or **Escape** for no (returns to menu)

## Data files

Saved on your machine under:

`%LocalAppData%\VoidRunner\`

| File          | Purpose                          |
|---------------|----------------------------------|
| `scores.json` | Top high scores (JSON)        |
| `crash.log`   | Last unhandled exception text (if the app crashes) |

## Tech stack

- **.NET 10**, nullable reference types, warnings treated as errors
- **Silk.NET** — windowing (SDL), input, OpenGL, OpenAL
- **SixLabors.ImageSharp** / **Fonts** — text rendering to textures
- **Newtonsoft.Json** — high-score persistence

## Project layout

- `Program.cs` — entry point
- `App/` — application loop, menu, game session
- `Graphics/` — 2D OpenGL renderer
- `Models/` — player, enemies, constants
- `Strategies/` — enemy movement strategies
- `Services/` — audio, scores, translation
- `Localization/` — language strings and remote translation hookup
