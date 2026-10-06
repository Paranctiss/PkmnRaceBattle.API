namespace PkmnRaceBattle.API.Helpers.Randomness
{
    // Pauses du serveur pendant que le client anime les messages (remplacées par une pause nulle dans les tests)
    public static class GameDelay
    {
        // Durées (ms) calées sur la lecture côté client (src/app/shared/utils/timings.ts) : modifier les deux ensemble
        public const int Message = 700;            // un message de combat
        public const int HpChange = 300;           // une variation de PV
        public const int TurnPause = 300;          // petite pause après une action
        public const int BallShake = 600;          // une secousse de Poké Ball
        public const int StandaloneMessage = 1400; // message isolé (changement de Pokémon, capture…)

        public static Func<int, Task> Implementation { get; set; } = milliseconds => Task.Delay(milliseconds);

        public static Task Wait(int milliseconds) => Implementation(milliseconds);
    }
}
