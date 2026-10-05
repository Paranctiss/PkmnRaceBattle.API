using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.Tests.Support
{
    // Référence des règles attendues (mix Gen 1 + mécaniques modernes choisi pour le jeu, cf. docs/MECANIQUES_COMBAT.md)
    // et raccourcis pour appeler les helpers de combat
    public static class Rules
    {
        public static readonly string[] AllTypes =
            ["normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison", "ground", "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy"];

        // Table des types moderne (Gen 6+), seules les valeurs différentes de 1 sont listées (attaque -> défense)
        private static readonly Dictionary<(string, string), double> Chart = new()
        {
            [("normal", "rock")] = 0.5, [("normal", "ghost")] = 0, [("normal", "steel")] = 0.5,
            [("fire", "fire")] = 0.5, [("fire", "water")] = 0.5, [("fire", "grass")] = 2, [("fire", "ice")] = 2, [("fire", "bug")] = 2, [("fire", "rock")] = 0.5, [("fire", "dragon")] = 0.5, [("fire", "steel")] = 2,
            [("water", "fire")] = 2, [("water", "water")] = 0.5, [("water", "grass")] = 0.5, [("water", "ground")] = 2, [("water", "rock")] = 2, [("water", "dragon")] = 0.5,
            [("electric", "water")] = 2, [("electric", "electric")] = 0.5, [("electric", "grass")] = 0.5, [("electric", "ground")] = 0, [("electric", "flying")] = 2, [("electric", "dragon")] = 0.5,
            [("grass", "fire")] = 0.5, [("grass", "water")] = 2, [("grass", "grass")] = 0.5, [("grass", "poison")] = 0.5, [("grass", "ground")] = 2, [("grass", "flying")] = 0.5, [("grass", "bug")] = 0.5, [("grass", "rock")] = 2, [("grass", "dragon")] = 0.5, [("grass", "steel")] = 0.5,
            [("ice", "fire")] = 0.5, [("ice", "water")] = 0.5, [("ice", "grass")] = 2, [("ice", "ice")] = 0.5, [("ice", "ground")] = 2, [("ice", "flying")] = 2, [("ice", "dragon")] = 2, [("ice", "steel")] = 0.5,
            [("fighting", "normal")] = 2, [("fighting", "ice")] = 2, [("fighting", "poison")] = 0.5, [("fighting", "flying")] = 0.5, [("fighting", "psychic")] = 0.5, [("fighting", "bug")] = 0.5, [("fighting", "rock")] = 2, [("fighting", "ghost")] = 0, [("fighting", "dark")] = 2, [("fighting", "steel")] = 2, [("fighting", "fairy")] = 0.5,
            [("poison", "grass")] = 2, [("poison", "poison")] = 0.5, [("poison", "ground")] = 0.5, [("poison", "rock")] = 0.5, [("poison", "ghost")] = 0.5, [("poison", "steel")] = 0, [("poison", "fairy")] = 2,
            [("ground", "fire")] = 2, [("ground", "electric")] = 2, [("ground", "grass")] = 0.5, [("ground", "poison")] = 2, [("ground", "flying")] = 0, [("ground", "bug")] = 0.5, [("ground", "rock")] = 2, [("ground", "steel")] = 2,
            [("flying", "electric")] = 0.5, [("flying", "grass")] = 2, [("flying", "fighting")] = 2, [("flying", "bug")] = 2, [("flying", "rock")] = 0.5, [("flying", "steel")] = 0.5,
            [("psychic", "fighting")] = 2, [("psychic", "poison")] = 2, [("psychic", "psychic")] = 0.5, [("psychic", "dark")] = 0, [("psychic", "steel")] = 0.5,
            [("bug", "fire")] = 0.5, [("bug", "grass")] = 2, [("bug", "fighting")] = 0.5, [("bug", "poison")] = 0.5, [("bug", "flying")] = 0.5, [("bug", "psychic")] = 2, [("bug", "ghost")] = 0.5, [("bug", "dark")] = 2, [("bug", "steel")] = 0.5, [("bug", "fairy")] = 0.5,
            [("rock", "fire")] = 2, [("rock", "ice")] = 2, [("rock", "fighting")] = 0.5, [("rock", "ground")] = 0.5, [("rock", "flying")] = 2, [("rock", "bug")] = 2, [("rock", "steel")] = 0.5,
            [("ghost", "normal")] = 0, [("ghost", "psychic")] = 2, [("ghost", "ghost")] = 2, [("ghost", "dark")] = 0.5,
            [("dragon", "dragon")] = 2, [("dragon", "steel")] = 0.5, [("dragon", "fairy")] = 0,
            [("dark", "fighting")] = 0.5, [("dark", "psychic")] = 2, [("dark", "ghost")] = 2, [("dark", "dark")] = 0.5, [("dark", "fairy")] = 0.5,
            [("steel", "fire")] = 0.5, [("steel", "water")] = 0.5, [("steel", "electric")] = 0.5, [("steel", "ice")] = 2, [("steel", "rock")] = 2, [("steel", "steel")] = 0.5, [("steel", "fairy")] = 2,
            [("fairy", "fire")] = 0.5, [("fairy", "fighting")] = 2, [("fairy", "poison")] = 0.5, [("fairy", "dragon")] = 2, [("fairy", "dark")] = 2, [("fairy", "steel")] = 0.5,
        };

        public static double TypeMultiplier(string attackType, string defenseType) =>
            Chart.TryGetValue((attackType, defenseType), out double value) ? value : 1.0;

        public static double TypeMultiplier(string attackType, IEnumerable<string> defenseTypes) =>
            defenseTypes.Aggregate(1.0, (acc, t) => acc * TypeMultiplier(attackType, t));

        // Formule de dégâts du jeu (sans aléatoire) : coup critique x1,5
        public static int Damage(int level, int power, int attack, int defense, double stab = 1.0, double type = 1.0, bool critical = false, double roll = 1.0)
        {
            double damage = (2 * level / 5.0 + 2) * power * attack / defense / 50 + 2;
            return (int)(damage * stab * type * (critical ? 1.5 : 1.0) * roll);
        }

        // Paliers de stats modernes : (2 + n) / 2 et 2 / (2 - n)
        public static double StageMultiplier(int stage) => stage >= 0 ? (2 + stage) / 2.0 : 2.0 / (2 - stage);

        // Paliers de précision / esquive modernes : (3 + n) / 3 et 3 / (3 - n)
        public static double AccuracyStageMultiplier(int stage) => stage >= 0 ? (3 + stage) / 3.0 : 3.0 / (3 - stage);

        public static int ExpForLevel(int level, string growthRate) => growthRate switch
        {
            "fast" => 4 * level * level * level / 5,
            "medium" => level * level * level,
            "medium-slow" => 6 * level * level * level / 5 - 15 * level * level + 100 * level - 140,
            "slow" => 5 * level * level * level / 4,
            _ => throw new ArgumentException(growthRate)
        };

        // Lance une attaque via FightPerformMove.PerformMove comme le fait UseMove (message « X lance Y » préalable)
        public static TurnContext Attack(PokemonTeam attacker, PokemonTeam defender, PokemonTeamMove move, string? defenderField = null, bool playerAttacking = true, PlayerMongo? player = null)
        {
            var ctx = new TurnContext();
            ctx.AddPrioMessage(attacker.NameFr + " lance " + move.NameFr);
            PokemonTeam[] result = FightPerformMove.PerformMove(attacker, defender, move, defenderField!, ctx, playerAttacking, player!);
            CopyInto(result[0], attacker);
            CopyInto(result[1], defender);
            return ctx;
        }

        public static TurnContext Attack(PokemonTeam attacker, PokemonTeam defender, string moveNameFr, string? defenderField = null, bool playerAttacking = true) =>
            Attack(attacker, defender, Pkmn.Move(moveNameFr), defenderField, playerAttacking);

        public static int DamageDealt(PokemonTeam attacker, PokemonTeam defender, PokemonTeamMove move, string? defenderField = null)
        {
            int before = defender.CurrHp;
            Attack(attacker, defender, move, defenderField);
            return before - defender.CurrHp;
        }

        public static int DamageDealt(PokemonTeam attacker, PokemonTeam defender, string moveNameFr, string? defenderField = null) =>
            DamageDealt(attacker, defender, Pkmn.Move(moveNameFr), defenderField);

        private static void CopyInto(PokemonTeam source, PokemonTeam target)
        {
            if (ReferenceEquals(source, target)) return;
            foreach (var property in typeof(PokemonTeam).GetProperties().Where(p => p.CanWrite))
                property.SetValue(target, property.GetValue(source));
        }

        public static bool Contains(this TurnContext ctx, string fragment) =>
            ctx.Messages.Concat(ctx.PrioMessages).Any(m => m.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        public static string Dump(this TurnContext ctx) => string.Join(" | ", ctx.PrioMessages.Concat(ctx.Messages));
    }
}
