using PkmnRaceBattle.API.Helpers.StatsCalculator;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Hub
{
    // Combats complets (GameHub.HandleMove -> UseMove -> FinishFight)
    public class FightFlowTests
    {
        private static PokemonTeam Strong(string name = "Mew", int level = 50, params string[] moves) =>
            Pkmn.Create(name, level, moves.Length > 0 ? moves : ["Charge"]).WithStats(speed: 300);

        private static PokemonTeam Dummy(string name = "Ronflex", int level = 30, int hp = 999, params string[] moves) =>
            Pkmn.Create(name, level, moves.Length > 0 ? moves : ["Trempette"]).WithStats(hp: hp, speed: 1);

        [Fact]
        public async Task Attaque_LesPVDeLAdversaireBaissentEnBaseEtAuClient()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Ultimapoing"), Dummy());

            await battle.Use("Ultimapoing");

            int lost = 999 - battle.Foe.CurrHp;
            Assert.True(lost > 0);
            Assert.Equal(lost, battle.HpShownForOpponent);
            Assert.True(battle.Said("Mew lance Ultimapoing"));
        }

        [Fact]
        public async Task Tour_SeTermineParTurnFinishedAvecLEtatAJour()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Ultimapoing"), Dummy());

            await battle.Use("Ultimapoing");

            SentMessage finished = battle.Received("turnFinished").Single();
            Assert.Equal(battle.PlayerId, finished.Arg<PlayerMongo>(0)._id);
            Assert.Equal(battle.Foe.CurrHp, finished.Arg<PlayerMongo>(1).Team[0].CurrHp);
            Assert.Equal("turnFinished", battle.Received("useMoveResult").Concat(battle.Received("turnFinished")).Last().Method);
        }

        [Fact]
        public async Task AdversaireAttaque_LesPVDuJoueurBaissentEnBaseEtAuClient()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Trempette").WithStats(hp: 500), Pkmn.Create("Ronflex", 50, "Ultimapoing").WithStats(hp: 999, speed: 1));

            await battle.Use("Trempette");

            int lost = 500 - battle.Mine.CurrHp;
            Assert.True(lost > 0);
            Assert.Equal(lost, battle.HpShownForPlayer);
        }

        [Fact]
        public async Task LePlusRapideAttaqueEnPremier()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Pkmn.Create("Mew", 50, "Charge").WithStats(speed: 10, hp: 500), Pkmn.Create("Ronflex", 50, "Charge").WithStats(hp: 999, speed: 300));

            await battle.Use("Charge");

            var dialog = battle.Dialog;
            Assert.True(dialog.FindIndex(m => m.StartsWith("Ronflex lance")) < dialog.FindIndex(m => m.StartsWith("Mew lance")), string.Join(" | ", dialog));
        }

        // Pour chaque capacité du jeu : les variations de PV envoyées au client (barre de vie) == variations réelles en base
        public static IEnumerable<object[]> AllMoves() => GameData.Moves
            .Where(m => m.NameFr is not ("Métronome" or "Lutte" or "Explosion" or "Destruction")) // couvertes par des tests dédiés
            .Select(m => new object[] { m.NameFr! });

        [Theory]
        [MemberData(nameof(AllMoves))]
        public async Task BarreDeVie_VariationsEnvoyeesEgalesAuxVariationsReelles(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Mew", 50, move).WithStats(hp: 400, speed: 300).Also(p => p.CurrHp = 300),
                Pkmn.Create("Ronflex", 50, "Charge").WithStats(hp: 999, speed: 1));
            int playerBefore = battle.Mine.CurrHp;
            int foeBefore = battle.Foe.CurrHp;

            await battle.Use(move);
            // Les attaques en deux tours / sur plusieurs tours : on joue jusqu'à 4 tours
            for (int turn = 0; turn < 3 && battle.Mine.WaitingMove != null; turn++) await battle.Use(move);

            PokemonTeam mine = battle.Player.Team.First(p => p.IdDex == 151);
            int foeAfter = battle.Opponent.Team[0].CurrHp;
            Assert.Equal(playerBefore - mine.CurrHp, battle.HpShownForPlayer);
            Assert.Equal(foeBefore - foeAfter, battle.HpShownForOpponent);
        }

        [Fact]
        public async Task KOduSauvage_FinDuCombatGainDXPEtCombatSuivant()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong("Salamèche", 20, "Griffe").WithStats(atk: 500), Dummy("Rattata", 5, 10));
            int xpBefore = battle.Mine.CurrXP;
            PokemonTeam wild = battle.Foe;

            await battle.Use("Griffe");

            Assert.Equal(0, battle.Opponent.Team[0].CurrHp);
            Assert.True(battle.Said("Rattata est K.O"));
            int expectedXp = PokemonExperienceCalculator.ExpGained(wild, false, false, 1);
            Assert.Equal(xpBefore + expectedXp, battle.Mine.CurrXP);
            Assert.Equal(1, battle.Player.MapFightCount);
            Assert.Single(battle.Received("responseWildFight"));
        }

        [Fact]
        public async Task KOduSauvage_SansMultiExp_SeulsLesPokemonAyantParticipeGagnentDeLXP()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam bench = Pkmn.Create("Carapuce", 10);
            var battle = await Battle.VsWild(Strong("Salamèche", 20, "Griffe").WithStats(atk: 500), Dummy("Rattata", 5, 10), bench);
            battle.H.SetXpSettings(multiXp: false, multiplier: 1);

            await battle.Use("Griffe");

            Assert.Equal(bench.CurrXP, battle.Player.Team[1].CurrXP);
        }

        [Fact]
        public async Task KOduSauvage_MultiExpParDefaut_LeResteDeLEquipeGagneLaMoitieDeLXP()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam bench = Pkmn.Create("Carapuce", 10);
            var battle = await Battle.VsWild(Strong("Salamèche", 20, "Griffe").WithStats(atk: 500), Dummy("Rattata", 5, 10), bench);
            PokemonTeam wild = battle.Foe;
            int leadXp = battle.Mine.CurrXP;

            await battle.Use("Griffe");

            int full = PokemonExperienceCalculator.ExpGained(wild, false, false, 1);
            Assert.Equal(leadXp + full, battle.Player.Team[0].CurrXP);
            Assert.Equal(bench.CurrXP + full / 2, battle.Player.Team[1].CurrXP);
        }

        [Fact]
        public async Task KOduSauvage_MultiExp_UnPokemonKONeGagneRien()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam fainted = Pkmn.Create("Carapuce", 10).WithHp(0);
            var battle = await Battle.VsWild(Strong("Salamèche", 20, "Griffe").WithStats(atk: 500), Dummy("Rattata", 5, 10), fainted);
            battle.H.SetXpSettings(multiXp: true, multiplier: 5);

            await battle.Use("Griffe");

            Assert.Equal(fainted.CurrXP, battle.Player.Team[1].CurrXP);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public async Task KOduSauvage_LeMultiplicateurDXPSAppliqueATouteLEquipe(int multiplier)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam bench = Pkmn.Create("Carapuce", 10);
            var battle = await Battle.VsWild(Strong("Salamèche", 20, "Griffe").WithStats(atk: 500), Dummy("Rattata", 5, 10), bench);
            battle.H.SetXpSettings(multiXp: true, multiplier: multiplier);
            PokemonTeam wild = battle.Foe;
            int leadXp = battle.Mine.CurrXP;

            await battle.Use("Griffe");

            int full = PokemonExperienceCalculator.ExpGained(wild, false, false, 1);
            Assert.Equal(leadXp + full * multiplier, battle.Player.Team[0].CurrXP);
            Assert.Equal(bench.CurrXP + full / 2 * multiplier, battle.Player.Team[1].CurrXP);
        }

        [Fact]
        public async Task KOduSauvage_MultiExp_LeResteDeLEquipePeutMonterDeNiveau()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam bench = Pkmn.Create("Carapuce", 5);
            var battle = await Battle.VsWild(Strong("Salamèche", 20, "Griffe").WithStats(atk: 500), Dummy("Ronflex", 30, 10), bench);
            battle.H.SetXpSettings(multiXp: true, multiplier: 5);

            await battle.Use("Griffe");

            Assert.True(battle.Player.Team[1].Level > 5);
            Assert.Contains(battle.Received("pokemonLevelUp"), m => m.Arg<string>(0).Contains("Carapuce monte niveau"));
        }

        [Fact]
        public async Task KOduJoueur_AvecDAutresPokemon_DemandeDeChanger()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Salamèche", 5, "Griffe").WithStats(hp: 10, speed: 1),
                Pkmn.Create("Ronflex", 50, "Ultimapoing").WithStats(hp: 999, speed: 300),
                Pkmn.Create("Carapuce", 5));

            await battle.Use("Griffe");

            Assert.Equal(0, battle.Mine.CurrHp);
            Assert.Single(battle.Received("playerPokemonDeath"));
            Assert.Empty(battle.Received("playerLooseFight"));
        }

        [Fact]
        public async Task KODeToutLEquipe_DefaiteMoitieDeLArgentEtEquipeSoignee()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(
                Pkmn.Create("Salamèche", 5, "Griffe").WithStats(hp: 10, speed: 1),
                Pkmn.Create("Ronflex", 50, "Ultimapoing").WithStats(hp: 999, speed: 300));
            battle.Edit(p => { p.Credits = 3001; p.Team[0].IsPoisoned = 1; });

            await battle.Use("Griffe");

            Assert.Single(battle.Received("playerLooseFight"));
            Assert.Equal(1500, battle.Player.Credits);
            Assert.Equal(battle.Mine.BaseHp, battle.Mine.CurrHp);
            Assert.Equal(0, battle.Mine.IsPoisoned);
            Assert.Equal(1, battle.Player.MapFightCount);
        }

        [Fact]
        public async Task PokemonKO_NePeutPasAttaquer()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Charge"), Dummy());
            battle.Edit(p => p.Team[0].CurrHp = 0);

            await battle.Use("Charge");

            Assert.Equal(999, battle.Foe.CurrHp);
            Assert.True(battle.Said("K.O ne peut pas attaquer"));
        }

        [Fact]
        public async Task UtiliserUneCapacite_ConsommeUnPP()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Charge"), Dummy());
            int pp = battle.Mine.Moves[0].Pp;

            await battle.Use("Charge");

            Assert.Equal(pp - 1, battle.Mine.Moves[0].Pp);
        }

        [Fact]
        public async Task PlusDePP_LePokemonUtiliseLutte()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Charge").WithStats(hp: 400), Dummy());
            battle.Edit(p => p.Team[0].Moves[0].Pp = 0);

            await battle.Use("Charge");

            Assert.True(battle.Said("Lutte"), string.Join(" | ", battle.Dialog));
            Assert.True(battle.Foe.CurrHp < 999);
            Assert.True(battle.Mine.CurrHp < 400, "Lutte inflige un contrecoup au lanceur");
        }

        [Fact]
        public async Task Peur_EmpecheLAdversairePlusLentDAttaquer()
        {
            using var _ = TestRandom.Neutral().AlwaysSecondaryEffect().Install();
            var battle = await Battle.VsWild(Strong(moves: "Écrasement").WithStats(hp: 400), Pkmn.Create("Ronflex", 50, "Charge").WithStats(hp: 999, speed: 1));

            await battle.Use("Écrasement");

            Assert.Equal(400, battle.Mine.CurrHp);
            Assert.True(battle.Said("La peur empêche"));
            Assert.False(battle.Foe.IsFlinched);
        }

        [Fact]
        public async Task Peur_SansEffetSiLAdversaireADejaAttaque()
        {
            using var _ = TestRandom.Neutral().AlwaysSecondaryEffect().Install();
            var battle = await Battle.VsWild(Pkmn.Create("Mew", 50, "Écrasement").WithStats(hp: 400, speed: 1), Pkmn.Create("Ronflex", 50, "Trempette").WithStats(hp: 999, speed: 300));

            await battle.Use("Écrasement");
            await battle.Use("Écrasement");

            Assert.DoesNotContain(battle.Dialog, m => m.Contains("La peur empêche"));
        }

        [Fact]
        public async Task DegatsDeFinDeTour_PoisonDuJoueurAppliqueEtAffiche()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Trempette").WithStats(hp: 160), Dummy());
            battle.Edit(p => p.Team[0].IsPoisoned = 1);

            await battle.Use("Trempette");

            Assert.Equal(140, battle.Mine.CurrHp);
            Assert.Equal(20, battle.HpShownForPlayer);
        }

        [Fact]
        public async Task DegatsDeFinDeTour_LeSauvageKOParLePoisonTermineLeCombat()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: "Trempette"), Dummy(hp: 160).WithHp(5));
            battle.EditOpponent(o => o.Team[0].IsPoisoned = 1);

            await battle.Use("Trempette");

            Assert.Equal(0, battle.Opponent.Team[0].CurrHp);
            Assert.Single(battle.Received("responseWildFight"));
        }

        [Fact]
        public async Task Jackpot_LArgentEstAjouteApresLaVictoire()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong("Miaouss", 20, "Jackpot").WithStats(atk: 999), Dummy("Rattata", 5, 10));
            int credits = battle.Player.Credits;

            await battle.Use("Jackpot");

            Assert.Equal(credits + 100, battle.Player.Credits);
            Assert.Equal(0, battle.Player.Jackpot);
        }

        [Fact]
        public async Task Teleport_FuitLeCombatSauvage()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong("Abra", 20, "Téléport"), Dummy());

            await battle.Use("Téléport");

            Assert.Equal(1, battle.Player.MapFightCount);
            Assert.Single(battle.Received("responseWildFight"));
            Assert.Equal(battle.Mine.CurrXP, Pkmn.Create("Abra", 20).CurrXP);
        }

        [Theory]
        [InlineData("Cyclone")]
        [InlineData("Hurlement")]
        public async Task CycloneHurlement_TerminentLeCombatSauvage(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong(moves: move), Dummy());

            await battle.Use(move);

            Assert.Equal(1, battle.Player.MapFightCount);
            Assert.Single(battle.Received("responseWildFight"));
        }

        [Fact]
        public async Task Explosion_LeJoueurEstKO()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsWild(Strong("Voltorbe", 30, "Destruction"), Dummy(), Pkmn.Create("Carapuce", 10));

            await battle.Use("Destruction");

            Assert.Equal(0, battle.Player.Team.First(p => p.IdDex == 100).CurrHp);
            Assert.Single(battle.Received("playerPokemonDeath"));
        }
    }

    // Combat contre un dresseur
    public class TrainerFightTests
    {
        [Fact]
        public async Task KODuPremierPokemon_LeDresseurEnvoieLeSuivant()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsTrainer(Pkmn.Create("Mew", 50, "Ultimapoing").WithStats(atk: 999, speed: 300),
                [Pkmn.Create("Racaillou", 10, "Charge").WithStats(hp: 5), Pkmn.Create("Onix", 12, "Charge")]);

            await battle.Use("Ultimapoing");

            var switched = battle.Received("onTrainerSwitchPokemon").Single().Arg<PlayerMongo>(0);
            Assert.Equal("Onix", switched.Team[0].NameFr);
            Assert.True(battle.Said("Pierre envoie Onix"));
            Assert.Equal(0, battle.Player.MapFightCount);
            Assert.Empty(battle.Received("responseWildFight"));
        }

        [Fact]
        public async Task KODuPremierPokemon_XPGagneeAvecBonusDresseur()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam first = Pkmn.Create("Racaillou", 10, "Charge").WithStats(hp: 5);
            var battle = await Battle.VsTrainer(Pkmn.Create("Mew", 50, "Ultimapoing").WithStats(atk: 999, speed: 300), [first, Pkmn.Create("Onix", 12, "Charge")]);
            int xp = battle.Mine.CurrXP;

            await battle.Use("Ultimapoing");

            Assert.Equal(xp + PokemonExperienceCalculator.ExpGained(first, true, false, 1), battle.Mine.CurrXP);
        }

        [Fact]
        public async Task VictoireContreLeDresseur_GagneDeLArgentEtPasseAuTourSuivant()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsTrainer(Pkmn.Create("Mew", 50, "Ultimapoing").WithStats(atk: 999, speed: 300),
                [Pkmn.Create("Racaillou", 10, "Charge").WithStats(hp: 5)]);
            battle.Edit(p => p.MapFightCount = 5);
            int credits = battle.Player.Credits;

            await battle.Use("Ultimapoing");

            Assert.Equal(credits + 2000, battle.Player.Credits);
            Assert.True(battle.Said("Vous remportez 2000"));
            // Map terminée : le joueur passe à l'étape suivante du chemin
            Assert.Equal(2, battle.Player.CurrentPath.X);
            Assert.True(battle.Received("responseWildFight").Any() || battle.Received("chooseNextPath").Any());
        }

        [Fact]
        public async Task Pokeball_InterditeContreUnDresseur()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsTrainer(Pkmn.Create("Mew", 50, "Charge"), [Pkmn.Create("Racaillou", 10, "Charge")]);
            int balls = battle.Player.Items.Single(i => i.Name == "Pokeball").Number;

            await battle.UseItem("Pokeball", "ball");

            Assert.True(battle.Said("Voler n'est pas bon"));
            Assert.Equal(balls, battle.Player.Items.Single(i => i.Name == "Pokeball").Number);
            Assert.Empty(battle.Received("launchBall"));
        }

        [Theory]
        [InlineData("Cyclone")]
        [InlineData("Hurlement")]
        public async Task CycloneDuDresseur_LeJoueurDoitEnvoyerUnAutrePokemon(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam bench = Pkmn.Create("Carapuce", 10);
            var battle = await Battle.VsTrainer(Pkmn.Create("Racaillou", 10, "Trempette").WithStats(speed: 1),
                [Pkmn.Create("Roucool", 30, move).WithStats(speed: 999), Pkmn.Create("Rattata", 5)], bench);
            string lead = battle.Mine.Id;

            await battle.Use("Trempette");
            Assert.Contains(battle.Received("playerPokemonDeath"), m => m.Arg<string>(0) == "Changez de Pokémon");

            // Renvoyer le Pokémon éjecté est refusé : le choix est redemandé, le tour ne continue pas
            int turnsBefore = battle.Received("turnFinished").Count();
            await battle.H.Hub(battle.Connection).ReplacePokemon(battle.PlayerId, lead, battle.OpponentRef._id, false);
            Assert.Equal(2, battle.Received("playerPokemonDeath").Count());
            Assert.Equal(turnsBefore, battle.Received("turnFinished").Count());
            Assert.Equal(lead, battle.Mine.Id);

            // Un autre Pokémon est accepté
            await battle.SwitchTo(1);
            Assert.Equal(bench.Id, battle.Mine.Id);
            Assert.DoesNotContain(GameHub.MustSwitchCase, battle.Player.Team.Single(p => p.Id == lead).SpecialCases);
        }

        [Theory]
        [InlineData("Téléport")]
        [InlineData("Cyclone")]
        public async Task FuiteImpossibleContreUnDresseur(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await Battle.VsTrainer(Pkmn.Create("Mew", 50, move).WithStats(speed: 300), [Pkmn.Create("Racaillou", 10, "Trempette")]);

            await battle.Use(move);

            Assert.True(battle.Said("michou"));
            Assert.Equal(0, battle.Player.MapFightCount);
            Assert.Empty(battle.Received("responseWildFight"));
        }
    }
}
