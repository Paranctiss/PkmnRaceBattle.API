# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Always respond to the user in French.**

ASP.NET Core 8 server of PkmnRaceBattle (SignalR + MongoDB). The client lives in the sibling `../PokemonRaceBattle` repo; the workspace-level `../CLAUDE.md` describes the client ↔ server SignalR contract.

## Commands

```bash
dotnet build PkmnRaceBattle.API.sln
dotnet run --project PkmnRaceBattle.API
```

`Program.cs` forces `UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001")`, overriding `launchSettings.json` ports. There is no test project. `publish/` is a committed deployment output, not source.

## Architecture

### Layering
`API` → `Persistence` → `Application` → `Domain`:
- `Domain/Models` — Mongo document classes (`PlayerMongo`, `PokemonMongo`, `RoomMongo`, `BracketMongo`, `EnvironmentMongo`) and pokeapi JSON shapes.
- `Application/Contracts` — repository interfaces (`IMongo*Repository`).
- `Persistence/Repositories` — implementations; `ExternalAPI/PokemonExtAPI` imports data from pokeapi.co.
- Repositories are registered as singletons in `Program.cs` with collection names from `appsettings.json` `MongoSettings`. Adding a collection means adding a setting, an interface, a repository and a registration.

### `GameHub` is one partial class split across many files
The hub is mapped at `/gameHub`. `Hub/GameHub.cs` holds the constructor (injected Mongo repositories) plus the `PokemonChanges` DTO. Each other file under `Hub/` adds methods to the same `partial class GameHub`, grouped by concern:
- `GameManager/` — create / join / start / leave / end game
- `TurnManager/` — `GetNewTurn` advances the player along their path and dispatches to WildFight / TrainerFight / PokeCenter / PokeShop / PvpFight; `FinishFight`
- `FightManager/` — `HandleMove` → `UseMove` → `HandleUseMoveResult`, level-up, special cases
- `TeamManager/`, `MovesManager/`, `TournamentManager/`, `PlayerManager/`

Public hub method names and `SendAsync` event names are the contract with the client's `hub.service.ts` — renaming either side breaks the other.

### Battle logic
Rules live in stateless helpers under `PkmnRaceBattle.API/Helpers/`: `MoveManager/Fights/*` (damage, status, ailments, priority, catch, items, AI move choice, experience), `StatsCalculator/`, `PokemonGeneration/`, `TrainerGeneration/`, `PathManager/` (map path).

A `TurnContext` accumulates messages and HP/stat changes during a turn and is sent to the client as `useMoveResult`; its `CalculateDelay()` drives client animation timing.

Fight flow: `HandleMove` loads player + opponent from Mongo (wild/trainer opponents come from the `WildPokemon` collection, PvP opponents from `Player`), validates via `ValidatorMove`, then resolves both moves. Special pseudo-moves are encoded in the move name string: `item:<name>` and `swap:<index>` (converted by `PokemonMoveSelector.ConvertToActionMove`). In PvP, the first player's choice is persisted as `ChosenMove` and the caller gets `waitingOpponent` until the other player submits.

All game state is persisted in MongoDB after each action — the hub holds no in-memory game state (except the timer). Reload entities from repositories rather than caching them.

### HTTP & config
- `PokemonMongoController` exposes `GET /Pokemon/{id}`, the only REST endpoint the client uses.
- `WeatherForecastController` is repurposed as a manual trigger for seeding MongoDB from pokeapi.co via `PokemonExtAPI` (swap the commented call to choose what to import).
- The CORS allow-list in `Program.cs` must include the client origin (`http://localhost:4200` and the production IP).
