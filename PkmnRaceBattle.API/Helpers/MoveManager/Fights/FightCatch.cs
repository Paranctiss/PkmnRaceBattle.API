using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightCatch
    {
        public static int TryCatchPokemon(PokemonTeam pokemon, string pokeballType)
        {
            double baseCaptureRate = pokemon.TauxCapture;

            double ballModifier = GetBallModifier(pokeballType);

            double hpModifier = (3.0 * pokemon.BaseHp - 2.0 * pokemon.CurrHp) / (3.0 * pokemon.BaseHp);

            double statusModifier = GetStatusModifier(pokemon);

            double captureRate = (baseCaptureRate * ballModifier * hpModifier * statusModifier) / 3;

            Random random = new Random();
            double randomValue = random.NextDouble();

            if (randomValue < captureRate)
            {
                return -1; // -1 signifie que le Pokémon est capturé
            }

            int shakeCount = CalculateShakeCount(captureRate, random);
            return shakeCount;
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
                    return 255.0;
                default:
                    return 1.0;
            }
        }

        private static double GetStatusModifier(PokemonTeam pokemon)
        {
            if (pokemon.IsSleeping > 0 || pokemon.IsFrozen)
            {
                return 2.5;
            }
            else if (pokemon.IsParalyzed || pokemon.IsBurning || pokemon.IsPoisoned > 0)
            {
                return 1.5;
            }
            else
            {
                return 1.0;
            }
        }

        private static int CalculateShakeCount(double captureRate, Random random)
        {
            double shakeThreshold = Math.Pow(captureRate / 255, 0.25);

            int shakeCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (random.NextDouble() < shakeThreshold)
                {
                    shakeCount++;
                }
                else
                {
                    break;
                }
            }

            return shakeCount;
        }

    }
}
