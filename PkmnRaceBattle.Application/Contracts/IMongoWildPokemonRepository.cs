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
        public Task<string> CreateAsync(PlayerMongo wildOpponent);

        public Task<PlayerMongo> GetByIdAsync(string wildOpponentId);
        public Task UpdateAsync(PlayerMongo newWildOpponent);
        public Task<PlayerMongo> UpdatePokemonTeamAsync(PokemonTeam pokemon, PlayerMongo player);

        public Task<PokemonTeam> GetPlayerPokemonById(string playerId, string pokemonId);
    }
}
