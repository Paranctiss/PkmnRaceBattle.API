using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.API.Helpers.PathManager;
using PkmnRaceBattle.API.Helpers.StatsCalculator;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using PkmnRaceBattle.Tests.Support;
using PathModel = PkmnRaceBattle.Domain.Models.PlayerMongo.Path;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Multi Exp et multiplicateur d'XP choisis par l'hôte (XpSettings, PokemonExperienceCalculator.ExpReceived)
    public class XpSettingsTests
    {
        private static readonly PokemonTeam Defeated = Pkmn.Create("Ronflex", 30);
        private static int Full => PokemonExperienceCalculator.ExpGained(Defeated, false, false, 1);

        [Fact]
        public void ParDefaut_MultiExpActiveEtXPNormale()
        {
            Assert.Equal(new XpSettings(true, 1), XpSettings.Default);
            Assert.Equal(XpSettings.Default, XpSettings.FromRoom(null));
            Assert.Equal(XpSettings.Default, XpSettings.FromRoom(new RoomMongo()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(100)]
        public void MultiplicateurInconnu_XPNormale(int multiplier)
        {
            Assert.Equal(1, XpSettings.FromRoom(new RoomMongo { XpMultiplier = multiplier }).Multiplier);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Participant_ToujoursToutelXP(bool multiXp)
        {
            Assert.Equal(Full, PokemonExperienceCalculator.ExpReceived(Defeated, false, true, new XpSettings(multiXp, 1)));
        }

        [Fact]
        public void NonParticipant_MoitieDeLXPAvecLeMultiExp()
        {
            Assert.Equal(Full / 2, PokemonExperienceCalculator.ExpReceived(Defeated, false, false, new XpSettings(true, 1)));
        }

        [Fact]
        public void NonParticipant_RienSansLeMultiExp()
        {
            Assert.Equal(0, PokemonExperienceCalculator.ExpReceived(Defeated, false, false, new XpSettings(false, 5)));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        public void Multiplicateur_SAppliqueAuxParticipantsEtAuResteDeLEquipe(int multiplier)
        {
            var settings = new XpSettings(true, multiplier);
            Assert.Equal(Full * multiplier, PokemonExperienceCalculator.ExpReceived(Defeated, false, true, settings));
            Assert.Equal(Full / 2 * multiplier, PokemonExperienceCalculator.ExpReceived(Defeated, false, false, settings));
        }

        [Fact]
        public void BonusDresseur_ConserveAvecLeMultiplicateur()
        {
            int trainerFull = PokemonExperienceCalculator.ExpGained(Defeated, true, false, 1);
            Assert.Equal(trainerFull * 2, PokemonExperienceCalculator.ExpReceived(Defeated, true, true, new XpSettings(false, 2)));
        }
    }

    // Paliers de niveaux des zones de combat (ZoneLevels)
    public class ZoneLevelsTests
    {
        public static IEnumerable<object[]> AllSettings() =>
            from multiXp in new[] { true, false }
            from multiplier in XpSettings.AllowedMultipliers
            select new object[] { multiXp, multiplier };

        [Theory]
        [MemberData(nameof(AllSettings))]
        public void Paliers_CoherentsEtCroissants(bool multiXp, int multiplier)
        {
            var settings = new XpSettings(multiXp, multiplier);
            ZoneLevelRange previous = ZoneLevels.GetRange(1, settings);
            Assert.Equal(2, previous.WildMin);
            for (int zone = 1; zone <= 30; zone++)
            {
                ZoneLevelRange range = ZoneLevels.GetRange(zone, settings);
                Assert.True(range.WildMin >= 2 && range.TrainerMax <= 100, $"zone {zone} : {range}");
                Assert.True(range.WildMin <= range.WildMax, $"zone {zone} : {range}");
                Assert.True(range.TrainerMin <= range.TrainerMax, $"zone {zone} : {range}");
                // Le dresseur clôt la zone : jamais plus faible que les sauvages
                Assert.True(range.TrainerMin >= range.WildMax - 1 && (range.TrainerMax > range.WildMax || range.TrainerMax == 100), $"zone {zone} : {range}");
                if (zone > 1)
                {
                    // Au plafond (niveau 100), les zones suivantes restent au maximum
                    Assert.True(range.WildMin > previous.WildMin || range.TrainerMax == 100, $"zone {zone} : {range} après {previous}");
                    Assert.True(range.TrainerMax >= previous.TrainerMax, $"zone {zone} : {range} après {previous}");
                }
                previous = range;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void XPPlusRapide_ZonesPlusHautes(bool multiXp)
        {
            for (int zone = 2; zone <= 16; zone++)
            {
                int x1 = ZoneLevels.GetRange(zone, new XpSettings(multiXp, 1)).WildMin;
                int x2 = ZoneLevels.GetRange(zone, new XpSettings(multiXp, 2)).WildMin;
                int x5 = ZoneLevels.GetRange(zone, new XpSettings(multiXp, 5)).WildMin;
                Assert.True(x1 < x2 && x2 < x5, $"zone {zone} : x1 {x1}, x2 {x2}, x5 {x5}");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void SansMultiExp_ZonesUnPeuPlusDouces(int multiplier)
        {
            for (int zone = 2; zone <= 16; zone++)
            {
                ZoneLevelRange on = ZoneLevels.GetRange(zone, new XpSettings(true, multiplier));
                ZoneLevelRange off = ZoneLevels.GetRange(zone, new XpSettings(false, multiplier));
                Assert.True(off.WildMin <= on.WildMin && off.TrainerMax <= on.TrainerMax, $"zone {zone} : {off} / {on}");
            }
        }

        [Fact]
        public void PremiereZone_PlusFaibleQueLeStarter()
        {
            foreach (object[] s in AllSettings())
            {
                ZoneLevelRange range = ZoneLevels.GetRange(1, new XpSettings((bool)s[0], (int)s[1]));
                Assert.True(range.WildMin < 5, range.ToString());
            }
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(5, 5)]
        [InlineData(6, 6)]
        [InlineData(8, 6)]  // fin du chemin : 6 Pokémon au plus
        [InlineData(12, 6)] // tours de boucle
        public void Dresseur_UnPokemonParZone_SixAuPlus(int zone, int expected)
        {
            Assert.Equal(expected, ZoneLevels.TrainerTeamSize(zone));
        }

        [Fact]
        public void Paliers_MoinsDifficiles_SauvagesSousLeDresseur()
        {
            // Les sauvages de la zone ne dépassent jamais le dresseur qui la clôt
            foreach (object[] s in AllSettings())
                for (int zone = 1; zone <= 16; zone++)
                {
                    ZoneLevelRange range = ZoneLevels.GetRange(zone, new XpSettings((bool)s[0], (int)s[1]));
                    Assert.True(range.WildMax <= range.TrainerMin, $"zone {zone} : {range}");
                }
        }

        [Fact]
        public void NiveauSauvage_MonteDuBasAuHautDeLaFourchette()
        {
            var range = new ZoneLevelRange(10, 18, 18, 20);
            for (int seed = 0; seed < 30; seed++)
            {
                using var _ = new TestRandom(seed).Install();
                Assert.InRange(ZoneLevels.WildLevel(range, 0), 10, 11);
                Assert.InRange(ZoneLevels.WildLevel(range, 2), 13, 15);
                Assert.InRange(ZoneLevels.WildLevel(range, 4), 17, 18);
            }
        }

        private static PlayerMongo PlayerOn(int x, params (int X, string Env)[] steps)
        {
            var player = new PlayerMongo
            {
                PlayerPath = new PathModel { PathPoints = steps.Select(s => new PathPoint { X = s.X, Y = 1, EnvironmentName = s.Env }).ToList() },
            };
            player.CurrentPath = x == 0 ? new PathPoint { X = 0, EnvironmentName = "Default" } : player.PlayerPath.PathPoints.First(p => p.X == x);
            return player;
        }

        private static readonly (int, string)[] Steps =
            [(1, "Plaine"), (2, "Foret"), (3, "Shop"), (4, "Grotte"), (5, "Eau"), (6, "Centre"), (7, "Volcan")];

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 2)] // Shop : pas de nouvelle zone
        [InlineData(4, 3)]
        [InlineData(5, 4)]
        [InlineData(6, 4)] // Centre
        [InlineData(7, 5)]
        public void Zone_SeulesLesMapsDeCombatComptent(int x, int expectedZone)
        {
            Assert.Equal(expectedZone, ZoneLevels.GetZone(PlayerOn(x, Steps)));
        }

        [Fact]
        public void Zone_EmbranchementCompteUneSeuleFois()
        {
            PlayerMongo player = PlayerOn(3, (1, "Plaine"), (2, "Foret"), (3, "Volcan"));
            player.PlayerPath.PathPoints.Add(new PathPoint { X = 2, Y = 2, EnvironmentName = "Eau" });
            Assert.Equal(3, ZoneLevels.GetZone(player));
        }

        [Fact]
        public void Zone_ToursDeBoucleAjoutes()
        {
            PlayerMongo player = PlayerOn(7, Steps);
            player.PathLoopCount = 3;
            Assert.Equal(8, ZoneLevels.GetZone(player));
        }

        [Fact]
        public void CheminGenere_DeuxZonesDeCombatToutesLesQuatreEtapes()
        {
            // Plaine, combat, Centre, Boutique, puis (combat, combat, Centre, Boutique) à l'infini
            using var _ = new TestRandom(3).Install();
            var player = new PlayerMongo();
            PlayerPathHelper.InitPlayerPath(player);
            PlayerPathHelper.AppendSteps(player.PlayerPath, 24);
            foreach ((int x, int zone) in new[] { (1, 1), (2, 2), (4, 2), (5, 3), (6, 4), (10, 6), (34, 18) })
            {
                player.CurrentPath = player.PlayerPath.PathPoints.First(p => p.X == x);
                Assert.Equal(zone, ZoneLevels.GetZone(player));
            }
        }

        [Fact]
        public void AnnoterLeChemin_FourchetteSurLesMapsDeCombatSeulement()
        {
            PlayerMongo player = PlayerOn(0, Steps);
            var settings = new XpSettings(true, 2);

            ZoneLevels.AnnotatePath(player, settings);

            PathPoint grotte = player.PlayerPath.PathPoints.Single(p => p.X == 4);
            ZoneLevelRange zone3 = ZoneLevels.GetRange(3, settings);
            Assert.Equal((zone3.WildMin, zone3.TrainerMax), (grotte.MinLevel, grotte.MaxLevel));
            Assert.All(player.PlayerPath.PathPoints.Where(p => p.EnvironmentName is "Shop" or "Centre"), p => Assert.Null(p.MinLevel));
            Assert.Null(player.CurrentPath.MinLevel);
        }
    }
}
