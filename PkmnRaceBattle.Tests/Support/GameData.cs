using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PkmnRaceBattle.Domain.Models.EnvironmentMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;

namespace PkmnRaceBattle.Tests.Support
{
    // Données réelles du jeu (export de la base MongoDB) chargées une seule fois pour tous les tests.
    // Fixtures/pokemon.json = collection Pokemon (+ niveaux d'évolution des évolutions par échange, cf. Fixtures/README.md)
    // Fixtures/moves.json   = collection Move
    // Fixtures/environments.json = collection Environment
    public static class GameData
    {
        private static readonly Lazy<List<PokemonMongo>> LazyPokemons = new(() => Load<PokemonMongo>("pokemon.json"));
        private static readonly Lazy<List<MoveMongo>> LazyMoves = new(() => Load<MoveMongo>("moves.json"));
        private static readonly Lazy<List<EnvironmentMongo>> LazyEnvironments = new(() => Load<EnvironmentMongo>("environments.json"));

        public static IReadOnlyList<PokemonMongo> Pokemons => LazyPokemons.Value;
        public static IReadOnlyList<MoveMongo> Moves => LazyMoves.Value;
        public static IReadOnlyList<EnvironmentMongo> Environments => LazyEnvironments.Value;

        // Copie indépendante : les helpers du jeu modifient les objets reçus
        public static PokemonMongo Pokemon(int idDex) => Clone(Pokemons.Single(p => p.Id == idDex));

        public static PokemonMongo Pokemon(string nameFr) => Clone(Pokemons.Single(p => p.NameFr == nameFr));

        public static MoveMongo Move(string nameFr)
        {
            MoveMongo? move = Moves.SingleOrDefault(m => m.NameFr == nameFr);
            if (move == null) throw new ArgumentException($"Capacité inconnue dans Fixtures/moves.json : {nameFr}");
            return Clone(move);
        }

        public static T Clone<T>(T value) => BsonSerializer.Deserialize<T>(value.ToBson());

        private static List<T> Load<T>(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
            BsonArray documents = BsonSerializer.Deserialize<BsonArray>(File.ReadAllText(path));
            return documents.Select(d => BsonSerializer.Deserialize<T>(d.AsBsonDocument)).ToList();
        }
    }
}
