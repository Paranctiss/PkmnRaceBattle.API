using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Persistence.Helpers;

namespace PkmnRaceBattle.API.Helpers.MoveManager
{
    // Tirage de l'attaque lancée par Métronome (pokeapi en production, remplaçable par les tests)
    public static class MetronomeMoveProvider
    {
        private static readonly AsyncLocal<Func<Task<MoveMongo>>?> OverrideProvider = new();

        public static IDisposable Use(Func<Task<MoveMongo>> provider)
        {
            Func<Task<MoveMongo>>? previous = OverrideProvider.Value;
            OverrideProvider.Value = provider;
            return new Restore(() => OverrideProvider.Value = previous);
        }

        public static Task<MoveMongo> GetMoveAsync() => (OverrideProvider.Value ?? GetMoveExtApi.GetMetronomeMove)();

        private sealed class Restore(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }
}
