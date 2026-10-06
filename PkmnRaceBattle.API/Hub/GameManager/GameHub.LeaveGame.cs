using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.Domain.Models.BracketMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task LeaveGame(string groupName, string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            UserConnectionManager.RemoveUserFromRoom(userId, groupName);
            await RemovePlayerFromWaitingRoom(groupName, userId);
            await Clients.Group(groupName).SendAsync("UserLeft", userId);
        }

        // Retour au menu depuis la salle d'attente : le joueur n'apparaît plus dans la salle,
        // et s'il en était l'hôte, le joueur suivant le devient
        private async Task RemovePlayerFromWaitingRoom(string roomId, string userId)
        {
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(roomId);
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            if (room == null || room.state != 0 || player == null || player.RoomId != roomId) return;

            bool wasHost = player.IsHost;
            player.RoomId = "";
            player.IsHost = false;
            await _mongoPlayerRepository.UpdateAsync(player);

            if (!wasHost) return;
            PlayerMongo newHost = (await _mongoPlayerRepository.GetByRoomId(roomId)).FirstOrDefault();
            room.hostUserId = newHost?._id ?? "";
            await _mongoRoomRepository.UpdateAsync(roomId, room);
            if (newHost == null) return;
            newHost.IsHost = true;
            await _mongoPlayerRepository.UpdateAsync(newHost);
        }

        // Fin du tournoi : la salle redevient une salle d'attente vide. Chaque joueur rechoisit
        // son starter puis la rejoint (JoinGame) ; le premier revenu en devient l'hôte
        public async Task ReplayGame(string gameCode, string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameCode);
            UserConnectionManager.RemoveUserFromRoom(userId, gameCode);

            // Seul le premier joueur à cliquer remet la salle à zéro (le tournoi terminé n'est alors plus rattaché à la salle)
            BracketMongo bracket = await _mongoBracketRepository.GetByRoomId(gameCode);
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(gameCode);
            if (room == null || bracket == null || bracket.Champion == null) return;

            await EndGame(gameCode);

            bracket.GameCode = "";
            await _mongoBracketRepository.UpdateAsync(bracket);

            // Les joueurs de la partie précédente sont détachés de la salle
            foreach (PlayerMongo player in await _mongoPlayerRepository.GetByRoomId(gameCode))
            {
                player.RoomId = "";
                player.IsHost = false;
                await _mongoPlayerRepository.UpdateAsync(player);
            }

            room.state = 0;
            room.hostUserId = "";
            await _mongoRoomRepository.UpdateAsync(gameCode, room);
        }
    }
}
