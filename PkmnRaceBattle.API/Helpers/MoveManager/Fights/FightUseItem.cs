using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightUseItem
    {
        public static PokemonTeam[] UseItem(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove usedMove, TurnContext turnContext, bool playerAttacking)
        {

            switch (usedMove.NameFr)
            {
                case "Potion":
                    attacker = UsePotion(attacker, "Basic", turnContext, playerAttacking);
                    break;
                case "Pokeball":
                    break;
            }
            return [attacker, defenser];
        }


        public static PokemonTeam UsePotion(PokemonTeam pokemonTarget, string potionType, TurnContext turnContext, bool playerAttacking)
        {
            int givenHp = 0;
            switch (potionType)
            {
                case "Basic":
                    givenHp = 20;
                    break;
                case "Super":
                    givenHp = 50;
                    break;
                case "Hyper":
                    givenHp += 120;
                    break;
            }
            int oldHp = pokemonTarget.CurrHp;
            pokemonTarget.CurrHp += givenHp;
            pokemonTarget.CurrHp = Math.Clamp(pokemonTarget.CurrHp, 0, pokemonTarget.BaseHp);

            if (playerAttacking) turnContext.Player.Hp.Add(oldHp-pokemonTarget.CurrHp);
            if (!playerAttacking) turnContext.Opponent.Hp.Add(oldHp - pokemonTarget.CurrHp);

            return pokemonTarget;

        }
    }
}
