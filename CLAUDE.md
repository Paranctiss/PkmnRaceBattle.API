# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Always respond to the user in French.**

ASP.NET Core 8 server of PkmnRaceBattle (SignalR + MongoDB). The client lives in the sibling `../PokemonRaceBattle` repo; the workspace-level `../CLAUDE.md` describes the client ↔ server SignalR contract.

## Commands

```bash
dotnet build PkmnRaceBattle.API.sln
dotnet run --project PkmnRaceBattle.API
dotnet test PkmnRaceBattle.Tests                                   # ~1170 tests, ~25 s, no MongoDB needed
dotnet test PkmnRaceBattle.Tests --artifacts-path %TEMP%/pkmn-tests  # when the API runs in the IDE and locks bin/
dotnet test PkmnRaceBattle.Tests --filter "FullyQualifiedName~Moves.MultiTurnMovesTests"
```

`Program.cs` forces `UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001")`, overriding `launchSettings.json` ports. `publish/` is a committed deployment output, not source. Mongo settings can be overridden with environment variables (`MongoSettings__ConnectionString`, `MongoSettings__DatabaseName`) — the client's E2E tests use this to point the API at the local `PkmnRaceBattle_Test` database.

## Tests

`PkmnRaceBattle.Tests` (xUnit + Moq) — read `PkmnRaceBattle.Tests/README.md` before adding tests. Expected game rules are in `docs/MECANIQUES_COMBAT.md`: a deliberate **mix of Gen 1 and modern mechanics** chosen by the owner (modern type chart, 1/8 burn/poison, 20 % thaw, ×1.5 crits, etc.). Tests follow that mix and only fail on real bugs (1 known gap listed in the tests README: Patience, rule to decide); fix the game code (after asking the owner), not the test. When the code differs from a given generation, decide whether it is a deliberate modern rule or a bug, and ask when unsure.
- `Mechanics/` and `Moves/SingleTurnSpecialMovesTests` call the static helpers directly; `Hub/` and `Moves/MultiTurnMovesTests` drive `GameHub` through `Support/HubHarness` (in-memory repositories with BSON round-trip, recorded `SendAsync` calls) and `Support/Battle`.
- Real game data in `PkmnRaceBattle.Tests/Fixtures/*.json` (exported from MongoDB, + trade evolutions at level 37).
- Tests run sequentially: `UserConnectionManager` uses static non-thread-safe dictionaries (also a production concurrency risk).
- `Contract/` tests read the sibling client repo (folder `PokemonRaceBattle` locally, `PkmnRaceBattle` in a fresh clone) to check SignalR names/arguments and shop prices.

### Testability rules for game code
- Randomness: always `GameRandom.Next(RandomPurpose.X, min, max)` / `GameRandom.NextDouble(RandomPurpose.X)` (`Helpers/Randomness/GameRandom.cs`), never `new Random()`. Tests drive each purpose separately with `TestRandom`.
- Animation pauses: `await GameDelay.Wait(ms)`, never `Task.Delay` (tests set it to zero).
- Metronome: `MetronomeMoveProvider.GetMoveAsync()` (pokeapi in production, overridable in tests).

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

A `TurnContext` accumulates messages and HP/stat changes during a turn and is sent to the client as `useMoveResult`; its `CalculateDelay()` drives client animation timing. Every HP change applied to a Pokémon must also be added to `TurnContext.Player/Opponent.Hp` (positive = damage, negative = heal), otherwise the client HP bar is wrong until `turnFinished`.

Detailed turn flow, per-Pokémon battle state fields, what is reset on switch / end of fight, formulas and the expected behaviour of every special move: `docs/MECANIQUES_COMBAT.md`.

Fight flow: `HandleMove` loads player + opponent from Mongo (wild/trainer opponents come from the `WildPokemon` collection, PvP opponents from `Player`), validates via `ValidatorMove`, then resolves both moves. Special pseudo-moves are encoded in the move name string: `item:<name>:<pocket>` (target team slot in the `index` argument) and `swap:` (sent by `ReplacePokemon` after it reorders the team), converted by `PokemonMoveSelector.ConvertToActionMove`. In PvP, the first player's choice is persisted as `ChosenMove` and the caller gets `waitingOpponent` until the other player submits.

All game state is persisted in MongoDB after each action — the hub holds no in-memory game state (except the timer). Reload entities from repositories rather than caching them.

### HTTP & config
- `PokemonMongoController` exposes `GET /Pokemon/{id}`, the only REST endpoint the client uses.
- `WeatherForecastController` is repurposed as a manual trigger for seeding MongoDB from pokeapi.co via `PokemonExtAPI` (swap the commented call to choose what to import).
- The CORS allow-list in `Program.cs` must include the client origin (`http://localhost:4200` and the production IP).
