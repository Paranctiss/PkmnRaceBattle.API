using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.StatsCalculator;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task FinishFight(PlayerMongo player, PlayerMongo opponent, bool unexpectedEnd = false)
        {
            PokemonTeam opponentPokemon = opponent.Team[0];
            string opponentConnectionId = "";
            if (opponent.IsPlayer) opponentConnectionId = UserConnectionManager.GetConnectionId(opponent._id, opponent.RoomId);

            bool lost = false;
            if (unexpectedEnd && opponent.IsTrainer)
            {
                if (player.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")) != null)
                {
                    // Changement obligatoire : ReplacePokemon refuse le Pokémon éjecté et redemande un choix
                    player.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")).SpecialCases = new() { MustSwitchCase };
                    _mongoPlayerRepository.UpdateAsync(player);
                    await Clients.Caller.SendAsync("playerPokemonDeath", "Changez de Pokémon");
                    return;
                }
                if (opponent.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")) != null)
                {
                    opponent.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")).SpecialCases = new();
                    _mongoWildPokemonRepository.UpdateAsync(opponent);
                    if (opponent.IsPlayer)
                    {
                        await Clients.Client(opponentConnectionId).SendAsync("playerPokemonDeath", "Changez de Pokémon");
                        await Clients.Caller.SendAsync("playerPokemonDeath", "Changez de Pokémon");
                    }
                    else
                    {
                        await TrainerSendNextPokemon(player, opponent);
                    }

                    return;
                }
            }

            // PvP : un K.O. qui laisse des Pokémon aux deux joueurs ne termine pas le combat,
            // celui qui vient de perdre son Pokémon en envoie un autre pendant que l'autre attend
            if (opponent.IsPlayer && !unexpectedEnd
                && player.Team.Any(x => x.CurrHp > 0) && opponent.Team.Any(x => x.CurrHp > 0))
            {
                await _mongoPlayerRepository.UpdateAsync(player);
                await _mongoPlayerRepository.UpdateAsync(opponent);
                await Clients.Client(opponentConnectionId).SendAsync("playerPokemonDeath", opponentPokemon.NameFr + " est K.O");
                await Clients.Caller.SendAsync("waitingOpponent");
                return;
            }

            // Les effets de terrain ne survivent pas au combat
            player.FieldChange = null;
            player.FieldChangeCount = null;

            if (player.Team.FirstOrDefault(t => t.CurrHp > 0) != null)
            {
                XpSettings xpSettings = await GetXpSettings(player.RoomId);

                for (int i = 0; i <= player.Team.Length - 1; i++)
                {
                    PokemonTeam team = player.Team[i];
                    team.AtkChanges = 0;
                    team.AtkSpeChanges = 0;
                    team.DefChanges = 0;
                    team.DefSpeChanges = 0;
                    team.SpeedChanges = 0;
                    team.CritChanges = 0;
                    team.AccuracyChanges = 0;
                    team.EvasionChanges = 0;
                    team.IsFlinched = false;
                    team.Substitute = null;
                    if (team.IsPoisoned == 2) team.PoisonCount = 0;
                    team.SpecialCases = new();
                    team.MultiTurnsMoveCount = null;
                    team.MultiTurnsMove = null;
                    team.CantUseMoves = new();
                    team.WaitingMove = null;
                    team.WaitingMoveTurns = null;
                    team.Untargetable = null;
                    team.BlowsTaken = 0;
                    team.BlowsTakenType = null;
                    team.IsConfused = 0;
                    if (team.ConvertedType != null)
                    {
                        team.Types[0].Name = team.ConvertedType;
                        team.ConvertedType = null;
                    }
                    if (team.UnmorphedForm != null)
                    {
                        team = team.UnmorphedForm;
                        team.UnmorphedForm = null;
                    }
                    if (team.SavedMove != null)
                    {
                        team.Moves[(int)team.SavedMoveSlot] = team.SavedMove;
                        team.SavedMove = null;
                        team.SavedMoveSlot = null;
                    }

                    // XP aux Pokémon debout : participants, et reste de l'équipe si le Multi Exp est activé
                    if (team.CurrHp > 0 && !unexpectedEnd)
                    {
                        int earnedXp = PokemonExperienceCalculator.ExpReceived(opponentPokemon, opponent.IsTrainer, team.HavePlayed, xpSettings);
                        team.HavePlayed = false;

                        if (earnedXp > 0)
                        {
                            team.CurrXP += earnedXp;
                            team = await CheckLevelUp(team);
                        }
                    }
                    player.Team[i] = team;

                }
                if (!unexpectedEnd)
                {
                    player.Credits += player.Jackpot;
                    player.Jackpot = 0;
                    if (opponent.IsTrainer && opponent.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                    {
                        TurnContext turnContext = new TurnContext();

                        if (!opponent.IsPlayer)
                        {
                            int earnedCredits = 2000;
                            player.Credits += earnedCredits;
                            turnContext.AddMessage("Vous avez battu " + opponent.Name);
                            turnContext.AddMessage("Vous remportez " + earnedCredits + " Pokédollz");
                        }
                        else
                        {
                            turnContext.AddMessage("Vous avez battu " + opponent.Name + ", vous êtes qualifié pour la suite");
                        }

                        await Clients.Caller.SendAsync("useMoveResult", turnContext);


                        if (opponent.IsPlayer)
                        {
                            string message = "Vous n'avez plus de pokémon en forme, vous êtes éliminé.";
                            await Clients.Client(opponentConnectionId).SendAsync("playerLooseFight", message);
                        }
                        await GameDelay.Wait(turnContext.CalculateDelay());
                    }
                }

            }
            else
            {
                for (int i = 0; i <= player.Team.Length - 1; i++)
                {
                    PokemonTeam team = player.Team[i];
                    team.AtkChanges = 0;
                    team.AtkSpeChanges = 0;
                    team.DefChanges = 0;
                    team.DefSpeChanges = 0;
                    team.SpeedChanges = 0;
                    team.CritChanges = 0;
                    team.AccuracyChanges = 0;
                    team.EvasionChanges = 0;
                    team.IsFlinched = false;
                    team.Substitute = null;
                    team.CurrHp = team.BaseHp;
                    team.SpecialCases = new();
                    team.MultiTurnsMoveCount = null;
                    team.MultiTurnsMove = null;
                    team.CantUseMoves = new();
                    team.WaitingMove = null;
                    team.WaitingMoveTurns = null;
                    team.Untargetable = null;
                    team.BlowsTaken = 0;
                    team.BlowsTakenType = null;
                    team.IsConfused = 0;
                    team.IsSleeping = 0;
                    team.IsBurning = false;
                    team.IsFrozen = false;
                    team.IsParalyzed = false;
                    team.IsPoisoned = 0;
                    team.PoisonCount = null;
                    team.HavePlayed = false;
                    if (team.ConvertedType != null)
                    {
                        team.Types[0].Name = team.ConvertedType;
                        team.ConvertedType = null;
                    }
                    if (team.UnmorphedForm != null)
                    {
                        team = team.UnmorphedForm;
                        team.UnmorphedForm = null;
                    }
                    if (team.SavedMove != null)
                    {
                        team.Moves[(int)team.SavedMoveSlot] = team.SavedMove;
                        team.SavedMove = null;
                        team.SavedMoveSlot = null;
                    }
                    player.Team[i] = team;
                }
                //Combat perdu go heal + diviser l'argent en 2
                int lostCredits = player.Credits / 2;
                player.Credits = lostCredits;
                lost = true;
                string message = "Vous n'avez plus de pokémon en forme, vous perdez " + lostCredits + " pokédolz";
                await Clients.Caller.SendAsync("playerLooseFight", message);

                if (opponent.IsPlayer)
                {
                    TurnContext turnContext = new TurnContext();
                    turnContext.AddMessage("Vous avez battu " + opponent.Name + ", vous êtes qualifié pour la manche suivante");
                    await Clients.Client(opponentConnectionId).SendAsync("useMoveResult", turnContext);
                }

                await GameDelay.Wait(GameDelay.StandaloneMessage + GameDelay.TurnPause);
            }

            // Un dresseur PvE à qui il reste des Pokémon continue le combat
            bool trainerContinues = !opponent.IsPlayer && opponent.IsTrainer && !lost
                && opponent.Team.Any(x => x.CurrHp > 0);

            // Combat PvE terminé (victoire, défaite, capture ou fuite) : il compte dans la progression de la map
            if (!opponent.IsPlayer && !trainerContinues) player.MapFightCount++;

            await this._mongoPlayerRepository.UpdateAsync(player);

            if (!opponent.IsPlayer) await this._mongoWildPokemonRepository.UpdateAsync(opponent);
            else await this._mongoPlayerRepository.UpdateAsync(opponent);

            // Le PvP est piloté par le tournoi, pas par le chemin
            if (opponent.IsPlayer)
            {
                if (!unexpectedEnd)
                {
                    string? winnerId = lost ? opponent._id
                        : opponent.Team.All(x => x.CurrHp <= 0) ? player._id
                        : null;
                    if (winnerId != null) await AdvanceTournament(player.RoomId, winnerId);
                }
                return;
            }

            if (trainerContinues)
                await TrainerSendNextPokemon(player, opponent);
            else
                await GetNewTurn(player._id);
        }
    }
}
