using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightAilmentMove
    {
        public static PokemonTeam PerformAilment(PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            if (!defenser.IsBurning && !defenser.IsParalyzed && !defenser.IsFrozen && defenser.IsSleeping == 0 && !defenser.IsPoisoned)
            {
                switch (move.Ailment)
                {
                    case "paralysis":
                        defenser.IsParalyzed = true;
                        turnContext.AddMessage(defenser.NameFr + " est Paralysé");
                        break;
                    case "burn":
                        if (defenser.Types.FirstOrDefault(x => x.Name == "fire") == null)
                        {
                            defenser.IsBurning = true;
                            turnContext.AddMessage(defenser.NameFr + " est brûlé");
                        }
                        break;
                    case "poison":
                        defenser.IsPoisoned = true;
                        turnContext.AddMessage(defenser.NameFr + " est empoisonné");
                        break;
                    case "freeze":
                        defenser.IsFrozen = true;
                        turnContext.AddMessage(defenser.NameFr + " est gelé");
                        break;
                    case "sleep":
                        Random rnd = new Random();
                        defenser.IsSleeping = rnd.Next(2, 6);
                        turnContext.AddMessage(defenser.NameFr + " s'endort");
                        break;
                    case "confusion":
                        Random rnde = new Random();
                        defenser.IsConfused = rnde.Next(1, 5);
                        turnContext.AddMessage("Cela rend " + defenser.NameFr + " confus");
                        break;
                }
            }

            return defenser;
        }

        public static bool CanPokemonPlay(PokemonTeam attacker, TurnContext turnContext)
        {
            if (attacker.IsParalyzed)
            {
                Random rnd = new Random();
                int randomValue = rnd.Next(1, 101);
                if (randomValue <= 25) {
                    turnContext.DeleteLastPrioMessage();
                    turnContext.AddMessage(attacker.NameFr + " est paralysé, il ne peut pas attaquer");
                    return false;
                }
                else
                {
                    return true;
                }
            }

            if (attacker.IsFrozen) {
                turnContext.DeleteLastPrioMessage();
                turnContext.AddMessage(attacker.NameFr + " est gelé, il ne peut pas attaquer");
                return false;
            }

            if(attacker.IsSleeping > 0)
            {
                turnContext.DeleteLastPrioMessage();
                turnContext.AddMessage(attacker.NameFr + " est en train de rompiche ZZzzzz");
                return false;
            }

            if (attacker.IsConfused > 0) {
                Random rnd = new Random();
                int randomValue = rnd.Next(1, 101);
                if (randomValue <= 50)
                {
                    turnContext.DeleteLastPrioMessage();
                    turnContext.AddMessage(attacker.NameFr + " se blesse dans sa confusion");
                    return false;
                }
                else
                {
                    return true;
                }
            }

            return true;
        }

        public static PokemonTeam SufferConfusion(PokemonTeam pokemon, TurnContext turnContext)
        {
            PokemonTeamMove confusionMove = new PokemonTeamMove();
            confusionMove.Name = "confusion";
            confusionMove.NameFr = "Confusion";
            confusionMove.Pp = 1;
            confusionMove.Target = "opponent";
            confusionMove.Accuracy = 100;
            confusionMove.DamageType = "physical";
            confusionMove.Power = 40;
            confusionMove.Type = "none";
            PokemonTeam[] result = FightDamageMove.PerformDamageMove(pokemon, pokemon, confusionMove, turnContext);
            return result[1];
        }

        public static PokemonTeam SufferAilment(PokemonTeam pokemon, TurnContext turnContext)
        {
            if (pokemon.IsBurning && pokemon.CurrHp > 0) {

                int burningDamage = pokemon.BaseHp / 8;
                pokemon.CurrHp -= burningDamage;
                turnContext.AddMessage(pokemon.NameFr + " souffre de sa brûlure");
            }
            if(pokemon.IsPoisoned && pokemon.CurrHp > 0)
            {
                int poisonDamage = pokemon.BaseHp / 8;
                pokemon.CurrHp -= poisonDamage;
                turnContext.AddMessage(pokemon.NameFr + " souffre du poison");
            }
            return pokemon;
        }

        public static PokemonTeam TryRemoveAilment(PokemonTeam pokemon, TurnContext turnContext)
        {
            if (pokemon.IsFrozen)
            {
                Random rnd = new Random();
                int randomValue = rnd.Next(1, 101);
                if(randomValue <= 20)
                {
                    pokemon.IsFrozen = false;
                    turnContext.AddPrioMessage(pokemon.NameFr + " n'est plus gelé");
                }

            }

            if(pokemon.IsSleeping > 0)
            {
                pokemon.IsSleeping--;
                if(pokemon.IsSleeping == 0)
                {
                    turnContext.AddPrioMessage(pokemon.NameFr + " se réveille");
                }
            }

            if (pokemon.IsConfused > 0) { 
                pokemon.IsConfused--;
                if(pokemon.IsConfused == 0)
                {
                    turnContext.AddPrioMessage(pokemon.NameFr + " n'est plus confus");
                }
                else
                {
                    turnContext.AddPrioMessage(pokemon.NameFr + " est confus");
                }
            }

            return pokemon;
        }


    }
}
