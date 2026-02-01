using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task GetPlayer(string userId)
        {
            PlayerMongo playerMongo = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            await Clients.Caller.SendAsync("GetPlayerResponse", playerMongo);
        }

        public async Task GetPlayersInRoom(string gameCode)
        {
            List<PlayerMongo> players = await _mongoPlayerRepository.GetByRoomId(gameCode);
            await Clients.Caller.SendAsync("ResponsePlayersInRoom", players);
        }
    }
}
