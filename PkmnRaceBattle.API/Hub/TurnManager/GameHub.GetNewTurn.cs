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
        public async Task GetNewTurn(string userId)
        {
            string[] turnTypes = ["WildFight", "TrainerFight", "PokeCenter", "PokeShop"];
            Random rnd = new Random();
            string turnType = turnTypes[turnTypes.Length - 1];

            int random = rnd.Next(1, 101);

            if (random > 30)
            {
                turnType = "WildFight";
            }
            if (random > 20 && random <= 30)
            {
                turnType = "PokeShop";
            }
            if (random > 10 && random <= 20)
            {
                turnType = "PokeCenter";
            }
            if (random <= 10)
            {
                turnType = "TrainerFight";
            }


            switch (turnType)
            {

                case "WildFight":
                    await GetWildFight(userId);
                    break;
                case "TrainerFight":
                    await GetTrainerFight(userId);
                    break;
                case "PokeCenter":
                    await GetPokeCenter(userId);
                    break;
                case "PokeShop":
                    await GetPokeShop(userId);
                    break;
            }
        }
    }
}
