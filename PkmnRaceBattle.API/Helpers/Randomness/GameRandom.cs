namespace PkmnRaceBattle.API.Helpers.Randomness
{
    // Usage d'un tirage aléatoire : permet aux tests de piloter chaque type de tirage séparément
    public enum RandomPurpose
    {
        Accuracy,        // Précision d'une attaque
        Critical,        // Coup critique
        DamageRoll,      // Facteur aléatoire des dégâts (85 % - 100 %)
        SecondaryEffect, // Chance d'effet secondaire (statut, peur, changement de stats)
        Duration,        // Durée d'un état ou d'une attaque sur plusieurs tours
        MultiHit,        // Nombre de coups d'une attaque multi-coups
        StatusCheck,     // Paralysie totale, auto-dégâts de confusion, dégel
        Catch,           // Secousses de la Poké Ball
        AiMoveChoice,    // Choix de l'attaque par l'IA
        Generation,      // Génération : niveau, shiny, chemin, dresseurs, tournoi
        SpecialMove,     // Tirages propres à une attaque (Vague Psy, Triplattaque…)
        TurnOrder        // Départage de deux Pokémon de même vitesse
    }

    public interface IRandomSource
    {
        // Entier dans [minInclusive, maxExclusive[ (même contrat que Random.Next)
        int Next(RandomPurpose purpose, int minInclusive, int maxExclusive);

        // Réel dans [0, 1[ (même contrat que Random.NextDouble)
        double NextDouble(RandomPurpose purpose);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        public int Next(RandomPurpose purpose, int minInclusive, int maxExclusive) => Random.Shared.Next(minInclusive, maxExclusive);

        public double NextDouble(RandomPurpose purpose) => Random.Shared.NextDouble();
    }

    // Point d'entrée unique de l'aléatoire du jeu (remplaçable par les tests, isolé par flux asynchrone)
    public static class GameRandom
    {
        private static readonly IRandomSource DefaultSource = new SystemRandomSource();
        private static readonly AsyncLocal<IRandomSource?> OverrideSource = new();

        public static IRandomSource Current => OverrideSource.Value ?? DefaultSource;

        public static IDisposable Use(IRandomSource source)
        {
            IRandomSource? previous = OverrideSource.Value;
            OverrideSource.Value = source;
            return new Restore(() => OverrideSource.Value = previous);
        }

        public static int Next(RandomPurpose purpose, int minInclusive, int maxExclusive) => Current.Next(purpose, minInclusive, maxExclusive);

        public static int Next(RandomPurpose purpose, int maxExclusive) => Current.Next(purpose, 0, maxExclusive);

        public static double NextDouble(RandomPurpose purpose) => Current.NextDouble(purpose);

        private sealed class Restore(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }
}
