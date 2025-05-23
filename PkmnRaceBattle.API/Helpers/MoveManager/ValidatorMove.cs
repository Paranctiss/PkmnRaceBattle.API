﻿using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager
{
    public static class ValidatorMove
    {
        public static bool IsEverythingOk(PlayerMongo player1, PokemonTeam pokemon1, PokemonTeamMove pokemon1Move, PlayerMongo player2, PokemonTeam pokemon2, TurnContext turnContext)
        {
            if(pokemon1Move.Type != "item" && pokemon1.CurrHp <= 0)
            {
                turnContext.AddMessage("Un Pokémon K.O ne peut pas attaquer");
                return false;
            }

            if(pokemon1Move.Pp <= 0)
            {
                turnContext.AddMessage("La capacité utilisée n'a plus de PP");
                return false;
            }

            if (pokemon1Move.NameFr.Contains("ball") && player2.IsTrainer)
            {
                turnContext.AddMessage("Voler n'est pas bon");
                return false;
            }

            if(pokemon1Move.NameFr.Contains("ball") && pokemon2.Substitute != null)
            {
                turnContext.AddMessage("Cible invalide");
                return false;
            }

            if(pokemon1Move.Type == "item")
            {
                if(pokemon1Move.NameFr == "Guérison" && (!FightUseItem.IsItemUseful(pokemon1, pokemon1Move.NameFr, turnContext) || pokemon1.CurrHp < pokemon1.BaseHp))
                {
                    return true;
                }

                if(pokemon1Move.DamageType == "ailment" && !FightUseItem.IsItemUseful(pokemon1, pokemon1Move.NameFr, turnContext)){
                    turnContext.AddMessage("Cela n'aura aucun effet");
                    return false;
                }

                if (pokemon1Move.DamageType == "potion")
                {
                    if (pokemon1Move.NameFr.Contains("Rappel") && pokemon1.CurrHp > 0)
                    {
                        turnContext.AddMessage("Seul un Pokémon K.O peut être rappelé");
                        return false;
                    }

                    if (!pokemon1Move.NameFr.Contains("Rappel") && pokemon1.CurrHp == 0)
                    {
                        turnContext.AddMessage("Un Pokémon K.O ne peut être soigné avec un potion");
                        return false;
                    }

                    if (pokemon1.CurrHp >= pokemon1.BaseHp) 
                    {
                        turnContext.AddMessage(pokemon1.NameFr + " a déjà ses PV au max");
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
