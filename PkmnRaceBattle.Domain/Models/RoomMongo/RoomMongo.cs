using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Domain.Models.RoomMongo
{
    public class RoomMongo
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public string roomId { get; set; }

        public int state { get; set; }
        public string hostUserId { get; set; }
    }
}
