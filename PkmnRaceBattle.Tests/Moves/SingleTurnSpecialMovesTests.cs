using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Moves
{
    // Attaques au comportement particulier dont l'effet se résout en un seul tour (règles du jeu, cf. docs/MECANIQUES_COMBAT.md)
    public class SingleTurnSpecialMovesTests
    {
        private static PokemonTeam User(int level = 50, int hp = 300) => Pkmn.Create("Mew", level).WithStats(hp: hp, speed: 200).WithTypes("neutre");
        private static PokemonTeam Foe(string name = "Ronflex", int level = 50, int hp = 999) => Pkmn.Create(name, level).WithStats(hp: hp, speed: 10);

        // ---------- Dégâts fixes ----------

        [Theory]
        [InlineData("Sonic Boom", 20)]
        [InlineData("Draco-Rage", 40)]
        public void DegatsFixes(string move, int expected)
        {
            using var _ = TestRandom.Neutral().Install();
            Assert.Equal(expected, Rules.DamageDealt(User(level: 10), Foe(), move));
            Assert.Equal(expected, Rules.DamageDealt(User(level: 90), Foe("Racaillou", 90), move));
        }

        [Theory]
        [InlineData("Frappe Atlas", 10)]
        [InlineData("Frappe Atlas", 42)]
        [InlineData("Ombre Nocturne", 10)]
        [InlineData("Ombre Nocturne", 73)]
        public void DegatsEgauxAuNiveau(string move, int level)
        {
            using var _ = TestRandom.Neutral().Install();
            Assert.Equal(level, Rules.DamageDealt(User(level), Foe("Racaillou", 20), move));
        }

        [Fact]
        public void OmbreNocturne_SansEffetSurLesTypesNormal()
        {
            using var _ = TestRandom.Neutral().Install();
            Assert.Equal(0, Rules.DamageDealt(User(30), Foe("Ronflex"), "Ombre Nocturne"));
        }

        [Fact]
        public void FrappeAtlas_SansEffetSurLesTypesSpectre()
        {
            using var _ = TestRandom.Neutral().Install();
            Assert.Equal(0, Rules.DamageDealt(User(30), Foe("Ectoplasma"), "Frappe Atlas"));
        }

        [Theory]
        [InlineData(200, 100)]
        [InlineData(51, 25)]
        [InlineData(1, 1)]
        public void CrocFatal_RetireLaMoitieDesPVActuels(int currHp, int expected)
        {
            using var _ = TestRandom.Neutral().Install();
            Assert.Equal(expected, Rules.DamageDealt(User(), Foe().WithHp(currHp), "Croc Fatal"));
        }

        [Fact]
        public void VaguePsy_EntreUneDemiEtUneFoisEtDemieLeNiveau()
        {
            int level = 40;
            int low, high;
            using (TestRandom.Neutral().Set(RandomPurpose.SpecialMove, TestRandom.Lowest).Install()) low = Rules.DamageDealt(User(level), Foe(), "Vague Psy");
            using (TestRandom.Neutral().Set(RandomPurpose.SpecialMove, TestRandom.Highest).Install()) high = Rules.DamageDealt(User(level), Foe(), "Vague Psy");

            Assert.InRange(low, level / 2, level * 7 / 10);
            Assert.InRange(high, level * 3 / 2 - 2, level * 3 / 2);
        }

        // ---------- Puissance particulière ----------

        // Balayage : puissance selon le poids de la cible (< 10 kg : 20, < 25 kg : 40, < 50 kg : 60, < 100 kg : 80, < 200 kg : 100, sinon 120)
        // Attention : pokeapi donne le poids en hectogrammes (Salamèche = 85 -> 8,5 kg)
        [Theory]
        [InlineData("Fantominus", 20)]   //   0,1 kg
        [InlineData("Salamèche", 20)]    //   8,5 kg
        [InlineData("Racaillou", 40)]    //  20 kg
        [InlineData("Arcanin", 100)]     // 155 kg
        [InlineData("Ronflex", 120)]     // 460 kg
        public void Balayage_PuissanceSelonLePoidsEnKilogrammes(string target, int power)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam foe = Foe(target).WithStats(def: 100).WithTypes("neutre");

            int dealt = Rules.DamageDealt(User().WithStats(atk: 100), foe, "Balayage");

            int expected = Rules.Damage(50, power, 100, 100);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Theory]
        [InlineData("Explosion", 250)]
        [InlineData("Destruction", 200)]
        public void Explosion_KOleLanceurEtFrappeAvecSaPuissance(string move, int power)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User().WithStats(atk: 100);
            PokemonTeam foe = Foe().WithStats(def: 200).WithTypes("neutre");

            int dealt = Rules.DamageDealt(user, foe, move);

            Assert.Equal(0, user.CurrHp);
            int expected = Rules.Damage(50, power, 100, 200);
            Assert.InRange(dealt, expected - 1, expected + 1);
        }

        [Fact]
        public void Explosion_LeKODuLanceurEstEnvoyeAuClient()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 300);
            var ctx = Rules.Attack(user, Foe(), "Explosion");
            Assert.Equal(300, ctx.Player.Hp.Sum());
        }

        // ---------- Soin, drain et contrecoup ----------

        [Theory]
        [InlineData("Vole-Vie")]
        [InlineData("Méga-Sangsue")]
        [InlineData("Vampirisme")]
        public void Drain_RendLaMoitieDesDegatsInfliges(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 500).WithHp(100);
            PokemonTeam foe = Foe().WithTypes("neutre");

            int dealt = Rules.DamageDealt(user, foe, move);

            Assert.Equal(100 + Math.Max(1, dealt / 2), user.CurrHp);
        }

        [Fact]
        public void Devoreve_RendLaMoitieDesDegatsSurUneCibleEndormie()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 500).WithHp(100);
            PokemonTeam foe = Foe().WithTypes("neutre");
            foe.IsSleeping = 3;

            int dealt = Rules.DamageDealt(user, foe, "Dévorêve");

            Assert.True(dealt > 0);
            Assert.Equal(100 + dealt / 2, user.CurrHp);
        }

        [Fact]
        public void Devoreve_EchoueSiLaCibleEstEveillee()
        {
            Assert.True(FightPerformMove.SpecialCaseFail(User(), Pkmn.Move("Dévorêve"), Foe(), Pkmn.Move("Charge")));
        }

        [Theory]
        [InlineData("Bélier")]
        [InlineData("Sacrifice")]
        [InlineData("Damoclès")]
        public void Contrecoup_ProportionnelAuxDegatsInfliges(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 500);
            PokemonTeam foe = Foe().WithTypes("neutre");
            int recoilPercent = -Pkmn.Move(move).Drain;

            int dealt = Rules.DamageDealt(user, foe, move);

            int expectedRecoil = Math.Max(1, dealt * recoilPercent / 100);
            Assert.InRange(500 - user.CurrHp, expectedRecoil - 1, expectedRecoil + 1);
        }

        [Fact]
        public void Contrecoup_EnvoyeAuClientPourLaBarreDeVie()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 500);
            var ctx = Rules.Attack(user, Foe(), "Bélier");
            Assert.Equal(500 - user.CurrHp, ctx.Player.Hp.Sum());
        }

        [Theory]
        [InlineData("Soin")]
        [InlineData("E-Coque")]
        public void Soin_RendLaMoitieDesPVMax(string move)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 300).WithHp(50);
            Rules.Attack(user, Foe(), move);
            Assert.Equal(200, user.CurrHp);
        }

        [Fact]
        public void Soin_EchoueAuxPVMax()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 300);
            var ctx = Rules.Attack(user, Foe(), "Soin");
            Assert.Equal(300, user.CurrHp);
            Assert.True(ctx.Contains("michou") || ctx.Contains("échoue"), ctx.Dump());
        }

        [Fact]
        public void Repos_EchoueAuxPVMax()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 300);
            Rules.Attack(user, Foe(), "Repos");
            Assert.Equal(0, user.IsSleeping);
        }

        [Theory]
        [InlineData("Pied Sauté")]
        [InlineData("Pied Voltige")]
        public void PiedSaute_LaMoitieDesPVMaxSiLAttaqueRate(string move)
        {
            using var _ = TestRandom.Neutral().AlwaysMiss().Install();
            PokemonTeam user = User(hp: 300);
            Rules.Attack(user, Foe(), move);
            Assert.Equal(150, user.CurrHp);
        }

        // ---------- Attaques multi-coups ----------

        [Theory]
        [InlineData(0.10, 2)]
        [InlineData(0.34, 2)]
        [InlineData(0.36, 3)]
        [InlineData(0.69, 3)]
        [InlineData(0.71, 4)]
        [InlineData(0.84, 4)]
        [InlineData(0.86, 5)]
        public void MultiCoups_Repartition35_35_15_15(double roll, int expectedHits)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.MultiHit, roll).Install();
            var ctx = Rules.Attack(User(), Foe(), "Furie");
            Assert.Equal(expectedHits, ctx.Opponent.Hp.Count);
            Assert.True(ctx.Contains($"Touché {expectedHits} fois"), ctx.Dump());
        }

        [Theory]
        [InlineData("Double Pied")]
        [InlineData("Osmerang")]
        [InlineData("Double Dard")]
        public void DoubleCoup_ToujoursDeuxCoups(string move)
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.MultiHit, TestRandom.Highest).Install();
            var ctx = Rules.Attack(User(), Foe().WithTypes("neutre"), move);
            Assert.Equal(2, ctx.Opponent.Hp.Count);
        }

        [Fact]
        public void MultiCoups_ToutesLesVariationsDePVSontEnvoyees()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.MultiHit, 0.95).Install();
            PokemonTeam foe = Foe();
            var ctx = Rules.Attack(User(), foe, "Pilonnage");
            Assert.Equal(999 - foe.CurrHp, ctx.Opponent.Hp.Sum());
        }

        // ---------- Peur ----------

        [Fact]
        public void Peur_SelonSaProbabilite()
        {
            int chance = Pkmn.Move("Écrasement").FlinchChance;
            using (TestRandom.Neutral().Set(RandomPurpose.SecondaryEffect, (chance - 1) / 100.0).Install())
            {
                PokemonTeam foe = Foe();
                Rules.Attack(User(), foe, "Écrasement");
                Assert.True(foe.IsFlinched);
            }
            using (TestRandom.Neutral().Set(RandomPurpose.SecondaryEffect, (chance + 1) / 100.0).Install())
            {
                PokemonTeam foe = Foe();
                Rules.Attack(User(), foe, "Écrasement");
                Assert.False(foe.IsFlinched);
            }
        }

        [Fact]
        public void Triplattaque_PeutBrulerGelerOuParalyser()
        {
            using (TestRandom.Neutral().Set(RandomPurpose.SpecialMove, TestRandom.Lowest).Install())
            {
                PokemonTeam foe = Foe();
                Rules.Attack(User(), foe, "Triplattaque");
                Assert.True(foe.IsBurning || foe.IsFrozen || foe.IsParalyzed);
            }
            using (TestRandom.Neutral().Set(RandomPurpose.SpecialMove, TestRandom.Highest).Install())
            {
                PokemonTeam foe = Foe();
                Rules.Attack(User(), foe, "Triplattaque");
                Assert.False(foe.IsBurning || foe.IsFrozen || foe.IsParalyzed);
            }
        }

        // ---------- Transformations ----------

        [Fact]
        public void Morphing_CopieTypesStatsEtCapacitesAvec5PP()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam ditto = Pkmn.Create("Métamorph", 40, "Morphing");
            PokemonTeam target = Pkmn.Create("Dracaufeu", 40, "Lance-Flammes", "Tranche", "Cru-Ailes", "Danse Flammes");
            int dittoHp = ditto.BaseHp;

            Rules.Attack(ditto, target, "Morphing");

            Assert.Equal(target.Types.Select(t => t.Name), ditto.Types.Select(t => t.Name));
            Assert.Equal(target.Atk, ditto.Atk);
            Assert.Equal(target.Def, ditto.Def);
            Assert.Equal(target.AtkSpe, ditto.AtkSpe);
            Assert.Equal(target.DefSpe, ditto.DefSpe);
            Assert.Equal(target.Speed, ditto.Speed);
            Assert.Equal(dittoHp, ditto.BaseHp);
            Assert.Equal(target.Moves.Select(m => m.NameFr), ditto.Moves.Select(m => m.NameFr));
            Assert.All(ditto.Moves, m => Assert.Equal(5, m.Pp));
            Assert.NotNull(ditto.UnmorphedForm);
        }

        [Fact]
        public void Morphing_NeModifiePasLesPPDeLaCible()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam ditto = Pkmn.Create("Métamorph", 40, "Morphing");
            PokemonTeam target = Pkmn.Create("Dracaufeu", 40, "Lance-Flammes", "Tranche");
            int[] originalPp = target.Moves.Select(m => m.Pp).ToArray();

            Rules.Attack(ditto, target, "Morphing");

            Assert.Equal(originalPp, target.Moves.Select(m => m.Pp).ToArray());
        }

        [Fact]
        public void Morphing_CopieLesPaliersDeStats()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam ditto = Pkmn.Create("Métamorph", 40, "Morphing");
            PokemonTeam target = Pkmn.Create("Dracaufeu", 40);
            target.AtkChanges = 2;
            target.SpeedChanges = -1;

            Rules.Attack(ditto, target, "Morphing");

            Assert.Equal(2, ditto.AtkChanges);
            Assert.Equal(-1, ditto.SpeedChanges);
        }

        [Fact]
        public void Conversion_PrendLeTypeDeLaPremiereCapacite()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam porygon = Pkmn.Create("Porygon", 30, "Pistolet à O", "Conversion");

            Rules.Attack(porygon, Pkmn.Create("Dracaufeu", 30), "Conversion");

            Assert.Equal("water", porygon.Types[0].Name);
            Assert.Equal("normal", porygon.ConvertedType);
        }

        // ---------- Clone ----------

        [Fact]
        public void Clonage_CouteUnQuartDesPVMaxEtCreeUnClone()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 200);
            Rules.Attack(user, Foe(), "Clonage");
            Assert.Equal(150, user.CurrHp);
            Assert.NotNull(user.Substitute);
            Assert.InRange(user.Substitute!.CurrHp, 50, 51);
        }

        [Fact]
        public void Clonage_EchoueSansAssezDePV()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 200).WithHp(50);
            Rules.Attack(user, Foe(), "Clonage");
            Assert.Equal(50, user.CurrHp);
            Assert.Null(user.Substitute);
        }

        [Fact]
        public void Clonage_EchoueSiUnCloneExisteDeja()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam user = User(hp: 200);
            Rules.Attack(user, Foe(), "Clonage");
            Rules.Attack(user, Foe(), "Clonage");
            Assert.Equal(150, user.CurrHp);
        }

        // ---------- Effets divers ----------

        [Fact]
        public void Frenesie_LAttaqueAugmenteAChaqueCoupRecu()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam rager = User();
            Rules.Attack(rager, Foe(), "Frénésie");
            Assert.Contains("Frénésie", rager.SpecialCases);

            Rules.Attack(Foe(), rager, Pkmn.Move("Charge"), playerAttacking: false);
            Assert.Equal(1, rager.AtkChanges);
            Rules.Attack(Foe(), rager, Pkmn.Move("Charge"), playerAttacking: false);
            Assert.Equal(2, rager.AtkChanges);
        }

        [Fact]
        public void Vampigraine_PlanteUneGraineSurLaCible()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam foe = Foe();
            Rules.Attack(User(), foe, "Vampigraine");
            Assert.Contains("Vampigraine", foe.SpecialCases);
        }

        [Fact]
        public void Vampigraine_SansEffetSurLesTypesPlante()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam foe = Foe("Bulbizarre");
            Rules.Attack(User(), foe, "Vampigraine");
            Assert.DoesNotContain("Vampigraine", foe.SpecialCases);
        }

        [Fact]
        public void Vampigraine_EchoueSiLaCibleEstDejaInfectee()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam foe = Foe();
            Rules.Attack(User(), foe, "Vampigraine");
            Rules.Attack(User(), foe, "Vampigraine");
            Assert.Single(foe.SpecialCases, c => c == "Vampigraine");
        }

        [Fact]
        public void Trempette_NeFaitRien()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam magicarpe = Pkmn.Create("Magicarpe", 10, "Trempette");
            PokemonTeam foe = Foe();
            var ctx = Rules.Attack(magicarpe, foe, "Trempette");
            Assert.Equal(999, foe.CurrHp);
            Assert.True(ctx.Contains("rien"), $"Le joueur doit voir que rien ne se passe : {ctx.Dump()}");
        }

        [Fact]
        public void Jackpot_AjouteDeLArgentAuGainDuCombat()
        {
            using var _ = TestRandom.Neutral().Install();
            var player = new PlayerMongo();
            PokemonTeam meowth = Pkmn.Create("Miaouss", 20, "Jackpot");
            var ctx = new TurnContext();
            ctx.AddPrioMessage("Miaouss lance Jackpot");
            FightPerformMove.PerformMove(meowth, Foe(), Pkmn.Move("Jackpot"), null!, ctx, true, player);
            Assert.Equal(100, player.Jackpot); // 5 x niveau (valeur du jeu)
        }

        [Theory]
        [InlineData("Brume")]
        [InlineData("Mur Lumière")]
        [InlineData("Protection")]
        public void EffetDeTerrain_SActiveUneSeuleFois(string move)
        {
            var player = new PlayerMongo();
            var ctx = new TurnContext();
            FightPerformMove.FieldChangeMove(Pkmn.Move(move), player, ctx);
            Assert.Equal(move, player.FieldChange);

            var ctx2 = new TurnContext();
            FightPerformMove.FieldChangeMove(Pkmn.Move(move), player, ctx2);
            Assert.True(ctx2.Contains("michou"));
        }

        [Fact]
        public void Entrave_EchoueSiLAdversaireUtiliseUnObjet()
        {
            Assert.True(FightPerformMove.SpecialCaseFail(User(), Pkmn.Move("Entrave"), Foe(), Pkmn.Item("Potion", "potion")));
        }

    }
}
