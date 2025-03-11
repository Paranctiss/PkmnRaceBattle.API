using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightPriority
    {
        public static bool IsPlayingFirst(PokemonTeam pokemon1, PokemonTeamMove move1, PokemonTeam pokemon2, PokemonTeamMove move2)
        {
            if(move1.Priority == move2.Priority)
            {
                double pokeSpeed1 = (double)pokemon1.Speed * FightDamageMove.GetStatMultiplier(pokemon1.SpeedChanges);
                double pokeSpeed2 = (double)pokemon2.Speed * FightDamageMove.GetStatMultiplier(pokemon2.SpeedChanges);

                if (pokemon1.IsParalyzed) {
                    pokeSpeed1 = pokeSpeed1 / 4;
                }

                if (pokemon2.IsParalyzed)
                {
                    pokeSpeed2 = pokeSpeed2 / 4;
                }

                return pokeSpeed1 > pokeSpeed2;
            }
            else
            {
                return move1.Priority > move2.Priority;
            }
        }

        public static bool MoveMustBePlayedLast(PokemonTeamMove move)
        {
            string[] moveThatMustBePlayedLast = ["Entrave", "Riposte", "Mimique", "Copie"];

            if (moveThatMustBePlayedLast.Contains(move.NameFr)) return true;

            return false;
        }
    }
}
