using MongoDB.Driver;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PkmnRaceBattle.Persistence.Models;
using PkmnRaceBattle.Domain.Models.PokemonMongo;

namespace PkmnRaceBattle.Persistence.Services
{
    public class PokemonDbService
    {
        private readonly IMongoCollection<PokemonMongo> _pokemonCollection;

        public PokemonDbService(
            IOptions<MongoSettings> DatabaseSettings)
        {
            var mongoClient = new MongoClient(
                DatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                DatabaseSettings.Value.DatabaseName);

            _pokemonCollection = mongoDatabase.GetCollection<PokemonMongo>(
                DatabaseSettings.Value.PokemonCollectionName);
        }

        public async Task<List<PokemonMongo>> GetAsync() =>
            await _pokemonCollection.Find(_ => true).ToListAsync();

        public async Task<PokemonMongo> GetAsync(int id) =>
            await _pokemonCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task CreateAsync(PokemonMongo newPokemon) =>
            await _pokemonCollection.InsertOneAsync(newPokemon);

        public async Task UpdateAsync(int id, PokemonMongo updatedPokemon) =>
            await _pokemonCollection.ReplaceOneAsync(x => x.Id == id, updatedPokemon);

        public async Task RemoveAsync(int id) =>
            await _pokemonCollection.DeleteOneAsync(x => x.Id == id);

    }
}
