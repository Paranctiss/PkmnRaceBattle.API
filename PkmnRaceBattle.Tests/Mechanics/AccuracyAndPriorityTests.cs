using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    public class AccuracyTests
    {
        private static bool Hits(string moveName, double roll, int accuracyStage = 0, int evasionStage = 0, Action<PokemonTeam, PokemonTeam>? setup = null)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Accuracy, roll).Install();
            PokemonTeam attacker = Pkmn.Create("Mew", 50).WithStats(speed: 200);
            PokemonTeam defender = Pkmn.Create("Ronflex", 50).WithStats(hp: 999, speed: 10);
            attacker.AccuracyChanges = accuracyStage;
            defender.EvasionChanges = evasionStage;
            setup?.Invoke(attacker, defender);
            var ctx = Rules.Attack(attacker, defender, moveName);
            return !ctx.Contains("rate son attaque");
        }

        [Theory]
        [InlineData(TestRandom.Lowest)]
        [InlineData(0.5)]
        [InlineData(TestRandom.Highest)]
        public void Precision100_ToucheToujours(double roll)
        {
            Assert.True(Hits("Charge", roll));
        }

        [Theory]
        [InlineData("Fatal-Foudre", 0.69, true)]   // 70 %
        [InlineData("Fatal-Foudre", 0.71, false)]
        [InlineData("Hydrocanon", 0.79, true)]     // 80 %
        [InlineData("Hydrocanon", 0.81, false)]
        [InlineData("Poudre Dodo", 0.74, true)]    // 75 %
        [InlineData("Poudre Dodo", 0.76, false)]
        public void ToucheSiLeJetEstInferieurALaPrecision(string move, double roll, bool expectedHit)
        {
            Assert.Equal(expectedHit, Hits(move, roll));
        }

        [Theory]
        [InlineData(-1, 0.70, true)]   // 100 % x 3/4
        [InlineData(-1, 0.80, false)]
        [InlineData(-2, 0.55, true)]   // 100 % x 3/5
        [InlineData(-2, 0.65, false)]
        public void PalierDePrecision_ReduitLaChanceDeToucher(int stage, double roll, bool expectedHit)
        {
            Assert.Equal(expectedHit, Hits("Charge", roll, accuracyStage: stage));
        }

        [Theory]
        [InlineData(1, 0.70, true)]    // 100 % x 3/4
        [InlineData(1, 0.80, false)]
        [InlineData(2, 0.55, true)]    // 100 % x 3/5
        [InlineData(2, 0.65, false)]
        public void PalierDEsquive_ReduitLaChanceDeToucher(int stage, double roll, bool expectedHit)
        {
            Assert.Equal(expectedHit, Hits("Charge", roll, evasionStage: stage));
        }

        [Fact]
        public void PrecisionEtEsquive_SeCompensent()
        {
            Assert.True(Hits("Charge", 0.95, accuracyStage: 1, evasionStage: 1));
        }

        [Fact]
        public void Meteores_NeRateJamais()
        {
            Assert.True(Hits("Météores", TestRandom.Highest, accuracyStage: -6, evasionStage: 6));
        }

        [Fact]
        public void AttaqueRatee_NInfligePasDeDegats()
        {
            using var _ = TestRandom.Neutral().AlwaysMiss().Install();
            PokemonTeam defender = Pkmn.Create("Ronflex", 50);
            int before = defender.CurrHp;
            Rules.Attack(Pkmn.Create("Mew", 50), defender, "Fatal-Foudre");
            Assert.Equal(before, defender.CurrHp);
        }

        [Fact]
        public void PokemonSousVol_EstIntouchable()
        {
            // Pendant le tour de préparation de Vol / Tunnel, les attaques adverses échouent
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam flying = Pkmn.Create("Roucarnage", 50);
            flying.Untargetable = "Vol";
            int before = flying.CurrHp;

            Rules.Attack(Pkmn.Create("Mew", 50), flying, "Charge");

            Assert.Equal(before, flying.CurrHp);
        }

        // Attaques K.O. en un coup : précision = 30 % + (niveau du lanceur - niveau de la cible), comme le code le prévoit
        [Theory]
        [InlineData("Guillotine")]
        [InlineData("Empal’Korne")]
        [InlineData("Abîme")]
        public void AttaqueKO_PrecisionDe30PlusLEcartDeNiveau(string move)
        {
            Assert.True(OhkoKnocksOut(move, 0.38, attackerLevel: 60, defenderLevel: 50));
            Assert.False(OhkoKnocksOut(move, 0.42, attackerLevel: 60, defenderLevel: 50));
            Assert.True(OhkoKnocksOut(move, 0.18, attackerLevel: 40, defenderLevel: 50));
            Assert.False(OhkoKnocksOut(move, 0.22, attackerLevel: 40, defenderLevel: 50));
        }

        [Fact]
        public void AttaqueKO_NeToucheJamaisUneCibleBeaucoupPlusForte()
        {
            Assert.False(OhkoKnocksOut("Guillotine", TestRandom.Lowest, attackerLevel: 10, defenderLevel: 60));
        }

        private static bool OhkoKnocksOut(string move, double roll, int attackerLevel, int defenderLevel, bool attackerFaster = true)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Accuracy, roll).Install();
            PokemonTeam attacker = Pkmn.Create("Krabboss", attackerLevel).WithStats(speed: attackerFaster ? 200 : 10).WithTypes("neutre");
            PokemonTeam defender = Pkmn.Create("Ronflex", defenderLevel).WithStats(speed: attackerFaster ? 10 : 200).WithTypes("neutre");
            Rules.Attack(attacker, defender, move);
            return defender.CurrHp == 0;
        }
    }

    public class PriorityTests
    {
        private static bool PlayerFirst(PokemonTeam player, string playerMove, PokemonTeam opponent, string opponentMove) =>
            FightPriority.IsPlayingFirst(player, Pkmn.Move(playerMove), opponent, Pkmn.Move(opponentMove));

        [Fact]
        public void LePlusRapideAttaqueEnPremier()
        {
            Assert.True(PlayerFirst(Pkmn.Create("Mew", 50).WithStats(speed: 120), "Charge", Pkmn.Create("Ronflex", 50).WithStats(speed: 60), "Charge"));
            Assert.False(PlayerFirst(Pkmn.Create("Mew", 50).WithStats(speed: 60), "Charge", Pkmn.Create("Ronflex", 50).WithStats(speed: 120), "Charge"));
        }

        [Fact]
        public void ViveAttaque_PasseAvantUnPokemonPlusRapide()
        {
            Assert.True(PlayerFirst(Pkmn.Create("Mew", 50).WithStats(speed: 10), "Vive-Attaque", Pkmn.Create("Ronflex", 50).WithStats(speed: 300), "Charge"));
        }

        [Fact]
        public void Riposte_PasseApresUnPokemonPlusLent()
        {
            Assert.False(PlayerFirst(Pkmn.Create("Mew", 50).WithStats(speed: 300), "Riposte", Pkmn.Create("Ronflex", 50).WithStats(speed: 10), "Charge"));
        }

        [Fact]
        public void ObjetEtChangement_PassentAvantLesAttaques()
        {
            var slow = Pkmn.Create("Mew", 50).WithStats(speed: 1);
            var fast = Pkmn.Create("Ronflex", 50).WithStats(speed: 300);
            Assert.True(FightPriority.IsPlayingFirst(slow, Pkmn.Item("Potion", "potion"), fast, Pkmn.Move("Vive-Attaque")));
            Assert.True(FightPriority.IsPlayingFirst(slow, PkmnRaceBattle.API.Helpers.MoveManager.PokemonMoveSelector.ConvertToActionMove("swap:", 0), fast, Pkmn.Move("Vive-Attaque")));
        }

        [Fact]
        public void Paralysie_DiviseLaVitesseParQuatre()
        {
            var paralyzed = Pkmn.Create("Mew", 50).WithStats(speed: 200);
            paralyzed.IsParalyzed = true;
            Assert.False(PlayerFirst(paralyzed, "Charge", Pkmn.Create("Ronflex", 50).WithStats(speed: 60), "Charge"));
            Assert.True(PlayerFirst(paralyzed, "Charge", Pkmn.Create("Ronflex", 50).WithStats(speed: 40), "Charge"));
        }

        [Fact]
        public void PalierDeVitesse_EstPrisEnCompte()
        {
            var boosted = Pkmn.Create("Mew", 50).WithStats(speed: 100);
            boosted.SpeedChanges = 2;
            Assert.True(PlayerFirst(boosted, "Charge", Pkmn.Create("Ronflex", 50).WithStats(speed: 190), "Charge"));
        }

        [Fact]
        public void EgaliteDeVitesse_EstTireeAuSort()
        {
            bool Outcome(double roll)
            {
                using var _ = TestRandom.Neutral().Set(RandomPurpose.TurnOrder, roll).Install();
                return PlayerFirst(Pkmn.Create("Mew", 50).WithStats(speed: 100), "Charge", Pkmn.Create("Ronflex", 50).WithStats(speed: 100), "Charge");
            }
            Assert.NotEqual(Outcome(TestRandom.Lowest), Outcome(TestRandom.Highest));
        }
    }
}
