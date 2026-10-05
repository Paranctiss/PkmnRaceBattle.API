using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Changements de stats (paliers -6 à +6)
    public class StatStageTests
    {
        private static (PokemonTeam user, PokemonTeam foe, PkmnRaceBattle.API.Hub.TurnContext ctx) Use(string move, Action<PokemonTeam, PokemonTeam>? setup = null, string? foeField = null)
        {
            PokemonTeam user = Pkmn.Create("Mew", 50).WithStats(hp: 300);
            PokemonTeam foe = Pkmn.Create("Ronflex", 50).WithStats(hp: 999);
            setup?.Invoke(user, foe);
            var ctx = Rules.Attack(user, foe, move, foeField);
            return (user, foe, ctx);
        }

        [Theory]
        [InlineData("Danse Lames", "attack", 2)]
        [InlineData("Yoga", "attack", 1)]
        [InlineData("Affûtage", "attack", 1)]
        [InlineData("Armure", "defense", 1)]
        [InlineData("Bouclier", "defense", 2)]
        [InlineData("Acidarmure", "defense", 2)]
        [InlineData("Hâte", "speed", 2)]
        [InlineData("Reflet", "evasion", 1)]
        [InlineData("Lilliput", "evasion", 2)]
        [InlineData("Amnésie", "special-defense", 2)]
        public void CapaciteBoostante_AugmenteLaStatDuLanceur(string move, string stat, int expected)
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, foe, _) = Use(move);
            Assert.Equal(expected, Stage(user, stat));
            Assert.Equal(0, Stage(foe, stat));
        }

        [Theory]
        [InlineData("Rugissement", "attack", -1)]
        [InlineData("Mimi-Queue", "defense", -1)]
        [InlineData("Groz’Yeux", "defense", -1)]
        [InlineData("Grincement", "defense", -2)]
        [InlineData("Sécrétion", "speed", -2)]
        [InlineData("Jet de Sable", "accuracy", -1)]
        [InlineData("Brouillard", "accuracy", -1)]
        [InlineData("Flash", "accuracy", -1)]
        public void CapaciteAffaiblissante_BaisseLaStatDeLaCible(string move, string stat, int expected)
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, foe, _) = Use(move);
            Assert.Equal(expected, Stage(foe, stat));
            Assert.Equal(0, Stage(user, stat));
        }

        [Fact]
        public void Croissance_AugmenteAttaqueEtAttaqueSpe()
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, _, _) = Use("Croissance");
            Assert.Equal(1, user.AtkChanges);
            Assert.Equal(1, user.AtkSpeChanges);
        }

        [Fact]
        public void Paliers_PlafonnesAPlus6()
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, _, _) = Use("Danse Lames", (u, _) => u.AtkChanges = 5);
            Assert.Equal(6, user.AtkChanges);
        }

        [Fact]
        public void Paliers_PlafonnesAMoins6()
        {
            using var _ = TestRandom.Neutral().Install();
            var (_, foe, _) = Use("Grincement", (_, f) => f.DefChanges = -5);
            Assert.Equal(-6, foe.DefChanges);
        }

        [Fact]
        public void Paliers_AuMaximumAucunChangementNiVariationEnvoyeeAuClient()
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, _, ctx) = Use("Danse Lames", (u, _) => u.AtkChanges = 6);
            Assert.Equal(6, user.AtkChanges);
            Assert.Equal(0, ctx.Player.Atk);
        }

        [Fact]
        public void EffetSecondaireDeStat_SelonSaProbabilite()
        {
            int chance = Pkmn.Move("Psyko").StatChance; // 10 %
            using (TestRandom.Neutral().Set(RandomPurpose.SecondaryEffect, (chance - 1) / 100.0).Install())
                Assert.Equal(-1, Use("Psyko").foe.DefSpeChanges);
            using (TestRandom.Neutral().Set(RandomPurpose.SecondaryEffect, (chance + 1) / 100.0).Install())
                Assert.Equal(0, Use("Psyko").foe.DefSpeChanges);
        }

        [Fact]
        public void Brume_EmpecheLesBaissesDeStatsAdverses()
        {
            using var _ = TestRandom.Neutral().Install();
            var (_, foe, _) = Use("Rugissement", foeField: "Brume");
            Assert.Equal(0, foe.AtkChanges);
        }

        [Fact]
        public void Brume_NEmpechePasDAugmenterSesPropresStats()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = Pkmn.Create("Mew", 50);
            Rules.Attack(user, Pkmn.Create("Ronflex", 50), "Danse Lames", "Brume");
            Assert.Equal(2, user.AtkChanges);
        }

        [Fact]
        public void BueeNoire_RemetLesPaliersDesDeuxPokemonAZero()
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, foe, _) = Use("Buée Noire", (u, f) =>
            {
                u.AtkChanges = 3; u.EvasionChanges = 2; u.SpeedChanges = -2;
                f.DefChanges = -4; f.AccuracyChanges = -1; f.AtkSpeChanges = 2;
            });
            Assert.All(new[] { user.AtkChanges, user.EvasionChanges, user.SpeedChanges, foe.DefChanges, foe.AccuracyChanges, foe.AtkSpeChanges }, v => Assert.Equal(0, v));
        }

        // Le client additionne les variations reçues dans le TurnContext (ChangesBoard) : elles doivent correspondre au changement réel
        [Theory]
        [InlineData("Danse Lames")]
        [InlineData("Croissance")]
        [InlineData("Amnésie")]
        [InlineData("Hâte")]
        [InlineData("Bouclier")]
        public void VariationsEnvoyeesAuClient_CorrespondentAuxPaliersDuLanceur(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var (user, _, ctx) = Use(move);
            Assert.Equal(user.AtkChanges, ctx.Player.Atk);
            Assert.Equal(user.AtkSpeChanges, ctx.Player.AtkSpe);
            Assert.Equal(user.DefChanges, ctx.Player.Def);
            Assert.Equal(user.DefSpeChanges, ctx.Player.DefSpe);
            Assert.Equal(user.SpeedChanges, ctx.Player.Speed);
        }

        [Theory]
        [InlineData("Rugissement")]
        [InlineData("Grincement")]
        [InlineData("Sécrétion")]
        public void VariationsEnvoyeesAuClient_CorrespondentAuxPaliersDeLaCible(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var (_, foe, ctx) = Use(move);
            Assert.Equal(foe.AtkChanges, ctx.Opponent.Atk);
            Assert.Equal(foe.DefChanges, ctx.Opponent.Def);
            Assert.Equal(foe.SpeedChanges, ctx.Opponent.Speed);
            Assert.Equal(0, ctx.Player.Atk + ctx.Player.Def + ctx.Player.Speed);
        }

        [Fact]
        public void VariationsEnvoyeesAuClient_QuandLAdversaireUtiliseUneCapaciteBoostante()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam foe = Pkmn.Create("Ronflex", 50);
            var ctx = Rules.Attack(foe, Pkmn.Create("Mew", 50), Pkmn.Move("Danse Lames"), playerAttacking: false);
            Assert.Equal(2, ctx.Opponent.Atk);
            Assert.Equal(0, ctx.Player.Atk);
        }

        private static int Stage(PokemonTeam p, string stat) => stat switch
        {
            "attack" => p.AtkChanges,
            "defense" => p.DefChanges,
            "speed" => p.SpeedChanges,
            "special-attack" => p.AtkSpeChanges,
            "special-defense" => p.DefSpeChanges,
            "accuracy" => p.AccuracyChanges,
            "evasion" => p.EvasionChanges,
            _ => throw new ArgumentException(stat)
        };
    }
}
