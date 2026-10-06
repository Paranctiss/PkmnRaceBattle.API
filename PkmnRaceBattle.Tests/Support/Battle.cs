using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.Tests.Support
{
    // Combat joueur contre sauvage (ou dresseur) piloté de bout en bout par GameHub.HandleMove
    public sealed class Battle
    {
        public HubHarness H { get; } = new();
        public PlayerMongo PlayerRef { get; private set; } = null!;
        public PlayerMongo OpponentRef { get; private set; } = null!;

        public string PlayerId => PlayerRef._id;
        public string Connection => HubHarness.Connection(PlayerRef._id);

        // État actuel « en base »
        public PlayerMongo Player => H.Players.Get(PlayerRef._id);
        public PlayerMongo Opponent => H.Opponents.Get(OpponentRef._id);
        public PokemonTeam Mine => Player.Team[0];
        public PokemonTeam Foe => Opponent.Team[0];

        // Astuce : donner Trempette comme seule capacité à l'adversaire pour qu'il ne fasse rien
        public static async Task<Battle> VsWild(PokemonTeam player, PokemonTeam wild, params PokemonTeam[] bench)
        {
            var battle = new Battle();
            battle.PlayerRef = await battle.H.AddPlayer("Sacha", [player, .. bench]);
            battle.OpponentRef = await battle.H.AddWildOpponent(wild);
            return battle;
        }

        public static async Task<Battle> VsTrainer(PokemonTeam player, PokemonTeam[] trainerTeam, params PokemonTeam[] bench)
        {
            var battle = new Battle();
            battle.PlayerRef = await battle.H.AddPlayer("Sacha", [player, .. bench]);
            battle.OpponentRef = await battle.H.AddTrainerOpponent("Pierre", trainerTeam);
            return battle;
        }

        public Task Use(string move, int index = 0) => H.PlayerUses(PlayerRef, move, OpponentRef, index);

        public Task UseItem(string item, string type, int index = 0) => Use("item:" + item + ":" + type, index);

        public Task SwitchTo(int teamIndex) =>
            H.Hub(Connection).ReplacePokemon(PlayerId, Player.Team[teamIndex].Id, OpponentRef._id, false);

        public void Edit(Action<PlayerMongo> change)
        {
            PlayerMongo player = Player;
            change(player);
            H.UpdatePlayer(player);
        }

        public void EditOpponent(Action<PlayerMongo> change)
        {
            PlayerMongo opponent = Opponent;
            change(opponent);
            H.Opponents.UpdateAsync(opponent).GetAwaiter().GetResult();
        }

        public List<string> Dialog => H.DialogFor(Connection);

        public bool Said(string fragment) => Dialog.Any(m => m.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<SentMessage> Received(string method) => H.Named(method, Connection);

        // Somme des variations de PV envoyées au client pour le Pokémon du joueur / de l'adversaire (barre de vie)
        public int HpShownForPlayer => Received("useMoveResult").Concat(Received("useItemResult"))
            .Sum(m => ((TurnContext)m.Args[0]!).Player.Hp.Sum());

        public int HpShownForOpponent => Received("useMoveResult").Sum(m => ((TurnContext)m.Args[0]!).Opponent.Hp.Sum());

        public void ClearMessages() => H.Sent.Clear();
    }
}
