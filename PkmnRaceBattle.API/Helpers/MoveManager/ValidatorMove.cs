using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager
{
    public static class ValidatorMove
    {
        public static bool IsEverythingOk(PlayerMongo player1, PokemonTeam pokemon1, PokemonTeamMove pokemon1Move, PlayerMongo player2, PokemonTeam pokemon2, PokemonTeamMove pokemon2Move, TurnContext turnContext)
        {
            if(pokemon1.CurrHp <= 0 || pokemon2.CurrHp <= 0)
            {
                turnContext.AddMessage("Un Pokémon K.O ne peut pas attaquer");
                return false;
            }
            if(pokemon1Move.Pp <= 0 || pokemon2Move.Pp <=0)
            {
                turnContext.AddMessage("La capacité utilisée n'a plus de PP");
                return false;
            }

            if (pokemon1Move.NameFr.Contains("ball") && player2.IsTrainer)
            {
                turnContext.AddMessage("Voler n'est pas bon");
                return false;
            }

            if (pokemon2Move.NameFr.Contains("ball") && player1.IsTrainer)
            {
                turnContext.AddMessage("Voler n'est pas bon");
                return false;
            }

            return true;
        }
    }
}
