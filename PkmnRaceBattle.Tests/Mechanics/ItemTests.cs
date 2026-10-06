using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Mechanics
{
    // Potions, rappels, objets de soin de statut et validation de leur utilisation (valeurs du jeu conservées : Hyper Potion = 120 PV)
    public class ItemTests
    {
        private static PokemonTeam Hurt(int maxHp, int currHp) => Pkmn.Create("Ronflex", 50).WithStats(hp: maxHp).WithHp(currHp);

        private static PokemonTeam UseItem(PokemonTeam pokemon, string item, string type, out TurnContext ctx)
        {
            ctx = new TurnContext();
            return FightUseItem.UseItem(pokemon, Pkmn.Create("Mew", 50), Pkmn.Item(item, type), ctx, true)[0];
        }

        [Theory]
        [InlineData("Potion", 200, 100, 120)]
        [InlineData("Potion", 200, 190, 200)]
        [InlineData("Super Potion", 200, 100, 150)]
        [InlineData("Super Potion", 200, 180, 200)]
        [InlineData("Hyper Potion", 300, 100, 220)]
        [InlineData("Hyper Potion", 200, 100, 200)]
        [InlineData("Potion Max", 300, 1, 300)]
        [InlineData("Guérison", 300, 1, 300)]
        public void Potion_RendLesPVSansDepasserLeMaximum(string item, int maxHp, int currHp, int expected)
        {
            PokemonTeam pokemon = UseItem(Hurt(maxHp, currHp), item, "potion", out var ctx);
            Assert.Equal(expected, pokemon.CurrHp);
            // La barre de vie du client reçoit le soin en négatif (currHp -= variation)
            Assert.Equal(currHp - expected, ctx.Player.Hp.Sum());
        }

        [Theory]
        [InlineData("Rappel", 200, 100)]
        [InlineData("Rappel Max", 200, 200)]
        public void Rappel_RanimeUnPokemonKO(string item, int maxHp, int expected)
        {
            PokemonTeam pokemon = UseItem(Hurt(maxHp, 0), item, "potion", out _);
            Assert.Equal(expected, pokemon.CurrHp);
        }

        [Fact]
        public void Guerison_SoigneAussiLeStatut()
        {
            PokemonTeam pokemon = Hurt(200, 50);
            pokemon.IsParalyzed = true;
            pokemon = UseItem(pokemon, "Guérison", "potion", out _);
            Assert.False(pokemon.IsParalyzed);
        }

        [Theory]
        [InlineData("Anti-Brûle", "burn")]
        [InlineData("Antidote", "poison")]
        [InlineData("Antidote", "toxic")]
        [InlineData("Antigel", "freeze")]
        [InlineData("Anti-Para", "paralysis")]
        [InlineData("Réveil", "sleep")]
        public void SoinDeStatut_SoigneLeStatutCorrespondant(string item, string status)
        {
            PokemonTeam pokemon = Hurt(200, 200);
            SetStatus(pokemon, status);
            pokemon = UseItem(pokemon, item, "ailment", out _);
            Assert.False(HasAnyStatus(pokemon), $"{item} doit soigner {status}");
        }

        [Theory]
        [InlineData("burn")]
        [InlineData("poison")]
        [InlineData("toxic")]
        [InlineData("freeze")]
        [InlineData("paralysis")]
        [InlineData("sleep")]
        public void TotalSoin_SoigneTousLesStatuts(string status)
        {
            PokemonTeam pokemon = Hurt(200, 200);
            SetStatus(pokemon, status);
            pokemon = UseItem(pokemon, "Total Soin", "ailment", out _);
            Assert.False(HasAnyStatus(pokemon));
        }

        [Theory]
        [InlineData("Anti-Brûle", "paralysis")]
        [InlineData("Antidote", "burn")]
        [InlineData("Réveil", "freeze")]
        public void SoinDeStatut_SansEffetSurUnAutreStatut(string item, string status)
        {
            PokemonTeam pokemon = Hurt(200, 200);
            SetStatus(pokemon, status);
            pokemon = UseItem(pokemon, item, "ailment", out _);
            Assert.True(HasAnyStatus(pokemon));
        }

        // ---------- ValidatorMove : objets refusés ----------

        // Joueur qui possède 5 exemplaires de chaque objet
        private static PlayerMongo Owner()
        {
            var player = new PlayerMongo();
            foreach (var item in player.Items) item.Number = 5;
            return player;
        }

        private static bool Validate(PokemonTeam pokemon, PokemonTeamMove move, PlayerMongo? opponent = null, PokemonTeam? opponentPokemon = null) =>
            ValidatorMove.IsEverythingOk(Owner(), pokemon, move, opponent ?? WildOpponent(), opponentPokemon ?? Pkmn.Create("Rattata", 5), new TurnContext());

        [Fact]
        public void ObjetQueLeJoueurNePossedePas_EstRefuse()
        {
            var player = new PlayerMongo();
            Assert.False(ValidatorMove.IsEverythingOk(player, Hurt(100, 100), Pkmn.Item("Masterball", "ball"), WildOpponent(), Pkmn.Create("Rattata", 5), new TurnContext()));
        }

        private static PlayerMongo WildOpponent()
        {
            var opponent = new PlayerMongo();
            opponent.GenerateWild();
            return opponent;
        }

        [Fact]
        public void Potion_RefuseeSurUnPokemonAuxPVMax() => Assert.False(Validate(Hurt(200, 200), Pkmn.Item("Potion", "potion")));

        [Fact]
        public void Potion_RefuseeSurUnPokemonKO() => Assert.False(Validate(Hurt(200, 0), Pkmn.Item("Super Potion", "potion")));

        [Fact]
        public void Rappel_RefuseSurUnPokemonEnForme() => Assert.False(Validate(Hurt(200, 50), Pkmn.Item("Rappel", "potion")));

        [Fact]
        public void Rappel_AccepteSurUnPokemonKO() => Assert.True(Validate(Hurt(200, 0), Pkmn.Item("Rappel", "potion")));

        [Fact]
        public void SoinDeStatut_RefuseSansStatut() => Assert.False(Validate(Hurt(200, 200), Pkmn.Item("Antidote", "ailment")));

        [Fact]
        public void Guerison_AccepteeAuxPVMaxSiLePokemonAUnStatut()
        {
            PokemonTeam pokemon = Hurt(200, 200);
            pokemon.IsSleeping = 3;
            Assert.True(Validate(pokemon, Pkmn.Item("Guérison", "potion")));
        }

        [Fact]
        public void Guerison_RefuseeSurUnPokemonEnPleineForme() => Assert.False(Validate(Hurt(200, 200), Pkmn.Item("Guérison", "potion")));

        [Fact]
        public void Pokeball_RefuseeContreUnDresseur()
        {
            var trainer = new PlayerMongo { IsTrainer = true, IsPlayer = false };
            var ctx = new TurnContext();
            Assert.False(ValidatorMove.IsEverythingOk(new PlayerMongo(), Hurt(100, 100), Pkmn.Item("Pokeball", "ball"), trainer, Pkmn.Create("Rattata", 5), ctx));
        }

        [Theory]
        [InlineData("Pokeball")]
        [InlineData("Superball")]
        [InlineData("Hyperball")]
        [InlineData("Masterball")]
        public void Pokeball_AccepteeContreUnSauvage(string ball) =>
            Assert.True(Validate(Hurt(100, 100), Pkmn.Item(ball, "ball")));

        [Fact]
        public void Pokeball_RefuseeSiLaCibleEstDerriereUnClone()
        {
            PokemonTeam wild = Pkmn.Create("Rattata", 5);
            wild.Substitute = wild.CreateSubstitute(5);
            Assert.False(Validate(Hurt(100, 100), Pkmn.Item("Pokeball", "ball"), opponentPokemon: wild));
        }

        [Fact]
        public void Attaque_RefuseeParUnPokemonKO() => Assert.False(Validate(Hurt(100, 0), Pkmn.Move("Charge")));

        [Fact]
        public void Attaque_RefuseeSansPP()
        {
            PokemonTeamMove move = Pkmn.Move("Charge");
            move.Pp = 0;
            Assert.False(Validate(Hurt(100, 100), move));
        }

        [Fact]
        public void Attaque_RefuseeSiElleEstSousEntrave()
        {
            PokemonTeam pokemon = Hurt(100, 100);
            pokemon.CantUseMoves.Add("Charge");
            Assert.False(Validate(pokemon, Pkmn.Move("Charge")));
        }

        [Fact]
        public void ConversionDesActions_ItemEtSwap()
        {
            PokemonTeamMove item = PokemonMoveSelector.ConvertToActionMove("item:Super Potion:potion", 2);
            Assert.Equal("item", item.Type);
            Assert.Equal("Super Potion", item.NameFr);
            Assert.Equal("potion", item.DamageType);
            Assert.Equal("2", item.Target);

            PokemonTeamMove swap = PokemonMoveSelector.ConvertToActionMove("swap:", 0);
            Assert.Equal("swap", swap.Type);
            Assert.Equal("swap", swap.NameFr);
        }

        private static void SetStatus(PokemonTeam p, string status)
        {
            switch (status)
            {
                case "burn": p.IsBurning = true; break;
                case "poison": p.IsPoisoned = 1; break;
                case "toxic": p.IsPoisoned = 2; p.PoisonCount = 3; break;
                case "freeze": p.IsFrozen = true; break;
                case "paralysis": p.IsParalyzed = true; break;
                case "sleep": p.IsSleeping = 3; break;
            }
        }

        private static bool HasAnyStatus(PokemonTeam p) => p.IsBurning || p.IsPoisoned > 0 || p.IsFrozen || p.IsParalyzed || p.IsSleeping > 0;
    }
}
