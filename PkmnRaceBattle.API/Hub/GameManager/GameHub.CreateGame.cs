using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using Microsoft.Extensions.Configuration.UserSecrets;
using PkmnRaceBattle.API.Helpers.PathManager;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task CreateGame(string username, int starterId, string trainerSprite)
        {

            var gameCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

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

            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(starterId);
            /*  PokemonMongo pkmn= await _mongoPokemonRepository.GetPokemonMongoById(133);
              PokemonMongo pkmn1 = await _mongoPokemonRepository.GetPokemonMongoById(133);
            PokemonMongo pkmn2 = await _mongoPokemonRepository.GetPokemonMongoById(133);
            PokemonMongo pkmn3 = await _mongoPokemonRepository.GetPokemonMongoById(150);*/
            PokemonMongo pkmn4 = await _mongoPokemonRepository.GetPokemonMongoById(130);

            PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 6);
            /*  PokemonTeam pkmnTeam = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn, 15, 15);
                PokemonTeam pkmn1Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn1, 15, 15);
               PokemonTeam pkmn2Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn2, 15, 15);
              PokemonTeam pkmn3Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn3, 15, 15);*/
            //PokemonTeam pkmn4Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn4, 15, 15);

            PlayerMongo playerMongo = new PlayerMongo();
            playerMongo.Name = username;
            playerMongo.RoomId = gameCode;
            playerMongo.Team = [pokemonTeam];
            //playerMongo.Team = [pokemonTeam, pkmn1Team, pkmnTeam, pkmn2Team];
            playerMongo.IsHost = true;
            playerMongo.Sprite = trainerSprite;
            PlayerPathHelper.InitPlayerPath(playerMongo);

            string id = await _mongoPlayerRepository.CreateAsync(playerMongo);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);
            await _mongoRoomRepository.CreateAsync(new RoomMongo
            {
                hostUserId = id,
                state = 0,
                roomId = gameCode
            });

            UserConnectionManager.AddUserToRoom(id, gameCode, Context.ConnectionId);

            // Informez le client de la création de la salle de jeu et du code de la salle
            await Clients.Caller.SendAsync("GameCreated", gameCode, id);
        }
    }
}
