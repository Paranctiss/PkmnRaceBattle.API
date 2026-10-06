using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Smoke
{
    public class InfrastructureTests
    {
        [Fact]
        public void Fixtures_ContiennentLes151PokemonEtLes165Capacites()
        {
            Assert.Equal(151, GameData.Pokemons.Count);
            Assert.Equal(165, GameData.Moves.Count);
            Assert.Equal(6, GameData.Environments.Count);
            Assert.Equal("Bulbizarre", GameData.Pokemon(1).NameFr);
            Assert.Equal(45, GameData.Pokemon(1).Stats.Hp);
        }

        [Fact]
        public void Pkmn_CreeUnPokemonAvecLesCapacitesDemandees()
        {
            var pikachu = Pkmn.Create("Pikachu", 20, "Éclair", "Vive-Attaque");
            Assert.Equal(20, pikachu.Level);
            Assert.Equal(new[] { "Éclair", "Vive-Attaque" }, pikachu.Moves.Select(m => m.NameFr));
            Assert.False(pikachu.IsShiny);
        }
    }
}
