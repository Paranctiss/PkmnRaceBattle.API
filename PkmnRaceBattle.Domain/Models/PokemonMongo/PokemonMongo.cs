using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PkmnRaceBattle.Domain.Models.PokemonJson;

namespace PkmnRaceBattle.Domain.Models.PokemonMongo
{
    public class PokemonMongo
    {
        public PokemonMongo() { }
        public PokemonMongo(PokemonJson.PokemonJson pokemonJson)
        {
            Id = pokemonJson.id;
            Name = pokemonJson.name;
            NameFr = pokemonJson.species.PokemonSpecies.names.FirstOrDefault(x => x.language.name == "fr")?.name;
            BaseExperience = pokemonJson.base_experience;
            Moves = pokemonJson.moves
                 .Where(m => m.version_group_details.Any(w => w.move_learn_method.name == "level-up"
                 && (w.version_group.name == "red-blue" || w.version_group.name == "yellow")))
                 .Select(m => new MoveMongo(m))
                 .ToArray();
            BaseHappiness = pokemonJson.species.PokemonSpecies.base_happiness;
            CaptureRate = pokemonJson.species.PokemonSpecies.capture_rate;
            GrowthRate = pokemonJson.species.PokemonSpecies.growth_rate.name;
            Weight = pokemonJson.weight;
            Sprites = new SpritesMongo(pokemonJson.sprites);
            Types = pokemonJson.types
                .Select(t => new TypeMongo(t))
                .ToArray();
            Stats = new StatsMongo(pokemonJson.stats);
            EvolutionDetails = new EvolvesToMongo(pokemonJson.species, pokemonJson.name);
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; } = string.Empty;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public string? NameFr { get; set; }

        public int BaseExperience { get; set; }

        public MoveMongo[] Moves { get; set; }

        public int BaseHappiness { get; set; }

        public int CaptureRate { get; set; }

        public string GrowthRate { get; set; }

        public int Weight {  get; set; }

        public SpritesMongo Sprites { get; set; }

        public TypeMongo[] Types { get; set; }

        public StatsMongo Stats {  get; set; }

        public EvolvesToMongo EvolutionDetails { get; set; }

    }

    public class MoveMongo
    {

        public MoveMongo(MovesJson moveJson)
        {
            Id = moveJson.move.moveDetails.id;
            Name = moveJson.move.name;
            NameFr = moveJson.move.moveDetails.names.FirstOrDefault(x => x.language.name == "fr")?.name;
            Accuracy = moveJson.move.moveDetails.accuracy;
            Pp = moveJson.move.moveDetails.pp;
            Power = moveJson.move.moveDetails.power;
            Priority = moveJson.move.moveDetails.priority;
            Target = moveJson.move.moveDetails.target.name;
            Type = moveJson.move.moveDetails.type.name;
            LearnedAtLvl = moveJson.version_group_details?.FirstOrDefault(x => x.move_learn_method.name == "level-up")?.level_learned_at;
            LearnMethod = moveJson.version_group_details?.FirstOrDefault(x => x.move_learn_method.name == "level-up")?.move_learn_method.name;
            DamageType = moveJson.move.moveDetails.damage_class.name;
            FlavorText = moveJson.move.moveDetails.flavor_text_entries.FirstOrDefault(x => x.language.name == "fr").flavor_text;
            Ailment = moveJson.move.moveDetails.meta.ailment.name;
            AilmentChance = moveJson.move.moveDetails.meta.ailment_chance;
            Category = moveJson.move.moveDetails.meta.category.name;
            CritRate = moveJson.move.moveDetails.meta.crit_rate;
            Drain = moveJson.move.moveDetails.meta.drain;
            FlinchChance = moveJson.move.moveDetails.meta.flinch_chance;
            Healing = moveJson.move.moveDetails.meta.healing;
            MaxHits = moveJson.move.moveDetails.meta.max_hits;
            MaxTurns = moveJson.move.moveDetails.meta.max_turns;
            MinHits = moveJson.move.moveDetails.meta.min_hits;
            MinTurns = moveJson.move.moveDetails.meta.min_turns;
            StatChance = moveJson.move.moveDetails.meta.stat_chance;
            EffectChance = moveJson.move.moveDetails.effect_chance;
            StatChanges = moveJson.move.moveDetails.stat_changes
            .Select(sc => new StatChangesMongo
            {
                Changes = sc.change,
                Name = sc.stat.name
            })
            .ToArray() ?? [];
        }

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameFr { get; set; }
        public int? Accuracy { get; set; }
        public int Pp { get; set; }
        public int? Power { get; set; }
        public int? Priority { get; set; }
        public string? Target { get; set; }
        public string Type { get; set; }
        public int? LearnedAtLvl { get; set; }
        public string? LearnMethod { get; set; }
        public string DamageType { get; set; }
        public string FlavorText { get; set; }
        public string Ailment { get; set; }
        public int AilmentChance { get; set; }
        public string Category { get; set; }
        public int CritRate { get; set; }
        public int Drain { get; set; }
        public int FlinchChance { get; set; }
        public int Healing { get; set; }
        public int? MaxHits { get; set; }
        public int? MaxTurns { get; set; }
        public int? MinHits { get; set; }
        public int? MinTurns { get; set; }
        public int StatChance { get; set; }
        public int? EffectChance { get; set; }

        public StatChangesMongo[] StatChanges { get; set; } = [];
    }

    public class StatChangesMongo
    {
        public int Changes { get; set; }
        public string Name { get; set; }
    }

    public class SpritesMongo
    {
        public SpritesMongo(SpritesJson spritesJson)
        {
            BackDefault = spritesJson.back_default;
            BackFemale = spritesJson.back_female;
            BackShiny = spritesJson.back_shiny;
            BackShinyFemale = spritesJson.back_shiny_female;
            FrontDefault = spritesJson.front_default;
            FrontFemale = spritesJson.front_female;
            FrontShiny = spritesJson.front_shiny;
            FrontShinyFemale = spritesJson.front_shiny_female;
        }

        public string BackDefault { get; set; }
        public string BackFemale { get; set; }
        public string BackShiny { get; set; }
        public string BackShinyFemale { get; set; }
        public string FrontDefault { get; set; }
        public string FrontFemale { get; set; }
        public string FrontShiny { get; set; }
        public string FrontShinyFemale { get; set; }
    }

    public class StatsMongo
    {
        public StatsMongo(StatsJson[] statsJson) 
        {

            Hp = statsJson.FirstOrDefault(x => x.stat.name == "hp").base_stat;
            HpE = statsJson.FirstOrDefault(x => x.stat.name == "hp").effort;
            Atk = statsJson.FirstOrDefault(x => x.stat.name == "attack").base_stat;
            AtkE = statsJson.FirstOrDefault(x => x.stat.name == "attack").effort;
            Def = statsJson.FirstOrDefault(x => x.stat.name == "defense").base_stat;
            DefE = statsJson.FirstOrDefault(x => x.stat.name == "defense").effort;
            DefSpe = statsJson.FirstOrDefault(x => x.stat.name == "special-defense").base_stat;
            DefSpeE = statsJson.FirstOrDefault(x => x.stat.name == "special-defense").effort;
            AtkSpe = statsJson.FirstOrDefault(x => x.stat.name == "special-attack").base_stat;
            AtkSpeE = statsJson.FirstOrDefault(x => x.stat.name == "special-attack").effort;
            Speed = statsJson.FirstOrDefault(x => x.stat.name == "speed").base_stat;
            SpeedE = statsJson.FirstOrDefault(x => x.stat.name == "speed").effort;
        }
        public int Hp;
        public int HpE;
        public int Atk;
        public int AtkE;
        public int Def;
        public int DefE;
        public int AtkSpe;
        public int AtkSpeE;
        public int DefSpe;
        public int DefSpeE;
        public int Speed;
        public int SpeedE;
    }

    public class TypeMongo
    {
        public TypeMongo(TypesJson typesJson)
        {
            Slot = typesJson.slot;
            Name = typesJson.type.name;
        }
        public int Slot { get; set; }
        public string Name { get; set; }

    }

    public class EvolvesToMongo
    {
        public EvolvesToMongo(SpeciesJson species, string pokemonName)
        {
            //Pokemon de base
            if (species.PokemonSpecies.evolution_chain.evolutions.chain.species.name == pokemonName)
            {   //A t'il une évolution ?
                if(species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to.Length > 0)
                {   
                    PokemonName = species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].species.name;
                    MinLevel = species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].evolution_details[0].min_level;
                    EvolutionTrigger = species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].evolution_details[0].trigger.name;
                }
                else
                {
                    PokemonName = null;
                    MinLevel = null;
                    EvolutionTrigger= null;
                }
            }
            else
            {   //1ère évolution
                if (species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].species.name == pokemonName)
                {
                    //Possède une évolution supérieur
                    if (species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].evolves_to.Length > 0)
                    {
                        PokemonName = species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].evolves_to[0].species.name;
                        MinLevel = species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].evolves_to[0].evolution_details[0].min_level;
                        EvolutionTrigger = species.PokemonSpecies.evolution_chain.evolutions.chain.evolves_to[0].evolves_to[0].evolution_details[0].trigger.name;
                    }
                    else
                    {
                        PokemonName = null;
                        MinLevel = null;
                        EvolutionTrigger = null;
                    }
                }
                else //2ème évolution
                {
                    PokemonName = null;
                    MinLevel = null;
                    EvolutionTrigger = null;
                }

            }
        }
        public string? PokemonName { get; set; } 
        public int? MinLevel { get; set; }

        public string? EvolutionTrigger { get; set; }


    }
}
