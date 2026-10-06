namespace PkmnRaceBattle.API.Helpers.Randomness
{
    // Pauses du serveur pendant que le client anime les messages (remplacées par une pause nulle dans les tests)
    public static class GameDelay
    {
        public static Func<int, Task> Implementation { get; set; } = milliseconds => Task.Delay(milliseconds);

        public static Task Wait(int milliseconds) => Implementation(milliseconds);
    }
}
