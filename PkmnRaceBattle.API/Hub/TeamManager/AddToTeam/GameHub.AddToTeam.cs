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
        public async Task AddPokemonToTeam(string userId, string wildOpponentId, int index)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo opponent = await _mongoWildPokemonRepository.GetByIdAsync(wildOpponentId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetPlayerPokemonById(opponent._id, opponent.Team[0].Id);
            if (index == -1)
            {
                PokemonTeam[] newTeam = new PokemonTeam[player.Team.Length + 1];
                Array.Copy(player.Team, newTeam, player.Team.Length);
                newTeam[newTeam.Length - 1] = wildPokemon;
                player.Team = newTeam;
            }
            else
            {
                player.Team[index] = wildPokemon;
            }

            await _mongoPlayerRepository.UpdateAsync(player);
            await FinishFight(player, opponent);

        }
    }
}
