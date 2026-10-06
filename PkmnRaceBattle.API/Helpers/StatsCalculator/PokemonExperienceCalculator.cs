using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonJson;

namespace PkmnRaceBattle.API.Helpers.StatsCalculator
{
    public static class PokemonExperienceCalculator
    {
        public static int ExpForLevel(int level, string growthRate)
        {
            switch (growthRate)
            {
                case "fast":
                    return 4 * level * level * level / 5;
                case "medium":
                    return level * level * level;
                case "medium-fast":
                    return (int)(Math.Pow(level, 3));
                case "medium-slow":
                    return 6 * level * level * level / 5 - 15 * level * level + 100 * level - 140;
                case "slow":
                    return 5 * level * level * level / 4;
                default:
                    throw new ArgumentException("Taux de croissance invalide");
            }
        }


        public static int ExpToNextLevel(int level, string growthRate, int currXp)
        {
            int expForNextLevel = ExpForLevel(level + 1, growthRate);
            return expForNextLevel - currXp;
        }

        public static int ExpToNextLevel(PokemonTeam pokemon)
        {
            int expForNextLevel = ExpForLevel(pokemon.Level + 1, pokemon.GrowthRate);
            return expForNextLevel - pokemon.CurrXP;
        }


        public static int ExpGained(PokemonTeam defeatedPokemon, bool isTrainer, bool hasExpShare, int participantsCount)
        {

            int baseExp = defeatedPokemon.BaseXP;
            int level = defeatedPokemon.Level;


            double wildModifier = isTrainer ? 1.5 : 1.0;
            double expShareModifier = hasExpShare ? 1.5 : 1.0;
            //double participantsModifier = 1.0 / participantsCount; // Partage entre les Pokémon participants

            int expGained = (int)((baseExp * level * wildModifier * expShareModifier) / 7);
            return expGained;
        }
    }
}
