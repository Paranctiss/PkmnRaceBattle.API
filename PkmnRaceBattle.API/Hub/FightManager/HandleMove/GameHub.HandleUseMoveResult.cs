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
        public async Task HandleUseMoveResult(TurnContext turnContext, string opponentConnectionId)
        {
            await Clients.Caller.SendAsync("useMoveResult", turnContext);
            if (opponentConnectionId != "")
            {
                PokemonChanges player = turnContext.Opponent;
                PokemonChanges opponent = turnContext.Player;

                turnContext.Player = player;
                turnContext.Opponent = opponent;
                await Clients.Client(opponentConnectionId).SendAsync("useMoveResult", turnContext);
            }
        }
    }
}
