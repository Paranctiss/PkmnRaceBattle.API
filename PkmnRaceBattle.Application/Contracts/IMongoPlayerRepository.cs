using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoPlayerRepository
    {
        public Task<PlayerMongo> GetByPlayerIdAsync(string playerId);

        public Task<List<PlayerMongo>> GetByRoomId(string roomId);
        public Task<string> CreateAsync(PlayerMongo player);

        public Task<PokemonTeam> GetPlayerPokemonById(string playerId, string pokemonId);

        public Task UpdateAsync(PlayerMongo player);

        public Task<PlayerMongo> UpdatePokemonTeamAsync(PokemonTeam pokemon, PlayerMongo player);

        public Task<PokemonTeamMove> GetPokemonTeamMoveByName(string playerId, string pokemonId, string moveName);
    }
}
