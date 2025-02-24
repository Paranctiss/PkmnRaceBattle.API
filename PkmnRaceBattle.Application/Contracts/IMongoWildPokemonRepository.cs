using PkmnRaceBattle.Domain.Models.PlayerMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoWildPokemonRepository
    {
        public Task<string> CreateAsync(PokemonTeam pokemon);

        public Task<PokemonTeam> GetByIdAsync(string pokemonId);
        public Task UpdateAsync(PokemonTeam newWildPokemon);
    }
}
