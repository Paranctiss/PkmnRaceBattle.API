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

        // Réglages d'XP choisis par l'hôte au lancement : Multi Exp (le reste de l'équipe reçoit la moitié de l'XP)
        // et multiplicateur d'XP (1, 2 ou 5). Ils fixent aussi les paliers de niveaux des zones.
        public bool MultiXp { get; set; } = true;
        public int XpMultiplier { get; set; } = 1;
    }
}
