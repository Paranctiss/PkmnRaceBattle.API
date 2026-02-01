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
        public async Task GetWildFight(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            int levelAvg = player.GetAverageLevel();
            PokemonMongo rndPokemon = await _mongoPokemonRepository.GetRandom();
            //PokemonMongo rndPokemon = await _mongoPokemonRepository.GetPokemonMongoById(122);

            PokemonTeam wildPokemon = GenerateNewPokemon.GenerateNewPokemonTeam(rndPokemon, levelAvg - 3, levelAvg - 2);
            PlayerMongo wildOpponent = new PlayerMongo();
            wildOpponent.GenerateWild();
            wildOpponent.Team = [wildPokemon];
            await _mongoWildPokemonRepository.CreateAsync(wildOpponent);
            await Clients.Caller.SendAsync("responseWildFight", wildOpponent);
        }
    }
}
