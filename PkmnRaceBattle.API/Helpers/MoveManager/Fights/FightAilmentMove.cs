using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightAilmentMove
    {
        private static bool HasType(PokemonTeam pokemon, string type) => pokemon.Types?.Any(t => t.Name == type) ?? false;

        public static PokemonTeam PerformAilment(PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            // La confusion est un état volatil : elle s'ajoute à un éventuel statut majeur
            if (move.Ailment == "confusion")
            {
                if (defenser.IsConfused != 0) { turnContext.AddMessage(defenser.NameFr + " est déjà confus"); return defenser; }
                // Compteur décrémenté au début de chaque tour : 1 à 4 tours de confusion
                defenser.IsConfused = GameRandom.Next(RandomPurpose.Duration, 2, 6);
                turnContext.AddMessage("Cela rend " + defenser.NameFr + " confus");
                return defenser;
            }

            // Une capacité de statut Électrik (Cage Éclair) n'affecte pas les types Sol
            if (move.DamageType == "status" && move.Type == "electric" && HasType(defenser, "ground"))
            {
                turnContext.AddMessage("Cela n'affecte pas " + defenser.NameFr);
                return defenser;
            }

            if (!defenser.IsBurning && !defenser.IsParalyzed && !defenser.IsFrozen && defenser.IsSleeping == 0 && defenser.IsPoisoned == 0)
            {
                switch (move.Ailment)
                {
                    case "paralysis":
                        if (HasType(defenser, "electric")) break;
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
                        if (HasType(defenser, "poison") || HasType(defenser, "steel")) break;
                        if(move.NameFr == "Toxik")
                        {
                            defenser.IsPoisoned = 2;
                            defenser.PoisonCount = 0;
                            turnContext.AddMessage(defenser.NameFr + " est gravement empoisonné");
                        }
                        else
                        {
                            defenser.IsPoisoned = 1;

                            turnContext.AddMessage(defenser.NameFr + " est empoisonné");
                        }

                        break;
                    case "freeze":
                        if (HasType(defenser, "ice")) break;
                        defenser.IsFrozen = true;
                        turnContext.AddMessage(defenser.NameFr + " est gelé");
                        break;
                    case "sleep":
                        defenser.IsSleeping = GameRandom.Next(RandomPurpose.Duration, 2, 6);
                        turnContext.AddMessage(defenser.NameFr + " s'endort");
                        break;
                }
            }

            return defenser;
        }

        public static bool CanPokemonPlay(PokemonTeam attacker, TurnContext turnContext)
        {
            if (attacker.IsParalyzed)
            {
                int randomValue = GameRandom.Next(RandomPurpose.StatusCheck, 1, 101);
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
                int randomValue = GameRandom.Next(RandomPurpose.StatusCheck, 1, 101);
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

        public static PokemonTeam SufferConfusion(PokemonTeam pokemon, string fieldChange, TurnContext turnContext)
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
            PokemonTeam[] result = FightDamageMove.PerformDamageMove(pokemon, pokemon, confusionMove, fieldChange, turnContext);
            return result[1];
        }

        // playerSide : camp du Pokémon, pour envoyer la perte de PV au client (null = non envoyée)
        public static PokemonTeam SufferAilment(PokemonTeam pokemon, TurnContext turnContext, bool? playerSide = null)
        {
            int hpBefore = pokemon.CurrHp;
            pokemon = ApplyAilmentDamage(pokemon, turnContext);
            if (pokemon.CurrHp < 0) pokemon.CurrHp = 0;
            if (playerSide != null) FightPerformMove.AddHpChange(turnContext, playerSide.Value, hpBefore - pokemon.CurrHp);
            return pokemon;
        }

        private static PokemonTeam ApplyAilmentDamage(PokemonTeam pokemon, TurnContext turnContext)
        {
            if (pokemon.IsBurning && pokemon.CurrHp > 0) {

                int burningDamage = pokemon.BaseHp / 8;
                pokemon.CurrHp -= burningDamage;
                turnContext.AddMessage(pokemon.NameFr + " souffre de sa brûlure");
            }
            if(pokemon.IsPoisoned > 0 && pokemon.CurrHp > 0)
            {
                double poisonDamage;
                if (pokemon.IsPoisoned == 1)
                {
                    poisonDamage = pokemon.BaseHp / 8;

                    turnContext.AddMessage(pokemon.NameFr + " souffre du poison");
                }
                else
                {
                    pokemon.PoisonCount++;
                    double diviseur = (double)pokemon.PoisonCount / 16.0;
                    poisonDamage = pokemon.BaseHp * diviseur;
                    turnContext.AddMessage(pokemon.NameFr + " souffre gravement du poison");
                }
                pokemon.CurrHp -= (int)Math.Round(poisonDamage);
            }
            return pokemon;
        }

        public static PokemonTeam TryRemoveAilment(PokemonTeam pokemon, TurnContext turnContext)
        {
            if (pokemon.IsFrozen)
            {
                int randomValue = GameRandom.Next(RandomPurpose.StatusCheck, 1, 101);
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

        public static PokemonTeam RemoveAllAilments(PokemonTeam pokemon, string exception= "") {

            if(exception != "sleep") pokemon.IsSleeping = 0;
            pokemon.IsBurning = false;
            pokemon.IsFrozen = false;
            pokemon.IsParalyzed = false;
            pokemon.IsPoisoned = 0;
            pokemon.PoisonCount = null;

            return pokemon;

        }


    }
}
