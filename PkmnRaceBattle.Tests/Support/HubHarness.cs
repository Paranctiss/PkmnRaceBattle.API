using Microsoft.AspNetCore.SignalR;
using Moq;
using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.PathManager;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;

namespace PkmnRaceBattle.Tests.Support
{
    // Message envoyé par le serveur. Target = "caller:<connexion>", "client:<connexion>" ou "group:<salle>"
    public record SentMessage(string From, string Target, string Method, object?[] Args)
    {
        public T Arg<T>(int index) => (T)Args[index]!;
        public override string ToString() => $"{Target} <- {Method}";
    }

    // Environnement d'exécution du GameHub : dépôts en mémoire + clients SignalR qui enregistrent tout ce qui est envoyé
    public sealed class HubHarness
    {
        public InMemoryPlayerRepository Players { get; } = new();
        public InMemoryWildPokemonRepository Opponents { get; } = new();
        public InMemoryPokemonRepository Pokemons { get; } = new();
        public InMemoryMoveRepository Moves { get; } = new();
        public InMemoryRoomRepository Rooms { get; } = new();
        public InMemoryBracketRepository Brackets { get; } = new();

        public List<SentMessage> Sent { get; } = new();

        // Code de salle unique : UserConnectionManager est statique et partagé entre les tests
        public string RoomId { get; } = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        public GameHub Hub(string connectionId)
        {
            var hubContext = new Mock<IHubContext<GameHub>>();
            var hubClients = new Mock<IHubClients>();
            hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns<string>(g => Proxy("timer", "group:" + g));
            hubContext.Setup(c => c.Clients).Returns(hubClients.Object);

            var hub = new GameHub(Rooms, Moves, Players, Pokemons, Opponents, Brackets, hubContext.Object);

            var clients = new Mock<IHubCallerClients>();
            clients.Setup(c => c.Caller).Returns(() => Proxy(connectionId, "caller:" + connectionId));
            clients.Setup(c => c.Client(It.IsAny<string>())).Returns<string>(id => Proxy(connectionId, "client:" + id));
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns<string>(g => Proxy(connectionId, "group:" + g));
            clients.Setup(c => c.Others).Returns(() => Proxy(connectionId, "others:" + connectionId));
            clients.Setup(c => c.All).Returns(() => Proxy(connectionId, "all"));
            hub.Clients = clients.Object;

            var context = new Mock<HubCallerContext>();
            context.Setup(c => c.ConnectionId).Returns(connectionId);
            hub.Context = context.Object;

            hub.Groups = new Mock<IGroupManager>().Object;
            return hub;
        }

        private ISingleClientProxy Proxy(string from, string target) => new RecordingProxy(Sent, from, target);

        public IEnumerable<SentMessage> SentTo(string connectionId) =>
            Sent.Where(m => m.Target == "caller:" + connectionId || m.Target == "client:" + connectionId);

        public IEnumerable<SentMessage> Named(string method) => Sent.Where(m => m.Method == method);

        public IEnumerable<SentMessage> Named(string method, string connectionId) => SentTo(connectionId).Where(m => m.Method == method);

        // Tous les messages affichés au joueur dans les TurnContext reçus (prioritaires puis normaux), dans l'ordre
        public List<string> DialogFor(string connectionId) => SentTo(connectionId)
            .Where(m => m.Method == "useMoveResult" || m.Method == "useItemResult")
            .SelectMany(m => { var ctx = (TurnContext)m.Args[0]!; return ctx.PrioMessages.Concat(ctx.Messages); })
            .ToList();

        // Joueur prêt à jouer, enregistré en base et auprès d'UserConnectionManager
        public async Task<PlayerMongo> AddPlayer(string name, params PokemonTeam[] team)
        {
            var player = new PlayerMongo
            {
                Name = name,
                Sprite = "red",
                RoomId = RoomId,
                Team = team
            };
            PlayerPathHelper.InitPlayerPath(player);
            // Le joueur est déjà sur la première map de combat (Plaine), sans combat terminé
            PlayerPathHelper.MoveTo(player, player.PlayerPath.PathPoints.First(p => p.X == 1));
            string id = await Players.CreateAsync(player);
            UserConnectionManager.AddUserToRoom(id, RoomId, Connection(id));
            return Players.Get(id);
        }

        public static string Connection(string playerId) => "conn-" + playerId;

        // Réglages d'XP de la salle (sans salle en base : Multi Exp activé, XP normale)
        public void SetXpSettings(bool multiXp, int multiplier)
        {
            Rooms.CreateAsync(new RoomMongo { roomId = RoomId, state = 1, MultiXp = multiXp, XpMultiplier = multiplier })
                .GetAwaiter().GetResult();
        }

        public async Task<PlayerMongo> AddWildOpponent(PokemonTeam pokemon)
        {
            var opponent = new PlayerMongo();
            opponent.GenerateWild();
            opponent.Team = [pokemon];
            await Opponents.CreateAsync(opponent);
            return Opponents.Get(opponent._id);
        }

        public async Task<PlayerMongo> AddTrainerOpponent(string name, params PokemonTeam[] team)
        {
            var trainer = new PlayerMongo
            {
                Name = name,
                Sprite = "brock",
                IsHost = false,
                IsPlayer = false,
                IsTrainer = true,
                Credits = 0,
                Items = [],
                Team = team
            };
            await Opponents.CreateAsync(trainer);
            return Opponents.Get(trainer._id);
        }

        public void UpdatePlayer(PlayerMongo player) => Players.UpdateAsync(player).GetAwaiter().GetResult();

        // Le joueur utilise une capacité (ou "item:..."/"swap:") contre un adversaire sauvage / dresseur
        public Task PlayerUses(PlayerMongo player, string move, PlayerMongo opponent, int index = 0)
        {
            PlayerMongo fresh = Players.Get(player._id);
            PlayerMongo freshOpponent = Opponents.Get(opponent._id);
            return Hub(Connection(player._id)).HandleMove(player._id, fresh.Team[0].Id, move, opponent._id, freshOpponent.Team[0].Id, true, false, index);
        }

        public Task PvpUses(PlayerMongo player, string move, PlayerMongo opponent, int index = 0)
        {
            PlayerMongo fresh = Players.Get(player._id);
            PlayerMongo freshOpponent = Players.Get(opponent._id);
            return Hub(Connection(player._id)).HandleMove(player._id, fresh.Team[0].Id, move, opponent._id, freshOpponent.Team[0].Id, true, true, index);
        }

        public static IDisposable Metronome(string moveNameFr) => MetronomeMoveProvider.Use(() => Task.FromResult<MoveMongo>(GameData.Move(moveNameFr)));
    }

    internal sealed class RecordingProxy(List<SentMessage> log, string from, string target) : ISingleClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            // Copie des arguments au moment de l'envoi : le serveur continue de modifier ses objets ensuite
            object?[] snapshot = args.Select(Snapshot).ToArray();
            lock (log) log.Add(new SentMessage(from, target, method, snapshot));
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static object? Snapshot(object? arg) => arg switch
        {
            null => null,
            TurnContext ctx => CloneContext(ctx),
            PlayerMongo p => GameData.Clone(p),
            PokemonTeam p => GameData.Clone(p),
            _ => arg
        };

        private static TurnContext CloneContext(TurnContext ctx)
        {
            var copy = new TurnContext { ActionName = ctx.ActionName };
            copy.Messages.AddRange(ctx.Messages);
            copy.PrioMessages.AddRange(ctx.PrioMessages);
            copy.Player = CloneChanges(ctx.Player);
            copy.Opponent = CloneChanges(ctx.Opponent);
            return copy;
        }

        private static PokemonChanges CloneChanges(PokemonChanges c) => new()
        {
            Hp = c.Hp.ToList(), Atk = c.Atk, AtkSpe = c.AtkSpe, Def = c.Def, DefSpe = c.DefSpe, Speed = c.Speed, Index = c.Index
        };
    }
}
