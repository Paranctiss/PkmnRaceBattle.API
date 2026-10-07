using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.API.Helpers.PathManager;
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
            XpSettings xpSettings = await GetXpSettings(player.RoomId);

            // Le dresseur clôt la zone : ses Pokémon sont au-dessus des sauvages de la map, et il en a un de plus à chaque zone
            int zone = ZoneLevels.GetZone(player);
            ZoneLevelRange range = ZoneLevels.GetRange(zone, xpSettings);
            PlayerMongo trainer = await GenerateNewTrainer.GenerateNewTrainerTeam(ZoneLevels.TrainerTeamSize(zone), range.TrainerMin, range.TrainerMax, _mongoPokemonRepository);

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
                await GameDelay.Wait(GameDelay.Message);
                turnContext = new();
                await Clients.Caller.SendAsync("onTrainerSwitchPokemon", trainer);
            }
        }
    }
}
