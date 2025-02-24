using PkmnRaceBattle.Domain.Models.PlayerMongo;
using System.Security.Cryptography.X509Certificates;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightExperience
    {
        public static int CalculateEarnedXP(PokemonTeam opponentPokemon)
        {
            return 100;
        }

        public static PokemonTeam ApplyXP(PokemonTeam myPokemon, PokemonTeam opponentPokemon)
        {
            int xp = CalculateEarnedXP(opponentPokemon);

            return myPokemon;
        }
    }
}
