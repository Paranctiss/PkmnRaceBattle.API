using PkmnRaceBattle.API.Helpers.Randomness;

namespace PkmnRaceBattle.Tests.Support
{
    // Aléatoire pilotable par les tests.
    // Chaque tirage est décrit par une fraction f dans [0, 1[ : un entier dans [min, max[ vaut min + floor(f * (max - min)),
    // un réel vaut f. Un test exprime donc une probabilité (« le jet tombe dans les premiers 25 % ») sans dépendre
    // de la façon exacte dont le code tire son nombre.
    public sealed class TestRandom : IRandomSource
    {
        public const double Lowest = 0.0;
        public const double Highest = 0.999999;

        private readonly Random _fallback;
        private readonly Dictionary<RandomPurpose, Queue<double>> _queued = new();
        private readonly Dictionary<RandomPurpose, double> _fixed = new();

        public TestRandom(int seed = 20250101)
        {
            _fallback = new Random(seed);
        }

        public List<(RandomPurpose Purpose, int Min, int Max, int Result)> IntCalls { get; } = new();
        public List<(RandomPurpose Purpose, double Result)> DoubleCalls { get; } = new();

        // Valeurs « neutres » : l'attaque touche, pas de critique, dégâts maximum (facteur 1), pas d'effet secondaire,
        // pas de paralysie totale / d'auto-dégâts de confusion / de dégel, durées et nombre de coups minimum,
        // l'IA choisit sa première attaque. La génération (niveau, shiny, chemin…) reste pseudo-aléatoire mais reproductible.
        public static TestRandom Neutral() => new TestRandom()
            .Set(RandomPurpose.Accuracy, Lowest)
            .Set(RandomPurpose.Critical, Highest)
            .Set(RandomPurpose.DamageRoll, Highest)
            .Set(RandomPurpose.SecondaryEffect, Highest)
            .Set(RandomPurpose.StatusCheck, Highest)
            .Set(RandomPurpose.Duration, Lowest)
            .Set(RandomPurpose.MultiHit, Lowest)
            .Set(RandomPurpose.AiMoveChoice, Lowest)
            .Set(RandomPurpose.SpecialMove, Lowest)
            .Set(RandomPurpose.Catch, Highest);

        public TestRandom Set(RandomPurpose purpose, double fraction)
        {
            _fixed[purpose] = Check(fraction);
            return this;
        }

        // Les fractions données sont consommées dans l'ordre, puis on revient à la valeur fixée
        public TestRandom Queue(RandomPurpose purpose, params double[] fractions)
        {
            if (!_queued.TryGetValue(purpose, out Queue<double>? queue))
            {
                queue = new Queue<double>();
                _queued[purpose] = queue;
            }
            foreach (double fraction in fractions) queue.Enqueue(Check(fraction));
            return this;
        }

        public TestRandom AlwaysHit() => Set(RandomPurpose.Accuracy, Lowest);
        public TestRandom AlwaysMiss() => Set(RandomPurpose.Accuracy, Highest);
        public TestRandom AlwaysCrit() => Set(RandomPurpose.Critical, Lowest);
        public TestRandom NeverCrit() => Set(RandomPurpose.Critical, Highest);
        public TestRandom AlwaysSecondaryEffect() => Set(RandomPurpose.SecondaryEffect, Lowest);
        public TestRandom NeverSecondaryEffect() => Set(RandomPurpose.SecondaryEffect, Highest);
        public TestRandom StatusChecksAlwaysTrigger() => Set(RandomPurpose.StatusCheck, Lowest);
        public TestRandom StatusChecksNeverTrigger() => Set(RandomPurpose.StatusCheck, Highest);
        public TestRandom MinDamageRoll() => Set(RandomPurpose.DamageRoll, Lowest);
        public TestRandom MaxDamageRoll() => Set(RandomPurpose.DamageRoll, Highest);

        // Installe ce générateur pour le flux asynchrone courant (à utiliser avec using)
        public IDisposable Install() => GameRandom.Use(this);

        public int Next(RandomPurpose purpose, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                IntCalls.Add((purpose, minInclusive, maxExclusive, minInclusive));
                return minInclusive;
            }
            double fraction = NextFraction(purpose);
            int result = minInclusive + (int)Math.Floor(fraction * ((long)maxExclusive - minInclusive));
            result = Math.Clamp(result, minInclusive, maxExclusive - 1);
            IntCalls.Add((purpose, minInclusive, maxExclusive, result));
            return result;
        }

        public double NextDouble(RandomPurpose purpose)
        {
            double result = NextFraction(purpose);
            DoubleCalls.Add((purpose, result));
            return result;
        }

        private double NextFraction(RandomPurpose purpose)
        {
            if (_queued.TryGetValue(purpose, out Queue<double>? queue) && queue.Count > 0) return queue.Dequeue();
            if (_fixed.TryGetValue(purpose, out double value)) return value;
            return _fallback.NextDouble();
        }

        private static double Check(double fraction)
        {
            if (fraction < 0 || fraction >= 1) throw new ArgumentOutOfRangeException(nameof(fraction), "Une fraction doit être dans [0, 1[");
            return fraction;
        }
    }
}
