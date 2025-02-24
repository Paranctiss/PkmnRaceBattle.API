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
        private readonly IMongoCollection<PokemonTeam> _pokemonCollection;

        public MongoWildPokemonRepository(IMongoDatabase database, string collectionName)
        {
            _pokemonCollection = database.GetCollection<PokemonTeam>(collectionName);
        }

        public async Task<string> CreateAsync(PokemonTeam pokemon)
        {
            await _pokemonCollection.InsertOneAsync(pokemon);
            return pokemon.Id;
        }

        public async Task<PokemonTeam> GetByIdAsync(string pokemonId)
        {
            return await _pokemonCollection.Find(p => p.Id == pokemonId).FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(PokemonTeam updatedPokemon) =>
            await _pokemonCollection.ReplaceOneAsync(x => x.Id == updatedPokemon.Id, updatedPokemon);


}
}
