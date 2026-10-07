using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.API.Helpers.PathManager;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Hub
{
    // Créer / rejoindre / lancer / quitter une partie, choix du starter
    public class GameLifecycleTests
    {
        private static async Task<(HubHarness h, string code, string hostId)> CreateGame(int starterId = 4, string name = "Sacha", string sprite = "red")
        {
            var h = new HubHarness();
            await h.Hub("host-conn-" + h.RoomId).CreateGame(name, starterId, sprite);
            SentMessage created = h.Named("GameCreated").Single();
            return (h, created.Arg<string>(0), created.Arg<string>(1));
        }

        [Fact]
        public async Task CreerUnePartie_EnvoieUnCodeDeSalleEtLIdentifiantDuJoueur()
        {
            var (h, code, hostId) = await CreateGame();

            Assert.Matches("^[0-9A-F]{6}$", code);
            Assert.False(string.IsNullOrEmpty(hostId));
            Assert.Equal("caller:host-conn-" + h.RoomId, h.Named("GameCreated").Single().Target);
        }

        [Fact]
        public async Task CreerUnePartie_EnregistreLHoteEtLaSalle()
        {
            var (h, code, hostId) = await CreateGame(name: "Ondine", sprite: "misty");
            PlayerMongo host = h.Players.Get(hostId);

            Assert.True(host.IsHost);
            Assert.True(host.IsPlayer);
            Assert.Equal("Ondine", host.Name);
            Assert.Equal("misty", host.Sprite);
            Assert.Equal(code, host.RoomId);
            Assert.Equal(3000, host.Credits);
            var room = h.Rooms.All.Single();
            Assert.Equal(code, room.roomId);
            Assert.Equal(hostId, room.hostUserId);
            Assert.Equal(0, room.state);
            Assert.Equal(UserConnectionManager.GetConnectionId(hostId, code), "host-conn-" + h.RoomId);
        }

        [Theory]
        [InlineData(1, "Bulbizarre")]
        [InlineData(4, "Salamèche")]
        [InlineData(7, "Carapuce")]
        public async Task CreerUnePartie_LeStarterChoisiEstDansLEquipe(int starterId, string expectedName)
        {
            var (h, _, hostId) = await CreateGame(starterId);
            PlayerMongo host = h.Players.Get(hostId);

            PokemonTeam starter = Assert.Single(host.Team);
            Assert.Equal(starterId, starter.IdDex);
            Assert.Equal(expectedName, starter.NameFr);
            Assert.Equal(starter.BaseHp, starter.CurrHp);
        }

        [Fact]
        public async Task CreerUnePartie_LeStarterEstNiveau5()
        {
            var (h, _, hostId) = await CreateGame();
            Assert.Equal(5, h.Players.Get(hostId).Team[0].Level);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        public async Task Starter_ConnaitLesCapacitesDeSonNiveau(int starterId)
        {
            var (h, code, hostId) = await CreateGame(starterId);
            await h.Hub("c-" + h.RoomId).JoinGame("Pierre", starterId, "brock", code);
            PokemonTeam starter = h.Players.All.Single(p => p._id != hostId).Team[0];

            var learnable = GameData.Pokemon(starterId).Moves.Where(m => m.LearnedAtLvl <= starter.Level).Select(m => m.NameFr).ToList();
            Assert.NotEmpty(starter.Moves);
            Assert.All(starter.Moves, m => Assert.Contains(m.NameFr, learnable));
        }

        [Fact]
        public async Task CreerUnePartie_LeJoueurCommenceAvantLaPremiereMap()
        {
            var (h, _, hostId) = await CreateGame();
            PlayerMongo host = h.Players.Get(hostId);

            Assert.Equal("Default", host.CurrentPath.EnvironmentName);
            Assert.Equal(0, host.CurrentPath.X);
            Assert.Equal(0, host.MapFightCount);
            Assert.Equal(PlayerPathHelper.InitialSteps, host.PlayerPath.PathPoints.Max(p => p.X));
        }

        [Fact]
        public async Task CreerUnePartie_SacDeDepart()
        {
            var (h, _, hostId) = await CreateGame();
            BagItem[] items = h.Players.Get(hostId).Items;

            Assert.Equal(15, items.Single(i => i.Name == "Pokeball").Number);
            Assert.Equal(10, items.Single(i => i.Name == "Potion").Number);
            Assert.Equal(1, items.Single(i => i.Name == "Rappel").Number);
            Assert.Equal(0, items.Single(i => i.Name == "Masterball").Number);
        }

        [Fact]
        public async Task RejoindreUnePartie_EnvoieJoinSuccessEtPrevientLaSalle()
        {
            var (h, code, hostId) = await CreateGame();

            await h.Hub("guest-conn").JoinGame("Pierre", 1, "brock", code);

            SentMessage success = h.Named("JoinSuccess").Single();
            Assert.Equal("caller:guest-conn", success.Target);
            Assert.Equal(code, success.Arg<string>(0));
            string guestId = success.Arg<string>(1);
            Assert.NotEqual(hostId, guestId);
            Assert.Contains(h.Named("UserJoined"), m => m.Target == "group:" + code);

            PlayerMongo guest = h.Players.Get(guestId);
            Assert.False(guest.IsHost);
            Assert.Equal(code, guest.RoomId);
            Assert.Equal(1, guest.Team.Single().IdDex);
            Assert.Equal(5, guest.Team.Single().Level);
        }

        [Fact]
        public async Task RejoindreUnePartieInexistante_EstRefuse()
        {
            var h = new HubHarness();
            await h.Hub("guest-conn").JoinGame("Pierre", 1, "brock", "ZZZZZZ");

            Assert.Empty(h.Named("JoinSuccess"));
            Assert.Empty(h.Players.All);
        }

        [Fact]
        public async Task ListeDesJoueurs_ContientTousLesJoueursDeLaSalle()
        {
            var (h, code, _) = await CreateGame();
            await h.Hub("guest-1").JoinGame("Pierre", 1, "brock", code);
            await h.Hub("guest-2").JoinGame("Ondine", 7, "misty", code);

            await h.Hub("guest-2").GetPlayersInRoom(code);

            var players = h.Named("ResponsePlayersInRoom").Single().Arg<List<PlayerMongo>>(0);
            Assert.Equal(new[] { "Ondine", "Pierre", "Sacha" }, players.Select(p => p.Name).OrderBy(n => n));
        }

        [Fact]
        public async Task GetPlayer_RenvoieLeJoueur()
        {
            var (h, _, hostId) = await CreateGame();
            await h.Hub("x").GetPlayer(hostId);
            Assert.Equal(hostId, h.Named("GetPlayerResponse").Single().Arg<PlayerMongo>(0)._id);
        }

        [Fact]
        public async Task LancerLaPartie_PasseLaSalleEnCoursEtPrevientTousLesJoueurs()
        {
            var (h, code, _) = await CreateGame();
            await h.Hub("host").StartGame(code, false, 5);

            Assert.Equal(1, h.Rooms.All.Single().state);
            Assert.Contains(h.Named("GameStarted"), m => m.Target == "group:" + code && m.Arg<string>(0) == code);
            Assert.Empty(h.Named("TimerUpdate"));
        }

        [Fact]
        public async Task LancerLaPartie_ReglagesDXPParDefaut_MultiExpEtXPNormale()
        {
            var (h, code, _) = await CreateGame();
            await h.Hub("host").StartGame(code, false, 5);

            RoomMongo room = h.Rooms.All.Single();
            Assert.True(room.MultiXp);
            Assert.Equal(1, room.XpMultiplier);
        }

        [Theory]
        [InlineData(false, 2, 2)]
        [InlineData(true, 5, 5)]
        [InlineData(true, 3, 1)] // valeur inconnue : XP normale
        public async Task LancerLaPartie_EnregistreLesReglagesDXPDeLHote(bool multiXp, int multiplier, int expected)
        {
            var (h, code, _) = await CreateGame();
            await h.Hub("host").StartGame(code, false, 5, multiXp, multiplier);

            RoomMongo room = h.Rooms.All.Single();
            Assert.Equal(multiXp, room.MultiXp);
            Assert.Equal(expected, room.XpMultiplier);
        }

        [Fact]
        public async Task LancerLaPartie_AfficheLesPaliersDeNiveauxSurLaCarteDeChaqueJoueur()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("guest-xp-" + h.RoomId).JoinGame("Ondine", 7, "misty", code);
            await h.Hub("host").StartGame(code, false, 5, false, 2);

            var settings = new XpSettings(false, 2);
            foreach (PlayerMongo player in h.Players.All)
            {
                PathPoint first = player.PlayerPath.PathPoints.Single(p => p.X == 1);
                Assert.Equal(ZoneLevels.GetRange(1, settings).WildMin, first.MinLevel);
                Assert.Equal(ZoneLevels.GetRange(1, settings).TrainerMax, first.MaxLevel);
                Assert.All(player.PlayerPath.PathPoints, p => Assert.Equal(PlayerPathHelper.IsFightEnvironment(p.EnvironmentName), p.MinLevel != null));
            }
        }

        [Fact]
        public async Task LancerLaPartieAvecMinuteur_EnvoieLeTempsRestant()
        {
            var (h, code, _) = await CreateGame();
            await h.Hub("host").StartGame(code, true, 10);
            try
            {
                double seconds = h.Named("TimerUpdate").First().Arg<double>(0);
                Assert.InRange(seconds, 590, 600);
            }
            finally
            {
                await h.Hub("host").EndGame(code);
            }
        }

        [Fact]
        public async Task FinDuMinuteur_SoigneLesEquipesEtPrevientLaSalle()
        {
            var (h, code, hostId) = await CreateGame();
            PlayerMongo host = h.Players.Get(hostId);
            host.Team[0].CurrHp = 1;
            host.Team[0].IsPoisoned = 1;
            host.Team[0].AtkChanges = 2;
            h.UpdatePlayer(host);

            await h.Hub("host").StartGame(code, true, 0);
            for (int i = 0; i < 50 && !h.Named("TimerEnded").Any(); i++) await Task.Delay(100);

            Assert.Contains(h.Named("TimerEnded"), m => m.Target == "group:" + code);
            Assert.Equal(GameHub.RoomStateRaceOver, h.Rooms.All.Single().state);
            PokemonTeam pokemon = h.Players.Get(hostId).Team[0];
            Assert.Equal(pokemon.BaseHp, pokemon.CurrHp);
            Assert.Equal(0, pokemon.IsPoisoned);
            Assert.Equal(0, pokemon.AtkChanges);
        }

        [Fact]
        public async Task QuitterLaPartie_PrevientLaSalle()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("host-conn-" + h.RoomId).LeaveGame(code, hostId);

            Assert.Contains(h.Named("UserLeft"), m => m.Target == "group:" + code && m.Arg<string>(0) == hostId);
            Assert.Null(UserConnectionManager.GetConnectionId(hostId, code));
        }

        [Fact]
        public async Task QuitterLaSalleDAttente_LeJoueurNEstPlusListe()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("guest").JoinGame("Pierre", 1, "brock", code);
            string guestId = h.Named("JoinSuccess").Single().Arg<string>(1);

            await h.Hub("guest").LeaveGame(code, guestId);

            List<PlayerMongo> players = await h.Players.GetByRoomId(code);
            Assert.Equal(hostId, Assert.Single(players)._id);
            Assert.True(players[0].IsHost);
        }

        [Fact]
        public async Task LHoteQuitteLaSalleDAttente_LeJoueurSuivantDevientHote()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("guest").JoinGame("Pierre", 1, "brock", code);
            string guestId = h.Named("JoinSuccess").Single().Arg<string>(1);

            await h.Hub("host-conn-" + h.RoomId).LeaveGame(code, hostId);

            PlayerMongo guest = Assert.Single(await h.Players.GetByRoomId(code));
            Assert.Equal(guestId, guest._id);
            Assert.True(guest.IsHost);
            Assert.Equal(guestId, h.Rooms.All.Single().hostUserId);
        }

        [Fact]
        public async Task QuitterUnePartieEnCours_LeJoueurResteDansLaPartie()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("host").StartGame(code, false, 5);

            await h.Hub("host-conn-" + h.RoomId).LeaveGame(code, hostId);

            Assert.Equal(code, h.Players.Get(hostId).RoomId);
        }

        private static async Task<(HubHarness h, string code, string hostId, string guestId)> FinishedTournament()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("guest").JoinGame("Pierre", 1, "brock", code);
            string guestId = h.Named("JoinSuccess").Single().Arg<string>(1);
            await h.Hub("host").StartGame(code, false, 5);
            await h.Brackets.CreateAsync(new PkmnRaceBattle.Domain.Models.BracketMongo.BracketMongo
            {
                GameCode = code,
                Players = (await h.Players.GetByRoomId(code)),
                Champion = guestId,
                NbTurn = 2
            });
            return (h, code, hostId, guestId);
        }

        [Fact]
        public async Task Rejouer_RemetLaSalleEnAttenteSansJoueurNiTournoi()
        {
            var (h, code, hostId, _) = await FinishedTournament();

            await h.Hub("guest").ReplayGame(code, hostId);

            RoomMongo room = h.Rooms.All.Single();
            Assert.Equal(0, room.state);
            Assert.Empty(await h.Players.GetByRoomId(code));
            Assert.Null(await h.Brackets.GetByRoomId(code));
        }

        [Fact]
        public async Task Rejouer_LePremierRevenuDevientHoteLesSuivantsInvites()
        {
            var (h, code, hostId, guestId) = await FinishedTournament();
            await h.Hub("guest").ReplayGame(code, guestId);
            await h.Hub("host-conn-" + h.RoomId).ReplayGame(code, hostId);

            await h.Hub("guest").JoinGame("Pierre", 7, "brock", code);
            await h.Hub("host-conn-" + h.RoomId).JoinGame("Sacha", 4, "red", code);

            List<PlayerMongo> players = await h.Players.GetByRoomId(code);
            Assert.Equal(2, players.Count);
            PlayerMongo pierre = players.Single(p => p.Name == "Pierre");
            Assert.True(pierre.IsHost);
            Assert.False(players.Single(p => p.Name == "Sacha").IsHost);
            Assert.Equal(7, Assert.Single(pierre.Team).IdDex);
            Assert.Equal(pierre._id, h.Rooms.All.Single().hostUserId);
        }

        [Fact]
        public async Task Rejouer_UnJoueurEnRetardNeRemetPasLaNouvellePartieAZero()
        {
            var (h, code, hostId, guestId) = await FinishedTournament();
            await h.Hub("guest").ReplayGame(code, guestId);
            await h.Hub("guest").JoinGame("Pierre", 7, "brock", code);

            await h.Hub("host-conn-" + h.RoomId).ReplayGame(code, hostId);

            Assert.Single(await h.Players.GetByRoomId(code));
        }

        [Fact]
        public async Task Rejouer_SansTournoiTermine_NeFaitRien()
        {
            var (h, code, hostId) = await CreateGame();
            await h.Hub("host").StartGame(code, false, 5);

            await h.Hub("host-conn-" + h.RoomId).ReplayGame(code, hostId);

            Assert.Equal(1, h.Rooms.All.Single().state);
            Assert.Single(await h.Players.GetByRoomId(code));
        }

        [Fact]
        public async Task RejoindreUneAutrePartie_QuitteLAncienneSalle()
        {
            var (h, code, _) = await CreateGame();
            await h.Hub("other-host").CreateGame("Ondine", 7, "misty");
            string otherCode = h.Named("GameCreated").Last().Arg<string>(0);
            await h.Hub("guest").JoinGame("Pierre", 1, "brock", code);

            await h.Hub("guest").JoinGame("Pierre", 1, "brock", otherCode);

            Assert.Contains(h.Named("UserLeft"), m => m.Target == "group:" + code);
        }
    }
}
