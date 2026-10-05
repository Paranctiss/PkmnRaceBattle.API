using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models;
using PkmnRaceBattle.Domain.Models.EnvironmentMongo;
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
        IMongoCollection<EnvironmentMongo> _environmentCollection;
        public MongoPokemonRepository(IMongoDatabase database, string collectionName, string environmentCollectionName) 
        {
            _pokemonCollection = database.GetCollection<PokemonMongo>(collectionName);
            _environmentCollection = database.GetCollection<EnvironmentMongo>(environmentCollectionName);
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

        private static readonly Dictionary<string, int> RarityWeights = new()
        {
            { "Commun", 50 },
            { "Peu commun", 30 },
            { "Rare", 15 },
            { "Très rare", 4 },
            { "Légendaire", 1 }
        };

        public async Task<PokemonMongo?> GetRandomByEnvironment(string environment)
        {
            var environmentMongo = await _environmentCollection
                .Find(x => x.Name == environment)
                .FirstOrDefaultAsync();

            if (environmentMongo == null || environmentMongo.PossiblePokemons == null)
                return null;

            // Tirage pondéré
            var spawn = PickRandomPokemon(environmentMongo.PossiblePokemons);

            // Récupération du Pokémon correspondant
            var pokemon = await _pokemonCollection
                .Find(p => p.Id == spawn.PokemonId)
                .FirstOrDefaultAsync();

            return pokemon;
        }

        private PokemonSpawn PickRandomPokemon(List<PokemonSpawn> pokemons)
        {
            if (pokemons == null || pokemons.Count == 0)
                throw new ArgumentException("La liste de Pokémon est vide");

            // Calcul du poids total
            int totalWeight = pokemons.Sum(p => RarityWeights[p.Rareté]);

            // Tirage aléatoire
            int randomValue = Random.Shared.Next(0, totalWeight);

            int cumulative = 0;
            foreach (var pokemon in pokemons)
            {
                cumulative += RarityWeights[pokemon.Rareté];

                if (randomValue < cumulative)
                    return pokemon;
            }

            // Sécurité (ne devrait jamais arriver)
            return pokemons.Last();
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
