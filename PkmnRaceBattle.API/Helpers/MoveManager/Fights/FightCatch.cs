using PkmnRaceBattle.Domain.Models.PlayerMongo;
using System;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightCatch
    {
        public static int TryCatchPokemon(PokemonTeam pokemon, string pokeballType)
        {
            // 1. Calculer la valeur a
            double maxHp = pokemon.BaseHp;
            double currentHp = pokemon.CurrHp;
            double catchRate = pokemon.TauxCapture;
            double ballBonus = GetBallModifier(pokeballType);
            double statusBonus = GetStatusModifier(pokemon);

            // Formule standard pour calculer a (taux de capture)
            double a = (((3 * maxHp - 2 * currentHp) * catchRate * ballBonus) / (3 * maxHp)) * statusBonus;

            // Limiter a à 255 maximum
            a = Math.Min(a, 255);

            // 2. Calculer la probabilité de capture
            // Probabilité = (a/255)^0.75 pour des raisons historiques
            double catchProbability = Math.Pow(a / 255, 0.75);

            // 3. Effectuer la tentative de capture
            Random random = new Random();

            // 4. Calculer le nombre de secousses si pas de capture immédiate
            int shakeCount = 0;
            bool caught = false;

            for (int i = 0; i < 4; i++)
            {
                // Chaque secousse a la même probabilité
                double shakeCheck = Math.Pow(a / 255, 0.1875);
                if (random.NextDouble() <= shakeCheck)
                {
                    shakeCount++;
                    if (shakeCount == 4)
                    {
                        caught = true;
                    }
                }
                else
                {
                    // Si une secousse échoue, on arrête de compter
                    break;
                }
            }

            // Si Masterball, capture garantie
            if (pokeballType == "Masterball")
            {
                caught = true;
                shakeCount = 4;
            }

            // Renvoyer le résultat
            return caught ? -1 : shakeCount;
        }

        private static double GetBallModifier(string pokeballType)
        {
            switch (pokeballType)
            {
                case "Pokeball":
                    return 1.0;
                case "Superball":
                    return 1.5;
                case "Hyperball":
                    return 2.0;
                case "Masterball":
                    return 255.0; // En pratique, la Masterball est toujours 100% de réussite
                default:
                    return 1.0;
            }
        }

        private static double GetStatusModifier(PokemonTeam pokemon)
        {
            if (pokemon.IsSleeping > 0 || pokemon.IsFrozen)
            {
                return 2.5; // Endormi ou gelé
            }
            else if (pokemon.IsParalyzed || pokemon.IsBurning || pokemon.IsPoisoned > 0)
            {
                return 1.5; // Paralysé, brûlé ou empoisonné
            }
            else
            {
                return 1.0; // Aucun statut
            }
        }

        // Méthode de calcul de pourcentage pour référence/debugging
        public static double CalculateCatchProbabilityPercentage(PokemonTeam pokemon, string pokeballType)
        {
            double maxHp = pokemon.BaseHp;
            double currentHp = pokemon.CurrHp;
            double catchRate = pokemon.TauxCapture;
            double ballBonus = GetBallModifier(pokeballType);
            double statusBonus = GetStatusModifier(pokemon);

            double a = (((3 * maxHp - 2 * currentHp) * catchRate * ballBonus) / (3 * maxHp)) * statusBonus;
            a = Math.Min(a, 255);

            double probability = Math.Pow(a / 255, 0.75) * 100; // Convertir en pourcentage

            return Math.Round(probability, 2); // Arrondir à 2 décimales
        }
    }
}