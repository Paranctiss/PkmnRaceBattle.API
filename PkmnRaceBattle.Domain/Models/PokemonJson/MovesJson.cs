using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.PokemonJson
{
    public class MovesJson
    {
        public MoveJson move {  get; set; }

        public VersionDetailJson[] version_group_details { get; set; }
    }

    public class MoveJson
    {
        public string name { get; set; }

        public string url { get; set; }

        public MoveDetailsJson moveDetails { get; set; }

    }

    public class MoveDetailsJson
    {
        public int ?accuracy { get; set; }
        public int id { get; set; }
        public int ?crit_rate { get; set; }
        public string name { get; set; }
        public int ?power { get; set; }
        public int pp { get; set; }
        public int ?priority { get; set; }
        public int ?effect_chance { get; set; }
        public StatChangesJson[] stat_changes { get; set; } = [];
        public TargetJson target { get; set; }
        public TypeJson type { get; set; }
        public NamesJson[] names {  get; set; }
        public FlavorTextEntriesJson[] flavor_text_entries { get; set; }
        public DamageClassJson damage_class { get; set; }
        public MetaJson meta { get; set; }
    }

    public class StatChangesJson
    {
        public int change { get; set; }
        public StatNameJson stat {  get; set; }   
    }

    public class StatNameJson
    {
        public string name { get; set; }
    }

    public class TargetJson
    {
        public string name { get; set; }
    }

    public class VersionDetailJson
    {
        public int level_learned_at { get; set; }
        public MoveLearnMethodJson move_learn_method { get; set; }
        public VersionGroupJson version_group {  get; set; }
    }

    public class MoveLearnMethodJson 
    { 
        public string name { get; set; }
    }

    public class VersionGroupJson
    {
        public string name { get; set; }
    }

    public class FlavorTextEntriesJson
    {
        public string flavor_text { get; set; }
        public LanguageJson language { get; set; }
        public VersionGroupJson version_group { get; set; }
    }

    public class DamageClassJson
    {
        public string name { get; set; }
    }

    public class MetaJson
    {
        public AilmentJson ailment { get; set; }
        public int ailment_chance { get; set; }
        public CategoryJson category { get; set; }
        public int crit_rate { get; set; }
        public int drain { get; set; }
        public int flinch_chance { get; set; }
        public int healing { get; set; }
        public int? max_hits {  get; set; }
        public int? max_turns {  get; set; }
        public int? min_hits { get; set; }
        public int? min_turns { get; set; }
        public int stat_chance { get; set; }

    }

    public class AilmentJson
    {
        public string name { get; set; }
    }

    public class CategoryJson 
    {
        public string name { get; set; }
    }
}
