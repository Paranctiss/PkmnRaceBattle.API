using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task LearnMove(int oldMoveId, int newMoveId, string pokemonId, string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            PokemonTeam pokemon = player.Team.FirstOrDefault(x => x.Id == pokemonId);

            PokemonMongo pokemonBase = await _mongoPokemonRepository.GetPokemonMongoById(pokemon.IdDex);

            PokemonTeamMove moveToLearn = PokemonMoveSelector.ConvertToTeamMove(pokemonBase.Moves.FirstOrDefault(x => x.Id == newMoveId));

            int oldMoveIndex = Array.FindIndex(pokemon.Moves, x => x.Id == oldMoveId);
            pokemon.Moves[oldMoveIndex] = moveToLearn;

            PlayerMongo updatedPlayer = await _mongoPlayerRepository.UpdatePokemonTeamAsync(pokemon, player);
            await Clients.Caller.SendAsync("moveLearned", updatedPlayer);
        }
    }
}
