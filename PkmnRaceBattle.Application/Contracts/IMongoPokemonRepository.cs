using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoPokemonRepository
    {
        public Task<PokemonMongo> GetPokemonMongoById(int id);
        public Task<PokemonMongo> GetPokemonMongoByOGName(string name);
        public Task<List<PokemonMongo>> GetAsync();

        public Task<PokemonMongo> GetRandom();

        public Task CreateAsync(PokemonMongo newPokemon);

        public Task UpdateAsync(int id, PokemonMongo updatedPokemon);

        public Task RemoveAsync(int id);
    }
}
