using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Pokéballs : formule de capture du jeu (Gen 3+, choix de design conservé)
    // TryCatchPokemon renvoie -1 si capturé, sinon le nombre de secousses (0 à 3)
    public class CatchTests
    {
        private static PokemonTeam Wild(string name = "Rattata", int level = 10) => Pkmn.Create(name, level);

        [Fact]
        public void Masterball_CaptureToujours()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Catch, TestRandom.Highest).Install();
            Assert.Equal(-1, FightCatch.TryCatchPokemon(Wild("Mewtwo", 70), "Masterball"));
        }

        [Fact]
        public void JetFavorable_Capture()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Catch, TestRandom.Lowest).Install();
            Assert.Equal(-1, FightCatch.TryCatchPokemon(Wild(), "Pokeball"));
        }

        [Fact]
        public void JetDefavorable_LePokemonSEchappeSansSecousse()
        {
            using var _ = TestRandom.Neutral().Set(RandomPurpose.Catch, TestRandom.Highest).Install();
            Assert.Equal(0, FightCatch.TryCatchPokemon(Wild("Mewtwo", 70), "Pokeball"));
        }

        [Fact]
        public void NombreDeSecousses_EntreZeroEtTroisSiEchec()
        {
            for (int fail = 0; fail < 4; fail++)
            {
                var rng = TestRandom.Neutral();
                for (int i = 0; i < fail; i++) rng.Queue(RandomPurpose.Catch, TestRandom.Lowest);
                rng.Queue(RandomPurpose.Catch, TestRandom.Highest);
                using var _ = rng.Install();
                Assert.Equal(fail, FightCatch.TryCatchPokemon(Wild("Mewtwo", 70), "Pokeball"));
            }
        }

        [Fact]
        public void MoinsDePV_PlusDeChancesDeCapture()
        {
            PokemonTeam full = Wild("Ronflex", 30);
            PokemonTeam low = Wild("Ronflex", 30).WithHp(1);
            Assert.True(FightCatch.CalculateCatchProbabilityPercentage(low, "Pokeball") > FightCatch.CalculateCatchProbabilityPercentage(full, "Pokeball"));
        }

        [Fact]
        public void MeilleureBall_PlusDeChancesDeCapture()
        {
            PokemonTeam pokemon = Wild("Ronflex", 30);
            double poke = FightCatch.CalculateCatchProbabilityPercentage(pokemon, "Pokeball");
            double super_ = FightCatch.CalculateCatchProbabilityPercentage(pokemon, "Superball");
            double hyper = FightCatch.CalculateCatchProbabilityPercentage(pokemon, "Hyperball");
            Assert.True(poke < super_ && super_ < hyper, $"{poke} < {super_} < {hyper}");
        }

        [Theory]
        [InlineData("sleep")]
        [InlineData("freeze")]
        [InlineData("paralysis")]
        [InlineData("burn")]
        [InlineData("poison")]
        public void Statut_AugmenteLesChancesDeCapture(string status)
        {
            PokemonTeam healthy = Wild("Ronflex", 30);
            PokemonTeam affected = Wild("Ronflex", 30);
            switch (status)
            {
                case "sleep": affected.IsSleeping = 2; break;
                case "freeze": affected.IsFrozen = true; break;
                case "paralysis": affected.IsParalyzed = true; break;
                case "burn": affected.IsBurning = true; break;
                case "poison": affected.IsPoisoned = 1; break;
            }
            Assert.True(FightCatch.CalculateCatchProbabilityPercentage(affected, "Pokeball") > FightCatch.CalculateCatchProbabilityPercentage(healthy, "Pokeball"));
        }

        [Fact]
        public void SommeilEtGel_AidentPlusQueLesAutresStatuts()
        {
            PokemonTeam asleep = Wild("Ronflex", 30);
            asleep.IsSleeping = 2;
            PokemonTeam paralyzed = Wild("Ronflex", 30);
            paralyzed.IsParalyzed = true;
            Assert.True(FightCatch.CalculateCatchProbabilityPercentage(asleep, "Pokeball") > FightCatch.CalculateCatchProbabilityPercentage(paralyzed, "Pokeball"));
        }

        [Fact]
        public void TauxDeCapture_EstCeluiDeLEspece()
        {
            Assert.Equal(GameData.Pokemon("Rattata").CaptureRate, Wild("Rattata").TauxCapture);
            Assert.Equal(3, Wild("Mewtwo", 70).TauxCapture);
        }

        [Fact]
        public void PokemonCommunPresqueKO_TresFacileACapturer()
        {
            Assert.True(FightCatch.CalculateCatchProbabilityPercentage(Wild("Rattata").WithHp(1), "Pokeball") > 90);
        }

        [Fact]
        public void LegendaireEnPleineForme_TresDifficileACapturer()
        {
            Assert.True(FightCatch.CalculateCatchProbabilityPercentage(Wild("Mewtwo", 70), "Pokeball") < 10);
        }
    }
}
