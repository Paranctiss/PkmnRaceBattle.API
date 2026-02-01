using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task DeleteMove(string playerId, int moveId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(playerId);
            PokemonMongo pokemon = await _mongoPokemonRepository.GetPokemonMongoById(152);
            List<PokemonTeamMove> movesPlayer = player.Team[0].Moves.ToList();
            movesPlayer.RemoveAll(move => move.Id == moveId);
            player.Team[0].Moves = movesPlayer.ToArray();


            List<MoveMongo> movesPokemon = pokemon.Moves.ToList();
            movesPokemon.RemoveAll(move => move.Id == moveId);
            pokemon.Moves = movesPokemon.ToArray();


            await _mongoPlayerRepository.UpdateAsync(player);
            await _mongoPokemonRepository.UpdateAsync(152, pokemon);
            await Clients.Caller.SendAsync("moveLearned", player);
        }
    }
}
