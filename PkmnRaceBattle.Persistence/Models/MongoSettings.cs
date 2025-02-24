using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Models
{
    public class MongoSettings
    {
        public string ConnectionString { get; set; } = null;

        public string DatabaseName { get; set; } = null;

        public string PokemonCollectionName { get; set; } = null;
        public string WildPokemonCollectionName { get; set; } = null;

        public string PlayerCollectionName { get; set; } = null;
        public string RoomCollectionName { get; set; } = null;
    }
}
