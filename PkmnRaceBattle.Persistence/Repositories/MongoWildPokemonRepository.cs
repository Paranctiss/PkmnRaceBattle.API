using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoWildPokemonRepository : IMongoWildPokemonRepository
    {
        private readonly IMongoCollection<PlayerMongo> _wildPokemonCollection;

        public MongoWildPokemonRepository(IMongoDatabase database, string collectionName)
        {
            _wildPokemonCollection = database.GetCollection<PlayerMongo>(collectionName);
        }

        public async Task<string> CreateAsync(PlayerMongo wildOpponent)
        {
            await _wildPokemonCollection.InsertOneAsync(wildOpponent);
            return wildOpponent._id;
        }

        public async Task<PlayerMongo> GetByIdAsync(string wildOpponentId)
        {
            return await _wildPokemonCollection.Find(p => p._id == wildOpponentId).FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(PlayerMongo updatedWildOpponent) =>
            await _wildPokemonCollection.ReplaceOneAsync(x => x._id == updatedWildOpponent._id, updatedWildOpponent);

        public async Task<PokemonTeam> GetPlayerPokemonById(string playerId, string pokemonId)
        {
            PlayerMongo player = await _wildPokemonCollection
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
    }
}
