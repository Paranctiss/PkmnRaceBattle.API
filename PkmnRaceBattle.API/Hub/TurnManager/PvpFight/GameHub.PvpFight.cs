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

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task GetPvpFight(string gameCode, string userId)
        {
            List<PlayerMongo> players = await _mongoPlayerRepository.GetByRoomId(gameCode);
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            BracketMongo bracket = await _mongoBracketRepository.GetByRoomId(gameCode);

            int playerIndex = bracket.Rounds[bracket.NbTurn - 1].PlayersInRace.IndexOf(userId);

            int opponentIndex = (playerIndex % 2 == 0) ? playerIndex + 1 : playerIndex - 1;

            string opponentId = bracket.Rounds[bracket.NbTurn - 1].PlayersInRace[opponentIndex];

            PlayerMongo opponent = bracket.Players.Where(x => x._id == opponentId).FirstOrDefault();

            //PlayerMongo opponent = players.FirstOrDefault(x => x._id != player._id);

            await Clients.Caller.SendAsync("responsePvpFight", opponent);
        }
    }
}
