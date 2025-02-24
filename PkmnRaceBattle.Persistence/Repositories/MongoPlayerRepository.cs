using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoPlayerRepository : IMongoPlayerRepository
    {
        private readonly IMongoCollection<PlayerMongo> _playerCollection;

        public MongoPlayerRepository(IMongoDatabase database, string collectionName)
        {
            _playerCollection = database.GetCollection<PlayerMongo>(collectionName);
        }

        public async Task<string> CreateAsync(PlayerMongo player)
        {
            await _playerCollection.InsertOneAsync(player);
            return player._id;
        }

        public async Task<PlayerMongo> GetByPlayerIdAsync(string playerId)
        {
            return await _playerCollection.Find(x => x._id == playerId).FirstOrDefaultAsync();
        }

        public async Task<List<PlayerMongo>> GetByRoomId(string roomId)
        {
            return await _playerCollection.Find(x => x.RoomId == roomId).ToListAsync();
        }

        public async Task<PokemonTeam> GetPlayerPokemonById(string playerId, string pokemonId)
        {
            PlayerMongo player = await _playerCollection
             .Find(p => p._id == playerId)
             .FirstOrDefaultAsync();

            if (player == null)
            {
                throw new Exception("Joueur non trouvé.");
            }

            // Récupérer le Pokémon dans la liste Team du joueur
            PokemonTeam pokemonTeam = player.Team
                .FirstOrDefault(t => t.Id == pokemonId);

            if (pokemonTeam == null)
            {
                throw new Exception("Pokémon non trouvé dans l'équipe du joueur.");
            }
            else
            {
                return pokemonTeam;
            }
        }

        public async Task UpdateAsync(PlayerMongo player) =>
               await _playerCollection.ReplaceOneAsync(x => x._id == player._id, player);

        public async Task<PlayerMongo> UpdatePokemonTeamAsync(PokemonTeam newPokemon, PlayerMongo player)
        {
            int index = Array.FindIndex(player.Team, x => x.Id == newPokemon.Id);
            if (index != -1)
            {
                player.Team[index] = newPokemon;
            }

            await UpdateAsync(player);
            return player;
        }

        public async Task<PokemonTeamMove> GetPokemonTeamMoveByName(string playerId, string pokemonId, string moveName)
        {
            PlayerMongo player = await GetByPlayerIdAsync(playerId);
            PokemonTeam pokemonTeam = player.Team.FirstOrDefault(t => t.Id == pokemonId);
            return pokemonTeam.Moves.FirstOrDefault(m => m.NameFr == moveName);
        }
    }
}
