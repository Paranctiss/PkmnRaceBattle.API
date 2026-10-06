using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Helpers.MoveManager;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task HandleMove(string playerId, string playerPokemonId, string usedMoveName, string opponentId, string opponentPokemonId, bool isAttacking, bool pvp, int index = 0, bool skipTurn = false)
        {
            TurnContext turnContext = new TurnContext();
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(playerId);
            PokemonTeam playerPokemonMongo = await _mongoPlayerRepository.GetPlayerPokemonById(player._id, playerPokemonId);

            PlayerMongo opponentMongo;
            PokemonTeam opponentPokemonMongo;
            if (pvp)
            {
                opponentMongo = await _mongoPlayerRepository.GetByPlayerIdAsync(opponentId);
                //opponentPokemonMongo = await _mongoPlayerRepository.GetPlayerPokemonById(opponentMongo._id, opponentPokemonId);
                opponentPokemonMongo = opponentMongo.Team[0];
            }
            else
            {
                opponentMongo = await _mongoWildPokemonRepository.GetByIdAsync(opponentId);
                opponentPokemonMongo = await _mongoWildPokemonRepository.GetPlayerPokemonById(opponentMongo._id, opponentPokemonId);
            }

            PokemonTeamMove usedMove;
            if (usedMoveName.StartsWith("item:") || usedMoveName.StartsWith("swap:"))
            {
                usedMove = PokemonMoveSelector.ConvertToActionMove(usedMoveName, index);
            }
            else
            {
                if (playerPokemonMongo.WaitingMove != null)
                {
                    usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName(playerPokemonMongo.WaitingMove.NameFr));
                }
                else
                {
                    usedMove = await _mongoPlayerRepository.GetPokemonTeamMoveByName(playerId, playerPokemonId, usedMoveName);
                    // Plus aucun PP : le Pokémon utilise Lutte
                    if (playerPokemonMongo.Moves.All(m => m.Pp <= 0))
                        usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName("Lutte"));
                }
                //usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName("Furie"));
            }

            PokemonTeam pkmnTeamToCheck;
            if (index != 0) pkmnTeamToCheck = player.Team[index];
            else pkmnTeamToCheck = playerPokemonMongo;
            if (!ValidatorMove.IsEverythingOk(player, pkmnTeamToCheck, usedMove, opponentMongo, opponentPokemonMongo, turnContext))
            {
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, opponentMongo);
                return;
            }


            PokemonTeamMove opponentMove;
            if (pvp)
            {
                if (opponentPokemonMongo.WaitingMove != null)
                {
                    opponentMongo.ChosenMove = opponentPokemonMongo.WaitingMove;
                }
                if (opponentMongo.ChosenMove == null)
                {
                    player.ChosenMove = usedMove;
                    player.ChosenIndex = index;
                    await _mongoPlayerRepository.UpdateAsync(player);
                    await Clients.Caller.SendAsync("waitingOpponent");
                }
                else
                {
                    await UseMove(player, playerPokemonMongo, usedMove, opponentMongo, opponentPokemonMongo, opponentMongo.ChosenMove, isAttacking, pvp, index, opponentMongo.ChosenIndex);
                }
            }
            else
            {
                if (opponentPokemonMongo.WaitingMove != null)
                {
                    opponentMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName(opponentPokemonMongo.WaitingMove.NameFr));
                }
                else
                {
                    opponentMove = AIChoseMove.GetARandomMove(opponentPokemonMongo)
                        ?? PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName("Lutte"));
                    //opponentMove = AIChoseMove.GetThatMove(opponentPokemonMongo, "Bouclier");
                }
                // Pierres et Super Bonbon : l'adversaire ne joue pas (skipTurn envoyé par le client)
                bool skipOpponent = skipTurn && usedMove.Type == "item" && usedMove.DamageType == "special";
                await UseMove(player, playerPokemonMongo, usedMove, opponentMongo, opponentPokemonMongo, opponentMove, isAttacking, pvp, index, opponentMongo.ChosenIndex, skipOpponent);
            }


        }
    }
}
