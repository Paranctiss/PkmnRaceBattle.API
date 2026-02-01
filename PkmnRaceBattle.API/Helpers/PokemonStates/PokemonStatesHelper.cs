using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.PokemonStates
{
    public static class PokemonStatesHelper
    {
        public static PokemonTeam ResetForSwap(PokemonTeam pokemon)
        {
            pokemon.SpecialCases = new();
            pokemon.MultiTurnsMoveCount = null;
            pokemon.MultiTurnsMove = null;
            pokemon.CantUseMoves = new();
            pokemon.WaitingMove = null;
            pokemon.WaitingMoveTurns = null;
            pokemon.Untargetable = null;
            pokemon.BlowsTaken = 0;
            pokemon.BlowsTakenType = null;
            pokemon.IsConfused = 0;
            pokemon.CritChanges = 0;
            pokemon.AtkChanges = 0;
            pokemon.AtkSpeChanges = 0;
            pokemon.DefChanges = 0;
            pokemon.DefSpeChanges = 0;
            pokemon.SpeedChanges = 0;
            pokemon.CritChanges = 0;
            pokemon.EvasionChanges = 0;
            pokemon.AccuracyChanges = 0;

            if (pokemon.ConvertedType != null)
            {
                pokemon.Types[0].Name = pokemon.ConvertedType;
                pokemon.ConvertedType = null;
            }
            if (pokemon.UnmorphedForm != null)
            {
                pokemon = pokemon.UnmorphedForm;
                pokemon.UnmorphedForm = null;
            }
            if (pokemon.SavedMove != null)
            {
                pokemon.Moves[(int)pokemon.SavedMoveSlot] = pokemon.SavedMove;
                pokemon.SavedMove = null;
                pokemon.SavedMoveSlot = null;
            }

            return pokemon;
        }
    }
}
