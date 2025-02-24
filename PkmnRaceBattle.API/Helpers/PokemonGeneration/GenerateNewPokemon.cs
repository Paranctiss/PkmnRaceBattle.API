using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;

namespace PkmnRaceBattle.API.Helpers.PokemonGeneration
{
    public static class GenerateNewPokemon
    {
        public static PokemonTeam GenerateNewPokemonTeam(PokemonMongo pokemonBase, int minLvl, int maxLvl)
        {
            PokemonTeam pokemon = new PokemonTeam();

            Random random = new Random();

            return PokemonBaseToTeam.ConvertBaseToTeam(pokemonBase, random.Next(minLvl, maxLvl));
        }
    }
}
