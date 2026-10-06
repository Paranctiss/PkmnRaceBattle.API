using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PathManager;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.Domain.Models.RoomMongo;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task JoinGame(string username, int starterId, string trainerSprite, string gameCode)
        {
            // Vérifier si l'utilisateur est déjà dans une salle et l'en retirer
            var (currentRoomId, existingUserId) = UserConnectionManager.GetUserRoomByConnectionId(Context.ConnectionId);

            if (!string.IsNullOrEmpty(currentRoomId) && currentRoomId != gameCode && !string.IsNullOrEmpty(existingUserId))
            {

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, currentRoomId);
                UserConnectionManager.RemoveUserFromRoom(existingUserId, currentRoomId);


                await Clients.Group(currentRoomId).SendAsync("UserLeft", currentRoomId);

                // Éventuellement supprimer les données de l'utilisateur si nécessaire
                if (!string.IsNullOrEmpty(existingUserId))
                {

                }
            }

            // Salle inconnue : on n'inscrit pas le joueur
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(gameCode);
            if (room == null) return;

            // Salle sans hôte (hôte parti, ou salle remise à zéro par « Rejouer ») : le premier arrivé le devient
            bool becomesHost = !(await _mongoPlayerRepository.GetByRoomId(gameCode)).Any(p => p.IsHost);

            // Ajouter l'utilisateur à la nouvelle salle
            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);
            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(starterId);
            PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 6);

            //PokemonMongo pkmn4 = await _mongoPokemonRepository.GetPokemonMongoById(149);
            //PokemonTeam pkmn4Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn4, 15, 15);

            PlayerMongo playerMongo = new PlayerMongo();
            playerMongo.Name = username;
            playerMongo.RoomId = gameCode;
            playerMongo.Team = [pokemonTeam];
            playerMongo.IsHost = becomesHost;
            playerMongo.Sprite = trainerSprite;
            PlayerPathHelper.InitPlayerPath(playerMongo);
            string id = await _mongoPlayerRepository.CreateAsync(playerMongo);
            if (becomesHost)
            {
                room.hostUserId = id;
                await _mongoRoomRepository.UpdateAsync(gameCode, room);
            }
            UserConnectionManager.AddUserToRoom(id, gameCode, Context.ConnectionId);
            await Clients.Caller.SendAsync("JoinSuccess", gameCode, id);
            await Clients.Group(gameCode).SendAsync("UserJoined", gameCode);
        }
    }
}
