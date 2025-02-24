using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.PokemonJson
{
    public class StatsJson
    {
        public int base_stat {  get; set; }
        public int effort {  get; set; }
        public StatJson stat { get; set; }
    }

    public class StatJson
    {
        public string name { get; set; }
        public string url { get; set; }
    }
}
