﻿using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.API.Helpers.MoveManager
{
    public static class PokemonMoveSelector
    {
        public static PokemonTeamMove[] SelectMoves(IEnumerable<MoveMongo> baseMoves, int level, bool goldy = false)
        {
            if (goldy)
            {
                return baseMoves
                    .OrderByDescending(m => m.Id)
                    .Select(ConvertToTeamMove)
                    .ToArray();
            }
            return baseMoves
                .Where(m => m.LearnedAtLvl <= level)
                .OrderByDescending(m => m.LearnedAtLvl)
                .ThenByDescending(m => m.Power ?? 0)
                .Take(4)
                .Select(ConvertToTeamMove)
                .ToArray();
        }

        public static PokemonTeamMove ConvertToTeamMove(MoveMongo move)
        {
            return new PokemonTeamMove
            {
                Id = move.Id,
                Name = move.Name,
                NameFr = move.NameFr,
                Accuracy = move.Accuracy,
                Pp = move.Pp,
                Power = move.Power,
                Priority = move.Priority,
                Target = move.Target,
                Type = move.Type,
                DamageType = move.DamageType,
                FlavorText = move.FlavorText,
                Ailment = move.Ailment,
                AilmentChance = move.AilmentChance,
                Category = move.Category,
                CritRate = move.CritRate,
                Drain = move.Drain,
                EffectChance = move.EffectChance,
                FlinchChance = move.FlinchChance,
                Healing = move.Healing,
                MaxHits = move.MaxHits,
                MaxTurns = move.MaxTurns,
                MinHits = move.MinHits,
                MinTurns = move.MinTurns,
                StatChance = move.StatChance,
                StatsChanges = move.StatChanges.Select(s => new MoveStatsChanges
                {
                    Changes = s.Changes,
                    Name = s.Name
                }).ToArray()
            };
        }

        public static PokemonTeamMove ConvertToActionMove(string usedMoveName, int index)
        {
            PokemonTeamMove usedMove;
            if (usedMoveName.StartsWith("item:"))
            {

                string[] itemProperties = usedMoveName.Split(':');
                string name = itemProperties[1];
                string type = itemProperties[2];

                usedMove = new PokemonTeamMove
                {
                    Accuracy = 100,
                    DamageType = type,
                    NameFr = name,
                    Power = 0,
                    Pp = 1,
                    Priority = 6,
                    StatsChanges = [],
                    Id = 0,
                    Type = "item",
                    Category = "item",
                    Name = name,
                    Target = index.ToString()
                };
            }
            else
            {
                usedMove = new PokemonTeamMove
                {
                    Accuracy = 100,
                    DamageType = "swap",
                    NameFr = "swap",
                    Power = 0,
                    Pp = 1,
                    Priority = 6,
                    StatsChanges = [],
                    Id = 0,
                    Type = "swap",
                    Name = "swap",
                    Target = "swap",
                    Category = "swap"
                };
            }

            return usedMove;
        }
    }
}
