using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Hub
{
    public class PokeCenterTests
    {
        [Fact]
        public async Task CentrePokemon_SoigneToutLEquipe()
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20, "Griffe"), Pkmn.Create("Carapuce", 20), Pkmn.Create("Bulbizarre", 20));
            player.Team[0].CurrHp = 3;
            player.Team[0].IsBurning = true;
            player.Team[1].CurrHp = 0;
            player.Team[2].IsSleeping = 3;
            player.Team[2].IsConfused = 2;
            player.Team[2].AtkChanges = -2;
            player.Team[0].Moves[0].Pp = 1;
            // Capacité enregistrée avant l'ajout des PP max
            player.Team[0].Moves[0].MaxPp = 0;
            h.UpdatePlayer(player);

            await h.Hub("c").UsePokeCenter(player._id);

            PlayerMongo healed = h.Players.Get(player._id);
            Assert.All(healed.Team, p =>
            {
                Assert.Equal(p.BaseHp, p.CurrHp);
                Assert.False(p.IsBurning || p.IsFrozen || p.IsParalyzed || p.IsSleeping > 0 || p.IsPoisoned > 0);
                Assert.Equal(0, p.IsConfused);
                Assert.Equal(0, p.AtkChanges);
            });
            Assert.Equal(GameData.Move("Griffe").Pp, healed.Team[0].Moves[0].Pp);
            Assert.Equal(GameData.Move("Griffe").Pp, healed.Team[0].Moves[0].MaxPp);
            Assert.Equal(player._id, h.Named("healedPokeCenter").Single().Arg<PlayerMongo>(0)._id);
        }

        [Fact]
        public async Task CentrePokemon_EcranDeBienvenue()
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20));
            await h.Hub("c").GetPokeCenter(player._id);
            Assert.Single(h.Named("responsePokeCenter"));
            Assert.Empty(h.Named("healedPokeCenter"));
        }
    }

    public class PokeShopTests
    {
        // Prix du jeu (PlayerMongo.Items) : le client affiche les mêmes (shared/utils/items.ts)
        public static IEnumerable<object[]> Prices() =>
        [
            ["Pokeball", 200], ["Superball", 600], ["Hyperball", 1200], ["Masterball", 10000],
            ["Potion", 300], ["Super Potion", 700], ["Hyper Potion", 1500], ["Potion Max", 2500], ["Guérison", 3000],
            ["Rappel", 1500], ["Rappel Max", 5000],
            ["Anti-Brûle", 250], ["Anti-Para", 200], ["Antidote", 100], ["Antigel", 200], ["Réveil", 200], ["Total Soin", 600],
            ["Pierre Eau", 2100], ["Pierre Feu", 2100], ["Pierre Foudre", 2100], ["Pierre Lune", 2100], ["Pierre Plante", 2100],
            ["Super Bonbon", 5000],
        ];

        [Theory]
        [MemberData(nameof(Prices))]
        public async Task Achat_DebiteLePrixEtAjouteLObjet(string item, int price)
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20));
            player.Credits = 20000;
            h.UpdatePlayer(player);
            int before = player.Items.Single(i => i.Name == item).Number;

            await h.Hub("c").BuyItem(player._id, item);

            PlayerMongo updated = h.Players.Get(player._id);
            Assert.Equal(20000 - price, updated.Credits);
            Assert.Equal(before + 1, updated.Items.Single(i => i.Name == item).Number);
            SentMessage response = h.Named("onBuyItemResponse").Single();
            Assert.Equal(item, response.Arg<string>(0));
            Assert.Equal(20000 - price, response.Arg<PlayerMongo>(1).Credits);
        }

        [Fact]
        public async Task Achat_ArgentInsuffisant_RienNeChange()
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20));
            player.Credits = 100;
            h.UpdatePlayer(player);

            await h.Hub("c").BuyItem(player._id, "Pokeball");

            PlayerMongo updated = h.Players.Get(player._id);
            Assert.Equal(100, updated.Credits);
            Assert.Equal(15, updated.Items.Single(i => i.Name == "Pokeball").Number);
            Assert.Single(h.Named("onBuyItemResponse"));
        }

        [Fact]
        public async Task Achat_PrixExact_Accepte()
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20));
            player.Credits = 300;
            h.UpdatePlayer(player);

            await h.Hub("c").BuyItem(player._id, "Potion");

            Assert.Equal(0, h.Players.Get(player._id).Credits);
        }

        [Fact]
        public async Task Achat_ObjetInconnu_NePlantePas()
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20));

            var exception = await Record.ExceptionAsync(() => h.Hub("c").BuyItem(player._id, "Objet inexistant"));

            Assert.Null(exception);
            Assert.Equal(3000, h.Players.Get(player._id).Credits);
        }

        [Fact]
        public async Task Boutique_EcranDeLaBoutique()
        {
            var h = new HubHarness();
            PlayerMongo player = await h.AddPlayer("Sacha", Pkmn.Create("Salamèche", 20));
            await h.Hub("c").GetPokeShop(player._id);
            Assert.Single(h.Named("responsePokeShop"));
        }
    }
}
