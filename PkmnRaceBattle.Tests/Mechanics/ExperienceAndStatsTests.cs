using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Helpers.StatsCalculator;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    public class ExperienceTests
    {
        // Courbes d'expérience de la 1re génération (noms pokeapi) : fast = 4n³/5, medium = n³, medium-slow = 6n³/5 - 15n² + 100n - 140, slow = 5n³/4
        [Theory]
        [InlineData("fast", 10)]
        [InlineData("fast", 50)]
        [InlineData("fast", 100)]
        [InlineData("medium", 10)]
        [InlineData("medium", 50)]
        [InlineData("medium", 100)]
        [InlineData("medium-slow", 5)]
        [InlineData("medium-slow", 16)]
        [InlineData("medium-slow", 100)]
        [InlineData("slow", 10)]
        [InlineData("slow", 100)]
        public void CourbeDExperience(string growthRate, int level)
        {
            Assert.Equal(Rules.ExpForLevel(level, growthRate), PokemonExperienceCalculator.ExpForLevel(level, growthRate));
        }

        [Fact]
        public void CourbesConnues_Niveau100()
        {
            Assert.Equal(800000, PokemonExperienceCalculator.ExpForLevel(100, "fast"));
            Assert.Equal(1000000, PokemonExperienceCalculator.ExpForLevel(100, "medium"));
            Assert.Equal(1059860, PokemonExperienceCalculator.ExpForLevel(100, "medium-slow"));
            Assert.Equal(1250000, PokemonExperienceCalculator.ExpForLevel(100, "slow"));
        }

        [Fact]
        public void TousLesPokemonDuJeu_OntUneCourbeConnue()
        {
            foreach (var pokemon in GameData.Pokemons)
            {
                var exception = Record.Exception(() => PokemonExperienceCalculator.ExpForLevel(10, pokemon.GrowthRate));
                Assert.Null(exception);
            }
        }

        // Gen 1 : XP = (XP de base de l'espèce x niveau du vaincu x 1,5 si dresseur) / 7 (partage entre participants non retenu : design du jeu)
        [Theory]
        [InlineData("Rattata", 5, false)]
        [InlineData("Rattata", 5, true)]
        [InlineData("Ronflex", 30, false)]
        [InlineData("Mewtwo", 70, true)]
        public void ExperienceGagnee(string defeated, int level, bool trainer)
        {
            PokemonTeam pokemon = Pkmn.Create(defeated, level);
            int expected = (int)(GameData.Pokemon(defeated).BaseExperience * level * (trainer ? 1.5 : 1.0) / 7);
            Assert.Equal(expected, PokemonExperienceCalculator.ExpGained(pokemon, trainer, false, 1));
        }

        [Fact]
        public void PokemonGenere_ADebutDeNiveau()
        {
            PokemonTeam pokemon = Pkmn.Create("Salamèche", 12);
            Assert.Equal(PokemonExperienceCalculator.ExpForLevel(12, "medium-slow"), pokemon.CurrXP);
            Assert.Equal(pokemon.CurrXP, pokemon.XpFromLastLvl);
            Assert.Equal(PokemonExperienceCalculator.ExpForLevel(13, "medium-slow"), pokemon.XpForNextLvl);
        }

        [Fact]
        public void SuperBonbon_DonneExactementLXPDuNiveauSuivant()
        {
            PokemonTeam pokemon = Pkmn.Create("Salamèche", 12);
            pokemon.CurrXP += 30;
            var result = FightUseItem.UseSpecial(pokemon, "Super Bonbon", new PkmnRaceBattle.API.Hub.TurnContext(), true);
            Assert.Equal(PokemonExperienceCalculator.ExpForLevel(13, "medium-slow"), result.CurrXP);
        }
    }

    // Statistiques : formule moderne (IV 31) conservée par choix de design
    public class StatCalculatorTests
    {
        [Theory]
        [InlineData(45, 5, 0, 21)]
        [InlineData(45, 50, 0, 120)]
        [InlineData(160, 100, 0, 461)]
        [InlineData(50, 10, 4, 33)]
        public void PV(int baseHp, int level, int ev, int expected)
        {
            Assert.Equal(expected, PokemonStatCalculator.CalculateHp(baseHp, level, ev));
        }

        [Theory]
        [InlineData(49, 5, 0, 11)]
        [InlineData(100, 50, 0, 120)]
        [InlineData(130, 100, 0, 296)]
        public void Statistique(int baseStat, int level, int ev, int expected)
        {
            Assert.Equal(expected, PokemonStatCalculator.CalculateStat(baseStat, level, ev));
        }

        [Fact]
        public void RecalculDesStats_ConserveLesDegatsSubis()
        {
            PokemonTeam pokemon = Pkmn.Create("Bulbizarre", 10);
            pokemon.CurrHp -= 7;
            int damage = pokemon.BaseHp - pokemon.CurrHp;
            pokemon.Level = 11;

            PokemonStatCalculator.CalculateAllStats(pokemon, GameData.Pokemon("Bulbizarre"));

            Assert.Equal(PokemonStatCalculator.CalculateHp(45, 11), pokemon.BaseHp);
            Assert.Equal(damage, pokemon.BaseHp - pokemon.CurrHp);
        }

    }

    public class PokemonGenerationTests
    {
        [Theory]
        [InlineData("Bulbizarre", 5)]
        [InlineData("Salamèche", 5)]
        [InlineData("Carapuce", 5)]
        [InlineData("Pikachu", 30)]
        [InlineData("Ronflex", 50)]
        public void Generation_CopieLesDonneesDeLEspece(string name, int level)
        {
            var species = GameData.Pokemon(name);
            PokemonTeam pokemon = Pkmn.Create(name, level);

            Assert.Equal(species.Id, pokemon.IdDex);
            Assert.Equal(species.NameFr, pokemon.NameFr);
            Assert.Equal(level, pokemon.Level);
            Assert.Equal(species.Types.Select(t => t.Name), pokemon.Types.Select(t => t.Name));
            Assert.Equal(pokemon.BaseHp, pokemon.CurrHp);
            Assert.Equal(species.CaptureRate, pokemon.TauxCapture);
            Assert.Equal(species.Sprites.FrontDefault, pokemon.FrontSprite);
            Assert.False(string.IsNullOrEmpty(pokemon.Id));
        }

        [Theory]
        [InlineData("Bulbizarre", 5)]
        [InlineData("Bulbizarre", 20)]
        [InlineData("Ronflex", 50)]
        [InlineData("Mew", 100)]
        public void Capacites_AuPlus4DejaApprisesAuNiveau(string name, int level)
        {
            PokemonTeam pokemon = Pkmn.Create(name, level);
            var learnable = GameData.Pokemon(name).Moves.Where(m => m.LearnedAtLvl <= level).ToList();

            Assert.InRange(pokemon.Moves.Length, 1, 4);
            Assert.Equal(Math.Min(4, learnable.Count), pokemon.Moves.Length);
            Assert.All(pokemon.Moves, m => Assert.Contains(learnable, l => l.NameFr == m.NameFr));
            // Les plus récentes sont retenues
            int minLearned = learnable.Where(l => pokemon.Moves.Any(m => m.NameFr == l.NameFr)).Min(l => l.LearnedAtLvl ?? 0);
            Assert.DoesNotContain(learnable, l => l.LearnedAtLvl > minLearned && pokemon.Moves.All(m => m.NameFr != l.NameFr));
        }

        [Fact]
        public void IdentifiantsUniques()
        {
            var ids = Enumerable.Range(0, 50).Select(_ => Pkmn.Create("Rattata", 3).Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void NiveauMinimum1()
        {
            Assert.Equal(1, Pkmn.Create("Rattata", 0).Level);
            Assert.Equal(1, Pkmn.Create("Rattata", -4).Level);
        }

        [Fact]
        public void IA_NeChoisitJamaisUneCapaciteSousEntrave()
        {
            PokemonTeam pokemon = Pkmn.Create("Rattata", 10, "Charge", "Mimi-Queue");
            pokemon.CantUseMoves.Add("Charge");
            for (int i = 0; i < 20; i++)
            {
                using var _ = new TestRandom().Install();
                Assert.Equal("Mimi-Queue", AIChoseMove.GetARandomMove(pokemon).NameFr);
            }
        }

        [Fact]
        public void SelectionDesCapacites_NePrendPasCellesDesNiveauxSuperieurs()
        {
            var moves = PokemonMoveSelector.SelectMoves(GameData.Pokemon("Salamèche").Moves, 1);
            Assert.All(moves, m => Assert.Contains(GameData.Pokemon("Salamèche").Moves, b => b.NameFr == m.NameFr && b.LearnedAtLvl <= 1));
        }
    }
}
