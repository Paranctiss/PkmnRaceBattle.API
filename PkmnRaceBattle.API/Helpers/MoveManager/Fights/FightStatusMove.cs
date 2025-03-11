using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightStatusMove
    {
        public static PokemonTeam[] PerformStatusMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move)
        {
            foreach(var changes in move.StatsChanges)
            {
                switch (changes.Name)
                {
                    case "attack":
                        break;
                    case "attack-special":
                        break;
                    case "defense":
                        break;
                    case "defense-special":
                        break;
                    case "speed":
                        break;

                }
            }
            return [attacker, defenser];
 

        }

        public static PokemonTeam PerformStatusMove(PokemonTeam target, PokemonTeamMove move, TurnContext turnContext, bool playerAttacking, string targetMove, string fieldChange)
        {
            foreach (var changes in move.StatsChanges)
            {
                if(changes.Changes < 0 && fieldChange == "Brume")
                {
                    turnContext.AddMessage("La brume empêche la réduction des stats");
                }
                else
                {
                    string message = "";
                    switch (changes.Name)
                    {
                        case "attack":
                            message += "L'attaque de ";
                            target.AtkChanges += changes.Changes;
                            break;
                        case "special-attack":
                            message += "L'attaque spé de ";
                            target.AtkSpeChanges += changes.Changes;
                            break;
                        case "defense":
                            message += "La défense de ";
                            target.DefChanges += changes.Changes;
                            break;
                        case "special-defense":
                            message += "La défense spé de ";
                            target.DefSpeChanges += changes.Changes;
                            break;
                        case "speed":
                            message += "La vitesse de ";
                            target.SpeedChanges += changes.Changes;
                            break;
                        case "accuracy":
                            message += "La précision de ";
                            target.AccuracyChanges += changes.Changes;
                            break;
                        case "evasion":
                            message += "L'esquive de ";
                            target.EvasionChanges += changes.Changes;
                            break;
                    }

                    target.AtkChanges = Math.Clamp(target.AtkChanges, -6, 6);
                    target.AtkSpeChanges = Math.Clamp(target.AtkSpeChanges, -6, 6);
                    target.DefChanges = Math.Clamp(target.DefChanges, -6, 6);
                    target.DefSpeChanges = Math.Clamp(target.DefSpeChanges, -6, 6);
                    target.SpeedChanges = Math.Clamp(target.SpeedChanges, -6, 6);
                    target.AccuracyChanges = Math.Clamp(target.AccuracyChanges, -6, 6);
                    target.EvasionChanges = Math.Clamp(target.EvasionChanges, -6, 6);

                    message += target.NameFr;
                    if (changes.Changes > 0) message += " augmente";
                    if (changes.Changes < 0) message += " baisse";
                    switch (changes.Changes)
                    {
                        case 1:
                        case -1:
                            message += " un peu.";
                            break;
                        case 3:
                        case -3:
                            message += " beaucoup.";
                            break;
                    }

                    bool isPlayerTarget = playerAttacking ? targetMove == "user" : targetMove != "user";

                    if (isPlayerTarget)
                    {
                        turnContext.Player.AddStatChange(changes.Name, changes.Changes);
                    }
                    else
                    {
                        turnContext.Opponent.AddStatChange(changes.Name, changes.Changes);
                    }

                    turnContext.AddMessage(message);
                }
            }

            return target;
        }
    }
}
