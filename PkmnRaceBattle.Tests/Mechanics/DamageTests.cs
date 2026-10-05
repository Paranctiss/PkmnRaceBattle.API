using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Formule de dégâts : ((2N/5 + 2) x Puissance x Att / Déf) / 50 + 2, puis STAB, type, critique x1,5, aléatoire 85-100 %
    public class DamageTests
    {
        private static PokemonTeam Attacker(int level = 50, int atk = 100, int atkSpe = 100) =>
            Pkmn.Create("Mew", level).WithStats(atk: atk, atkSpe: atkSpe).WithTypes("neutre");

        private static PokemonTeam Defender(int level = 50, int def = 100, int defSpe = 100) =>
            Pkmn.Create("Ronflex", level).WithStats(hp: 999, def: def, defSpe: defSpe).WithTypes("neutre");

        [Theory]
        [InlineData(5, 40, 10, 10)]
        [InlineData(20, 40, 30, 25)]
        [InlineData(50, 80, 120, 90)]
        [InlineData(50, 120, 150, 60)]
        [InlineData(100, 100, 250, 250)]
        public void FormuleDeBase_SansAleatoire(int level, int power, int atk, int def)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeamMove move = Pkmn.Move("Charge");
            move.Power = power;

            int actual = Rules.DamageDealt(Attacker(level, atk), Defender(def: def), move);

            int expected = Rules.Damage(level, power, atk, def);
            Assert.InRange(actual, expected - 1, expected + 1);
        }

        [Fact]
        public void FacteurAleatoire_EntreEnviron85Et100Pourcent()
        {
            PokemonTeamMove move = Pkmn.Move("Ultimapoing");
            int max, min;
            using (TestRandom.Neutral().MaxDamageRoll().Install()) max = Rules.DamageDealt(Attacker(), Defender(), move);
            using (TestRandom.Neutral().MinDamageRoll().Install()) min = Rules.DamageDealt(Attacker(), Defender(), Pkmn.Move("Ultimapoing"));

            Assert.True(min < max, $"Le jet minimum ({min}) doit faire moins que le jet maximum ({max})");
            Assert.InRange(min, (int)(max * 0.85) - 1, max);
        }

        [Fact]
        public void AttaqueSpeciale_UtiliseAttaqueSpeEtDefenseSpe()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeamMove surf = Pkmn.Move("Surf");
            int dealt = Rules.DamageDealt(Attacker(atk: 10, atkSpe: 200), Defender(def: 10, defSpe: 100), surf);

            int expected = Rules.Damage(50, 90, 200, 100);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(-1)]
        [InlineData(-2)]
        [InlineData(6)]
        [InlineData(-6)]
        public void PalierAttaque_ModifieLAttaqueSelonLaTableDesPaliers(int stage)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam attacker = Attacker(atk: 200);
            attacker.AtkChanges = stage;

            int dealt = Rules.DamageDealt(attacker, Defender(def: 200), "Ultimapoing");

            int expected = Rules.Damage(50, 80, (int)(200 * Rules.StageMultiplier(stage)), 200);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(-2)]
        public void PalierDefense_ModifieLaDefenseSelonLaTableDesPaliers(int stage)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam defender = Defender(def: 200);
            defender.DefChanges = stage;

            int dealt = Rules.DamageDealt(Attacker(atk: 200), defender, "Ultimapoing");

            int expected = Rules.Damage(50, 80, 200, (int)(200 * Rules.StageMultiplier(stage)));
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Fact]
        public void Brulure_DiviseLAttaquePhysiqueParDeux()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam attacker = Attacker(atk: 200);
            attacker.IsBurning = true;

            int dealt = Rules.DamageDealt(attacker, Defender(def: 200), "Ultimapoing");

            int expected = Rules.Damage(50, 80, 100, 200);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Fact]
        public void Brulure_NAffectePasLesAttaquesSpeciales()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam burned = Attacker(atkSpe: 200);
            burned.IsBurning = true;

            int burnedDamage = Rules.DamageDealt(burned, Defender(defSpe: 200), "Surf");
            int normalDamage = Rules.DamageDealt(Attacker(atkSpe: 200), Defender(defSpe: 200), "Surf");

            Assert.Equal(normalDamage, burnedDamage);
        }

        [Fact]
        public void Protection_DiviseParDeuxLesDegatsPhysiques()
        {
            using var _ = TestRandom.Neutral().Install();
            int normal = Rules.DamageDealt(Attacker(), Defender(), "Ultimapoing");
            int withReflect = Rules.DamageDealt(Attacker(), Defender(), "Ultimapoing", "Protection");
            int special = Rules.DamageDealt(Attacker(), Defender(), "Surf", "Protection");
            int specialNormal = Rules.DamageDealt(Attacker(), Defender(), "Surf");

            Assert.InRange(withReflect, normal / 2 - 1, normal / 2 + 1);
            Assert.Equal(specialNormal, special);
        }

        [Fact]
        public void MurLumiere_DiviseParDeuxLesDegatsSpeciaux()
        {
            using var _ = TestRandom.Neutral().Install();
            int normal = Rules.DamageDealt(Attacker(), Defender(), "Surf");
            int withScreen = Rules.DamageDealt(Attacker(), Defender(), "Surf", "Mur Lumière");
            int physical = Rules.DamageDealt(Attacker(), Defender(), "Ultimapoing", "Mur Lumière");
            int physicalNormal = Rules.DamageDealt(Attacker(), Defender(), "Ultimapoing");

            Assert.InRange(withScreen, normal / 2 - 1, normal / 2 + 1);
            Assert.Equal(physicalNormal, physical);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(50)]
        [InlineData(100)]
        public void CoupCritique_MultiplieLesDegatsPar1Virgule5(int level)
        {
            using var _ = TestRandom.Neutral().AlwaysCrit().Install();
            int dealt = Rules.DamageDealt(Attacker(level, atk: 100), Defender(def: 100), "Ultimapoing");

            int expected = Rules.Damage(level, 80, 100, 100, critical: true);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Fact]
        public void CoupCritique_AfficheUnMessage()
        {
            using var _ = TestRandom.Neutral().AlwaysCrit().Install();
            var ctx = Rules.Attack(Attacker(), Defender(), "Ultimapoing");
            Assert.True(ctx.Contains("Coup critique"), ctx.Dump());
        }

        [Fact]
        public void CoupCritique_IgnoreLesPalierDeStats()
        {
            using var _ = TestRandom.Neutral().AlwaysCrit().Install();
            PokemonTeam weakened = Attacker(atk: 100);
            weakened.AtkChanges = -6;
            PokemonTeam boosted = Defender(def: 100);
            boosted.DefChanges = 6;

            int dealt = Rules.DamageDealt(weakened, boosted, "Ultimapoing");

            int expected = Rules.Damage(50, 80, 100, 100, critical: true);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Fact]
        public void CoupCritique_IgnoreProtection()
        {
            using var _ = TestRandom.Neutral().AlwaysCrit().Install();
            int withReflect = Rules.DamageDealt(Attacker(), Defender(), "Ultimapoing", "Protection");
            int withoutReflect = Rules.DamageDealt(Attacker(), Defender(), "Ultimapoing");
            Assert.Equal(withoutReflect, withReflect);
        }

        // Taux de critique du jeu : 1/24, 1/8 pour les capacités à taux élevé (Tranche, Pince-Masse…), x2 par palier de critique
        [Theory]
        [InlineData("Charge", 0, 1 / 24.0)]
        [InlineData("Tranche", 0, 1 / 8.0)]
        [InlineData("Charge", 2, 4 / 24.0)]
        [InlineData("Pince-Masse", 2, 4 / 8.0)]
        public void TauxDeCritique(string move, int critStage, double rate)
        {
            Assert.True(IsCritical(move, rate - 0.01, critStage), $"{move} : jet sous {rate:P1} -> critique attendu");
            Assert.False(IsCritical(move, rate + 0.01, critStage), $"{move} : jet au-dessus de {rate:P1} -> pas de critique");
        }

        [Fact]
        public void Puissance_MonteLeCritiqueDeDeuxPaliers()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = Attacker();
            Rules.Attack(user, Defender(), "Puissance");
            Assert.Equal(2, user.CritChanges);
        }

        private static bool IsCritical(string moveName, double roll, int critStage)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Critical, roll).Install();
            PokemonTeam attacker = Attacker();
            attacker.CritChanges = critStage;
            return Rules.Attack(attacker, Defender(), moveName).Contains("Coup critique");
        }

        [Fact]
        public void Confusion_LePokemonSeBlesseAvecUneAttaqueDePuissance40SansType()
        {
            using var _ = TestRandom.Neutral().StatusChecksAlwaysTrigger().Install();
            PokemonTeam confused = Pkmn.Create("Mew", 50).WithStats(hp: 300, atk: 120, def: 80);
            confused.IsConfused = 3;
            PokemonTeam target = Defender();

            var ctx = Rules.Attack(confused, target, "Ultimapoing");

            int expected = Rules.Damage(50, 40, 120, 80);
            Assert.InRange(300 - confused.CurrHp, expected - 1, expected + 1);
            Assert.Equal(999, target.CurrHp);
            Assert.True(ctx.Contains("confusion"), ctx.Dump());
        }

        [Fact]
        public void Degats_NeFontPasDescendreLesPVSousZero()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam target = Defender().WithHp(5);
            Rules.Attack(Attacker(atk: 500), target, "Ultimapoing");
            Assert.Equal(0, target.CurrHp);
        }
    }
}
