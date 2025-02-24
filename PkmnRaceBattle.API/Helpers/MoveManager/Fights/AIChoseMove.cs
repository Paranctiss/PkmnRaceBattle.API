using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class AIChoseMove
    {
        public static PokemonTeamMove GetARandomMove(PokemonTeam pokemon)
        {
            Random rand = new Random();
            return pokemon.Moves[rand.Next(pokemon.Moves.Length)];
        }
    }
}
