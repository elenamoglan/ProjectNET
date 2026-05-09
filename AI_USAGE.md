# AI Usage Disclosure

## Tools used

- **Claude Sonnet 4.6** (Anthropic) - chat-based code assistance via claude.ai
- **Cursor** - AI-assisted code editor

---

## How it was used

### 1. Game idea and logic (Claude)
Used Claude to brainstorm the game concept: a top-down survival dodge game with multiple enemy types, lives, waves, and a score system. The core loop (dodge enemies, survive as long as possible, difficulty ramps over time) and the three enemy archetypes (Chaser, Speeder, Wanderer) were proposed by Claude and adopted as-is.

### 2. Architecture review and requirement gap analysis
Asked Claude to read the assignment requirements and identify what was missing or incorrect in my existing project (wrong target framework, missing `TreatWarningsAsErrors`, incomplete `TryTypeChar` method in `GameApplication.cs`).

### 3. Bug fixes and missing files
Claude fixed the identified issues and generated the files listed below as fully AI-generated.

### 4. Feature addition - Power-ups
Discussed adding a new gameplay mechanic. Claude implemented `PowerUp.cs` and integrated it into `GameSession.cs` (spawning logic, collision pickup, shield/slow-time effect timers, HUD indicators). The integration touched my existing `GameSession` and `Player` files.

### 5. API integration (Cursor)
Used Cursor's inline AI suggestions while writing `TranslationService.cs` and `ScoreService.cs`, specifically the MyMemory HTTP request, JSON parsing, and async file I/O patterns. The surrounding class structure and decisions about caching and fallback behaviour were my own.

---

## Files that are fully AI-generated

| File | Notes |
|------|-------|
| `Models/PowerUp.cs` | New feature, written entirely by Claude |
| `README.md` | Written by Claude based on the codebase |

---

## Files I wrote, with AI fixes applied


| File | Tool | What AI changed |
|------|------|-----------------|
| `Services/TranslationService.cs` | Cursor | HTTP request, JSON parsing, async patterns |
| `Services/ScoreService.cs` | Cursor | Async file I/O, JSON serialization patterns |
| `App/GameApplication.cs` | Claude | Removed unused `_typeKeyPrev` field; completed truncated `TryTypeChar`; added power-up legend |
| `App/GameSession.cs` | Claude | Added power-up list, spawn timer, slow-time timer, `ApplyPowerUp`, `SpawnPowerUp`; wired into `UpdateLiveGame` and `Draw` |
| `Models/Player.cs` | Claude | Added `TriggerShield(float duration)` method and shield glow rendering |
| `VoidRunner.csproj` | Claude | Changed `net8.0` to `net10.0`; added `TreatWarningsAsErrors` |

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
