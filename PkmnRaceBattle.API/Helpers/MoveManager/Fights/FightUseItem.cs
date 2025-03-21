using PkmnRaceBattle.API.Helpers.StatsCalculator;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightUseItem
    {
        public static PokemonTeam[] UseItem(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove usedMove, TurnContext turnContext, bool playerAttacking)
        {

            if(usedMove.DamageType=="potion") attacker = UsePotion(attacker, usedMove.NameFr, turnContext, playerAttacking);
            if(usedMove.DamageType=="ailment") attacker = UseAilment(attacker, usedMove.NameFr, turnContext, playerAttacking);
            if (usedMove.DamageType == "special") attacker = UseSpecial(attacker, usedMove.NameFr, turnContext, playerAttacking);

            return [attacker, defenser];
        }


        public static PokemonTeam UsePotion(PokemonTeam pokemonTarget, string potionType, TurnContext turnContext, bool playerAttacking)
        {
            int givenHp = 0;
            switch (potionType)
            {
                case "Potion":
                    givenHp = 20;
                    break;
                case "Super Potion":
                    givenHp = 50;
                    break;
                case "Hyper Potion":
                    givenHp = 120;
                    break;
                case "Potion Max":
                    givenHp = pokemonTarget.BaseHp;
                    break;
                case "Guérison":
                    givenHp = pokemonTarget.BaseHp;
                    pokemonTarget = ResetAilments(pokemonTarget);
                    break;
                case "Rappel":
                    givenHp = pokemonTarget.BaseHp / 2;
                    break;
                case "Rappel Max":
                    givenHp = pokemonTarget.BaseHp;
                    break;
            }
            int oldHp = pokemonTarget.CurrHp;
            pokemonTarget.CurrHp += givenHp;
            pokemonTarget.CurrHp = Math.Clamp(pokemonTarget.CurrHp, 0, pokemonTarget.BaseHp);

            if (playerAttacking) turnContext.Player.Hp.Add(oldHp-pokemonTarget.CurrHp);
            if (!playerAttacking) turnContext.Opponent.Hp.Add(oldHp - pokemonTarget.CurrHp);

            return pokemonTarget;

        }

        public static PokemonTeam UseAilment(PokemonTeam pokemonTarget, string potionType, TurnContext turnContext, bool playerAttacking)
        {
            switch (potionType) 
            {
                case "Anti-Brûle":
                    pokemonTarget.IsBurning = false;
                    turnContext.AddPrioMessage(pokemonTarget.NameFr + " n'est plus brûlé");
                    break;
                case "Antidote":
                    pokemonTarget.IsPoisoned = 0;
                    turnContext.AddPrioMessage(pokemonTarget.NameFr + " n'est plus empoisonné");
                    break;
                case "Antigel":
                    pokemonTarget.IsFrozen = false;
                    turnContext.AddPrioMessage(pokemonTarget.NameFr + " n'est plus gelé");
                    break;
                case "Anti-Para":
                    pokemonTarget.IsParalyzed = false;
                    turnContext.AddPrioMessage(pokemonTarget.NameFr + " n'est plus paralysé");
                    break;
                case "Réveil":
                    pokemonTarget.IsSleeping = 0;
                    turnContext.AddPrioMessage(pokemonTarget.NameFr + " s'est réveillé");
                    break;
                case "Total Soin":
                    pokemonTarget = ResetAilments(pokemonTarget);
                    turnContext.AddPrioMessage(pokemonTarget.NameFr + " n'a plus de problème de statut");
                    break;
            }

            return pokemonTarget;
        }

        public static PokemonTeam UseSpecial(PokemonTeam pokemonTarget, string itemType, TurnContext turnContext, bool playerAttacking)
        {
            switch (itemType) 
            {
                case "Super Bonbon":
                    pokemonTarget.CurrXP += PokemonExperienceCalculator.ExpToNextLevel(pokemonTarget);
                    break;
            }

            return pokemonTarget;
        }

        public static string GetStoneLabel(string stoneName)
        {
            switch (stoneName)
            {
                case "Pierre Feu":
                    return "fire-stone";
                case "Pierre Eau":
                    return "water-stone";
                case "Pierre Foudre":
                    return "thunder-stone";
                case "Pierre Plante":
                    return "leaf-stone";
                case "Pierre Lune":
                    return "moon-stone";

            }

            return "";
        }

        private static PokemonTeam ResetAilments(PokemonTeam pokemonTarget)
        {
            pokemonTarget.IsBurning = false;
            pokemonTarget.IsFrozen = false;
            pokemonTarget.IsParalyzed = false;
            pokemonTarget.IsPoisoned = 0;
            pokemonTarget.IsSleeping = 0;

            return pokemonTarget;
        }

        public static bool IsItemUseful(PokemonTeam pokemonTarget, string itemType, TurnContext turnContext)
        {
            switch (itemType) 
            {
                case "Anti-Brûle":
                    if(pokemonTarget.IsBurning == false) return false;
                    break;
                case "Antidote":
                    if (pokemonTarget.IsPoisoned == 0) return false;
                    break;
                case "Antigel":
                    if(pokemonTarget.IsFrozen == false) return false;
                    break;
                case "Anti-Para":
                    if (pokemonTarget.IsParalyzed == false) return false;
                    break;
                case "Réveil":
                    if(pokemonTarget.IsSleeping == 0) return false;
                    break;
                case "Total Soin":
                    if (!IsTotalSoinUseful(pokemonTarget)) return false; ;
                    break;
            }
            return true;
        }

        private static bool IsTotalSoinUseful(PokemonTeam pokemonTarget) { 
            if (
                pokemonTarget.IsBurning == false &&
                pokemonTarget.IsFrozen == false &&
                pokemonTarget.IsParalyzed == false &&
                pokemonTarget.IsPoisoned == 0 &&
                pokemonTarget.IsSleeping == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
        }

    }
}
