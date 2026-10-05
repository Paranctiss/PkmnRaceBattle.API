using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
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
        private PokemonTeam[] SufferSpecialCases(PokemonTeam user, PokemonTeam target, TurnContext turnContext, bool userIsPlayer = true)
        {
            foreach (string specialCase in target.SpecialCases)
            {
                switch (specialCase)
                {
                    case "Vampigraine":
                        turnContext.AddMessage(user.NameFr + " draine l'énergie de " + target.NameFr);

                        int damageDealt = 0;
                        if (target.CurrHp > 16)
                        {
                            damageDealt = target.BaseHp / 8;
                        }
                        else
                        {
                            damageDealt = 1;
                        }
                        int targetHp = target.CurrHp;
                        int userHp = user.CurrHp;
                        target.CurrHp -= damageDealt;
                        if (target.CurrHp < 0) target.CurrHp = 0;
                        user.CurrHp += damageDealt;
                        if (user.CurrHp > user.BaseHp) user.CurrHp = user.BaseHp;
                        FightPerformMove.AddHpChange(turnContext, !userIsPlayer, targetHp - target.CurrHp);
                        FightPerformMove.AddHpChange(turnContext, userIsPlayer, userHp - user.CurrHp);
                        break;
                }
            }
            return [user, target];
        }
    }
}
