using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.PokemonJson
{
    public class AbilitiesJson
    {
        public AbilityJson ability {  get; set; } = new AbilityJson();
    }

    public class AbilityJson
    {
        public string name { get; set; }
        public string url { get; set; }
    }
}
