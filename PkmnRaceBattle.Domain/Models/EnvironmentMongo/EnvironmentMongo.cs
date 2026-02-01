using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.EnvironmentMongo
{
    public class EnvironmentMongo
    {
        public string Name { get; set; }
        public List<PokemonSpawn> PossiblePokemons { get; set; }
    }

    public class PokemonSpawn
    {
        public int PokemonId { get; set; }
        public int MinimumLevel { get; set; }
        public string Rareté { get; set; }
    }
}
