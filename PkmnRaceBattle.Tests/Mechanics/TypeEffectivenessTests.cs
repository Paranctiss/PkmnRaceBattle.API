using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Affinités de types : table moderne (Gen 6+), cohérente avec les types des données (Acier, Fée, Ténèbres)
    public class TypeEffectivenessTests
    {
        public static IEnumerable<object[]> AllMatchups()
        {
            string[] attackTypes = Rules.AllTypes.Where(t => t != "steel").ToArray(); // aucune capacité Acier dans les données
            string[] defenseTypes = Rules.AllTypes.Where(t => t != "dark").ToArray(); // aucun Pokémon Ténèbres
            foreach (string atk in attackTypes)
                foreach (string def in defenseTypes)
                    yield return [atk, def];
        }

        private static int DamageAgainst(string moveType, params string[] defenderTypes)
        {
            using var _ = TestRandom.Neutral().Install();
            // Attaquant sans STAB, dégâts assez grands pour que les arrondis soient négligeables
            PokemonTeam attacker = Pkmn.Create("Mew", 100).WithStats(atk: 300).WithTypes("neutre");
            PokemonTeam defender = Pkmn.Create("Ronflex", 100).WithStats(hp: 999, def: 300).WithTypes(defenderTypes);
            PokemonTeamMove move = Pkmn.Move("Charge");
            move.Type = moveType;
            move.Power = 100;
            return Rules.DamageDealt(attacker, defender, move);
        }

        [Theory]
        [MemberData(nameof(AllMatchups))]
        public void Multiplicateur_TypeSimple(string attackType, string defenseType)
        {
            double expected = Rules.TypeMultiplier(attackType, defenseType);
            int neutral = DamageAgainst(attackType, "neutre");
            int actual = DamageAgainst(attackType, defenseType);

            Assert.True(Math.Abs(actual - neutral * expected) <= 1,
                $"{attackType} -> {defenseType} : attendu x{expected} ({neutral * expected} PV), obtenu {actual} PV (neutre = {neutral})");
        }

        [Theory]
        [InlineData("ground", "fire", "rock", 4.0)]          // Sol sur Feu/Roche
        [InlineData("fighting", "normal", "fairy", 1.0)]      // Combat sur Rondoudou : 2 x 0.5
        [InlineData("dragon", "dragon", "fairy", 0.0)]        // Fée immunisée contre Dragon
        [InlineData("poison", "electric", "steel", 0.0)]      // Acier immunisé contre Poison (Magnéti)
        [InlineData("water", "rock", "ground", 4.0)]          // Eau sur Racaillou
        [InlineData("grass", "rock", "ground", 4.0)]          // Plante sur Racaillou
        [InlineData("ice", "grass", "flying", 4.0)]           // Glace sur Plante/Vol
        [InlineData("electric", "water", "flying", 4.0)]      // Électrik sur Léviator
        [InlineData("fire", "fire", "dragon", 0.25)]
        [InlineData("grass", "grass", "poison", 0.25)]        // Plante sur Bulbizarre
        [InlineData("fighting", "poison", "flying", 0.25)]    // Combat sur Nosferapti
        [InlineData("electric", "ground", "flying", 0.0)]     // l'immunité l'emporte
        [InlineData("ground", "electric", "flying", 0.0)]     // Électhor est immunisé contre Sol
        [InlineData("ghost", "ghost", "poison", 2.0)]         // Spectre sur Ectoplasma : 2 x 1
        [InlineData("bug", "grass", "poison", 1.0)]           // Insecte sur Bulbizarre : 2 x 0.5
        [InlineData("psychic", "ghost", "poison", 2.0)]       // Psy sur Ectoplasma
        [InlineData("ghost", "psychic", "water", 2.0)]        // Spectre sur Flagadoss
        public void Multiplicateur_DoubleType(string attackType, string type1, string type2, double expected)
        {
            int neutral = DamageAgainst(attackType, "neutre");
            int actual = DamageAgainst(attackType, type1, type2);
            Assert.True(Math.Abs(actual - neutral * expected) <= 1,
                $"{attackType} -> {type1}/{type2} : attendu x{expected} ({neutral * expected} PV), obtenu {actual} PV");
        }

        [Theory]
        [InlineData("electric", "ground", "aucun effet")]
        [InlineData("normal", "ghost", "aucun effet")]
        [InlineData("ground", "flying", "aucun effet")]
        [InlineData("water", "fire", "super efficace")]
        [InlineData("fire", "water", "pas très efficace")]
        public void MessageDEfficacite(string attackType, string defenseType, string expectedFragment)
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam attacker = Pkmn.Create("Mew", 50).WithTypes("neutre");
            PokemonTeam defender = Pkmn.Create("Ronflex", 50).WithTypes(defenseType);
            PokemonTeamMove move = Pkmn.Move("Charge");
            move.Type = attackType;

            var ctx = Rules.Attack(attacker, defender, move);

            Assert.True(ctx.Contains(expectedFragment), $"Message « {expectedFragment} » attendu, obtenu : {ctx.Dump()}");
        }

        [Fact]
        public void AttaqueSansEffet_NeRetirePasDePV()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeam pikachu = Pkmn.Create("Pikachu", 30, "Tonnerre");
            PokemonTeam taupiqueur = Pkmn.Create("Taupiqueur", 30);

            Assert.Equal(0, Rules.DamageDealt(pikachu, taupiqueur, "Tonnerre"));
        }

        [Fact]
        public void Stab_MultipliePar1Virgule5()
        {
            using var _ = TestRandom.Neutral().Install();
            PokemonTeamMove ember = Pkmn.Move("Flammèche");
            PokemonTeam defender() => Pkmn.Create("Ronflex", 50).WithStats(hp: 999, defSpe: 150).WithTypes("neutre");

            int withStab = Rules.DamageDealt(Pkmn.Create("Salamèche", 50).WithStats(atkSpe: 150).WithTypes("fire"), defender(), ember);
            int withoutStab = Rules.DamageDealt(Pkmn.Create("Salamèche", 50).WithStats(atkSpe: 150).WithTypes("water"), defender(), ember);

            Assert.True(Math.Abs(withStab - withoutStab * 1.5) <= 1, $"STAB : {withStab} vs {withoutStab} x 1.5");
        }
    }
}
