using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.PokemonJson;

namespace PkmnRaceBattle.Domain.Models.PlayerMongo
{
    public class PlayerMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Name { get; set; }
        public string Sprite {  get; set; }
        public string RoomId { get; set; }
        public bool IsHost { get; set; } = false;
        public PokemonTeam[] Team { get; set; }
        public string? FieldChange { get; set; } = null;
        public int? FieldChangeCount { get; set; } = null;
        public int Credits { get; set; } = 3000;
        public int Jackpot { get; set; } = 0;
        public BagItem[] Items { get; set; } = [new BagItem("Potion", 5), new BagItem("Pokeball", 5)];
        public int GetAverageLevel()
        {
            int teamLevel = 0;
            for (int i = 0; i < Team.Length; i++) {
                teamLevel += Team[i].Level;
            }
            return teamLevel / Team.Length;
        }
    }

    public class BagItem
    {
        public string Name { get; set; }
        public int Number { get; set; }

        public BagItem(string name, int number)
        {
            Name = name;
            Number = number;
        }
    }

    public class PokemonTeam : ICloneable
    {
        public string? FieldChange { get; set; } = null;
        public int? FieldChangeCount { get; set; } = null;
        public string Id { get; set; }
        public int IdDex { get; set; }
        public string Name { get; set; }
        public string NameFr { get; set; }
        public int Level { get; set; }
        public int BaseXP { get; set; }
        public int CurrXP { get; set; }
        public int XpForNextLvl { get; set; }
        public int XpFromLastLvl { get; set; }
        public int BaseHp { get; set; }
        public int CurrHp { get; set; }
        public int Atk { get; set; }
        public int AtkChanges { get; set; } = 0;
        public int AtkSpe {  get; set; }
        public int AtkSpeChanges { get; set; } = 0;
        public int Def { get; set; }
        public int DefChanges { get; set; } = 0;
        public int DefSpe {  get; set; }
        public int DefSpeChanges { get; set; } = 0;
        public int Speed { get; set; }
        public int SpeedChanges {  get; set; } = 0;
        public int CritChanges { get; set; } = 0;
        public int? Weight { get; set; }
        public int AccuracyChanges { get; set; } = 0;
        public int EvasionChanges { get; set; } = 0;
        public int TauxCapture { get; set; } = 0;
        public string GrowthRate { get; set; }
        public bool IsShiny { get; set; } = false;
        public string FrontSprite { get; set; }
        public string BackSprite { get; set; }
        public int IsSleeping { get; set; } = 0;
        public int IsConfused { get; set; } = 0;
        public bool IsBurning { get; set; } = false;
        public bool IsFrozen { get; set; } = false;
        public bool IsParalyzed { get; set; } = false;
        public int IsPoisoned { get; set; } = 0;
        public int? PoisonCount { get; set; } = null;
        public bool IsFlinched {  get; set; } = false;
        public bool HavePlayed {  get; set; } = false;
        public string? Untargetable { get; set; } = null;
        public int BlowsTaken { get; set; } = 0;
        public string? BlowsTakenType { get; set; } = null;
        public List<string> CantUseMoves { get; set; } = new List<string>();
        public List<string> SpecialCases { get; set; } = new List<string>();
        public int? WaitingMoveTurns { get; set; } = null;
        public PokemonTeamMove? WaitingMove { get; set; } = null;
        public int? MultiTurnsMoveCount { get; set; } = null;
        public PokemonTeamMove? MultiTurnsMove { get; set; } = null;
        public EvolvesToMongo EvolutionDetails { get; set; }
        public TypeMongo[] Types { get; set; }
        public string? ConvertedType { get; set; } = null;

        public PokemonTeamMove[] Moves { get; set; }
        public PokemonTeamMove? SavedMove { get; set; }
        public int? SavedMoveSlot { get; set; } = null;

        public PokemonTeam? UnmorphedForm { get; set; } = null;

        public PokemonTeam? Substitute { get; set; } = null;

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public PokemonTeam CreateSubstitute(int hp)
        {
            PokemonTeam model = (PokemonTeam)this.MemberwiseClone();
            model.NameFr = model.NameFr + " (Clone)";
            model.BaseHp = hp;
            model.CurrHp = hp;
            model.BackSprite = "/assets/substituteback.png";
            model.FrontSprite= "/assets/substitute.png";

            return model;
        }
    }

    public class PokemonTeamMove
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameFr { get; set; }
        public int? Accuracy { get; set; }
        public int Pp { get; set; }
        public int? Power { get; set; }
        public int? Priority { get; set; }
        public string? Target { get; set; }
        public string Type { get; set; }
        public string DamageType { get; set; }
        public int? BrutDamages { get; set; } = null;
        public string FlavorText { get; set; }
        public string Ailment { get; set; }
        public int AilmentChance { get; set; }
        public string Category { get; set; }
        public int CritRate { get; set; }
        public int Drain { get; set; }
        public int FlinchChance { get; set; }
        public int Healing { get; set; }
        public int? MaxHits {  get; set; }
        public int? MaxTurns { get; set; }
        public int? MinHits { get; set; }
        public int? MinTurns { get; set; }
        public int StatChance { get; set; }
        public int? EffectChance { get; set; }

        public MoveStatsChanges[] StatsChanges { get; set; }
    }

    public class MoveStatsChanges
    {
        public int Changes { get; set; }
        public string Name { get; set; }
    }
}
