using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.StatsCalculator;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.API.Helpers.PokemonGeneration
{
    public static class PokemonBaseToTeam
    {
        public static PokemonTeam ConvertBaseToTeam(PokemonMongo pokemonBase, int level, bool setShiny = false)
        {
            Random rnd = new Random();
            int result = rnd.Next(512);

            if (level <= 0) { level = 1; }

            if(pokemonBase.NameFr == "Goldy")
            {
                level = 10;
            }

            PokemonTeam pokemonTeam = new PokemonTeam()
            {
                Id = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                IdDex = pokemonBase.Id,
                Name = pokemonBase.Name,
                NameFr = pokemonBase.NameFr,
                Level = level,
                BaseXP = pokemonBase.BaseExperience,
                XpForNextLvl = PokemonExperienceCalculator.ExpForLevel(level+1, pokemonBase.GrowthRate),
                CurrXP = PokemonExperienceCalculator.ExpForLevel(level, pokemonBase.GrowthRate),
                XpFromLastLvl = PokemonExperienceCalculator.ExpForLevel(level, pokemonBase.GrowthRate),
                GrowthRate = pokemonBase.GrowthRate,
                BaseHp = PokemonStatCalculator.CalculateHp(pokemonBase.Stats.Hp, level, pokemonBase.Stats.HpE),
                CurrHp = PokemonStatCalculator.CalculateHp(pokemonBase.Stats.Hp, level, pokemonBase.Stats.HpE),
                Atk = PokemonStatCalculator.CalculateStat(pokemonBase.Stats.Atk, level, pokemonBase.Stats.AtkE),
                Def = PokemonStatCalculator.CalculateStat(pokemonBase.Stats.Def, level, pokemonBase.Stats.DefE),
                AtkSpe = PokemonStatCalculator.CalculateStat(pokemonBase.Stats.AtkSpe, level, pokemonBase.Stats.AtkSpeE),
                DefSpe = PokemonStatCalculator.CalculateStat(pokemonBase.Stats.DefSpe, level, pokemonBase.Stats.DefSpeE),
                Speed = PokemonStatCalculator.CalculateStat(pokemonBase.Stats.Speed, level, pokemonBase.Stats.SpeedE),
                TauxCapture = pokemonBase.CaptureRate,
                FrontSprite = pokemonBase.Sprites.FrontDefault,
                BackSprite = pokemonBase.Sprites.BackDefault,
                Weight = pokemonBase.Weight,
                EvolutionDetails = pokemonBase.EvolutionDetails,
                Moves = PokemonMoveSelector.SelectMoves(pokemonBase.Moves, level),
                Types = pokemonBase.Types
            };

            if(pokemonBase.NameFr == "Goldy")
            {
                pokemonTeam.Moves = PokemonMoveSelector.SelectMoves(pokemonBase.Moves, level, true);
                pokemonTeam.Def = 9999;
                pokemonTeam.DefSpe = 9999;
                pokemonTeam.Speed = 9999;
                PokemonTeamMove charge = new PokemonTeamMove();
                charge.Name = "Charge";
                charge.NameFr = "Charge";
                charge.Accuracy = 100;
                charge.Category = "damage";
                charge.Id = 33;
                charge.Pp = 35;
                charge.Power = 5;
                charge.Priority = 0;
                charge.Target = "selected-pokemon";
                charge.Type = "normal";
                charge.DamageType = "physical";
                charge.FlavorText = "ouaf ouaf";
                charge.Ailment = "none";
                charge.AilmentChance = 0;
                charge.CritRate = 0;
                charge.Drain = 0;
                charge.FlinchChance = 0;
                charge.Healing = 0;
                charge.StatChance = 0;
                charge.StatsChanges = [];
                pokemonTeam.Moves = [pokemonTeam.Moves[0], charge];
                //pokemonTeam.CurrHp = (pokemonTeam.CurrHp / 2) - 2;
            }

            if(result == 0 || setShiny == true)
            {
                pokemonTeam.IsShiny = true;
                pokemonTeam.FrontSprite = pokemonBase.Sprites.FrontShiny;
                pokemonTeam.BackSprite = pokemonBase.Sprites.BackShiny;
            }

            return pokemonTeam;
        }
    }
}
