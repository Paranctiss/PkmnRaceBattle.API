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
        private async Task<bool> ManageSpecialCasesAfterMove(PlayerMongo opponent, PokemonTeam wildPokemon, PokemonTeam playerPokemon, PlayerMongo player, TurnContext turnContext)
        {
            foreach (string specialCase in playerPokemon.SpecialCases)
            {
                switch (specialCase)
                {
                    case "Ejected":
                        if (player.Team.Where(x => x.CurrHp > 0).ToList().Count > 1) return true;
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }
                        break;
                    case "Teleport":
                        if (!opponent.IsTrainer)
                        {
                            turnContext.AddMessage(wildPokemon.NameFr + " se téléporte");
                        }
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }

                        return true;
                        break;
                }
            }

            foreach (string specialCase in wildPokemon.SpecialCases)
            {
                switch (specialCase)
                {
                    case "Ejected":
                        if (opponent.Team.Where(x => x.CurrHp > 0).ToList().Count > 1)
                        {
                            return true;
                        }
                        else
                        {
                            if (!opponent.IsTrainer)
                            {
                                turnContext.AddMessage(playerPokemon.NameFr + " met fin au combat");
                                return true;
                            }
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }

                        break;
                    case "Teleport":
                        if (!opponent.IsTrainer)
                        {
                            turnContext.AddMessage(playerPokemon.NameFr + " se téléporte");
                        }
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }
                        return true;
                        break;
                }
            }

            return false;

        }
    }
}
