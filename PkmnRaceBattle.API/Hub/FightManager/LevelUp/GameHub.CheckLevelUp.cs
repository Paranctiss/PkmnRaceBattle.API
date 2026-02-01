using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.StatsCalculator;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task<PokemonTeam> CheckLevelUp(PokemonTeam team)
        {
            List<MoveMongo> movesToLearn = new List<MoveMongo>();
            bool evolvedThisTurn = false;
            string learnedMove = "";
            int oldLevel = team.Level;
            string oldPkmnName = "";
            while (PokemonExperienceCalculator.ExpToNextLevel(team) <= 0)
            {
                team.Level++;

                PokemonMongo pokemonBase;

                var possibleEvolutions = team.EvolutionDetails?.Where(evo => evo.MinLevel != null && evo.MinLevel <= team.Level).ToList();

                if (possibleEvolutions != null && possibleEvolutions.Count > 0)
                {
                    //A voir plus tard si différentes évolutions peuvent avoir lieu au même niveau 
                    var evolutionToUse = possibleEvolutions[0];

                    pokemonBase = await _mongoPokemonRepository.GetPokemonMongoByOGName(evolutionToUse.PokemonName);
                    PokemonTeam EvolvedPokemon = PokemonBaseToTeam.ConvertBaseToTeam(pokemonBase, team.Level, team.IsShiny);
                    EvolvedPokemon.Moves = team.Moves;
                    EvolvedPokemon.CurrHp = team.CurrHp + (EvolvedPokemon.BaseHp - team.BaseHp);
                    EvolvedPokemon.CurrXP = team.CurrXP;
                    oldPkmnName = team.NameFr;
                    team = EvolvedPokemon;
                    evolvedThisTurn = true;
                }
                else
                {
                    pokemonBase = await _mongoPokemonRepository.GetPokemonMongoById(team.IdDex);
                }

                if (pokemonBase != null && pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level) != null)
                {
                    MoveMongo moveMongo = pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level);
                    if (team.Moves.FirstOrDefault(x => x.NameFr == moveMongo.NameFr) == null)
                    {
                        if (team.Moves.Length < 4)
                        {
                            PokemonTeamMove move = PokemonMoveSelector.ConvertToTeamMove(moveMongo);

                            PokemonTeamMove[] newMoves = new PokemonTeamMove[team.Moves.Length + 1];
                            Array.Copy(team.Moves, newMoves, team.Moves.Length);
                            newMoves[newMoves.Length - 1] = move;
                            team.Moves = newMoves;
                            learnedMove = move.NameFr;

                        }
                        else
                        {
                            movesToLearn.Add(moveMongo);
                        }
                    }
                }

                team.XpFromLastLvl = PokemonExperienceCalculator.ExpForLevel(team.Level, team.GrowthRate);
                team.XpForNextLvl = PokemonExperienceCalculator.ExpForLevel(team.Level + 1, team.GrowthRate);
                PokemonMongo basePokemon = await _mongoPokemonRepository.GetPokemonMongoById(team.IdDex);
                team = PokemonStatCalculator.CalculateAllStats(team, basePokemon);
            }
            if (oldLevel < team.Level)
            {
                string message;
                if (evolvedThisTurn) message = oldPkmnName + " monte niveau " + team.Level + "|" + oldPkmnName + " a évolué en " + team.NameFr;
                else message = team.NameFr + " monte niveau " + team.Level;

                if (learnedMove != "") message += "| " + oldPkmnName + " apprend " + learnedMove;

                await Clients.Caller.SendAsync("pokemonLevelUp", message, team, movesToLearn);
                int delay = 500;
                if (evolvedThisTurn) delay += 500;
                if (learnedMove != "") delay += 500;
                await Task.Delay(delay);
            }

            return team;
        }
    }
}
