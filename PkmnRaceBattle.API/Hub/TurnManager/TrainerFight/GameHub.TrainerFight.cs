using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.TrainerGeneration;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task GetTrainerFight(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            int levelAvg = player.GetAverageLevel();

            PlayerMongo trainer = await GenerateNewTrainer.GenerateNewTrainerTeam(3, levelAvg, _mongoPokemonRepository);

            await _mongoWildPokemonRepository.CreateAsync(trainer);
            await Clients.Caller.SendAsync("responseTrainerFight", trainer);
        }

        public async Task TrainerSendNextPokemon(PlayerMongo player, PlayerMongo trainer)
        {
            // Récupère le premier Pokémon dont les HP sont > 0 et qui n'est pas à l'index 0
            PokemonTeam alivePokemon = trainer.Team.Where((x, index) => x.CurrHp > 0 && index != 0).FirstOrDefault();

            if (alivePokemon != null)
            {
                int indexAlive = Array.IndexOf(trainer.Team, alivePokemon);
                PokemonTeam deadPokemon = trainer.Team[0];
                trainer.Team[0] = alivePokemon;
                trainer.Team[indexAlive] = deadPokemon;

                await _mongoWildPokemonRepository.UpdateAsync(trainer);

                TurnContext turnContext = new TurnContext();
                turnContext.AddPrioMessage(trainer.Name + " envoie " + trainer.Team[0].NameFr);
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Task.Delay(1000);
                turnContext = new();
                await Clients.Caller.SendAsync("onTrainerSwitchPokemon", trainer);
            }
        }
    }
}
