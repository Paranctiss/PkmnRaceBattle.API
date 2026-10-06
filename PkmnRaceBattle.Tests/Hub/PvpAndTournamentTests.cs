using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.BracketMongo;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Tests.Support;

namespace PkmnRaceBattle.Tests.Hub
{
    // Combats PvP : système d'attente du tour de l'adversaire, perspective de chaque joueur, fin de combat
    public class PvpTests
    {
        private sealed record Duel(HubHarness H, PlayerMongo Red, PlayerMongo Blue)
        {
            public PlayerMongo RedNow => H.Players.Get(Red._id);
            public PlayerMongo BlueNow => H.Players.Get(Blue._id);
            public string RedConn => HubHarness.Connection(Red._id);
            public string BlueConn => HubHarness.Connection(Blue._id);
        }

        private static async Task<Duel> Setup(PokemonTeam red, PokemonTeam blue, PokemonTeam[]? redBench = null, PokemonTeam[]? blueBench = null)
        {
            var h = new HubHarness();
            PlayerMongo r = await h.AddPlayer("Red", [red, .. redBench ?? []]);
            PlayerMongo b = await h.AddPlayer("Blue", [blue, .. blueBench ?? []]);
            return new Duel(h, r, b);
        }

        private static PokemonTeam Fighter(string name, int speed, int hp = 300, params string[] moves) =>
            Pkmn.Create(name, 50, moves.Length > 0 ? moves : ["Charge"]).WithStats(hp: hp, speed: speed);

        [Fact]
        public async Task PremierAChoisir_AttendLAdversaire()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100));

            await d.H.PvpUses(d.Red, "Charge", d.Blue);

            Assert.Single(d.H.Named("waitingOpponent", d.RedConn));
            Assert.Empty(d.H.SentTo(d.BlueConn));
            Assert.Equal("Charge", d.RedNow.ChosenMove?.NameFr);
            Assert.Equal(300, d.BlueNow.Team[0].CurrHp);
            Assert.Empty(d.H.Named("useMoveResult"));
        }

        [Fact]
        public async Task SecondAChoisir_LeTourEstJoueChezLesDeuxJoueurs()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100));

            await d.H.PvpUses(d.Red, "Charge", d.Blue);
            await d.H.PvpUses(d.Blue, "Charge", d.Red);

            Assert.True(d.RedNow.Team[0].CurrHp < 300);
            Assert.True(d.BlueNow.Team[0].CurrHp < 300);
            Assert.NotEmpty(d.H.Named("useMoveResult", d.RedConn));
            Assert.NotEmpty(d.H.Named("useMoveResult", d.BlueConn));
            Assert.Null(d.RedNow.ChosenMove);
            Assert.Null(d.BlueNow.ChosenMove);
        }

        [Fact]
        public async Task FinDuTour_ChaqueJoueurRecoitSonEtatPuisCeluiDeLAdversaire()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100));

            await d.H.PvpUses(d.Red, "Charge", d.Blue);
            await d.H.PvpUses(d.Blue, "Charge", d.Red);

            SentMessage red = d.H.Named("turnFinished", d.RedConn).Single();
            SentMessage blue = d.H.Named("turnFinished", d.BlueConn).Single();
            Assert.Equal((d.Red._id, d.Blue._id), (red.Arg<PlayerMongo>(0)._id, red.Arg<PlayerMongo>(1)._id));
            Assert.Equal((d.Blue._id, d.Red._id), (blue.Arg<PlayerMongo>(0)._id, blue.Arg<PlayerMongo>(1)._id));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BarreDeVie_ChaqueJoueurVoitSesPropresDegatsCoteJoueur(bool redFirstToChoose)
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100));

            if (redFirstToChoose)
            {
                await d.H.PvpUses(d.Red, "Charge", d.Blue);
                await d.H.PvpUses(d.Blue, "Charge", d.Red);
            }
            else
            {
                await d.H.PvpUses(d.Blue, "Charge", d.Red);
                await d.H.PvpUses(d.Red, "Charge", d.Blue);
            }

            int redLost = 300 - d.RedNow.Team[0].CurrHp;
            int blueLost = 300 - d.BlueNow.Team[0].CurrHp;
            static (int player, int opponent) Shown(IEnumerable<SentMessage> messages) => (
                messages.Sum(m => ((TurnContext)m.Args[0]!).Player.Hp.Sum()),
                messages.Sum(m => ((TurnContext)m.Args[0]!).Opponent.Hp.Sum()));

            Assert.Equal((redLost, blueLost), Shown(d.H.Named("useMoveResult", d.RedConn)));
            Assert.Equal((blueLost, redLost), Shown(d.H.Named("useMoveResult", d.BlueConn)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LePlusRapideAttaqueEnPremier_QuelQueSoitLOrdreDesChoix(bool fastChoosesFirst)
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100));

            if (fastChoosesFirst)
            {
                await d.H.PvpUses(d.Red, "Charge", d.Blue);
                await d.H.PvpUses(d.Blue, "Charge", d.Red);
            }
            else
            {
                await d.H.PvpUses(d.Blue, "Charge", d.Red);
                await d.H.PvpUses(d.Red, "Charge", d.Blue);
            }

            var dialog = d.H.DialogFor(d.RedConn);
            Assert.True(dialog.FindIndex(m => m.StartsWith("Pikachu lance")) < dialog.FindIndex(m => m.StartsWith("Évoli lance")), string.Join(" | ", dialog));
        }

        [Fact]
        public async Task ChaqueJoueurVoitLesDeuxAttaques()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200, moves: "Éclair"), Fighter("Évoli", 100, moves: "Griffe"));

            await d.H.PvpUses(d.Red, "Éclair", d.Blue);
            await d.H.PvpUses(d.Blue, "Griffe", d.Red);

            foreach (string conn in new[] { d.RedConn, d.BlueConn })
            {
                var dialog = d.H.DialogFor(conn);
                Assert.Contains(dialog, m => m == "Pikachu lance Éclair");
                Assert.Contains(dialog, m => m == "Évoli lance Griffe");
            }
        }

        [Fact]
        public async Task KOduDernierPokemon_LeVainqueurEstQualifieEtLePerdantElimine()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200, hp: 300, "Ultimapoing").WithStats(atk: 999), Fighter("Évoli", 100, hp: 10));

            await d.H.PvpUses(d.Blue, "Charge", d.Red);
            await d.H.PvpUses(d.Red, "Ultimapoing", d.Blue);

            Assert.Contains(d.H.DialogFor(d.RedConn), m => m.Contains("qualifié"));
            Assert.Single(d.H.Named("playerLooseFight", d.BlueConn));
            Assert.Empty(d.H.Named("playerLooseFight", d.RedConn));
            Assert.Empty(d.H.Named("responseWildFight"));
        }

        [Fact]
        public async Task KOduDernierPokemon_ParLeJoueurQuiAChoisiEnPremier()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200, hp: 300, "Ultimapoing").WithStats(atk: 999), Fighter("Évoli", 100, hp: 10));

            await d.H.PvpUses(d.Red, "Ultimapoing", d.Blue);
            await d.H.PvpUses(d.Blue, "Charge", d.Red);

            Assert.Contains(d.H.DialogFor(d.RedConn), m => m.Contains("qualifié"));
            Assert.Single(d.H.Named("playerLooseFight", d.BlueConn));
            Assert.Empty(d.H.Named("playerLooseFight", d.RedConn));
        }

        [Fact]
        public async Task KOAvecDesPokemonRestants_LePerdantChangeEtLAutreAttend()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200, hp: 300, "Ultimapoing").WithStats(atk: 999), Fighter("Évoli", 100, hp: 10), blueBench: [Fighter("Racaillou", 50)]);

            await d.H.PvpUses(d.Blue, "Charge", d.Red);
            await d.H.PvpUses(d.Red, "Ultimapoing", d.Blue);

            Assert.Single(d.H.Named("playerPokemonDeath", d.BlueConn));
            Assert.NotEmpty(d.H.Named("waitingOpponent", d.RedConn));
            Assert.Empty(d.H.Named("playerLooseFight"));
        }

        [Fact]
        public async Task Changement_LAdversaireVoitLeNouveauPokemon()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100), redBench: [Fighter("Racaillou", 50)]);

            await d.H.Hub(d.RedConn).ReplacePokemon(d.Red._id, d.RedNow.Team[1].Id, d.Blue._id, true);
            await d.H.PvpUses(d.Blue, "Charge", d.Red);

            Assert.Equal("Racaillou", d.H.Named("foeSwapPokemon", d.BlueConn).Single().Arg<PokemonTeam>(0).NameFr);
            Assert.Equal("Racaillou", d.H.Named("swapPokemon", d.RedConn).Single().Arg<PokemonTeam>(0).NameFr);
            Assert.True(d.RedNow.Team[0].CurrHp < 300, "Racaillou subit l'attaque d'Évoli");
        }

        [Fact]
        public async Task Pvp_NeFaitPasAvancerLaCarte()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200, hp: 300, "Ultimapoing").WithStats(atk: 999), Fighter("Évoli", 100, hp: 10));

            await d.H.PvpUses(d.Blue, "Charge", d.Red);
            await d.H.PvpUses(d.Red, "Ultimapoing", d.Blue);

            Assert.Equal(0, d.RedNow.MapFightCount);
            Assert.Empty(d.H.Named("responseWildFight"));
            Assert.Empty(d.H.Named("responseTrainerFight"));
        }

        [Fact]
        public async Task Pokeball_InterditeEnPvp()
        {
            using var _ = TestRandom.Neutral().Install();
            var d = await Setup(Fighter("Pikachu", 200), Fighter("Évoli", 100));

            await d.H.PvpUses(d.Red, "item:Pokeball:ball", d.Blue);

            Assert.Empty(d.H.Named("launchBall"));
            Assert.Null(d.RedNow.ChosenMove);
        }
    }

    // Tournoi de fin de partie
    public class TournamentTests
    {
        private static async Task<(HubHarness h, List<PlayerMongo> players)> Room(int count)
        {
            var h = new HubHarness();
            var players = new List<PlayerMongo>();
            for (int i = 0; i < count; i++) players.Add(await h.AddPlayer("J" + i, Pkmn.Create("Pikachu", 20, "Charge")));
            return (h, players);
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(4, 2)]
        [InlineData(8, 3)]
        public async Task Tableau_ContientTousLesJoueursEtLeBonNombreDeTours(int players, int rounds)
        {
            var (h, all) = await Room(players);
            using var _ = new TestRandom(5).Install();

            await h.Hub("host").BuildTournament(h.RoomId);

            BracketMongo bracket = h.Brackets.All.Single();
            Assert.Equal(rounds, bracket.Rounds.Count);
            Assert.Equal(1, bracket.NbTurn);
            Assert.Equal(all.Select(p => p._id).OrderBy(x => x), bracket.Rounds[^1].PlayersInRace.OrderBy(x => x));
            Assert.Equal(all.Count, bracket.Players.Count);
            Assert.Contains(h.Named("bracketCreated"), m => m.Target == "group:" + h.RoomId);
        }

        [Fact]
        public async Task LancerLeTournoi_PrevientToutLeMonde()
        {
            var (h, _) = await Room(2);
            await h.Hub("host").LaunchTournament(h.RoomId);
            Assert.Contains(h.Named("triggerTournament"), m => m.Target == "group:" + h.RoomId);
        }

        [Fact]
        public async Task CombatPvp_LesJoueursSontAppariesDeuxADeux()
        {
            var (h, all) = await Room(4);
            using var _ = new TestRandom(5).Install();
            await h.Hub("host").BuildTournament(h.RoomId);
            List<string> order = h.Brackets.All.Single().Rounds[^1].PlayersInRace;

            foreach (PlayerMongo player in all)
            {
                h.Sent.Clear();
                await h.Hub(HubHarness.Connection(player._id)).GetPvpFight(h.RoomId, player._id);
                PlayerMongo opponent = h.Named("responsePvpFight").Single().Arg<PlayerMongo>(0);
                int index = order.IndexOf(player._id);
                Assert.Equal(order[index % 2 == 0 ? index + 1 : index - 1], opponent._id);
            }
        }

        [Fact]
        public async Task NombreImpairDeJoueurs_NePlantePas()
        {
            var (h, all) = await Room(3);
            using var _ = new TestRandom(5).Install();
            await h.Hub("host").BuildTournament(h.RoomId);

            foreach (PlayerMongo player in all)
            {
                var exception = await Record.ExceptionAsync(() => h.Hub(HubHarness.Connection(player._id)).GetPvpFight(h.RoomId, player._id));
                Assert.Null(exception);
            }
        }

        [Fact]
        public async Task VictoirePvp_LeVainqueurPasseAuTourSuivant()
        {
            var (h, all) = await Room(4);
            using var _ = TestRandom.Neutral().Install();
            await h.Hub("host").BuildTournament(h.RoomId);
            List<string> order = h.Brackets.All.Single().Rounds[^1].PlayersInRace;
            PlayerMongo winner = h.Players.Get(order[0]);
            PlayerMongo loser = h.Players.Get(order[1]);
            winner.Team[0] = winner.Team[0].WithStats(atk: 999, speed: 300);
            loser.Team[0] = loser.Team[0].WithStats(hp: 5, speed: 1);
            h.UpdatePlayer(winner);
            h.UpdatePlayer(loser);

            await h.PvpUses(loser, "Charge", winner);
            await h.PvpUses(winner, "Charge", loser);

            BracketMongo bracket = h.Brackets.All.Single();
            Assert.Contains(winner._id, bracket.Rounds[0].PlayersInRace);
            Assert.DoesNotContain(loser._id, bracket.Rounds[0].PlayersInRace);
        }

        [Fact]
        public async Task NombreImpairDeJoueurs_LExempteEstDejaQualifie()
        {
            var (h, _) = await Room(3);
            using var _r = new TestRandom(5).Install();
            await h.Hub("host").BuildTournament(h.RoomId);

            BracketMongo bracket = h.Brackets.All.Single();
            List<string> firstRound = bracket.Rounds[^1].PlayersInRace;
            int bye = firstRound.IndexOf("?");
            string exempt = firstRound[bye ^ 1];
            Assert.Equal(1, firstRound.Count(x => x == "?"));
            Assert.Equal(exempt, bracket.Rounds[0].PlayersInRace[bye / 2]);

            h.Sent.Clear();
            await h.Hub(HubHarness.Connection(exempt)).GetPvpFight(h.RoomId, exempt);
            Assert.Empty(h.Named("responsePvpFight"));
        }

        [Fact]
        public async Task TourTermine_LesVainqueursSAffrontentAuTourSuivant()
        {
            var (h, _) = await Room(4);
            using var _r = TestRandom.Neutral().Install();
            await h.Hub("host").BuildTournament(h.RoomId);
            List<string> order = h.Brackets.All.Single().Rounds[^1].PlayersInRace;

            // Le premier de chaque duel gagne
            foreach (int match in new[] { 0, 1 })
            {
                PlayerMongo winner = h.Players.Get(order[match * 2]);
                PlayerMongo loser = h.Players.Get(order[match * 2 + 1]);
                winner.Team[0] = winner.Team[0].WithStats(atk: 999, speed: 300);
                loser.Team[0] = loser.Team[0].WithStats(hp: 5, speed: 1);
                h.UpdatePlayer(winner);
                h.UpdatePlayer(loser);
                await h.PvpUses(loser, "Charge", winner);
                await h.PvpUses(winner, "Charge", loser);
            }

            BracketMongo bracket = h.Brackets.All.Single();
            Assert.Equal(2, bracket.NbTurn);
            Assert.Equal(new[] { order[0], order[2] }, bracket.Rounds[0].PlayersInRace);
            Assert.Contains(h.Named("bracketCreated"), m => m.Target == "group:" + h.RoomId && m.Arg<BracketMongo>(0).NbTurn == 2);

            h.Sent.Clear();
            await h.Hub(HubHarness.Connection(order[0])).GetPvpFight(h.RoomId, order[0]);
            Assert.Equal(order[2], h.Named("responsePvpFight").Single().Arg<PlayerMongo>(0)._id);

            h.Sent.Clear();
            await h.Hub(HubHarness.Connection(order[1])).GetPvpFight(h.RoomId, order[1]);
            Assert.Empty(h.Named("responsePvpFight"));
        }

        [Fact]
        public async Task FinaleGagnee_LeChampionEstAnnonceATous()
        {
            var (h, all) = await Room(2);
            using var _r = TestRandom.Neutral().Install();
            await h.Hub("host").BuildTournament(h.RoomId);
            List<string> order = h.Brackets.All.Single().Rounds[^1].PlayersInRace;
            PlayerMongo winner = h.Players.Get(order[0]);
            PlayerMongo loser = h.Players.Get(order[1]);
            winner.Team[0] = winner.Team[0].WithStats(atk: 999, speed: 300);
            loser.Team[0] = loser.Team[0].WithStats(hp: 5, speed: 1);
            h.UpdatePlayer(winner);
            h.UpdatePlayer(loser);
            h.Sent.Clear();

            await h.PvpUses(loser, "Charge", winner);
            await h.PvpUses(winner, "Charge", loser);

            BracketMongo bracket = h.Brackets.All.Single();
            Assert.Equal(winner._id, bracket.Champion);
            Assert.Contains(h.Named("bracketCreated"), m => m.Target == "group:" + h.RoomId && m.Arg<BracketMongo>(0).Champion == winner._id);

            // Tournoi terminé : plus de combat
            h.Sent.Clear();
            await h.Hub(HubHarness.Connection(winner._id)).GetPvpFight(h.RoomId, winner._id);
            Assert.Empty(h.Named("responsePvpFight"));
        }
    }
}
