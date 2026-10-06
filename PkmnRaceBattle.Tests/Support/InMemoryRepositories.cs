using MongoDB.Bson;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.BracketMongo;
using PkmnRaceBattle.Domain.Models.EnvironmentMongo;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;

namespace PkmnRaceBattle.Tests.Support
{
    // Dépôts en mémoire qui imitent MongoDB : chaque lecture/écriture passe par une sérialisation BSON,
    // donc un objet modifié en mémoire n'est « en base » que s'il a été explicitement sauvegardé.

    public class InMemoryPlayerRepository : IMongoPlayerRepository
    {
        private readonly Dictionary<string, PlayerMongo> _store = new();

        public IEnumerable<PlayerMongo> All => _store.Values.Select(GameData.Clone);

        public PlayerMongo Get(string id) => GameData.Clone(_store[id]);

        public Task<string> CreateAsync(PlayerMongo player)
        {
            if (string.IsNullOrEmpty(player._id)) player._id = ObjectId.GenerateNewId().ToString();
            _store[player._id] = GameData.Clone(player);
            return Task.FromResult(player._id);
        }

        public Task<PlayerMongo> GetByPlayerIdAsync(string playerId) =>
            Task.FromResult(_store.TryGetValue(playerId, out PlayerMongo? p) ? GameData.Clone(p) : null!);

        public Task<List<PlayerMongo>> GetByRoomId(string roomId) =>
            Task.FromResult(_store.Values.Where(p => p.RoomId == roomId).Select(GameData.Clone).ToList());

        public Task<PokemonTeam> GetPlayerPokemonById(string playerId, string pokemonId)
        {
            if (!_store.TryGetValue(playerId, out PlayerMongo? player)) throw new Exception("Joueur non trouvé.");
            PokemonTeam? pokemon = player.Team.FirstOrDefault(t => t.Id == pokemonId);
            if (pokemon == null) throw new Exception("Pokémon non trouvé dans l'équipe du joueur.");
            return Task.FromResult(GameData.Clone(pokemon));
        }

        public Task UpdateAsync(PlayerMongo player)
        {
            if (_store.ContainsKey(player._id)) _store[player._id] = GameData.Clone(player);
            return Task.CompletedTask;
        }

        public async Task<PlayerMongo> UpdatePokemonTeamAsync(PokemonTeam pokemon, PlayerMongo player)
        {
            int index = Array.FindIndex(player.Team, x => x.Id == pokemon.Id);
            if (index != -1) player.Team[index] = pokemon;
            await UpdateAsync(player);
            return player;
        }

        public Task<PokemonTeamMove> GetPokemonTeamMoveByName(string playerId, string pokemonId, string moveName)
        {
            PokemonTeam pokemon = _store[playerId].Team.First(t => t.Id == pokemonId);
            PokemonTeamMove? move = pokemon.Moves.FirstOrDefault(m => m.NameFr == moveName);
            return Task.FromResult(move == null ? null! : GameData.Clone(move));
        }
    }

    public class InMemoryWildPokemonRepository : IMongoWildPokemonRepository
    {
        private readonly Dictionary<string, PlayerMongo> _store = new();

        public IEnumerable<PlayerMongo> All => _store.Values.Select(GameData.Clone);

        public PlayerMongo Get(string id) => GameData.Clone(_store[id]);

        public Task<string> CreateAsync(PlayerMongo wildOpponent)
        {
            if (string.IsNullOrEmpty(wildOpponent._id)) wildOpponent._id = ObjectId.GenerateNewId().ToString();
            _store[wildOpponent._id] = GameData.Clone(wildOpponent);
            return Task.FromResult(wildOpponent._id);
        }

        public Task<PlayerMongo> GetByIdAsync(string wildOpponentId) =>
            Task.FromResult(_store.TryGetValue(wildOpponentId, out PlayerMongo? p) ? GameData.Clone(p) : null!);

        public Task UpdateAsync(PlayerMongo opponent)
        {
            if (_store.ContainsKey(opponent._id)) _store[opponent._id] = GameData.Clone(opponent);
            return Task.CompletedTask;
        }

        public async Task<PlayerMongo> UpdatePokemonTeamAsync(PokemonTeam pokemon, PlayerMongo player)
        {
            int index = Array.FindIndex(player.Team, x => x.Id == pokemon.Id);
            if (index != -1) player.Team[index] = pokemon;
            await UpdateAsync(player);
            return player;
        }

        public Task<PokemonTeam> GetPlayerPokemonById(string playerId, string pokemonId)
        {
            if (!_store.TryGetValue(playerId, out PlayerMongo? player)) throw new Exception("Joueur non trouvé.");
            PokemonTeam? pokemon = player.Team.FirstOrDefault(t => t.Id == pokemonId);
            if (pokemon == null) throw new Exception("Pokémon non trouvé dans l'équipe du joueur.");
            return Task.FromResult(GameData.Clone(pokemon));
        }
    }

    public class InMemoryPokemonRepository : IMongoPokemonRepository
    {
        private readonly List<PokemonMongo> _pokemons = GameData.Pokemons.Select(GameData.Clone).ToList();
        private readonly List<EnvironmentMongo> _environments = GameData.Environments.Select(GameData.Clone).ToList();

        // Pokémon imposés aux prochains tirages (GetRandom / GetRandomByEnvironment), par numéro de Pokédex
        public Queue<int> ForcedRandomIds { get; } = new();

        public List<string> RequestedEnvironments { get; } = new();

        public Task<PokemonMongo> GetPokemonMongoById(int id) =>
            Task.FromResult(_pokemons.Where(p => p.Id == id).Select(GameData.Clone).FirstOrDefault()!);

        public Task<PokemonMongo> GetPokemonMongoByOGName(string name) =>
            Task.FromResult(_pokemons.Where(p => p.Name == name).Select(GameData.Clone).FirstOrDefault()!);

        public Task<List<PokemonMongo>> GetAsync() => Task.FromResult(_pokemons.Select(GameData.Clone).ToList());

        public Task<PokemonMongo> GetRandom()
        {
            if (ForcedRandomIds.Count > 0) return GetPokemonMongoById(ForcedRandomIds.Dequeue());
            return Task.FromResult(GameData.Clone(_pokemons[GameRandom.Next(RandomPurpose.Generation, _pokemons.Count)]));
        }

        public Task<PokemonMongo> GetRandomByEnvironment(string environment, int level)
        {
            RequestedEnvironments.Add(environment);
            if (ForcedRandomIds.Count > 0) return GetPokemonMongoById(ForcedRandomIds.Dequeue());
            EnvironmentMongo? env = _environments.FirstOrDefault(e => e.Name == environment);
            if (env == null || env.PossiblePokemons == null) return Task.FromResult<PokemonMongo>(null!);
            // Même tirage que MongoPokemonRepository.GetRandomByEnvironment : à faire évoluer avec lui
            var candidates = env.PossiblePokemons.Where(p => p.MinimumLevel <= level).ToList();
            if (candidates.Count == 0)
            {
                int lowest = env.PossiblePokemons.Min(p => p.MinimumLevel);
                candidates = env.PossiblePokemons.Where(p => p.MinimumLevel == lowest).ToList();
            }
            int total = candidates.Sum(p => RarityWeights[p.Rareté]);
            int roll = GameRandom.Next(RandomPurpose.Generation, 0, total);
            int cumulative = 0;
            foreach (PokemonSpawn spawn in candidates)
            {
                cumulative += RarityWeights[spawn.Rareté];
                if (roll < cumulative) return GetPokemonMongoById(spawn.PokemonId);
            }
            return GetPokemonMongoById(candidates.Last().PokemonId);
        }

        private static readonly Dictionary<string, int> RarityWeights = new()
        {
            { "Commun", 50 }, { "Peu commun", 30 }, { "Rare", 15 }, { "Très rare", 4 }, { "Légendaire", 1 }
        };

        public Task CreateAsync(PokemonMongo newPokemon)
        {
            _pokemons.Add(GameData.Clone(newPokemon));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(int id, PokemonMongo updatedPokemon)
        {
            int index = _pokemons.FindIndex(p => p.Id == id);
            if (index != -1) _pokemons[index] = GameData.Clone(updatedPokemon);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(int id)
        {
            _pokemons.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }
    }

    public class InMemoryMoveRepository : IMongoMoveRepository
    {
        private readonly List<MoveMongo> _moves = GameData.Moves.Select(GameData.Clone).ToList();

        public Task<MoveMongo> GetMoveMongoById(int id) => Task.FromResult(_moves.Where(m => m.Id == id).Select(GameData.Clone).FirstOrDefault()!);

        public Task<MoveMongo> GetMoveMongoByName(string name) => Task.FromResult(_moves.Where(m => m.NameFr == name).Select(GameData.Clone).FirstOrDefault()!);

        public Task<List<MoveMongo>> GetAsync() => Task.FromResult(_moves.Select(GameData.Clone).ToList());

        public Task CreateAsync(MoveMongo moveMongo)
        {
            _moves.Add(GameData.Clone(moveMongo));
            return Task.CompletedTask;
        }
    }

    public class InMemoryRoomRepository : IMongoRoomRepository
    {
        private readonly Dictionary<string, RoomMongo> _store = new();

        public IEnumerable<RoomMongo> All => _store.Values.Select(GameData.Clone);

        public Task<RoomMongo> GetByRoomIdAsync(string gameRoom) =>
            Task.FromResult(_store.TryGetValue(gameRoom, out RoomMongo? r) ? GameData.Clone(r) : null!);

        public Task CreateAsync(RoomMongo room)
        {
            if (room._id == ObjectId.Empty) room._id = ObjectId.GenerateNewId();
            _store[room.roomId] = GameData.Clone(room);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(string roomCode, RoomMongo updatedRoom)
        {
            if (_store.ContainsKey(roomCode)) _store[roomCode] = GameData.Clone(updatedRoom);
            return Task.CompletedTask;
        }
    }

    public class InMemoryBracketRepository : IMongoBracketRepository
    {
        private readonly List<BracketMongo> _store = new();

        public IEnumerable<BracketMongo> All => _store.Select(GameData.Clone);

        public Task<BracketMongo> GetByPlayerId(string id) =>
            Task.FromResult(_store.Where(b => b.Players.Any(p => p._id == id)).Select(GameData.Clone).FirstOrDefault()!);

        public Task<BracketMongo> GetByRoomId(string gameCode) =>
            Task.FromResult(_store.Where(b => b.GameCode == gameCode).Select(GameData.Clone).FirstOrDefault()!);

        public Task<List<BracketMongo>> GetAsync() => Task.FromResult(_store.Select(GameData.Clone).ToList());

        public Task CreateAsync(BracketMongo bracketMongo)
        {
            if (string.IsNullOrEmpty(bracketMongo._id)) bracketMongo._id = ObjectId.GenerateNewId().ToString();
            _store.Add(GameData.Clone(bracketMongo));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BracketMongo bracket)
        {
            int index = _store.FindIndex(b => b._id == bracket._id);
            if (index != -1) _store[index] = GameData.Clone(bracket);
            return Task.CompletedTask;
        }
    }
}
