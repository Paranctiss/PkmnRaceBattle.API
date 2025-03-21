using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.PokemonJson
{
    public class SpeciesJson
    {
        public string name { get; set; }

        public string url { get; set; }

        public PokemonSpeciesJson PokemonSpecies { get; set; } = new PokemonSpeciesJson();
    }

    public class PokemonSpeciesJson
    {
        public int base_happiness { get; set; }

        public int capture_rate { get; set; }

        public EvolutionChain evolution_chain { get; set; }

        public NamesJson[] names { get; set; }

        public GrowthRate growth_rate { get; set; }
    }

    public class EvolutionChain {
        public string url { get; set; }

        public Evolutions evolutions { get; set; }
    }

    public class Evolutions
    {
        public Chain chain { get; set; }
    }

    public class Chain
    {
        public EvolvesTo[] evolves_to { get; set; }
        public SpeciesJson species { get; set; }
    }

    public class EvolvesTo
    {
        public EvolutionDetails[] evolution_details { get; set;}
        public EvolvesTo[] evolves_to { get; set; } = [];

        public SpeciesJson species { get; set; }
    }

    public class EvolutionDetails
    {
        public int? min_level { get; set; }
        public Trigger trigger { get; set; }
        public Item? item { get; set; }
    }

    public class Trigger
    {
        public string name { get; set; }
    }

    public class Item
    {
        public string? name { get; set; }
    }

    public class GrowthRate {
        public string name { get; set; }
    }

    public class NamesJson
    {
        public string name {  set; get; }
        public LanguageJson language { set; get; }
    }

    public class LanguageJson
    {
        public string name { set; get; }
    }


}
