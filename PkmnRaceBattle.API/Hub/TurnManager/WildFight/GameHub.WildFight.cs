using PkmnRaceBattle.API.Helpers.Randomness;
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


            // Niveau entre (moyenne de l'équipe - 3) et (moyenne - 2), au moins 1
            int wildLevel = Math.Max(1, GameRandom.Next(RandomPurpose.Generation, levelAvg - 3, levelAvg - 1));
            PokemonMongo rndPokemon = await _mongoPokemonRepository.GetRandomByEnvironment(player.CurrentPath.EnvironmentName, wildLevel);
            //PokemonMongo rndPokemon = await _mongoPokemonRepository.GetPokemonMongoById(122);

            //PokemonTeam wildPokemon = GenerateNewPokemon.GenerateNewPokemonTeam(rndPokemon, levelAvg - 3, levelAvg - 2);
            if(rndPokemon is null)
            {

            }
            PokemonTeam wildPokemon = PokemonBaseToTeam.ConvertBaseToTeam(rndPokemon, wildLevel);
            PlayerMongo wildOpponent = new PlayerMongo();
            wildOpponent.GenerateWild();
            wildOpponent.Team = [wildPokemon];
            await _mongoWildPokemonRepository.CreateAsync(wildOpponent);
            await Clients.Caller.SendAsync("responseWildFight", wildOpponent, player);
        }
    }
}
