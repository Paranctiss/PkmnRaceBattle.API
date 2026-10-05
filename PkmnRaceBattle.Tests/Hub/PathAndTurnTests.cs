using PkmnRaceBattle.API.Helpers.PathManager;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;
using PathModel = PkmnRaceBattle.Domain.Models.PlayerMongo.Path;

namespace PkmnRaceBattle.Tests.Hub
{
    // Génération de la carte et règles de progression (PlayerPathHelper)
    public class PathGenerationTests
    {
        private static readonly string[] FightEnvironments = ["Plaine", "Volcan", "Foret", "Grotte", "Centrale", "Eau"];
        private static readonly string[] Services = ["Shop", "Centre"];

        public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 40).Select(s => new object[] { s });

        private static PathModel Generate(int seed)
        {
            using var _ = new TestRandom(seed).Install();
            return PlayerPathHelper.GenerateNewPath();
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void Chemin_De11EtapesConsecutives(int seed)
        {
            PathModel path = Generate(seed);
            Assert.Equal(Enumerable.Range(1, 11), path.PathPoints.Select(p => p.X).Distinct().OrderBy(x => x));
            Assert.All(path.PathPoints.GroupBy(p => p.X), step =>
            {
                Assert.InRange(step.Count(), 1, 2);
                Assert.Equal(Enumerable.Range(1, step.Count()), step.Select(p => p.Y).OrderBy(y => y));
            });
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void Chemin_CommenceToujoursParLaPlaine(int seed)
        {
            PathPoint first = Assert.Single(Generate(seed).PathPoints, p => p.X == 1);
            Assert.Equal("Plaine", first.EnvironmentName);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void Chemin_UneHalteApresChaquePaireDeMapsDeCombat(int seed)
        {
            var steps = Generate(seed).PathPoints.GroupBy(p => p.X).OrderBy(g => g.Key).ToList();
            int fightsSinceService = 0;
            foreach (var step in steps)
            {
                bool isService = step.All(p => Services.Contains(p.EnvironmentName));
                if (isService)
                {
                    Assert.Equal(2, fightsSinceService);
                    Assert.Single(step);
                    fightsSinceService = 0;
                }
                else
                {
                    Assert.All(step, p => Assert.Contains(p.EnvironmentName, FightEnvironments));
                    fightsSinceService++;
                    Assert.True(fightsSinceService <= 2, $"Étape {step.Key} : plus de 2 maps de combat sans halte");
                }
            }
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void Chemin_LesEmbranchementsProposentDeuxEnvironnementsDifferents(int seed)
        {
            foreach (var step in Generate(seed).PathPoints.GroupBy(p => p.X).Where(g => g.Count() == 2))
            {
                Assert.NotEqual(step.First().EnvironmentName, step.Last().EnvironmentName);
            }
        }

        [Fact]
        public void Chemin_ContientDesEmbranchements()
        {
            Assert.Contains(Generate(1).PathPoints.GroupBy(p => p.X), g => g.Count() == 2);
        }

        [Fact]
        public void Chemin_TousLesEnvironnementsDeCombatPeuventApparaitre()
        {
            var seen = Enumerable.Range(1, 200).SelectMany(s => Generate(s).PathPoints).Select(p => p.EnvironmentName).ToHashSet();
            Assert.All(FightEnvironments.Concat(Services), env => Assert.Contains(env, seen));
        }

        [Fact]
        public void MapDeCombat_TermineeApresCinqSauvagesEtUnDresseur()
        {
            var player = new PlayerMongo { CurrentPath = new PathPoint { X = 1, Y = 1, EnvironmentName = "Plaine" } };
            for (int fights = 0; fights < 5; fights++)
            {
                player.MapFightCount = fights;
                Assert.False(PlayerPathHelper.IsTrainerFightNext(player));
                Assert.False(PlayerPathHelper.IsCurrentMapCompleted(player));
            }
            player.MapFightCount = 5;
            Assert.True(PlayerPathHelper.IsTrainerFightNext(player));
            Assert.False(PlayerPathHelper.IsCurrentMapCompleted(player));
            player.MapFightCount = 6;
            Assert.True(PlayerPathHelper.IsCurrentMapCompleted(player));
        }

        [Theory]
        [InlineData("Shop")]
        [InlineData("Centre")]
        [InlineData("Default")]
        public void Halte_TermineeDesQuOnLaQuitte(string environment)
        {
            var player = new PlayerMongo { CurrentPath = new PathPoint { X = 3, Y = 1, EnvironmentName = environment } };
            Assert.True(PlayerPathHelper.IsCurrentMapCompleted(player));
        }

        [Fact]
        public void Deplacement_GriseLaBrancheNonChoisieEtRemetLeCompteurAZero()
        {
            var player = new PlayerMongo { MapFightCount = 6 };
            PlayerPathHelper.InitPlayerPath(player);
            player.PlayerPath = new PathModel
            {
                PathPoints =
                [
                    new PathPoint { X = 1, Y = 1, EnvironmentName = "Plaine" },
                    new PathPoint { X = 2, Y = 1, EnvironmentName = "Foret" },
                    new PathPoint { X = 2, Y = 2, EnvironmentName = "Grotte" },
                ]
            };
            player.MapFightCount = 6;
            PathPoint chosen = player.PlayerPath.PathPoints[2];

            PlayerPathHelper.MoveTo(player, chosen);

            Assert.Same(chosen, player.CurrentPath);
            Assert.Equal(0, player.MapFightCount);
            Assert.True(player.PlayerPath.PathPoints[1].IsSkipped);
            Assert.False(player.PlayerPath.PathPoints[2].IsSkipped);
            Assert.False(player.PlayerPath.PathPoints[0].IsSkipped);
        }
    }

    // Enchaînement des tours (GetNewTurn / ChooseNextPath) : la carte affichée est-elle respectée ?
    public class TurnFlowTests
    {
        private static readonly PathModel BranchingPath = new()
        {
            PathPoints =
            [
                new PathPoint { X = 1, Y = 1, EnvironmentName = "Plaine" },
                new PathPoint { X = 2, Y = 1, EnvironmentName = "Foret" },
                new PathPoint { X = 3, Y = 1, EnvironmentName = "Volcan" },
                new PathPoint { X = 3, Y = 2, EnvironmentName = "Eau" },
                new PathPoint { X = 4, Y = 1, EnvironmentName = "Centre" },
                new PathPoint { X = 5, Y = 1, EnvironmentName = "Grotte" },
                new PathPoint { X = 6, Y = 1, EnvironmentName = "Shop" },
            ]
        };

        private static async Task<(HubHarness h, PlayerMongo player)> Setup(int x = 0, int y = 0, string env = "Default", int fights = 0, int level = 5)
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", level));
            player.PlayerPath = GameData.Clone(BranchingPath);
            player.CurrentPath = x == 0 ? new PathPoint { X = 0, Y = 0, EnvironmentName = "Default" } : player.PlayerPath.PathPoints.Single(p => p.X == x && p.Y == y);
            player.CurrentPath.EnvironmentName = x == 0 ? "Default" : player.CurrentPath.EnvironmentName;
            player.MapFightCount = fights;
            h.UpdatePlayer(player);
            return (h, player);
        }

        private static Task NewTurn(HubHarness h, PlayerMongo player) => h.Hub(HubHarness.Connection(player._id)).GetNewTurn(player._id);

        [Fact]
        public async Task PremierTour_AmeneSurLaPremiereMapEtLanceUnCombatSauvage()
        {
            var (h, player) = await Setup();
            using var _ = TestRandom.Neutral().Install();

            await NewTurn(h, player);

            PlayerMongo updated = h.Players.Get(player._id);
            Assert.Equal(1, updated.CurrentPath.X);
            Assert.Equal("Plaine", updated.CurrentPath.EnvironmentName);
            SentMessage fight = h.Named("responseWildFight").Single();
            Assert.Equal("Plaine", fight.Arg<PlayerMongo>(1).CurrentPath.EnvironmentName);
            Assert.Equal(new[] { "Plaine" }, h.Pokemons.RequestedEnvironments);
        }

        [Fact]
        public async Task CombatSauvage_LePokemonVientDeLEnvironnementDeLaMap()
        {
            var (h, player) = await Setup(5, 1, "Grotte");
            using var _ = new TestRandom(7).Install();

            await NewTurn(h, player);

            PlayerMongo wild = h.Named("responseWildFight").Single().Arg<PlayerMongo>(0);
            var allowed = GameData.Environments.Single(e => e.Name == "Grotte").PossiblePokemons.Select(p => p.PokemonId);
            Assert.Contains(wild.Team[0].IdDex, allowed);
            Assert.False(wild.IsTrainer);
            Assert.False(wild.IsPlayer);
            Assert.NotNull(h.Opponents.Get(wild._id));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(20)]
        [InlineData(45)]
        public async Task CombatSauvage_NiveauEntreMoyenneMoins3EtMoyenneMoins2(int teamLevel)
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var (h, player) = await Setup(1, 1, "Plaine", level: teamLevel);
                using var _ = new TestRandom(seed).Install();
                await NewTurn(h, player);
                int level = h.Named("responseWildFight").Single().Arg<PlayerMongo>(0).Team[0].Level;
                Assert.InRange(level, Math.Max(1, teamLevel - 3), Math.Max(1, teamLevel - 2));
            }
        }

        [Theory]
        [InlineData("Plaine", 5)]
        [InlineData("Foret", 8)]
        [InlineData("Grotte", 20)]
        public async Task CombatSauvage_JamaisSousLeNiveauMinimumDeLEspeceDansLaMap(string environment, int teamLevel)
        {
            // Environment.PossiblePokemons[].MinimumLevel : un Herbizarre (16) ne doit pas apparaître face à une équipe niveau 5
            var minimums = GameData.Environments.Single(e => e.Name == environment).PossiblePokemons.ToDictionary(p => p.PokemonId, p => p.MinimumLevel);
            var errors = new List<string>();
            for (int seed = 0; seed < 60; seed++)
            {
                var (h, player) = await Setup(1, 1, "Plaine", level: teamLevel);
                PlayerMongo p = h.Players.Get(player._id);
                p.CurrentPath.EnvironmentName = environment;
                h.UpdatePlayer(p);
                using var _ = new TestRandom(seed).Install();

                await NewTurn(h, player);

                PokemonTeam wild = h.Named("responseWildFight").Single().Arg<PlayerMongo>(0).Team[0];
                if (wild.Level < minimums[wild.IdDex]) errors.Add($"{wild.NameFr} niveau {wild.Level} (minimum {minimums[wild.IdDex]})");
            }
            Assert.True(errors.Count == 0, string.Join(", ", errors.Distinct()));
        }

        [Fact]
        public async Task ApresCinqCombatsSauvages_CombatDeDresseur()
        {
            var (h, player) = await Setup(1, 1, "Plaine", fights: 5, level: 20);
            using var _ = new TestRandom(3).Install();

            await NewTurn(h, player);

            Assert.Empty(h.Named("responseWildFight"));
            PlayerMongo trainer = h.Named("responseTrainerFight").Single().Arg<PlayerMongo>(0);
            Assert.True(trainer.IsTrainer);
            Assert.False(trainer.IsPlayer);
            Assert.Equal(3, trainer.Team.Length);
            Assert.False(string.IsNullOrEmpty(trainer.Name));
            Assert.All(trainer.Team, p => Assert.InRange(p.Level, 15, 18));
        }

        [Fact]
        public async Task CombatDeDresseur_NiveauJamaisInferieurA1()
        {
            var (h, player) = await Setup(1, 1, "Plaine", fights: 5, level: 3);
            using var _ = new TestRandom(3).Install();
            await NewTurn(h, player);
            Assert.All(h.Named("responseTrainerFight").Single().Arg<PlayerMongo>(0).Team, p => Assert.True(p.Level >= 1));
        }

        [Fact]
        public async Task MapTerminee_UneSeuleDestination_LeJoueurYVaDirectement()
        {
            var (h, player) = await Setup(1, 1, "Plaine", fights: 6);
            using var _ = TestRandom.Neutral().Install();

            await NewTurn(h, player);

            PlayerMongo updated = h.Players.Get(player._id);
            Assert.Equal((2, "Foret"), (updated.CurrentPath.X, updated.CurrentPath.EnvironmentName));
            Assert.Equal(0, updated.MapFightCount);
            Assert.Single(h.Named("responseWildFight"));
            Assert.Equal(new[] { "Foret" }, h.Pokemons.RequestedEnvironments);
        }

        [Fact]
        public async Task MapTerminee_Embranchement_LeJoueurDoitChoisir()
        {
            var (h, player) = await Setup(2, 1, "Foret", fights: 6);

            await NewTurn(h, player);

            var options = h.Named("chooseNextPath").Single().Arg<List<PathPoint>>(0);
            Assert.Equal(new[] { "Volcan", "Eau" }, options.Select(o => o.EnvironmentName));
            Assert.Empty(h.Named("responseWildFight"));
            Assert.Equal(2, h.Players.Get(player._id).CurrentPath.X);
        }

        [Theory]
        [InlineData(1, "Volcan", "Eau")]
        [InlineData(2, "Eau", "Volcan")]
        public async Task ChoixDeDestination_EstRespecte(int y, string chosen, string skipped)
        {
            var (h, player) = await Setup(2, 1, "Foret", fights: 6);
            using var _ = TestRandom.Neutral().Install();

            await h.Hub(HubHarness.Connection(player._id)).ChooseNextPath(player._id, 3, y);

            PlayerMongo updated = h.Players.Get(player._id);
            Assert.Equal(chosen, updated.CurrentPath.EnvironmentName);
            Assert.True(updated.PlayerPath.PathPoints.Single(p => p.EnvironmentName == skipped).IsSkipped);
            Assert.False(updated.PlayerPath.PathPoints.Single(p => p.EnvironmentName == chosen).IsSkipped);
            Assert.Equal(new[] { chosen }, h.Pokemons.RequestedEnvironments);
        }

        [Fact]
        public async Task ChoixDeDestination_InvalideEstIgnore()
        {
            var (h, player) = await Setup(2, 1, "Foret", fights: 6);

            await h.Hub(HubHarness.Connection(player._id)).ChooseNextPath(player._id, 5, 1);

            Assert.Equal(2, h.Players.Get(player._id).CurrentPath.X);
            Assert.Single(h.Named("chooseNextPath"));
        }

        [Fact]
        public async Task ChoixDeDestination_RefuseSiLaMapNEstPasTerminee()
        {
            var (h, player) = await Setup(2, 1, "Foret", fights: 2);
            using var _ = TestRandom.Neutral().Install();

            await h.Hub(HubHarness.Connection(player._id)).ChooseNextPath(player._id, 3, 2);

            Assert.Equal("Foret", h.Players.Get(player._id).CurrentPath.EnvironmentName);
        }

        [Fact]
        public async Task Centre_EnvoieLEcranDuCentrePokemon()
        {
            var (h, player) = await Setup(3, 1, "Volcan", fights: 6);
            await NewTurn(h, player);
            Assert.Single(h.Named("responsePokeCenter"));
            Assert.Equal("Centre", h.Players.Get(player._id).CurrentPath.EnvironmentName);
        }

        [Fact]
        public async Task Boutique_EnvoieLEcranDeLaBoutique()
        {
            var (h, player) = await Setup(5, 1, "Grotte", fights: 6);
            await NewTurn(h, player);
            Assert.Single(h.Named("responsePokeShop"));
        }

        [Fact]
        public async Task QuitterUneHalte_PasseALaMapSuivante()
        {
            var (h, player) = await Setup(4, 1, "Centre");
            using var _ = TestRandom.Neutral().Install();
            await NewTurn(h, player);
            Assert.Equal("Grotte", h.Players.Get(player._id).CurrentPath.EnvironmentName);
            Assert.Single(h.Named("responseWildFight"));
        }

        [Fact]
        public async Task FinDuChemin_LeJoueurResteSurSaMapEtRecommenceUnCycle()
        {
            var (h, player) = await Setup(5, 1, "Grotte", fights: 6);
            PlayerMongo p = h.Players.Get(player._id);
            p.PlayerPath.PathPoints.RemoveAll(pp => pp.X == 6);
            h.UpdatePlayer(p);
            using var _ = TestRandom.Neutral().Install();

            await NewTurn(h, player);

            PlayerMongo updated = h.Players.Get(player._id);
            Assert.Equal("Grotte", updated.CurrentPath.EnvironmentName);
            Assert.Equal(0, updated.MapFightCount);
            Assert.Single(h.Named("responseWildFight"));
        }
    }
}
