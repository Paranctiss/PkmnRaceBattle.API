using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Hub
{
    // Changement de Pokémon et remise à zéro des états temporaires (changement, fin de combat, défaite)
    public class SwapAndResetTests
    {
        private static Task<Battle> TwoPokemonBattle(string foeMove = "Trempette") => Battle.VsWild(
            Pkmn.Create("Salamèche", 20, "Griffe", "Copie").WithStats(hp: 100, speed: 300),
            Pkmn.Create("Rattata", 20, foeMove).WithStats(hp: 999, speed: 1),
            Pkmn.Create("Carapuce", 20, "Charge").WithStats(hp: 100));

        // Tous les états qui ne doivent durer que tant que le Pokémon reste au combat
        private static void Dirty(PokemonTeam p)
        {
            p.AtkChanges = 2; p.DefChanges = -1; p.AtkSpeChanges = 1; p.DefSpeChanges = -2; p.SpeedChanges = 3;
            p.AccuracyChanges = -1; p.EvasionChanges = 2; p.CritChanges = 2;
            p.IsConfused = 2;
            p.IsFlinched = true;
            p.SpecialCases = ["Vampigraine", "Frénésie"];
            p.CantUseMoves = ["Griffe"];
            p.BlowsTaken = 30; p.BlowsTakenType = "physical";
            p.Substitute = p.CreateSubstitute(20);
            p.IsPoisoned = 2; p.PoisonCount = 4;
            p.ConvertedType = "fire"; p.Types[0].Name = "water";
            p.SavedMove = Pkmn.Move("Copie"); p.SavedMoveSlot = 1; p.Moves[1] = Pkmn.Move("Surf");
        }

        private static void AssertClean(PokemonTeam p, bool keepsStatus = true)
        {
            Assert.Equal(0, p.AtkChanges);
            Assert.Equal(0, p.DefChanges);
            Assert.Equal(0, p.AtkSpeChanges);
            Assert.Equal(0, p.DefSpeChanges);
            Assert.Equal(0, p.SpeedChanges);
            Assert.Equal(0, p.AccuracyChanges);
            Assert.Equal(0, p.EvasionChanges);
            Assert.Equal(0, p.CritChanges);
            Assert.Equal(0, p.IsConfused);
            Assert.False(p.IsFlinched);
            Assert.Empty(p.SpecialCases);
            Assert.Empty(p.CantUseMoves);
            Assert.Equal(0, p.BlowsTaken);
            Assert.Null(p.Substitute);
            Assert.Null(p.WaitingMove);
            Assert.Null(p.MultiTurnsMove);
            Assert.Null(p.Untargetable);
            Assert.Equal("fire", p.Types[0].Name);
            Assert.Null(p.ConvertedType);
            Assert.Equal("Copie", p.Moves[1].NameFr);
            Assert.Null(p.SavedMove);
            Assert.Null(p.UnmorphedForm);
            if (keepsStatus)
            {
                Assert.Equal(2, p.IsPoisoned);          // toujours gravement empoisonné...
                Assert.Equal(0, p.PoisonCount ?? 0);    // ...mais le compteur de Toxik repart de zéro
            }
        }

        [Fact]
        public async Task Changement_LePokemonChoisiPasseEnTeteDEquipe()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await TwoPokemonBattle();

            await battle.SwitchTo(1);

            Assert.Equal("Carapuce", battle.Player.Team[0].NameFr);
            Assert.Equal("Salamèche", battle.Player.Team[1].NameFr);
            SentMessage swap = battle.Received("swapPokemon").Single();
            Assert.Equal("Carapuce", swap.Arg<PokemonTeam>(0).NameFr);
            Assert.Equal("Sacha change de Pokémon", swap.Arg<string>(1));
        }

        [Fact]
        public async Task Changement_LAdversaireAttaqueLeNouveauPokemon()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await TwoPokemonBattle("Charge");

            await battle.SwitchTo(1);

            Assert.True(battle.Player.Team[0].CurrHp < 100, "Carapuce doit subir l'attaque du tour");
            Assert.Equal(100, battle.Player.Team[1].CurrHp);
        }

        [Fact]
        public async Task Changement_RemetAZeroLesEtatsTemporairesDuPokemonRappele()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await TwoPokemonBattle();
            battle.Edit(p => Dirty(p.Team[0]));

            await battle.SwitchTo(1);

            AssertClean(battle.Player.Team[1]);
        }

        [Fact]
        public async Task Changement_LeMorphingEstAnnule()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Pkmn.Create("Métamorph", 20, "Morphing").WithStats(speed: 300), Pkmn.Create("Dracaufeu", 20, "Trempette"), Pkmn.Create("Carapuce", 20));
            await battle.Use("Morphing");
            Assert.Equal("fire", battle.Mine.Types[0].Name);

            await battle.SwitchTo(1);

            PokemonTeam ditto = battle.Player.Team[1];
            Assert.Equal("normal", ditto.Types[0].Name);
            Assert.Equal(new[] { "Morphing" }, ditto.Moves.Select(m => m.NameFr));
            Assert.Null(ditto.UnmorphedForm);
        }

        [Fact]
        public async Task Changement_VersUnPokemonKO_EstRefuse()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await TwoPokemonBattle();
            battle.Edit(p => p.Team[1].CurrHp = 0);

            await battle.SwitchTo(1);

            Assert.Equal("Salamèche", battle.Mine.NameFr);
        }

        [Fact]
        public async Task ApresUnKO_LeRemplacantNeSubitPasDAttaqueGratuite()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await TwoPokemonBattle("Charge");
            battle.Edit(p => p.Team[0].CurrHp = 0);

            await battle.SwitchTo(1);

            Assert.Equal("Carapuce", battle.Mine.NameFr);
            Assert.Equal(100, battle.Mine.CurrHp);
            Assert.True(battle.Said("Sacha envoie Carapuce"));
            Assert.Single(battle.Received("turnFinished"));
        }

        // Règle du jeu : un Pokémon pris dans Ligotage / Étreinte ne peut pas être rappelé
        [Theory]
        [InlineData("Ligotage")]
        [InlineData("Étreinte")]
        public async Task PokemonPiege_NePeutPasEtreRappele(string trap)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await TwoPokemonBattle();
            battle.Edit(p => { p.Team[0].MultiTurnsMove = Pkmn.Move(trap); p.Team[0].MultiTurnsMoveCount = 3; });

            await battle.SwitchTo(1);

            Assert.Equal("Salamèche", battle.Mine.NameFr);
            Assert.True(battle.Said("ne peut pas être remplacé"));
        }

        [Fact]
        public async Task FinDeCombatGagne_RemetAZeroLesEtatsTemporairesDeTouteLEquipe()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Salamèche", 20, "Griffe", "Copie").WithStats(hp: 100, atk: 999, speed: 300),
                Pkmn.Create("Rattata", 3, "Trempette").WithStats(hp: 1),
                Pkmn.Create("Carapuce", 20, "Charge", "Copie"));
            battle.Edit(p =>
            {
                Dirty(p.Team[0]);
                p.Team[0].Substitute = null;
                p.Team[0].CantUseMoves = [];
                Dirty(p.Team[1]);
            });

            await battle.Use("Griffe");

            Assert.Single(battle.Received("responseWildFight"));
            AssertClean(battle.Player.Team[0]);
            PokemonTeam bench = battle.Player.Team[1];
            Assert.Equal(0, bench.AtkChanges);
            Assert.Equal(0, bench.AccuracyChanges);
            Assert.Equal(0, bench.EvasionChanges);
            Assert.Null(bench.Substitute);
        }

        [Fact]
        public async Task FinDeCombatGagne_LeStatutEstConserve()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Pkmn.Create("Salamèche", 20, "Griffe").WithStats(atk: 999, speed: 300), Pkmn.Create("Rattata", 3, "Trempette").WithStats(hp: 1));
            battle.Edit(p => p.Team[0].IsParalyzed = true);

            await battle.Use("Griffe");

            Assert.True(battle.Mine.IsParalyzed);
        }

        [Fact]
        public async Task FinDeCombatGagne_LesEffetsDeTerrainSontRetires()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Pkmn.Create("Salamèche", 20, "Griffe").WithStats(atk: 999, speed: 300), Pkmn.Create("Rattata", 3, "Trempette").WithStats(hp: 1));
            battle.Edit(p => { p.FieldChange = "Protection"; p.FieldChangeCount = 4; });

            await battle.Use("Griffe");

            Assert.Null(battle.Player.FieldChange);
            Assert.Null(battle.Player.FieldChangeCount);
        }

        [Fact]
        public async Task Defaite_RemetToutLEquipeAZeroStatutsCompris()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Pkmn.Create("Salamèche", 5, "Griffe", "Copie").WithStats(hp: 5, speed: 1), Pkmn.Create("Ronflex", 50, "Ultimapoing").WithStats(speed: 300));
            battle.Edit(p => { Dirty(p.Team[0]); p.Team[0].Substitute = null; p.Team[0].CantUseMoves = []; p.Team[0].IsParalyzed = true; });

            await battle.Use("Griffe");

            PokemonTeam pokemon = battle.Mine;
            AssertClean(pokemon, keepsStatus: false);
            Assert.Equal(0, pokemon.IsPoisoned);
            Assert.False(pokemon.IsParalyzed);
            Assert.Equal(pokemon.BaseHp, pokemon.CurrHp);
        }
    }
}
