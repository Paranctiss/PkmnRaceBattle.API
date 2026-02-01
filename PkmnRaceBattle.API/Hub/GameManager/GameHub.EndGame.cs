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
        public async Task EndGame(string gameCode)
        {
            if (_timerObjects.ContainsKey(gameCode))
            {
                _timerObjects[gameCode].Dispose();
                _timerObjects.Remove(gameCode);
                _gameTimers.Remove(gameCode);
            }

            // Autres logiques de fin de jeu...
        }
    }
}
