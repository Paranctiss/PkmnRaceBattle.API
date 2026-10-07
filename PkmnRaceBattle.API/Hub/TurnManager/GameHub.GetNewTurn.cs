using PkmnRaceBattle.API.Helpers.PathManager;
using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using Microsoft.AspNetCore.SignalR;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task GetNewTurn(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            if (await StopIfRaceOver(player)) return;

            if (PlayerPathHelper.IsCurrentMapCompleted(player))
            {
                List<PathPoint> nextPoints = PlayerPathHelper.GetNextPathPoints(player);

                if (nextPoints.Count > 1)
                {
                    // Embranchement : le joueur doit choisir la prochaine map avant de lancer le tour
                    await Clients.Caller.SendAsync("chooseNextPath", nextPoints);
                    return;
                }

                if (nextPoints.Count == 1)
                {
                    PlayerPathHelper.MoveTo(player, nextPoints[0]);
                    // Étapes ajoutées au chemin : paliers de niveaux affichés sur la carte
                    ZoneLevels.AnnotatePath(player, await GetXpSettings(player.RoomId));
                }
                else
                {
                    // Fin du chemin (ne devrait plus arriver, le chemin est prolongé à chaque déplacement) :
                    // le joueur recommence un cycle de combats sur sa map actuelle,
                    // qui compte comme une zone de plus (niveaux plus élevés)
                    player.MapFightCount = 0;
                    player.PathLoopCount++;
                    if (PlayerPathHelper.IsFightEnvironment(player.CurrentPath.EnvironmentName))
                        ZoneLevels.SetLevels(player.CurrentPath, ZoneLevels.GetRange(ZoneLevels.GetZone(player), await GetXpSettings(player.RoomId)));
                }

                await _mongoPlayerRepository.UpdateAsync(player);
            }

            await LaunchTurnOnCurrentPath(player);
        }

        public async Task ChooseNextPath(string userId, int x, int y)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            if (await StopIfRaceOver(player)) return;

            PathPoint? chosenPoint = PlayerPathHelper.IsCurrentMapCompleted(player)
                ? PlayerPathHelper.GetNextPathPoints(player).FirstOrDefault(p => p.X == x && p.Y == y)
                : null;

            // Choix invalide (map hors de l'étape suivante, map courante pas terminée) : on renvoie l'état attendu
            if (chosenPoint == null)
            {
                await GetNewTurn(userId);
                return;
            }

            PlayerPathHelper.MoveTo(player, chosenPoint);
            ZoneLevels.AnnotatePath(player, await GetXpSettings(player.RoomId));
            await _mongoPlayerRepository.UpdateAsync(player);

            await LaunchTurnOnCurrentPath(player);
        }

        // Minuteur écoulé : un combat commencé avant la fin ne relance pas de tour,
        // le joueur reste sur l'écran de fin (TimerEnded renvoyé au cas où il l'aurait manqué)
        private async Task<bool> StopIfRaceOver(PlayerMongo player)
        {
            if (!await IsRaceOver(player.RoomId)) return false;
            await Clients.Caller.SendAsync("TimerEnded", player.RoomId);
            return true;
        }

        private async Task LaunchTurnOnCurrentPath(PlayerMongo player)
        {
            switch (player.CurrentPath.EnvironmentName)
            {
                case PlayerPathHelper.ShopEnvironment:
                    await GetPokeShop(player._id);
                    break;
                case PlayerPathHelper.CenterEnvironment:
                    await GetPokeCenter(player._id);
                    break;
                default:
                    if (PlayerPathHelper.IsTrainerFightNext(player))
                        await GetTrainerFight(player._id);
                    else
                        await GetWildFight(player._id);
                    break;
            }
        }
    }
}
