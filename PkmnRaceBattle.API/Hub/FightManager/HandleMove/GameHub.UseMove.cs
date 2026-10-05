using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.Randomness;
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
using PkmnRaceBattle.Persistence.Helpers;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        private static void ConsumePp(PokemonTeam pokemon, PokemonTeamMove move)
        {
            if (move == null || move.Type == "item" || move.Type == "swap" || pokemon.WaitingMove != null) return;
            PokemonTeamMove? known = pokemon.Moves?.FirstOrDefault(m => m.NameFr == move.NameFr);
            if (known != null && known.Pp > 0) known.Pp--;
        }

        public async Task UseMove(PlayerMongo player, PokemonTeam playerPokemonMongo, PokemonTeamMove usedMove, PlayerMongo opponentMongo, PokemonTeam opponentPokemonMongo, PokemonTeamMove opponentMove, bool isAttacking, bool pvp, int indexPlayer = 0, int indexOponnent = 0, bool skipTurn = false)
        {
            TurnContext turnContext = new TurnContext();
            string playerId = player._id;
            string opponentId = opponentMongo._id;

            player.ChosenMove = null;
            opponentMongo.ChosenMove = null;
            player.ChosenIndex = 0;
            opponentMongo.ChosenIndex = 0;

            playerPokemonMongo.HavePlayed = true;
            bool playerDeathNotified = false;

            // Chaque capacité utilisée consomme 1 PP (pas le deuxième tour d'une attaque en deux tours, ni Lutte)
            ConsumePp(playerPokemonMongo, usedMove);
            ConsumePp(opponentPokemonMongo, opponentMove);
            string opponentConnectionId = "";
            if (pvp) opponentConnectionId = UserConnectionManager.GetConnectionId(opponentMongo._id, opponentMongo.RoomId);

            PokemonTeam playerPokemon;
            if (playerPokemonMongo.Substitute != null)
            {
                playerPokemon = (PokemonTeam)playerPokemonMongo.Substitute.Clone();
            }
            else
            {
                playerPokemon = (PokemonTeam)playerPokemonMongo.Clone();
            }

            PokemonTeam opponentPokemon;
            if (opponentPokemonMongo.Substitute != null)
            {
                opponentPokemon = (PokemonTeam)opponentPokemonMongo.Substitute.Clone();
            }
            else
            {
                opponentPokemon = (PokemonTeam)opponentPokemonMongo.Clone();
            }
            int catchValue = 0;

            playerPokemon = FightAilmentMove.TryRemoveAilment(playerPokemon, turnContext);
            opponentPokemon = FightAilmentMove.TryRemoveAilment(opponentPokemon, turnContext);

            if (FightPriority.IsPlayingFirst(playerPokemon, usedMove, opponentPokemon, opponentMove))
            {//Joueur joue en premier

                if (usedMove.NameFr == "Métronome")
                {
                    turnContext.AddMessage(playerPokemon.NameFr + " lance Métronome");
                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await MetronomeMoveProvider.GetMoveAsync());
                    usedMove = newMove;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                }

                if (usedMove.Type == "item")
                {
                    player.Items.FirstOrDefault(i => i.Name == usedMove.NameFr).Number -= 1;
                    turnContext.AddPrioMessage(player.Name + " utilise " + usedMove.NameFr);
                }
                else if (usedMove.Type == "swap") turnContext.AddPrioMessage(player.Name + " change de Pokémon");
                else turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);

                if (usedMove.NameFr.EndsWith("ball"))
                {
                    await Clients.Caller.SendAsync("launchBall", usedMove.NameFr, turnContext);
                    catchValue = FightCatch.TryCatchPokemon(opponentPokemon, usedMove.NameFr);
                    await GameDelay.Wait(1000);
                    await Clients.Caller.SendAsync("catchResult", catchValue);
                    if (catchValue == -1)
                    {
                        await GameDelay.Wait(1000);
                    }
                    else
                    {
                        await GameDelay.Wait(1000 * (catchValue + 1));
                    }

                }
                else
                {
                    if (usedMove.NameFr == "swap")
                    {
                        await Clients.Caller.SendAsync("swapPokemon", playerPokemon, turnContext.PrioMessages[0]);
                        if (pvp) await Clients.Client(opponentConnectionId).SendAsync("foeSwapPokemon", playerPokemon, turnContext.PrioMessages[0]);
                        await GameDelay.Wait(2000);
                    }
                    else
                    {
                        if (FightPriority.MoveMustBePlayedLast(usedMove) || FightPerformMove.SpecialCaseFail(playerPokemon, usedMove, opponentPokemon, opponentMove))
                        {
                            turnContext.AddMessage("Mais cela michou");
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                            turnContext = new();
                        }
                        else
                        {
                            if (FightPerformMove.IsFieldChangeMove(usedMove)) player = FightPerformMove.FieldChangeMove(usedMove, player, turnContext);

                            if (indexPlayer != 0) playerPokemon = player.Team[indexPlayer];
                            PokemonTeam[] t1Result = FightPerformMove.PerformMove(playerPokemon, opponentPokemon, usedMove, opponentPokemon.FieldChange, turnContext, true, player);
                            playerPokemon = t1Result[0];
                            opponentPokemon = t1Result[1];
                            PokemonTeam[] t1SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(playerPokemon, opponentPokemon, usedMove, turnContext);
                            playerPokemon = t1SpeCaseResult[0];
                            opponentPokemon = t1SpeCaseResult[1];


                            if (usedMove.Type != "item")
                            {
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                                turnContext = new();
                            }


                            if (usedMove.Type == "item" && usedMove.DamageType == "special")
                            {
                                string stoneLabel = FightUseItem.GetStoneLabel(usedMove.NameFr);
                                var possibleEvolutions = playerPokemon.EvolutionDetails?.Where(evo => evo.Item != null && evo.Item == stoneLabel).ToList();

                                if (possibleEvolutions != null && possibleEvolutions.Count > 0)
                                {
                                    //A voir plus tard si différentes évolutions peuvent avoir lieu au même niveau
                                    var evolutionToUse = possibleEvolutions[0];

                                    PokemonMongo pokemonBase = await _mongoPokemonRepository.GetPokemonMongoByOGName(evolutionToUse.PokemonName);

                                    if (pokemonBase != null)
                                    {
                                        PokemonTeam EvolvedPokemon = Evolve(playerPokemon, pokemonBase);
                                        string oldPkmnName = playerPokemon.NameFr;
                                        playerPokemon = EvolvedPokemon;

                                        string message = oldPkmnName + " a évolué en " + playerPokemon.NameFr;

                                        await Clients.Caller.SendAsync("pokemonLevelUp", message, playerPokemon, new List<MoveMongo>());
                                        await GameDelay.Wait(500);
                                    }

                                }
                            }

                            playerPokemon = await CheckLevelUp(playerPokemon);
                            player.Team[indexPlayer] = playerPokemon;

                            if (usedMove.Type == "item")
                            {
                                await this._mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await Clients.Caller.SendAsync("useItemResult", turnContext, indexPlayer);
                                if (indexPlayer != 0) playerPokemon = playerPokemonMongo;
                                await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                                turnContext = new();
                            }

                            if (playerPokemon.Substitute != null)
                            {
                                playerPokemonMongo = (PokemonTeam)playerPokemon.Clone();
                                playerPokemon = (PokemonTeam)playerPokemonMongo.Substitute.Clone();
                            }

                            if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                            {
                                player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                                await FinishFight(player, opponentMongo, true);
                                return;
                            }
                            else
                            {
                                if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                            }
                        }
                    }
                }



                if (catchValue == -1)//Pokémon capturé
                {
                    await GameDelay.Wait(4000);
                    opponentMongo.Team[0] = opponentPokemon;
                    await Clients.Caller.SendAsync("caughtPokemon", opponentMongo);
                    skipTurn = true;
                }
                else
                {
                    turnContext = new();
                    if (opponentPokemon.CurrHp <= 0)//Pokémon mort
                    {
                        turnContext.AddPrioMessage(opponentPokemon.NameFr + " est K.O");

                        if (opponentPokemon.Substitute != null)
                        {
                            opponentPokemonMongo.Substitute = null;
                            opponentPokemon = opponentPokemonMongo;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                        }
                        else
                        {
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                            player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                            await FinishFight(player, opponentMongo);
                            return;
                        }
                    }
                    else
                    {
                        if (!skipTurn)
                        {
                            if (!opponentPokemon.IsFlinched)
                            {
                                if (opponentMove.NameFr == "Métronome")
                                {
                                    turnContext.AddMessage(opponentPokemon.NameFr + " lance Métronome");
                                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await MetronomeMoveProvider.GetMoveAsync());
                                    opponentMove = newMove;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                    turnContext = new();
                                }
                                if (opponentMove.NameFr == "Mimique")
                                {
                                    turnContext.AddMessage(opponentPokemon.NameFr + " lance Mimique");
                                    opponentMove = usedMove;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                    turnContext = new();
                                }


                                turnContext.AddPrioMessage(opponentPokemon.NameFr + " lance " + opponentMove.NameFr);
                                if (FightPerformMove.SpecialCaseFail(opponentPokemon, opponentMove, playerPokemon, usedMove))
                                {
                                    turnContext.AddMessage("Mais cela michou");
                                }
                                else
                                {

                                    if (FightPerformMove.IsFieldChangeMove(opponentMove)) opponentPokemon = FightPerformMove.FieldChangeMove(opponentMove, opponentPokemon, turnContext);
                                    PokemonTeam[] t2Result = FightPerformMove.PerformMove(opponentPokemon, playerPokemon, opponentMove, player.FieldChange, turnContext, false);
                                    playerPokemon = t2Result[1];
                                    opponentPokemon = t2Result[0];
                                    PokemonTeam[] t2SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(opponentPokemon, playerPokemon, opponentMove, turnContext, usedMove);
                                    playerPokemon = t2SpeCaseResult[1];
                                    opponentPokemon = t2SpeCaseResult[0];
                                    if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                                    {
                                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                        await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                                        await FinishFight(player, opponentMongo, true);
                                        return;
                                    }
                                    else
                                    {
                                        if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                        if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                        if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                        if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                                    }
                                }
                            }
                            else
                            {
                                turnContext.AddPrioMessage("La peur empêche " + opponentPokemon.NameFr + " d'attaquer");
                            }



                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                            turnContext = new();
                            if (playerPokemon.CurrHp <= 0)
                            {
                                string message = playerPokemon.NameFr + " est K.O";
                                if (playerPokemonMongo.Substitute != null)
                                {
                                    playerPokemonMongo.Substitute = null;
                                    playerPokemon = playerPokemonMongo;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                }
                                else
                                {
                                    player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                    if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                                    {
                                        await FinishFight(player, opponentMongo);
                                        return;
                                    }
                                    else
                                    {
                                        if (!playerDeathNotified)
                                        {
                                            playerDeathNotified = true;
                                            await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                            if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                                        }
                                    }
                                }
                            }
                            if (opponentPokemon.CurrHp <= 0)//Pokémon mort
                            {
                                turnContext.AddPrioMessage(opponentPokemon.NameFr + " est K.O");

                                if (opponentPokemon.Substitute != null)
                                {
                                    opponentPokemonMongo.Substitute = null;
                                    opponentPokemon = opponentPokemonMongo;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                }
                                else
                                {
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                    player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                    await FinishFight(player, opponentMongo);
                                    return;
                                }


                            }
                            turnContext = new();
                            playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext, true);
                            PokemonTeam[] specialCaseResponse1 = SufferSpecialCases(playerPokemon, opponentPokemon, turnContext, true);
                            specialCaseResponse1[0] = playerPokemon;
                            specialCaseResponse1[1] = opponentPokemon;
                            opponentPokemon = FightAilmentMove.SufferAilment(opponentPokemon, turnContext, false);
                            PokemonTeam[] specialCaseResponse2 = SufferSpecialCases(opponentPokemon, playerPokemon, turnContext, false);
                            specialCaseResponse2[0] = opponentPokemon;
                            specialCaseResponse2[1] = playerPokemon;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                        }
                    }
                }
            }
            else
            {//Adversaire joue en premier
                if (opponentMove.NameFr == "Métronome")
                {
                    turnContext.AddMessage(opponentPokemon.NameFr + " lance Métronome");
                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await MetronomeMoveProvider.GetMoveAsync());
                    opponentMove = newMove;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                }


                if (opponentMove.Type == "item")
                {
                    opponentMongo.Items.FirstOrDefault(i => i.Name == opponentMove.NameFr).Number -= 1;
                    turnContext.AddPrioMessage(opponentMongo.Name + " utilise " + opponentMove.NameFr);
                }
                else if (opponentMove.Type == "swap") turnContext.AddPrioMessage(opponentMongo.Name + " change de Pokémon");
                else turnContext.AddPrioMessage(opponentPokemon.NameFr + " lance " + opponentMove.NameFr);

                if (FightPriority.MoveMustBePlayedLast(opponentMove) || FightPerformMove.SpecialCaseFail(opponentPokemon, opponentMove, playerPokemon, usedMove))
                {
                    turnContext.AddMessage("Mais cela michou");
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                }
                else
                {
                    if (opponentMove.NameFr == "swap")
                    {
                        await Clients.Caller.SendAsync("foeSwapPokemon", opponentPokemon, turnContext.PrioMessages[0]);
                        if (pvp) await Clients.Client(opponentConnectionId).SendAsync("swapPokemon", opponentPokemon, turnContext.PrioMessages[0]);
                        await GameDelay.Wait(2000);
                    }
                    else
                    {
                        if (FightPerformMove.IsFieldChangeMove(opponentMove)) opponentPokemon = FightPerformMove.FieldChangeMove(opponentMove, opponentPokemon, turnContext);
                        if (indexOponnent != 0) opponentPokemon = opponentMongo.Team[indexOponnent];
                        PokemonTeam[] t1Result = FightPerformMove.PerformMove(opponentPokemon, playerPokemon, opponentMove, player.FieldChange, turnContext, false);
                        playerPokemon = t1Result[1];
                        opponentPokemon = t1Result[0];
                        PokemonTeam[] t1SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(opponentPokemon, playerPokemon, usedMove, turnContext);
                        playerPokemon = t1SpeCaseResult[1];
                        opponentPokemon = t1SpeCaseResult[0];

                        if (opponentMove.Type != "item")
                        {
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                            turnContext = new();
                        }

                        opponentPokemon = await CheckLevelUp(opponentPokemon);
                        opponentMongo.Team[indexOponnent] = opponentPokemon;

                        if (opponentMove.Type == "item")
                        {
                            await this._mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Clients.Client(opponentConnectionId).SendAsync("useItemResult", turnContext, indexOponnent);
                            if (indexOponnent != 0) opponentPokemon = opponentPokemonMongo;
                            await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                            turnContext = new();
                        }

                        if (opponentPokemon.Substitute != null)
                        {
                            opponentPokemonMongo = (PokemonTeam)opponentPokemon.Clone();
                            opponentPokemon = (PokemonTeam)opponentPokemonMongo.Substitute.Clone();
                        }

                        if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                        {
                            player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                            await FinishFight(player, opponentMongo, true);
                            return;
                        }
                        else
                        {
                            if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                            if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                            if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                            if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                        }

                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                    }
                }
                await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                turnContext = new();
                if (playerPokemon.CurrHp <= 0)
                {
                    string message = playerPokemon.NameFr + " est K.O";
                    if (playerPokemonMongo.Substitute != null)
                    {
                        playerPokemonMongo.Substitute = null;
                        playerPokemon = playerPokemonMongo;
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await GameDelay.Wait(turnContext.CalculateDelay());
                    }
                    else
                    {
                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                        if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                        {
                            await FinishFight(player, opponentMongo);
                            return;
                        }
                        else
                        {
                            if (!playerDeathNotified)
                            {
                                playerDeathNotified = true;
                                await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                            }
                        }
                    }
                }
                else
                {
                    if (!playerPokemon.IsFlinched)
                    {

                        if (usedMove.NameFr == "Métronome")
                        {
                            turnContext.AddMessage(playerPokemon.NameFr + " lance Métronome");
                            PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await MetronomeMoveProvider.GetMoveAsync());
                            usedMove = newMove;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                            turnContext = new();
                        }

                        if (usedMove.NameFr == "Mimique")
                        {
                            turnContext.AddMessage(playerPokemon.NameFr + " lance Mimique");
                            usedMove = opponentMove;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                            turnContext = new();
                        }

                        if (usedMove.NameFr == "Copie")
                        {
                            //opponentMove
                            var move = playerPokemon.Moves.FirstOrDefault(o => o.NameFr == "Copie");

                            int r = Array.IndexOf(playerPokemon.Moves, move);

                            playerPokemon.SavedMove = move;
                            playerPokemon.SavedMoveSlot = r;
                            playerPokemon.Moves[r] = opponentMove;

                        }

                        turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);

                        if (FightPerformMove.SpecialCaseFail(playerPokemon, usedMove, opponentPokemon, opponentMove))
                        {
                            turnContext.AddMessage("Mais cela michou");
                        }
                        else
                        {

                            if (FightPerformMove.IsFieldChangeMove(usedMove)) player = FightPerformMove.FieldChangeMove(usedMove, player, turnContext);
                            PokemonTeam[] t2Result = FightPerformMove.PerformMove(playerPokemon, opponentPokemon, usedMove, opponentPokemon.FieldChange, turnContext, true, player);
                            playerPokemon = t2Result[0];
                            opponentPokemon = t2Result[1];
                            PokemonTeam[] t2SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(playerPokemon, opponentPokemon, usedMove, turnContext, opponentMove);
                            playerPokemon = t2SpeCaseResult[0];
                            opponentPokemon = t2SpeCaseResult[1];
                            if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                            {
                                player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await GameDelay.Wait(turnContext.CalculateDelay() + 500);
                                await FinishFight(player, opponentMongo, true);
                                return;
                            }
                            else
                            {
                                if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                            }

                            if (opponentPokemon.CurrHp <= 0)
                            {
                                turnContext.AddMessage(opponentPokemon.NameFr + " est K.O");

                                if (opponentPokemon.Substitute != null)
                                {
                                    opponentPokemonMongo.Substitute = null;
                                    opponentPokemon = opponentPokemonMongo;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                }
                                else
                                {
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await GameDelay.Wait(turnContext.CalculateDelay());
                                    player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                    await FinishFight(player, opponentMongo);
                                    return;
                                }


                            }

                        }
                    }
                    else
                    {
                        turnContext.AddPrioMessage("La peur empêche " + playerPokemon.NameFr + " d'attaquer");
                    }




                    playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext, true);
                    PokemonTeam[] specialCaseResponse1 = SufferSpecialCases(playerPokemon, opponentPokemon, turnContext, true);
                    specialCaseResponse1[0] = playerPokemon;
                    specialCaseResponse1[1] = opponentPokemon;
                    opponentPokemon = FightAilmentMove.SufferAilment(opponentPokemon, turnContext, false);
                    PokemonTeam[] specialCaseResponse2 = SufferSpecialCases(opponentPokemon, playerPokemon, turnContext, false);
                    specialCaseResponse2[0] = opponentPokemon;
                    specialCaseResponse2[1] = playerPokemon;

                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                    if (opponentPokemon.CurrHp <= 0)
                    {
                        turnContext.AddMessage(opponentPokemon.NameFr + " est K.O");

                        if (opponentPokemon.Substitute != null)
                        {
                            opponentPokemonMongo.Substitute = null;
                            opponentPokemon = opponentPokemonMongo;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                        }
                        else
                        {
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                            player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                            await FinishFight(player, opponentMongo);
                            return;
                        }


                    }
                    if (playerPokemon.CurrHp <= 0)
                    {
                        if (playerPokemonMongo.Substitute != null)
                        {
                            playerPokemonMongo.Substitute = null;
                            playerPokemon = playerPokemonMongo;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await GameDelay.Wait(turnContext.CalculateDelay());
                        }
                        else
                        {
                            string message = playerPokemon.NameFr + " est K.O";
                            player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                            if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                            {
                                await FinishFight(player, opponentMongo);
                                return;
                            }
                            else
                            {
                                if (!playerDeathNotified)
                                {
                                    playerDeathNotified = true;
                                    await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                    if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                                }
                            }
                        }
                    }
                    turnContext = new();
                }

            }

            if (!skipTurn)
            {
                if (opponentPokemon.MultiTurnsMove != null)
                {
                    PokemonTeam[] response = FightPerformMove.PerformMultiTurnMove(playerPokemon, opponentPokemon, opponentPokemon.MultiTurnsMove, turnContext, false);
                    playerPokemon = response[0];
                    opponentPokemon = response[1];
                    opponentPokemon.MultiTurnsMoveCount--;
                    if (opponentPokemon.MultiTurnsMoveCount <= 0)
                    {
                        if (opponentPokemon.MultiTurnsMove.NameFr == "Entrave")
                        {
                            turnContext.AddPrioMessage(opponentPokemon.CantUseMoves[0] + " n'est plus sous entrave");
                            opponentPokemon.CantUseMoves = [];
                        }
                        opponentPokemon.MultiTurnsMoveCount = null;
                        opponentPokemon.MultiTurnsMove = null;
                    }
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                }

                if (opponentPokemon.CurrHp <= 0)
                {
                    turnContext.AddMessage(opponentPokemon.NameFr + " est K.O");

                    if (opponentPokemon.Substitute != null)
                    {
                        opponentPokemonMongo.Substitute = null;
                        opponentPokemon = opponentPokemonMongo;
                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await GameDelay.Wait(turnContext.CalculateDelay());
                    }
                    else
                    {
                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await GameDelay.Wait(turnContext.CalculateDelay());
                        turnContext = new();
                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                        await FinishFight(player, opponentMongo);
                        return;
                    }


                }

                if (playerPokemon.MultiTurnsMove != null)
                {
                    PokemonTeam[] response = FightPerformMove.PerformMultiTurnMove(opponentPokemon, playerPokemon, playerPokemon.MultiTurnsMove, turnContext, true);
                    opponentPokemon = response[0];
                    playerPokemon = response[1];
                    playerPokemon.MultiTurnsMoveCount--;
                    if (playerPokemon.MultiTurnsMoveCount <= 0)
                    {
                        if (playerPokemon.MultiTurnsMove.NameFr == "Entrave")
                        {
                            turnContext.AddPrioMessage(playerPokemon.CantUseMoves[0] + " n'est plus sous entrave");
                            playerPokemon.CantUseMoves = [];
                        }
                        playerPokemon.MultiTurnsMoveCount = null;
                        playerPokemon.MultiTurnsMove = null;
                    }
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                }

                if (playerPokemon.CurrHp <= 0)
                {
                    string message = playerPokemon.NameFr + " est K.O";
                    if (playerPokemonMongo.Substitute != null)
                    {
                        playerPokemonMongo.Substitute = null;
                        playerPokemon = playerPokemonMongo;
                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await GameDelay.Wait(turnContext.CalculateDelay());
                    }
                    else
                    {
                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                        if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                        {
                            await FinishFight(player, opponentMongo);
                            return;
                        }
                        else
                        {
                            if (!playerDeathNotified)
                            {
                                playerDeathNotified = true;
                                await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                            }
                        }
                    }

                    turnContext = new();
                }

                opponentPokemon.FieldChangeCount--;
                if (opponentPokemon.FieldChangeCount <= 0)
                {
                    if (opponentPokemon.FieldChange == "Brume") turnContext.AddMessage("La brume disparaît");
                    if (opponentPokemon.FieldChange == "Mur Lumière") turnContext.AddMessage("Mur lumière n'est plus actif");
                    if (opponentPokemon.FieldChange == "Protection") turnContext.AddMessage("Protection n'est plus actif");
                    opponentPokemon.FieldChangeCount = null;
                    opponentPokemon.FieldChange = null;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                }
                player.FieldChangeCount--;
                if (player.FieldChangeCount <= 0)
                {
                    if (player.FieldChange == "Brume") turnContext.AddMessage("La brume disparaît");
                    if (player.FieldChange == "Mur Lumière") turnContext.AddMessage("Mur lumière n'est plus actif");
                    if (player.FieldChange == "Protection") turnContext.AddMessage("Protection n'est plus actif");
                    player.FieldChangeCount = null;
                    player.FieldChange = null;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await GameDelay.Wait(turnContext.CalculateDelay());
                    turnContext = new();
                }

                if (!FightDamageMove.NeedStackDamages(playerPokemon))
                {
                    playerPokemon.BlowsTaken = 0;
                }
            }

            playerPokemon.IsFlinched = false;
            opponentPokemon.IsFlinched = false;

            if (playerPokemonMongo.Substitute != null)
            {
                playerPokemonMongo.Substitute = playerPokemon;
            }
            else
            {
                playerPokemonMongo = playerPokemon;
            }

            if (opponentPokemonMongo.Substitute != null)
            {
                opponentPokemonMongo.Substitute = opponentPokemon;
            }
            else
            {
                opponentPokemonMongo = opponentPokemon;
            }
            player.Team[0] = playerPokemonMongo;
            opponentMongo.Team[0] = opponentPokemonMongo;
            await _mongoPlayerRepository.UpdateAsync(player);
            //PlayerMongo updatedPlayer = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemonMongo, player);
            if (pvp) await _mongoPlayerRepository.UpdateAsync(opponentMongo);
            else opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);

            await Clients.Caller.SendAsync("turnFinished", player, opponentMongo);
            await Clients.Client(opponentConnectionId).SendAsync("turnFinished", opponentMongo, player);
        }
    }
}
