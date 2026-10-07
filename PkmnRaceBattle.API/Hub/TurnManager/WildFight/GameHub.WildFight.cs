using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.API.Helpers.PathManager;
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
            XpSettings xpSettings = await GetXpSettings(player.RoomId);

            // Palier de la zone : le niveau monte au fil des combats sauvages de la map
            ZoneLevelRange range = ZoneLevels.GetRange(ZoneLevels.GetZone(player), xpSettings);
            int wildLevel = ZoneLevels.WildLevel(range, player.MapFightCount);
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
