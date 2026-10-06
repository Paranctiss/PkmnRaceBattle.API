using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.PokemonStates;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task ReplacePokemon(string userId, string pokemonId, string opponentId, bool pvp)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo opponent;

            if (!pvp) opponent = await _mongoWildPokemonRepository.GetByIdAsync(opponentId);
            else opponent = await _mongoPlayerRepository.GetByPlayerIdAsync(opponentId);

            PokemonTeam pokemon = player.Team.FirstOrDefault(x => x.Id == pokemonId);

            if (pokemon == null || pokemon.CurrHp <= 0 || pokemon.Id == player.Team[0].Id)
            {
                TurnContext refused = new TurnContext();
                refused.AddMessage("Ce Pokémon ne peut pas être envoyé au combat");
                await Clients.Caller.SendAsync("useMoveResult", refused);
                await Clients.Caller.SendAsync("turnFinished", player, opponent);
                return;
            }

            if (player.Team[0].MultiTurnsMove != null && (player.Team[0].MultiTurnsMove.NameFr == "Ligotage" || player.Team[0].MultiTurnsMove.NameFr == "Étreinte") && player.Team[0].CurrHp > 0)
            {
                TurnContext turnContext = new TurnContext();
                if (player.Team[0].MultiTurnsMove.NameFr == "Ligotage") turnContext.AddMessage("Un Pokémon sous Ligotage ne peut pas être remplacé");
                if (player.Team[0].MultiTurnsMove.NameFr == "Étreinte") turnContext.AddMessage("Un Pokémon sous Ligotage ne peut pas être remplacé");
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, opponent);
                return;
            }

            pokemon = PokemonStatesHelper.ResetForSwap(pokemon);
            player.Team[0] = PokemonStatesHelper.ResetForSwap(player.Team[0]);


            PokemonTeam pokemonSwap = player.Team[0];

            // Échanger les positions
            int indexOfPokemon = Array.IndexOf(player.Team, pokemon);

            player.Team[0] = pokemon;
            player.Team[indexOfPokemon] = pokemonSwap;

            await _mongoPlayerRepository.UpdateAsync(player);

            if (pokemonSwap.CurrHp <= 0)
            {
                TurnContext turnContext = new TurnContext();
                turnContext.AddMessage(player.Name + " envoie " + pokemon.NameFr);
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                if (pvp)
                {
                    string opponentConnectionId = UserConnectionManager.GetConnectionId(opponent._id, opponent.RoomId);
                    await Clients.Client(opponentConnectionId).SendAsync("useMoveResult", turnContext);
                    await Clients.Client(opponentConnectionId).SendAsync("turnFinished", opponent, player);
                }
                await Clients.Caller.SendAsync("turnFinished", player, opponent);
            }
            else
            {
                await HandleMove(userId, pokemon.Id, "swap:", opponentId, opponent.Team[0].Id, true, pvp);
            }
        }
    }
}
