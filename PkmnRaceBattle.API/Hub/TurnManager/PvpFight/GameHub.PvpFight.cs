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
            BracketMongo bracket = await _mongoBracketRepository.GetByRoomId(gameCode);

            // Rounds va de la finale au premier tour : le tour en cours est compté depuis la fin
            int current = bracket.Rounds.Count - bracket.NbTurn;
            if (current < 0) return;
            List<string> playersInRace = bracket.Rounds[current].PlayersInRace;

            // Joueur éliminé (ou tournoi terminé) : pas de combat
            int playerIndex = playersInRace.IndexOf(userId);
            if (playerIndex == -1) return;

            int opponentIndex = (playerIndex % 2 == 0) ? playerIndex + 1 : playerIndex - 1;
            string opponentId = playersInRace[opponentIndex];

            // Exempté (pas d'adversaire) : déjà qualifié pour le tour suivant
            if (opponentId == "?") return;

            // Équipe à jour (celle du tableau date de sa création)
            PlayerMongo opponent = await _mongoPlayerRepository.GetByPlayerIdAsync(opponentId);
            if (opponent == null) return;

            await Clients.Caller.SendAsync("responsePvpFight", opponent);
        }
    }
}
