using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.PokemonStates;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task GetPokeCenter(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            await Clients.Caller.SendAsync("responsePokeCenter", player);
        }

        public async Task UsePokeCenter(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            for (int i = 0; i < player.Team.Length; i++)
            {
                player.Team[i].CurrHp = player.Team[i].BaseHp;
                player.Team[i].IsBurning = false;
                player.Team[i].IsParalyzed = false;
                player.Team[i].IsPoisoned = 0;
                player.Team[i].IsSleeping = 0;
                player.Team[i].IsFrozen = false;
                player.Team[i] = PokemonStatesHelper.ResetForSwap(player.Team[i]);
                foreach (PokemonTeamMove move in player.Team[i].Moves)
                {
                    MoveMongo reference = await _mongoMoveRepository.GetMoveMongoByName(move.NameFr);
                    if (reference != null) move.Pp = reference.Pp;
                }
            }

            await _mongoPlayerRepository.UpdateAsync(player);

            await Clients.Caller.SendAsync("healedPokeCenter", player);
        }
    }
}
