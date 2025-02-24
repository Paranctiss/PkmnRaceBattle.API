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
                    return (int)(0.4 * Math.Pow(level, 3));
                case "medium":
                    return (int)(0.8 * Math.Pow(level, 3));
                case "medium-fast":
                    return (int)(Math.Pow(level, 3));
                case "medium-slow":
                    return (int)(1.2 * Math.Pow(level, 3) - 15 * Math.Pow(level, 2) + 100 * level - 140);
                case "slow":
                    return (int)(1.25 * Math.Pow(level, 3));
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


        public static int ExpGained(PokemonTeam defeatedPokemon, bool isWild, bool hasExpShare, int participantsCount)
        {

            int baseExp = defeatedPokemon.BaseXP;
            int level = defeatedPokemon.Level;


            double wildModifier = isWild ? 1.0 : 1.5; 
            double expShareModifier = hasExpShare ? 1.5 : 1.0; 
            //double participantsModifier = 1.0 / participantsCount; // Partage entre les Pokémon participants

            int expGained = (int)((baseExp * level * wildModifier * expShareModifier) / 7);
            return expGained;
        }
    }
}
