using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Helpers.StatsCalculator;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Hub
{
    // Objets utilisés en combat, Pokéballs et capture
    public class ItemsInBattleTests
    {
        private static int Count(Battle battle, string item) => battle.Player.Items.Single(i => i.Name == item).Number;

        private static Task<Battle> WildBattle(PokemonTeam? mine = null, PokemonTeam? wild = null, params PokemonTeam[] bench) =>
            Battle.VsWild(mine ?? Pkmn.Create("Salamèche", 20, "Griffe").WithStats(hp: 100), wild ?? Pkmn.Create("Rattata", 5, "Trempette"), bench);

        [Fact]
        public async Task Potion_SoigneLePokemonActifEtConsommeLObjet()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle();
            battle.Edit(p => p.Team[0].CurrHp = 50);
            int potions = Count(battle, "Potion");

            await battle.UseItem("Potion", "potion");

            Assert.Equal(70, battle.Mine.CurrHp);
            Assert.Equal(potions - 1, Count(battle, "Potion"));
            SentMessage result = battle.Received("useItemResult").Single();
            Assert.Equal(0, result.Arg<int>(1));
            Assert.Equal(-20, ((TurnContext)result.Args[0]!).Player.Hp.Sum());
        }

        [Fact]
        public async Task Potion_SurUnPokemonDeLEquipe()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(bench: [Pkmn.Create("Carapuce", 10), Pkmn.Create("Bulbizarre", 10)]);
            battle.Edit(p => { p.Team[0].CurrHp = 50; p.Team[2].CurrHp = 5; });

            await battle.UseItem("Super Potion", "potion", 2);

            Assert.Equal(50, battle.Mine.CurrHp);
            Assert.Equal(Math.Min(55, battle.Player.Team[2].BaseHp), battle.Player.Team[2].CurrHp);
            Assert.Equal(2, battle.Received("useItemResult").Single().Arg<int>(1));
        }

        [Fact]
        public async Task Rappel_RanimeUnPokemonKODeLEquipe()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(bench: [Pkmn.Create("Carapuce", 10)]);
            battle.Edit(p => p.Team[1].CurrHp = 0);

            await battle.UseItem("Rappel", "potion", 1);

            Assert.Equal(battle.Player.Team[1].BaseHp / 2, battle.Player.Team[1].CurrHp);
            Assert.Equal(0, Count(battle, "Rappel"));
        }

        [Theory]
        [InlineData("Rappel")]
        [InlineData("Rappel Max")]
        public async Task Rappel_LePokemonRanimePerdSesStatuts(string item)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(bench: [Pkmn.Create("Carapuce", 10)]);
            battle.Edit(p =>
            {
                p.Team[1].CurrHp = 0;
                p.Team[1].IsBurning = true;
                p.Team[1].IsParalyzed = true;
                p.Team[1].IsPoisoned = 2;
                p.Team[1].PoisonCount = 3;
                p.Team[1].IsSleeping = 2;
                p.Team[1].IsFrozen = true;
                p.Items.Single(i => i.Name == item).Number = 1;
            });

            await battle.UseItem(item, "potion", 1);

            PokemonTeam revived = battle.Player.Team[1];
            Assert.True(revived.CurrHp > 0);
            Assert.False(revived.IsBurning || revived.IsParalyzed || revived.IsFrozen);
            Assert.Equal(0, revived.IsPoisoned);
            Assert.Null(revived.PoisonCount);
            Assert.Equal(0, revived.IsSleeping);
        }

        [Fact]
        public async Task ObjetUtilise_LAdversaireAttaqueEnsuite()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(wild: Pkmn.Create("Rattata", 30, "Charge").WithStats(speed: 999));
            battle.Edit(p => p.Team[0].CurrHp = 50);

            await battle.UseItem("Potion", "potion");

            Assert.True(battle.Said("Rattata lance Charge"));
            Assert.True(battle.Mine.CurrHp < 70);
        }

        [Fact]
        public async Task ObjetRefuse_NeConsommeNiLObjetNiLeTour()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(wild: Pkmn.Create("Rattata", 30, "Charge"));
            int potions = Count(battle, "Potion");

            await battle.UseItem("Potion", "potion");

            Assert.Equal(potions, Count(battle, "Potion"));
            Assert.Equal(battle.Mine.BaseHp, battle.Mine.CurrHp);
            Assert.True(battle.Said("a déjà ses PV au max"));
        }

        [Fact]
        public async Task ObjetEpuise_EstRefuse()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle();
            battle.Edit(p => p.Team[0].CurrHp = 10);

            await battle.UseItem("Potion Max", "potion");

            Assert.Equal(0, Count(battle, "Potion Max"));
            Assert.Equal(10, battle.Mine.CurrHp);
        }

        [Fact]
        public async Task SoinDeStatut_EnCombat()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle();
            battle.Edit(p => p.Team[0].IsParalyzed = true);

            await battle.UseItem("Anti-Para", "ailment");

            Assert.False(battle.Mine.IsParalyzed);
            Assert.Equal(0, Count(battle, "Anti-Para"));
        }

        [Fact]
        public async Task SuperBonbon_FaitMonterDUnNiveau()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle();
            battle.Edit(p => p.Items.Single(i => i.Name == "Super Bonbon").Number = 1);

            await battle.UseItem("Super Bonbon", "special");

            Assert.Equal(21, battle.Mine.Level);
            Assert.Equal(0, Count(battle, "Super Bonbon"));
            Assert.Contains(battle.Received("pokemonLevelUp"), m => m.Arg<string>(0).Contains("monte niveau 21"));
        }

        [Fact]
        public async Task SuperBonbon_AuNiveauDEvolution_LePokemonEvolue()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(Pkmn.Create("Salamèche", 15, "Griffe"));
            battle.Edit(p => p.Items.Single(i => i.Name == "Super Bonbon").Number = 1);

            await battle.UseItem("Super Bonbon", "special");

            Assert.Equal("Reptincel", battle.Mine.NameFr);
            Assert.Equal(16, battle.Mine.Level);
        }

        [Theory]
        [InlineData("Goupix", "Pierre Feu", "Feunard")]
        [InlineData("Pikachu", "Pierre Foudre", "Raichu")]
        [InlineData("Kokiyas", "Pierre Eau", "Crustabri")]
        [InlineData("Mélofée", "Pierre Lune", "Mélodelfe")]
        [InlineData("Ortide", "Pierre Plante", "Rafflesia")]
        [InlineData("Évoli", "Pierre Eau", "Aquali")]
        [InlineData("Évoli", "Pierre Foudre", "Voltali")]
        [InlineData("Évoli", "Pierre Feu", "Pyroli")]
        public async Task PierreDEvolution_FaitEvoluerLePokemon(string pokemon, string stone, string evolution)
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(Pkmn.Create(pokemon, 20, "Charge"));
            battle.Edit(p => p.Items.Single(i => i.Name == stone).Number = 1);
            string[] moves = battle.Mine.Moves.Select(m => m.NameFr!).ToArray();

            await battle.UseItem(stone, "special");

            PokemonTeam evolved = battle.Mine;
            Assert.Equal(evolution, evolved.NameFr);
            Assert.Equal(20, evolved.Level);
            Assert.Equal(moves, evolved.Moves.Select(m => m.NameFr));
            Assert.Equal(0, Count(battle, stone));
            Assert.Contains(battle.Received("pokemonLevelUp"), m => m.Arg<string>(0).Contains("a évolué en " + evolution));
        }

        [Fact]
        public async Task PierreDEvolution_SurUnPokemonDeLEquipe()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(bench: [Pkmn.Create("Caninos", 20)]);
            battle.Edit(p => p.Items.Single(i => i.Name == "Pierre Feu").Number = 1);

            await battle.UseItem("Pierre Feu", "special", 1);

            Assert.Equal("Salamèche", battle.Mine.NameFr);
            Assert.Equal("Arcanin", battle.Player.Team[1].NameFr);
        }

        [Fact]
        public async Task PierreDEvolution_SansEffetSurUnPokemonIncompatible_NestPasConsommee()
        {
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle();
            battle.Edit(p => p.Items.Single(i => i.Name == "Pierre Eau").Number = 1);

            await battle.UseItem("Pierre Eau", "special");

            Assert.Equal("Salamèche", battle.Mine.NameFr);
            Assert.Equal(1, Count(battle, "Pierre Eau"));
        }

        [Fact]
        public async Task PierreEtBonbon_NeDonnentPasDeTourALAdversaire()
        {
            // Le client envoie skipTurn = true pour les objets « spéciaux »
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(Pkmn.Create("Goupix", 20, "Charge").WithStats(hp: 100), Pkmn.Create("Rattata", 30, "Charge"));
            battle.Edit(p => p.Items.Single(i => i.Name == "Pierre Feu").Number = 1);
            PlayerMongo opponent = battle.Opponent;

            await battle.H.Hub(battle.Connection).HandleMove(battle.PlayerId, battle.Mine.Id, "item:Pierre Feu:special", opponent._id, opponent.Team[0].Id, true, false, 0, true);

            Assert.DoesNotContain(battle.Dialog, m => m.Contains("Rattata lance"));
        }

        // ---------- Pokéballs ----------

        [Fact]
        public async Task Capture_Reussie()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Catch, TestRandom.Lowest).Install();
            var battle = await WildBattle(wild: Pkmn.Create("Rattata", 5, "Charge"));
            int balls = Count(battle, "Pokeball");

            await battle.UseItem("Pokeball", "ball");

            Assert.Equal("Pokeball", battle.Received("launchBall").Single().Arg<string>(0));
            Assert.Equal(-1, battle.Received("catchResult").Single().Arg<int>(0));
            Assert.Equal("Rattata", battle.Received("caughtPokemon").Single().Arg<PlayerMongo>(0).Team[0].NameFr);
            Assert.Equal(balls - 1, Count(battle, "Pokeball"));
            Assert.DoesNotContain(battle.Dialog, m => m.Contains("Rattata lance"));
        }

        [Fact]
        public async Task Capture_Ratee_LAdversaireAttaque()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Catch, TestRandom.Highest).Install();
            var battle = await WildBattle(wild: Pkmn.Create("Mewtwo", 70, "Charge"));

            await battle.UseItem("Hyperball", "ball");

            Assert.Equal(0, battle.Received("catchResult").Single().Arg<int>(0));
            Assert.Empty(battle.Received("caughtPokemon"));
            Assert.True(battle.Said("Mewtwo lance Charge"));
            Assert.Equal(4, Count(battle, "Hyperball"));
        }

        [Fact]
        public async Task PokemonCapture_EstAjouteALEquipeEntierementSoigne()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam wild = Pkmn.Create("Rattata", 5, "Charge").WithHp(3);
            wild.IsParalyzed = true;
            wild.IsConfused = 2;
            wild.AtkChanges = -2;
            wild.Moves[0].Pp = 1;
            var battle = await WildBattle(wild: wild);

            await battle.H.Hub(battle.Connection).AddPokemonToTeam(battle.PlayerId, battle.OpponentRef._id, -1);

            PokemonTeam caught = battle.Player.Team.Last();
            Assert.Equal(2, battle.Player.Team.Length);
            Assert.Equal("Rattata", caught.NameFr);
            Assert.Equal(caught.BaseHp, caught.CurrHp);
            Assert.False(caught.IsParalyzed);
            Assert.Equal(0, caught.IsConfused);
            Assert.Equal(0, caught.AtkChanges);
            Assert.True(caught.Moves[0].MaxPp > 1);
            Assert.Equal(caught.Moves[0].MaxPp, caught.Moves[0].Pp);
            Assert.Equal(5, caught.Level);
            Assert.Equal(1, battle.Player.MapFightCount);
            Assert.Single(battle.Received("responseWildFight"));
        }

        [Fact]
        public async Task PokemonCapture_EquipePleine_RemplaceLePokemonChoisi()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam[] bench = Enumerable.Range(0, 5).Select(_ => Pkmn.Create("Chenipan", 3)).ToArray();
            var battle = await WildBattle(wild: Pkmn.Create("Rattata", 5, "Charge"), bench: bench);

            await battle.H.Hub(battle.Connection).AddPokemonToTeam(battle.PlayerId, battle.OpponentRef._id, 3);

            Assert.Equal(6, battle.Player.Team.Length);
            Assert.Equal("Rattata", battle.Player.Team[3].NameFr);
        }

        [Fact]
        public async Task PokemonCapture_JamaisPlusDe6PokemonDansLEquipe()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam[] bench = Enumerable.Range(0, 5).Select(_ => Pkmn.Create("Chenipan", 3)).ToArray();
            var battle = await WildBattle(wild: Pkmn.Create("Rattata", 5, "Charge"), bench: bench);

            await battle.H.Hub(battle.Connection).AddPokemonToTeam(battle.PlayerId, battle.OpponentRef._id, -1);

            Assert.Equal(6, battle.Player.Team.Length);
        }

        [Fact]
        public async Task Capture_RapporteDeLXPAuxParticipants()
        {
            // Règle du jeu (moderne) : capturer un Pokémon rapporte de l'XP comme une victoire
            using var _ = TestRandom.Neutral().Install();
            var battle = await WildBattle(wild: Pkmn.Create("Rattata", 5, "Charge"));
            int xp = battle.Mine.CurrXP;
            battle.Edit(p => p.Team[0].HavePlayed = true);

            await battle.H.Hub(battle.Connection).AddPokemonToTeam(battle.PlayerId, battle.OpponentRef._id, -1);

            Assert.True(battle.Mine.CurrXP > xp);
        }
    }

    // Montée de niveau et évolution (GameHub.CheckLevelUp)
    public class LevelUpAndEvolutionTests
    {
        private static async Task<(HubHarness h, PokemonTeam result)> GainXp(PokemonTeam pokemon, int targetLevel, int extraXp = 0)
        {
            var h = new HubHarness();
            pokemon.CurrXP = PokemonExperienceCalculator.ExpForLevel(targetLevel, pokemon.GrowthRate) + extraXp;
            PokemonTeam result = await h.Hub("conn").CheckLevelUp(pokemon);
            return (h, result);
        }

        [Fact]
        public async Task AssezDXP_LePokemonMonteDeNiveauEtSesStatsAugmentent()
        {
            PokemonTeam pokemon = Pkmn.Create("Carapuce", 10);
            int hp = pokemon.BaseHp, def = pokemon.Def;

            var (h, result) = await GainXp(pokemon, 11);

            Assert.Equal(11, result.Level);
            Assert.True(result.BaseHp >= hp && result.Def >= def && (result.BaseHp > hp || result.Def > def));
            Assert.Equal(PokemonExperienceCalculator.ExpForLevel(11, result.GrowthRate), result.XpFromLastLvl);
            Assert.Equal(PokemonExperienceCalculator.ExpForLevel(12, result.GrowthRate), result.XpForNextLvl);
            Assert.Contains("monte niveau 11", h.Named("pokemonLevelUp").Single().Arg<string>(0));
        }

        [Fact]
        public async Task PasAssezDXP_RienNeChange()
        {
            PokemonTeam pokemon = Pkmn.Create("Carapuce", 10);
            pokemon.CurrXP += 1;
            var h = new HubHarness();
            PokemonTeam result = await h.Hub("conn").CheckLevelUp(pokemon);
            Assert.Equal(10, result.Level);
            Assert.Empty(h.Named("pokemonLevelUp"));
        }

        [Fact]
        public async Task PlusieursNiveauxDUnCoup()
        {
            var (_, result) = await GainXp(Pkmn.Create("Carapuce", 10), 14, 5);
            Assert.Equal(14, result.Level);
        }

        [Fact]
        public async Task NiveauMaximum100()
        {
            var (_, result) = await GainXp(Pkmn.Create("Mew", 99), 100, 5_000_000);
            Assert.Equal(100, result.Level);
        }

        [Fact]
        public async Task MonteeDeNiveau_ConserveLesDegatsSubis()
        {
            PokemonTeam pokemon = Pkmn.Create("Carapuce", 10);
            pokemon.CurrHp -= 10;
            var (_, result) = await GainXp(pokemon, 11);
            Assert.Equal(10, result.BaseHp - result.CurrHp);
        }

        [Theory]
        [InlineData("Salamèche", 15, 16, "Reptincel")]
        [InlineData("Reptincel", 35, 36, "Dracaufeu")]
        [InlineData("Chenipan", 6, 7, "Chrysacier")]
        [InlineData("Magicarpe", 19, 20, "Léviator")]
        [InlineData("Miaouss", 27, 28, "Persian")]
        public async Task NiveauDEvolutionAtteint_LePokemonEvolue(string name, int from, int to, string evolution)
        {
            var (h, result) = await GainXp(Pkmn.Create(name, from), to);

            Assert.Equal(evolution, result.NameFr);
            Assert.Equal(to, result.Level);
            Assert.Contains("a évolué en " + evolution, h.Named("pokemonLevelUp").Single().Arg<string>(0));
        }

        [Theory]
        [InlineData("Kadabra", "Alakazam")]
        [InlineData("Machopeur", "Mackogneur")]
        [InlineData("Gravalanch", "Grolem")]
        [InlineData("Spectrum", "Ectoplasma")]
        public async Task EvolutionParEchange_SeFaitAuNiveau37(string name, string evolution)
        {
            var (_, before) = await GainXp(Pkmn.Create(name, 35), 36);
            Assert.Equal(name, before.NameFr);
            var (_, result) = await GainXp(Pkmn.Create(name, 36), 37);
            Assert.Equal(evolution, result.NameFr);
        }

        [Fact]
        public async Task NiveauDEvolutionDepasse_LePokemonEvolueALaProchaineMontee()
        {
            // Pokémon capturé au-dessus de son niveau d'évolution
            var (_, result) = await GainXp(Pkmn.Create("Salamèche", 25), 26);
            Assert.Equal("Reptincel", result.NameFr);
        }

        [Fact]
        public async Task EvolutionConserveCapacitesXPDegatsShinyIdentifiantEtStatut()
        {
            PokemonTeam pokemon = Pkmn.Create("Salamèche", 15, "Griffe", "Flammèche");
            pokemon.IsShiny = true;
            pokemon.CurrHp -= 8;
            pokemon.IsPoisoned = 1;
            string id = pokemon.Id;

            var (_, result) = await GainXp(pokemon, 16);

            Assert.Equal("Reptincel", result.NameFr);
            Assert.Equal(new[] { "Griffe", "Flammèche" }, result.Moves.Select(m => m.NameFr));
            Assert.Equal(PokemonExperienceCalculator.ExpForLevel(16, "medium-slow"), result.CurrXP);
            Assert.Equal(8, result.BaseHp - result.CurrHp);
            Assert.True(result.IsShiny);
            Assert.Equal(id, result.Id);
            Assert.Equal(1, result.IsPoisoned);
        }

        [Fact]
        public async Task EvolutionUtiliseLesStatsDeLaNouvelleEspece()
        {
            var (_, result) = await GainXp(Pkmn.Create("Salamèche", 15), 16);
            Assert.Equal(PokemonStatCalculator.CalculateHp(GameData.Pokemon("Reptincel").Stats.Hp, 16, GameData.Pokemon("Reptincel").Stats.HpE), result.BaseHp);
            Assert.Equal(5, result.IdDex);
        }

        [Theory]
        [InlineData("Évoli", 30)]
        [InlineData("Pikachu", 40)]
        [InlineData("Goupix", 40)]
        [InlineData("Ronflex", 40)]
        public async Task PasDEvolutionParNiveau(string name, int level)
        {
            var (_, result) = await GainXp(Pkmn.Create(name, level - 1), level);
            Assert.Equal(name, result.NameFr);
        }

        [Theory]
        [InlineData("M. Mime", 41, 42)]     // M. Glaquette (8G) absent du jeu
        [InlineData("Nosferalto", 40, 41)]  // Nostenfer (amitié, 2G)
        [InlineData("Excelangue", 40, 41)]
        public async Task EvolutionHorsGen1_IgnoreeSansErreur(string name, int from, int to)
        {
            var (_, result) = await GainXp(Pkmn.Create(name, from), to);
            Assert.Equal(name, result.NameFr);
            Assert.Equal(to, result.Level);
        }

        [Fact]
        public async Task NouvelleCapacite_ApprisedirectementSiMoinsDe4()
        {
            var (species, level, move) = FindLearnableMove(fourMoves: false);
            PokemonTeam pokemon = Pkmn.Create(species, level - 1, "Charge");

            var (h, result) = await GainXp(pokemon, level);

            Assert.Contains(result.Moves, m => m.NameFr == move);
            Assert.Contains("apprend " + move, h.Named("pokemonLevelUp").Single().Arg<string>(0));
        }

        [Fact]
        public async Task NouvelleCapacite_ProposeeSiDeja4Capacites()
        {
            var (species, level, move) = FindLearnableMove(fourMoves: true);
            PokemonTeam pokemon = Pkmn.Create(species, level - 1, "Charge", "Griffe", "Rugissement", "Trempette");

            var (h, result) = await GainXp(pokemon, level);

            Assert.Equal(4, result.Moves.Length);
            var proposed = h.Named("pokemonLevelUp").Single().Arg<List<PkmnRaceBattle.Domain.Models.PokemonMongo.MoveMongo>>(2);
            Assert.Contains(proposed, m => m.NameFr == move);
        }

        [Fact]
        public async Task ApprendreUneCapacite_RemplaceLAncienne()
        {
            var (species, level, move) = FindLearnableMove(fourMoves: true);
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create(species, level, "Charge", "Griffe", "Rugissement", "Trempette"));
            int oldId = player.Team[0].Moves[1].Id;
            int newId = GameData.Pokemon(species).Moves.First(m => m.NameFr == move).Id;

            await h.Hub("c").LearnMove(oldId, newId, player.Team[0].Id, player._id);

            var moves = h.Players.Get(player._id).Team[0].Moves.Select(m => m.NameFr).ToList();
            Assert.Equal(new[] { "Charge", move, "Rugissement", "Trempette" }, moves);
            Assert.Single(h.Named("moveLearned"));
        }

        // Une espèce qui apprend une capacité à un niveau donné (hors Charge / Griffe / Rugissement / Trempette)
        private static (string species, int level, string move) FindLearnableMove(bool fourMoves)
        {
            string[] excluded = ["Charge", "Griffe", "Rugissement", "Trempette"];
            foreach (var pokemon in GameData.Pokemons.Where(p => p.EvolutionDetails.All(e => e.MinLevel == null)))
                foreach (var move in pokemon.Moves.Where(m => m.LearnedAtLvl is > 5 and < 60 && !excluded.Contains(m.NameFr)))
                    if (pokemon.Moves.Count(m => m.LearnedAtLvl == move.LearnedAtLvl) == 1)
                        return (pokemon.NameFr!, move.LearnedAtLvl!.Value, move.NameFr!);
            throw new InvalidOperationException();
        }

        [Fact]
        public async Task VictoireDonnantAssezDXP_EvolutionApresLeCombat()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam charmander = Pkmn.Create("Salamèche", 15, "Griffe").WithStats(atk: 999, speed: 300);
            charmander.CurrXP = PokemonExperienceCalculator.ExpForLevel(16, "medium-slow") - 1;
            var battle = await Battle.VsWild(charmander, Pkmn.Create("Rattata", 5, "Trempette").WithStats(hp: 5));

            await battle.Use("Griffe");

            Assert.Equal("Reptincel", battle.Mine.NameFr);
            Assert.Equal(16, battle.Mine.Level);
        }
    }
}
