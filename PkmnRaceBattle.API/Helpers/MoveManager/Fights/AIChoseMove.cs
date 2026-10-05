using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class AIChoseMove
    {
        public static PokemonTeamMove GetARandomMove(PokemonTeam pokemon)
        {
            PokemonTeamMove[] moves = pokemon.Moves
            .Where(x => !pokemon.CantUseMoves.Contains(x.NameFr) && x.Pp > 0)
            .ToArray();
            // Plus de capacité utilisable : l'appelant utilise Lutte
            if (moves.Length == 0) return null!;
            return moves[GameRandom.Next(RandomPurpose.AiMoveChoice, moves.Length)];
        }

        public static PokemonTeamMove GetThatMove(PokemonTeam pokemon, string moveName)
        {
            return pokemon.Moves.FirstOrDefault(x => x.NameFr == moveName);
        }

    }
}
