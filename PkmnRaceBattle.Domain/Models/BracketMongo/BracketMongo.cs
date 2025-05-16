using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PkmnRaceBattle.Domain.Models;
using PlayerMongoModel = PkmnRaceBattle.Domain.Models.PlayerMongo.PlayerMongo;

namespace PkmnRaceBattle.Domain.Models.BracketMongo
{
    public class BracketMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; } = string.Empty;
        public int NbTurn { get; set; }
        public string GameCode { get; set; }
        public List<RoundMongo> Rounds { get; set; } = [];
        public List<PlayerMongoModel> Players { get; set; } = [];
    }

    public class RoundMongo
    {
        public int RoundNumber { get; set; }
        public List<string> PlayersInRace { get; set; } = [];
    }
}
