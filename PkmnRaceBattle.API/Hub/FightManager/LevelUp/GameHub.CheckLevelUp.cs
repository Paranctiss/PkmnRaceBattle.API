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
using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.StatsCalculator;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        // Nouvelle espèce : garde l'identifiant, les capacités, l'XP, les dégâts subis, le shiny et les statuts
        private static PokemonTeam Evolve(PokemonTeam pokemon, PokemonMongo evolutionBase)
        {
            PokemonTeam evolved = PokemonBaseToTeam.ConvertBaseToTeam(evolutionBase, pokemon.Level, pokemon.IsShiny);
            evolved.Id = pokemon.Id;
            evolved.Moves = pokemon.Moves;
            evolved.CurrHp = pokemon.CurrHp <= 0 ? 0 : pokemon.CurrHp + (evolved.BaseHp - pokemon.BaseHp);
            evolved.CurrXP = pokemon.CurrXP;
            evolved.HavePlayed = pokemon.HavePlayed;
            evolved.IsBurning = pokemon.IsBurning;
            evolved.IsFrozen = pokemon.IsFrozen;
            evolved.IsParalyzed = pokemon.IsParalyzed;
            evolved.IsPoisoned = pokemon.IsPoisoned;
            evolved.PoisonCount = pokemon.PoisonCount;
            evolved.IsSleeping = pokemon.IsSleeping;
            return evolved;
        }

        public async Task<PokemonTeam> CheckLevelUp(PokemonTeam team)
        {
            List<MoveMongo> movesToLearn = new List<MoveMongo>();
            bool evolvedThisTurn = false;
            string learnedMove = "";
            int oldLevel = team.Level;
            string oldPkmnName = "";
            while (team.Level < 100 && PokemonExperienceCalculator.ExpToNextLevel(team) <= 0)
            {
                team.Level++;

                PokemonMongo pokemonBase;

                var possibleEvolutions = team.EvolutionDetails?.Where(evo => evo.MinLevel != null && evo.MinLevel <= team.Level).ToList();

                // Évolution vers un Pokémon absent de la base (hors Gen 1) : ignorée
                PokemonMongo? evolutionBase = null;
                foreach (var evolution in possibleEvolutions ?? [])
                {
                    evolutionBase = await _mongoPokemonRepository.GetPokemonMongoByOGName(evolution.PokemonName);
                    if (evolutionBase != null) break;
                }

                if (evolutionBase != null)
                {
                    pokemonBase = evolutionBase;
                    oldPkmnName = team.NameFr;
                    team = Evolve(team, pokemonBase);
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
                int delay = GameDelay.TurnPause;
                if (evolvedThisTurn) delay += GameDelay.TurnPause;
                if (learnedMove != "") delay += GameDelay.TurnPause;
                await GameDelay.Wait(delay);
            }

            return team;
        }
    }
}
