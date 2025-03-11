using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.PokemonJson
{
    public class PokemonJson
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public int base_experience { get; set; }
        public int weight { get; set; }
        public AbilitiesJson[] abilities { get; set; } = [];

        public MovesJson[] moves { get; set; } = [];

        public SpeciesJson species { get; set; }

        public SpritesJson sprites { get; set; } = new SpritesJson();

        public StatsJson[] stats { get; set; } = [];

        public TypesJson[] types { get; set; } = [];
    }
}
