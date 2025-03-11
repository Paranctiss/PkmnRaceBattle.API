using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class AIChoseMove
    {
        public static PokemonTeamMove GetARandomMove(PokemonTeam pokemon)
        {
            Random rand = new Random();
            PokemonTeamMove[] moves = pokemon.Moves
            .Where(x => !pokemon.CantUseMoves.Contains(x.NameFr))
            .ToArray();
            return moves[rand.Next(moves.Length)];
        }

        public static PokemonTeamMove GetThatMove(PokemonTeam pokemon, string moveName)
        {
            return pokemon.Moves.FirstOrDefault(x => x.NameFr == moveName);
        }

    }
}
