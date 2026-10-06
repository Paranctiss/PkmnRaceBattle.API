using System.Runtime.CompilerServices;
using PkmnRaceBattle.API.Helpers.Randomness;

namespace PkmnRaceBattle.Tests.Support
{
    internal static class TestSetup
    {
        // Les pauses d'animation du serveur (GameDelay) sont supprimées pour tous les tests
        [ModuleInitializer]
        internal static void Initialize()
        {
            GameDelay.Implementation = _ => Task.CompletedTask;
        }
    }
}
