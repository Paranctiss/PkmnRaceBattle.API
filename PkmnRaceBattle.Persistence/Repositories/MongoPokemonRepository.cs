using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Persistence.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoPokemonRepository : IMongoPokemonRepository
    {
        IMongoCollection<PokemonMongo> _pokemonCollection;
        public MongoPokemonRepository(IMongoDatabase database, string collectionName) 
        {
            _pokemonCollection = database.GetCollection<PokemonMongo>(collectionName);
        }
        public async Task<PokemonMongo> GetPokemonMongoById(int id)
        {
            return await _pokemonCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<PokemonMongo> GetPokemonMongoByOGName(string name)
        {
            return await _pokemonCollection.Find(x => x.Name == name).FirstOrDefaultAsync();
        }

        public async Task<PokemonMongo> GetRandom()
        {
            var totalCount = await _pokemonCollection.CountDocumentsAsync(FilterDefinition<PokemonMongo>.Empty);

            if (totalCount == 0)
                return null;

            var randomIndex = new Random().Next(0, (int)totalCount);

            var randomPokemon = await _pokemonCollection
                .Find(FilterDefinition<PokemonMongo>.Empty)
                .Skip(randomIndex)
                .FirstOrDefaultAsync();

            return randomPokemon;
        }

        public async Task<List<PokemonMongo>> GetAsync() =>
            await _pokemonCollection.Find(_ => true).ToListAsync();

        public async Task CreateAsync(PokemonMongo newPokemon) =>
            await _pokemonCollection.InsertOneAsync(newPokemon);

        public async Task UpdateAsync(int id, PokemonMongo updatedPokemon) =>
            await _pokemonCollection.ReplaceOneAsync(x => x.Id == id, updatedPokemon);

        public async Task RemoveAsync(int id) =>
            await _pokemonCollection.DeleteOneAsync(x => x.Id == id);


    }
}
