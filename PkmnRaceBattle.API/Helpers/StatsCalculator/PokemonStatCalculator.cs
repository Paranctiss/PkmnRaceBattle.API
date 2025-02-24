using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.API.Helpers.StatsCalculator
{
    public static class PokemonStatCalculator
    {
        private const int DefaultIV = 31; // IV parfait
        private const int MaxEV = 252;
        private const int EvDivisor = 4;

        public static PokemonTeam CalculateAllStats(PokemonTeam pokemon, PokemonMongo pokemonBase)
        {

            int oldBaseHp = pokemon.BaseHp;
            pokemon.BaseHp = CalculateHp(pokemonBase.Stats.Hp, pokemon.Level);
            pokemon.CurrHp += pokemon.BaseHp-oldBaseHp;
            pokemon.Atk = CalculateStat(pokemonBase.Stats.Atk, pokemon.Level);
            pokemon.AtkSpe = CalculateStat(pokemonBase.Stats.AtkSpe, pokemon.Level);
            pokemon.Def = CalculateStat(pokemonBase.Stats.Def, pokemon.Level);
            pokemon.DefSpe = CalculateStat(pokemonBase.Stats.DefSpe, pokemon.Level);
            pokemon.Speed = CalculateStat(pokemonBase.Stats.Speed, pokemon.Level);
            return pokemon;
        }
        public static int CalculateHp(int baseHp, int level, int ev = 0)
        {
            ev = Math.Clamp(ev, 0, MaxEV);
            return ((2 * baseHp + DefaultIV + ev / EvDivisor) * level / 100) + level + 10;
        }

        public static int CalculateStat(int baseStat, int level, int ev = 0, float natureModifier = 1.0f)
        {
            ev = Math.Clamp(ev, 0, MaxEV);
            int rawStat = ((2 * baseStat + DefaultIV + ev / EvDivisor) * level / 100) + 5;
            return (int)(rawStat * natureModifier);
        }
    }
}
