# AI Usage Disclosure

## Tools used

- **Claude Sonnet 4.6** (Anthropic) - chat-based code assistance via claude.ai
- **Cursor** - AI-assisted code editor

---

## How it was used

### 1. Game idea and logic (Claude)

Used Claude to brainstorm the game concept: a top-down survival dodge game with multiple enemy types, lives, waves, and a score system. The core loop (dodge enemies, survive as long as possible, difficulty ramps over time) and the three enemy archetypes (Chaser, Speeder, Wanderer) were proposed by Claude and adopted as-is.

### 2. Architecture review and requirement gap analysis

Asked Claude to read the assignment requirements and identify what was missing or incorrect in my existing project.

### 3. Bug fixes and missing files

Claude fixed the identified issues and generated the files listed below as fully AI-generated.

### 5. API integration (Cursor)

Used Cursor's inline AI suggestions while writing `TranslationService.cs` and `ScoreService.cs`, specifically the JSON parsing, and async file I/O patterns. The surrounding class structure and decisions about caching and fallback behaviour were my own.

---

## Files that are fully AI-generated

| File        | Notes                                   |
| ----------- | --------------------------------------- |
| `README.md` | Written by Claude based on the codebase |

---

## Files I wrote, with AI fixes applied

| File                             | Tool   | What AI changed                                         |
| -------------------------------- | ------ | ------------------------------------------------------- |
| `Services/TranslationService.cs` | Cursor | JSON parsing, async patterns                            |
| `Services/ScoreService.cs`       | Cursor | Async file I/O, JSON serialization patterns             |
| `App/GameApplication.cs`         | Claude | Helped with `Run` function                              |
| `App/GameSession.cs`             | Claude | Helped with `UpdateLiveGame` and `SpawnEnemy` functions |

---

## Files entirely my own work

- `Graphics/GlRenderer2D.cs`
- `Graphics/IGameRenderer.cs`
- `Graphics/RectF.cs`
- `Graphics/ColorRgba.cs`
- `Strategies/IMovementStrategy.cs`
- `Strategies/ChaserStrategy.cs`
- `Strategies/SpeedDashStrategy.cs`
- `Strategies/WanderStrategy.cs`
- `Models/GameObject.cs`
- `Models/Enemy.cs`
- `Models/Player.cs` (original, before AI edits)
- `Models/HighScore.cs`
- `Models/GameConstants.cs`
- `Services/AudioService.cs`
- `Program.cs`
- `App/GameApplication.cs` (original, before AI edits)
- `App/GameSession.cs` (original, before AI edits)

---
