using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Moves
{
    // Attaques au comportement particulier qui s'étalent sur plusieurs tours, jouées via le hub (règles du jeu, cf. docs/MECANIQUES_COMBAT.md)
    public class MultiTurnMovesTests
    {
        private static Task<Battle> Fight(string playerMove, string foeMove = "Trempette", int playerSpeed = 300, int foeSpeed = 1, int foeHp = 999, params string[] otherPlayerMoves) =>
            Battle.VsWild(
                Pkmn.Create("Mew", 50, [playerMove, .. otherPlayerMoves]).WithStats(hp: 400, speed: playerSpeed).WithTypes("neutre"),
                Pkmn.Create("Ronflex", 50, foeMove).WithStats(hp: foeHp, speed: foeSpeed).WithTypes("neutre"));

        // ---------- Attaques en deux tours ----------

        [Theory]
        [InlineData("Vol")]
        [InlineData("Tunnel")]
        public async Task VolTunnel_InvulnerableAuPremierTourPuisFrappe(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight(move, "Charge", playerSpeed: 300);

            await battle.Use(move);
            Assert.Equal(999, battle.Foe.CurrHp);
            Assert.Equal(move, battle.Mine.Untargetable);
            Assert.Equal(400, battle.Mine.CurrHp);

            await battle.Use(move);
            Assert.True(battle.Foe.CurrHp < 999);
            Assert.Null(battle.Mine.Untargetable);
            Assert.Null(battle.Mine.WaitingMove);
        }

        [Theory]
        [InlineData("Lance-Soleil")]
        [InlineData("Coupe-Vent")]
        [InlineData("Piqué")]
        [InlineData("Coud’Krâne")]
        public async Task AttaqueChargee_RienAuPremierTourDegatsAuSecond(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight(move);

            await battle.Use(move);
            Assert.Equal(999, battle.Foe.CurrHp);
            Assert.NotNull(battle.Mine.WaitingMove);

            await battle.Use(move);
            Assert.True(battle.Foe.CurrHp < 999);
            Assert.Null(battle.Mine.WaitingMove);
        }

        [Fact]
        public async Task CoudKrane_AugmenteLaDefenseAuTourDeCharge()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Coud’Krâne");
            await battle.Use("Coud’Krâne");
            Assert.Equal(1, battle.Mine.DefChanges);
            await battle.Use("Coud’Krâne");
            Assert.Equal(1, battle.Mine.DefChanges);
        }

        [Fact]
        public async Task Ultralaser_DoitSeReposerAuTourSuivant()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Ultralaser", "Charge", otherPlayerMoves: "Charge");

            await battle.Use("Ultralaser");
            int afterFirst = battle.Foe.CurrHp;
            Assert.True(afterFirst < 999, "Ultralaser frappe dès le premier tour");

            battle.ClearMessages();
            await battle.Use("Charge");
            Assert.Equal(afterFirst, battle.Foe.CurrHp);
            Assert.True(battle.Said("doit se reposer"), string.Join(" | ", battle.Dialog));
            Assert.True(battle.Said("Ronflex lance Charge"));

            await battle.Use("Charge");
            Assert.True(battle.Foe.CurrHp < afterFirst);
        }

        [Fact]
        public async Task Ultralaser_PasDeReposSiLaCibleEstKO()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Ultralaser", foeHp: 1);

            await battle.Use("Ultralaser");

            Assert.Null(battle.Mine.WaitingMove);
            Assert.Single(battle.Received("responseWildFight"));
        }

        [Fact]
        public async Task Mania_DeuxOuTroisToursPuisConfusion()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Duration, TestRandom.Lowest).Install();
            var battle = await Fight("Mania");
            var hits = new List<int>();

            for (int turn = 0; turn < 6; turn++)
            {
                int before = battle.Foe.CurrHp;
                await battle.Use("Mania");
                hits.Add(before - battle.Foe.CurrHp);
                if (battle.Mine.WaitingMove == null) break;
            }

            Assert.Equal(2, hits.Count(h => h > 0));
            Assert.True(battle.Mine.IsConfused > 0);
        }

        [Fact]
        public async Task Patience_RendLeDoubleDesDegatsSubis()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Duration, TestRandom.Lowest).Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Mew", 50, "Patience").WithStats(hp: 400, speed: 1).WithTypes("neutre"),
                Pkmn.Create("Ronflex", 50, "Charge").WithStats(hp: 999, speed: 300).WithTypes("neutre"));

            // Priorité +1 (règle moderne) : Mew encaisse les tours 1 et 2, puis frappe au tour 3 avant Ronflex
            await battle.Use("Patience");
            await battle.Use("Patience");
            int takenWhileWaiting = 400 - battle.Mine.CurrHp;
            Assert.Equal(999, battle.Foe.CurrHp);

            await battle.Use("Patience");

            Assert.True(takenWhileWaiting > 0);
            Assert.Equal(2 * takenWhileWaiting, 999 - battle.Foe.CurrHp);
            Assert.True(battle.Dialog.FindLastIndex(m => m.StartsWith("Mew lance")) < battle.Dialog.FindLastIndex(m => m.StartsWith("Ronflex lance")));
        }

        // ---------- Riposte ----------

        [Fact]
        public async Task Riposte_RenvoieLeDoubleDesDegatsDUneAttaqueNormale()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Riposte", "Coupe", playerSpeed: 300, foeSpeed: 1);

            await battle.Use("Riposte");

            int taken = 400 - battle.Mine.CurrHp;
            Assert.True(taken > 0, "Riposte doit passer après l'attaque adverse");
            Assert.Equal(2 * taken, 999 - battle.Foe.CurrHp);
        }

        [Fact]
        public async Task Riposte_RenvoieAussiUneAttaquePhysiqueDUnAutreType()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Riposte", "Poing Feu");

            await battle.Use("Riposte");

            int taken = 400 - battle.Mine.CurrHp;
            Assert.Equal(2 * taken, 999 - battle.Foe.CurrHp);
        }

        [Fact]
        public async Task Riposte_EchoueContreUneAttaqueSpeciale()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Riposte", "Lance-Flammes");

            await battle.Use("Riposte");

            Assert.Equal(999, battle.Foe.CurrHp);
        }

        // ---------- Pièges (Ligotage, Étreinte, Danse Flammes, Claquoir) ----------
        // Règle du jeu : la cible perd 1/8 de ses PV max à chaque fin de tour pendant la durée du piège,
        // peut toujours attaquer mais ne peut pas être rappelée

        [Theory]
        [InlineData("Ligotage")]
        [InlineData("Étreinte")]
        [InlineData("Danse Flammes")]
        [InlineData("Claquoir")]
        public async Task Piege_UnHuitiemeDesPVMaxParTourPendantLaDuree(string move)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Duration, TestRandom.Lowest).Install();
            var battle = await Fight(move, foeHp: 800, otherPlayerMoves: "Trempette");
            int turns = Pkmn.Move(move).MinTurns!.Value;

            await battle.Use(move);
            var residuals = new List<int>();
            for (int turn = 0; turn < turns + 1; turn++)
            {
                int before = battle.Foe.CurrHp;
                await battle.Use("Trempette");
                residuals.Add(before - battle.Foe.CurrHp);
            }

            Assert.Equal(turns - 1, residuals.Count(r => r == 100));
            Assert.Equal(0, residuals.Last());
            Assert.Null(battle.Foe.MultiTurnsMove);
        }

        [Fact]
        public async Task Piege_LaCiblePeutToujoursAttaquer()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Ligotage", "Charge");
            await battle.Use("Ligotage");
            Assert.True(battle.Mine.CurrHp < 400);
        }

        // ---------- Entrave, Copie, Mimique, Métronome ----------

        [Fact]
        public async Task Entrave_BloqueUneCapaciteDeLaCible()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Mew", 50, "Entrave").WithStats(hp: 400, speed: 1),
                Pkmn.Create("Ronflex", 50, "Charge", "Trempette").WithStats(hp: 999, speed: 300));

            await battle.Use("Entrave");

            string disabled = Assert.Single(battle.Foe.CantUseMoves);
            Assert.Contains(disabled, new[] { "Charge", "Trempette" });
        }

        [Fact]
        public async Task Entrave_LIANeChoisitPlusLaCapaciteBloquee()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Mew", 50, "Entrave", "Trempette").WithStats(hp: 400, speed: 1),
                Pkmn.Create("Ronflex", 50, "Charge", "Trempette").WithStats(hp: 999, speed: 300));
            await battle.Use("Entrave");
            string disabled = battle.Foe.CantUseMoves.Single();
            battle.ClearMessages();

            await battle.Use("Trempette");

            Assert.DoesNotContain(battle.Dialog, m => m.Contains("Ronflex lance " + disabled));
        }

        [Fact]
        public async Task Copie_RemplaceCopieParUneCapaciteDeLaCibleJusquALaFinDuCombat()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Mew", 50, "Copie", "Ultimapoing").WithStats(hp: 400, speed: 1, atk: 999),
                Pkmn.Create("Ronflex", 50, "Coupe").WithStats(hp: 999, speed: 300));

            await battle.Use("Copie");

            Assert.Equal("Coupe", battle.Mine.Moves[0].NameFr);

            battle.EditOpponent(o => o.Team[0].CurrHp = 1);
            await battle.Use("Ultimapoing");

            Assert.Equal("Copie", battle.Mine.Moves[0].NameFr);
        }

        [Fact]
        public async Task Mimique_ReutiliseLaDerniereAttaqueDeLAdversaire()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Roucool", 50, "Mimique").WithStats(hp: 400, speed: 1),
                Pkmn.Create("Ronflex", 50, "Ultimapoing").WithStats(hp: 999, speed: 300));

            await battle.Use("Mimique");

            Assert.True(battle.Foe.CurrHp < 999);
            Assert.True(battle.Said("Roucool lance Ultimapoing"), string.Join(" | ", battle.Dialog));
        }

        // Règle du jeu : Mimique / Copie / Riposte / Entrave doivent être jouées après l'adversaire
        [Theory]
        [InlineData("Mimique")]
        [InlineData("Copie")]
        public async Task CapaciteAJouerEnDernier_EchoueSiLeLanceurEstPlusRapide(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight(move, "Ultimapoing", playerSpeed: 300, foeSpeed: 1);

            await battle.Use(move);

            Assert.Equal(999, battle.Foe.CurrHp);
            Assert.True(battle.Said("michou"));
        }

        [Fact]
        public async Task Metronome_LanceLAttaqueTiree()
        {
            using var _ = TestRandom.Neutral().Install();
            using var __ = HubHarness.Metronome("Ultimapoing");
            var battle = await Fight("Métronome");

            await battle.Use("Métronome");

            Assert.True(battle.Said("lance Métronome"));
            Assert.True(battle.Foe.CurrHp < 999);
            Assert.Equal(999 - battle.Foe.CurrHp, battle.HpShownForOpponent);
        }

        [Fact]
        public async Task Metronome_AdversairePlusRapide()
        {
            using var _ = TestRandom.Neutral().Install();
            using var __ = HubHarness.Metronome("Ultimapoing");
            var battle = await Fight("Métronome", "Trempette", playerSpeed: 1, foeSpeed: 300);

            await battle.Use("Métronome");

            Assert.True(battle.Foe.CurrHp < 999);
        }

        // ---------- Vampigraine, Clone, Frénésie, effets de terrain ----------

        [Fact]
        public async Task Vampigraine_DraineUnHuitiemeParTourAuProfitDuLanceur()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Bulbizarre", 50, "Vampigraine", "Trempette").WithStats(hp: 400, speed: 300).WithHp(300),
                Pkmn.Create("Ronflex", 50, "Trempette").WithStats(hp: 320, speed: 1));

            await battle.Use("Vampigraine");

            Assert.Equal(280, battle.Foe.CurrHp);
            Assert.Equal(340, battle.Mine.CurrHp);
            Assert.Equal(40, battle.HpShownForOpponent);
            Assert.Equal(-40, battle.HpShownForPlayer);
        }

        [Fact]
        public async Task Clonage_LeCloneEncaisseLesDegats()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Clonage", "Charge", otherPlayerMoves: "Trempette");

            await battle.Use("Clonage");
            int hpAfterSubstitute = battle.Mine.CurrHp;
            await battle.Use("Trempette");

            Assert.Equal(300, hpAfterSubstitute);
            Assert.Equal(300, battle.Mine.CurrHp);
        }

        [Fact]
        public async Task Clonage_UnCloneDetruitLaissePlaceAuPokemon()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Mew", 50, "Clonage", "Trempette").WithStats(hp: 400, speed: 300),
                Pkmn.Create("Ronflex", 50, "Ultimapoing").WithStats(hp: 999, speed: 1, atk: 999));

            // Ronflex détruit le clone dans le tour où il est créé : Mew garde ses PV moins le coût du clone
            await battle.Use("Clonage");

            Assert.Null(battle.Mine.Substitute);
            Assert.Equal(300, battle.Mine.CurrHp);
            Assert.Empty(battle.Received("playerPokemonDeath"));
            Assert.Empty(battle.Received("playerLooseFight"));

            // Sans clone, le coup suivant touche Mew
            await battle.Use("Trempette");
            Assert.Single(battle.Received("playerLooseFight"));
        }

        [Fact]
        public async Task Clonage_BloqueLesCapacitesDeStatut()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Clonage", "Cage Éclair", otherPlayerMoves: "Trempette");

            await battle.Use("Clonage");
            await battle.Use("Trempette");

            Assert.False(battle.Mine.IsParalyzed);
        }

        // Règle du jeu : Protection / Mur Lumière / Brume durent 5 tours (tour d'utilisation compris), quel que soit l'ordre des attaques
        [Theory]
        [InlineData(300, 1)]
        [InlineData(1, 300)]
        public async Task Protection_DureCinqTours(int playerSpeed, int foeSpeed)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Protection", playerSpeed: playerSpeed, foeSpeed: foeSpeed, otherPlayerMoves: "Trempette");

            await battle.Use("Protection");
            for (int turn = 0; turn < 3; turn++) await battle.Use("Trempette");
            Assert.Equal("Protection", battle.Player.FieldChange);

            await battle.Use("Trempette");
            Assert.Null(battle.Player.FieldChange);
            Assert.True(battle.Said("Protection n'est plus actif"));
        }

        [Fact]
        public async Task Protection_ReduitLesDegatsPhysiquesRecus()
        {
            using var _ = TestRandom.Neutral().Install();
            var reference = await Fight("Trempette", "Ultimapoing");
            await reference.Use("Trempette");
            int normalDamage = 400 - reference.Mine.CurrHp;

            var battle = await Fight("Protection", "Ultimapoing", otherPlayerMoves: "Trempette");
            await battle.Use("Protection");
            int before = battle.Mine.CurrHp;
            await battle.Use("Trempette");

            Assert.InRange(before - battle.Mine.CurrHp, normalDamage / 2 - 1, normalDamage / 2 + 1);
        }

        [Fact]
        public async Task Frenesie_LAttaqueMonteQuandLeLanceurEstTouche()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Fight("Frénésie", "Charge");

            await battle.Use("Frénésie");

            Assert.Equal(1, battle.Mine.AtkChanges);
        }
    }
}
