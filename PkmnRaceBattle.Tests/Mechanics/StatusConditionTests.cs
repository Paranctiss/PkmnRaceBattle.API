using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Helpers.PokemonStates;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Changements d'état (statuts) : règles du jeu (mix Gen 1 / moderne, cf. docs/MECANIQUES_COMBAT.md)
    public class StatusConditionTests
    {
        private static PokemonTeam Target(string name = "Ronflex", int hp = 160)
        {
            PokemonTeam pokemon = Pkmn.Create(name, 50);
            pokemon.BaseHp = hp;
            pokemon.CurrHp = hp;
            return pokemon;
        }

        private static PokemonTeam Apply(PokemonTeam target, string moveName)
        {
            var ctx = new TurnContext();
            return FightAilmentMove.PerformAilment(target, Pkmn.Move(moveName), ctx);
        }

        // Simule les tours suivants comme UseMove : début de tour (TryRemoveAilment) puis tentative d'attaque
        private static int TurnsUnableToAct(PokemonTeam pokemon, int maxTurns = 12)
        {
            int blocked = 0;
            for (int turn = 0; turn < maxTurns; turn++)
            {
                var ctx = new TurnContext();
                pokemon = FightAilmentMove.TryRemoveAilment(pokemon, ctx);
                ctx.AddPrioMessage(pokemon.NameFr + " lance Charge");
                if (FightAilmentMove.CanPokemonPlay(pokemon, ctx)) return blocked;
                blocked++;
            }
            return blocked;
        }

        // ---------- Application des statuts ----------

        [Theory]
        [InlineData("Cage Éclair", "paralysis")]
        [InlineData("Poudre Toxik", "poison")]
        [InlineData("Toxik", "toxic")]
        [InlineData("Poudre Dodo", "sleep")]
        [InlineData("Ultrason", "confusion")]
        public void CapaciteDeStatut_AppliqueLEtat(string move, string expected)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam target = Apply(Target(), move);

            switch (expected)
            {
                case "paralysis": Assert.True(target.IsParalyzed); break;
                case "poison": Assert.Equal(1, target.IsPoisoned); break;
                case "toxic": Assert.Equal(2, target.IsPoisoned); break;
                case "sleep": Assert.True(target.IsSleeping > 0); break;
                case "confusion": Assert.True(target.IsConfused > 0); break;
            }
        }

        [Fact]
        public void UnSeulStatutMajeurALaFois()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam target = Apply(Target(), "Cage Éclair");
            target = Apply(target, "Poudre Toxik");
            target = Apply(target, "Poudre Dodo");

            Assert.True(target.IsParalyzed);
            Assert.Equal(0, target.IsPoisoned);
            Assert.Equal(0, target.IsSleeping);
        }

        [Fact]
        public void Confusion_PeutSeCumulerAvecUnStatutMajeur()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam target = Apply(Target(), "Cage Éclair");
            target = Apply(target, "Onde Folie");

            Assert.True(target.IsParalyzed);
            Assert.True(target.IsConfused > 0, "La confusion est un état volatil : elle s'ajoute à la paralysie");
        }

        [Fact]
        public void TypeFeu_NePeutPasEtreBruleParUneAttaqueFeu()
        {
            using var _ = TestRandom.Neutral().AlwaysSecondaryEffect().Install();
            PokemonTeam ponyta = Target("Ponyta");
            Rules.Attack(Pkmn.Create("Mew", 50), ponyta, "Lance-Flammes");
            Assert.False(ponyta.IsBurning);
        }

        [Fact]
        public void TypeGlace_NePeutPasEtreGele()
        {
            using var _ = TestRandom.Neutral().AlwaysSecondaryEffect().Install();
            PokemonTeam lokhlass = Pkmn.Create("Lokhlass", 50).WithStats(hp: 999);
            Rules.Attack(Pkmn.Create("Mew", 50), lokhlass, "Laser Glace");
            Assert.False(lokhlass.IsFrozen);
        }

        [Fact]
        public void TypePoison_NePeutPasEtreEmpoisonne()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam abo = Apply(Target("Abo"), "Toxik");
            Assert.Equal(0, abo.IsPoisoned);
        }

        [Fact]
        public void CageEclair_NAffectePasLesTypesSol()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam taupiqueur = Pkmn.Create("Taupiqueur", 50);
            Rules.Attack(Pkmn.Create("Pikachu", 50), taupiqueur, "Cage Éclair");
            Assert.False(taupiqueur.IsParalyzed);
        }

        [Theory]
        [InlineData("Cage Éclair")]
        [InlineData("Para-Spore")]
        public void TypeElectrik_NePeutPasEtreParalyse(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam pikachu = Pkmn.Create("Pikachu", 50).WithStats(hp: 999);
            Rules.Attack(Pkmn.Create("Mew", 50), pikachu, move);
            Assert.False(pikachu.IsParalyzed);
        }

        [Theory]
        [InlineData("Poing Feu", "burn")]
        [InlineData("Poing Glace", "freeze")]
        [InlineData("Poing Éclair", "paralysis")]
        [InlineData("Dard-Venin", "poison")]
        [InlineData("Rafale Psy", "confusion")]
        public void EffetSecondaire_SeDeclencheSelonSaProbabilite(string move, string status)
        {
            static bool HasStatus(PokemonTeam p, string s) => s switch
            {
                "burn" => p.IsBurning, "freeze" => p.IsFrozen, "paralysis" => p.IsParalyzed,
                "poison" => p.IsPoisoned > 0, "confusion" => p.IsConfused > 0, _ => false
            };
            int chance = Pkmn.Move(move).AilmentChance;

            using (TestRandom.Neutral().Set(RandomPurpose.SecondaryEffect, (chance - 1) / 100.0).Install())
            {
                PokemonTeam target = Pkmn.Create("Ronflex", 50).WithStats(hp: 999);
                Rules.Attack(Pkmn.Create("Mew", 50).WithTypes("neutre"), target, move);
                Assert.True(HasStatus(target, status), $"{move} : jet sous {chance} % → {status} attendu");
            }
            using (TestRandom.Neutral().Set(RandomPurpose.SecondaryEffect, (chance + 1) / 100.0).Install())
            {
                PokemonTeam target = Pkmn.Create("Ronflex", 50).WithStats(hp: 999);
                Rules.Attack(Pkmn.Create("Mew", 50).WithTypes("neutre"), target, move);
                Assert.False(HasStatus(target, status), $"{move} : jet au-dessus de {chance} % → pas de {status}");
            }
        }

        // ---------- Paralysie ----------

        [Theory]
        [InlineData(0.24, false)]
        [InlineData(0.26, true)]
        public void Paralysie_25PourcentDeChanceDeNePasPouvoirAttaquer(double roll, bool canAct)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.StatusCheck, roll).Install();
            PokemonTeam pokemon = Target();
            pokemon.IsParalyzed = true;
            var ctx = new TurnContext();
            ctx.AddPrioMessage("Ronflex lance Charge");
            Assert.Equal(canAct, FightAilmentMove.CanPokemonPlay(pokemon, ctx));
        }

        // ---------- Sommeil ----------

        // Sommeil : compteur de 2 à 5 décrémenté au début de chaque tour, le Pokémon peut attaquer le tour de son réveil
        [Theory]
        [InlineData(TestRandom.Lowest, 1)]
        [InlineData(TestRandom.Highest, 4)]
        public void Sommeil_DureDe1A4Tours(double durationRoll, int expectedTurns)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Duration, durationRoll).Install();
            PokemonTeam pokemon = Apply(Target(), "Poudre Dodo");

            Assert.Equal(expectedTurns, TurnsUnableToAct(pokemon));
        }

        [Fact]
        public void Repos_SoigneEtEndortExactementDeuxTours()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam pokemon = Target().WithHp(20);
            pokemon.IsPoisoned = 1;

            Rules.Attack(pokemon, Target(), "Repos");

            Assert.Equal(pokemon.BaseHp, pokemon.CurrHp);
            Assert.Equal(0, pokemon.IsPoisoned);
            Assert.Equal(2, TurnsUnableToAct(pokemon));
        }

        // ---------- Gel ----------

        [Theory]
        [InlineData(0.19, 0)]
        [InlineData(0.21, 12)]
        public void Gel_20PourcentDeChanceDeDegelerChaqueTour(double roll, int expectedFrozenTurns)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.StatusCheck, roll).Install();
            PokemonTeam pokemon = Target();
            pokemon.IsFrozen = true;

            Assert.Equal(expectedFrozenTurns, TurnsUnableToAct(pokemon, maxTurns: 12));
        }

        [Fact]
        public void Gel_UneAttaqueFeuDegeleLaCible()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam pokemon = Pkmn.Create("Ronflex", 50).WithStats(hp: 999);
            pokemon.IsFrozen = true;
            Rules.Attack(Pkmn.Create("Mew", 50), pokemon, "Flammèche");
            Assert.False(pokemon.IsFrozen);
        }

        // ---------- Confusion ----------

        [Theory]
        [InlineData(0.49, false)]
        [InlineData(0.51, true)]
        public void Confusion_50PourcentDeChanceDeSeBlesser(double roll, bool attacks)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.StatusCheck, roll).Install();
            PokemonTeam pokemon = Target();
            pokemon.IsConfused = 3;
            var ctx = new TurnContext();
            ctx.AddPrioMessage("Ronflex lance Charge");
            Assert.Equal(attacks, FightAilmentMove.CanPokemonPlay(pokemon, ctx));
        }

        [Theory]
        [InlineData(TestRandom.Lowest, 1)]
        [InlineData(TestRandom.Highest, 4)]
        public void Confusion_DureDe1A4ToursDeConfusion(double durationRoll, int expectedConfusedTurns)
        {
            // Gen 1 : compteur de 2 à 5 décrémenté au début de chaque tour ; à 0 le Pokémon n'est plus confus
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Duration, durationRoll).Install();
            PokemonTeam pokemon = Apply(Target(), "Ultrason");

            int confusedTurns = 0;
            for (int turn = 0; turn < 10; turn++)
            {
                pokemon = FightAilmentMove.TryRemoveAilment(pokemon, new TurnContext());
                if (pokemon.IsConfused == 0) break;
                confusedTurns++;
            }
            Assert.Equal(expectedConfusedTurns, confusedTurns);
        }

        // ---------- Dégâts de fin de tour ----------

        [Fact]
        public void Brulure_RetireUnHuitiemeDesPVMaxParTour()
        {
            PokemonTeam pokemon = Target(hp: 160);
            pokemon.IsBurning = true;
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            Assert.Equal(140, pokemon.CurrHp);
        }

        [Fact]
        public void Poison_RetireUnHuitiemeDesPVMaxParTour()
        {
            PokemonTeam pokemon = Target(hp: 160);
            pokemon.IsPoisoned = 1;
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            Assert.Equal(140, pokemon.CurrHp);
        }

        [Fact]
        public void Toxik_DegatsCroissantsDeNSeiziemes()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam pokemon = Apply(Target(hp: 160), "Toxik");

            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            Assert.Equal(150, pokemon.CurrHp);
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            Assert.Equal(130, pokemon.CurrHp);
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            Assert.Equal(100, pokemon.CurrHp);
        }

        [Fact]
        public void Toxik_LeCompteurRepartAZeroQuandLePokemonEstRappele()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam pokemon = Apply(Target(hp: 160), "Toxik");
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());

            pokemon = PokemonStatesHelper.ResetForSwap(pokemon);
            pokemon.CurrHp = 160;
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());

            Assert.Equal(2, pokemon.IsPoisoned);
            Assert.Equal(150, pokemon.CurrHp);
        }

        [Fact]
        public void DegatsDeFinDeTour_NeDescendentPasSousZero()
        {
            PokemonTeam pokemon = Target(hp: 160).WithHp(3);
            pokemon.IsBurning = true;
            FightAilmentMove.SufferAilment(pokemon, new TurnContext());
            Assert.Equal(0, pokemon.CurrHp);
        }

        [Fact]
        public void DegatsDeFinDeTour_SontEnvoyesAuClientPourLaBarreDeVie()
        {
            // Le client n'anime la barre de vie qu'avec les variations reçues dans TurnContext.Player/Opponent.Hp
            PokemonTeam pokemon = Target(hp: 160);
            pokemon.IsPoisoned = 1;
            var ctx = new TurnContext();
            FightAilmentMove.SufferAilment(pokemon, ctx, playerSide: true);
            Assert.Equal(160 - pokemon.CurrHp, ctx.Player.Hp.Sum());
        }
    }
}
