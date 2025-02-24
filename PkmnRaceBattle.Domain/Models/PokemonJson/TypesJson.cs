using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.PokemonJson
{
    public class TypesJson
    {
        public int slot {  get; set; }

        public TypeJson type { get; set; }
    }

    public class TypeJson
    {
        public string name { get; set; }

        public string url { get; set; }
    }
}
