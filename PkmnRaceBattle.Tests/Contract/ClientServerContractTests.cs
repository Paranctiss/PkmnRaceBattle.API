using System.Reflection;
using System.Text.RegularExpressions;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.Tests.Contract
{
    // Le contrat SignalR est fait de chaînes écrites à la main des deux côtés : ces tests lisent le code du client Angular
    // (dépôt voisin ../PokemonRaceBattle) et vérifient qu'il correspond au serveur. Ils sont ignorés si le client est absent.
    public class ClientServerContractTests
    {
        // Nom du dossier du client : PokemonRaceBattle en local, PkmnRaceBattle (nom du dépôt GitHub) dans un clone frais
        private static readonly string[] ClientFolders = { "PokemonRaceBattle", "PkmnRaceBattle" };
        private static readonly string? ClientRoot = FindClient();
        private static readonly string ServerRoot = FindServer();

        private static string? FindClient()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                foreach (string folder in ClientFolders)
                {
                    string candidate = System.IO.Path.Combine(dir.FullName, folder, "src", "app");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            // Exécution depuis le dossier temporaire de build : on part du code source du projet de tests
            return ClientFolders
                .Select(folder => System.IO.Path.GetFullPath(System.IO.Path.Combine(SourceDir(), "..", "..", folder, "src", "app")))
                .FirstOrDefault(Directory.Exists);
        }

        private static string FindServer() => System.IO.Path.GetFullPath(System.IO.Path.Combine(SourceDir(), "..", "PkmnRaceBattle.API"));

        private static string SourceDir([System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
            System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(file)!)!;

        private static IEnumerable<string> ClientFiles() => Directory.EnumerateFiles(ClientRoot!, "*.ts", SearchOption.AllDirectories).Where(f => !f.EndsWith(".spec.ts"));

        private static string HubService() => File.ReadAllText(System.IO.Path.Combine(ClientRoot!, "core", "services", "Hub", "hub.service.ts"));

        // invoke('Nom', a, b, c) -> (Nom, 3)
        private static List<(string Method, int Args)> ClientInvocations() =>
            Regex.Matches(HubService(), @"invoke\('(\w+)'((?:,[^)]*)?)\)")
                .Select(m => (m.Groups[1].Value, m.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Length))
                .ToList();

        private static HashSet<string> ClientListeners() => ClientFiles()
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?:connection\.on|registerSignalREvent)\('(\w+)'").Select(m => m.Groups[1].Value))
            .ToHashSet();

        private static HashSet<string> ServerEvents() => Directory.EnumerateFiles(System.IO.Path.Combine(ServerRoot, "Hub"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"SendAsync\(""(\w+)""").Select(m => m.Groups[1].Value))
            .ToHashSet();

        [Fact]
        public void ChaqueAppelDuClient_ExisteSurLeHubAvecLeBonNombreDArguments()
        {
            if (ClientRoot == null) return;
            var methods = typeof(GameHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var errors = new List<string>();

            foreach (var (method, args) in ClientInvocations())
            {
                var candidates = methods.Where(m => m.Name == method).ToList();
                if (candidates.Count == 0) { errors.Add($"{method} : méthode absente du GameHub"); continue; }
                bool ok = candidates.Any(m =>
                {
                    var parameters = m.GetParameters();
                    return args >= parameters.Count(p => !p.IsOptional) && args <= parameters.Length;
                });
                if (!ok) errors.Add($"{method} : le client envoie {args} argument(s)");
            }

            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }

        [Fact]
        public void ChaqueAppelDuClient_UtiliseDesTypesSimplesCommeLeServeur()
        {
            // Le client n'envoie que des chaînes / nombres / booléens : une méthode qui attend un objet ne peut pas être appelée
            if (ClientRoot == null) return;
            var errors = ClientInvocations()
                .Select(i => i.Method).Distinct()
                .SelectMany(name => typeof(GameHub).GetMethods().Where(m => m.Name == name))
                .Where(m => m.GetParameters().Any(p => !(p.ParameterType.IsPrimitive || p.ParameterType == typeof(string))))
                .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) n'est pas appelable par le client")
                .ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }

        [Fact]
        public void ChaqueEvenementDuServeur_EstEcouteParLeClient()
        {
            if (ClientRoot == null) return;
            var missing = ServerEvents().Except(ClientListeners()).ToList();
            Assert.True(missing.Count == 0, "Événements envoyés mais jamais écoutés : " + string.Join(", ", missing));
        }

        [Fact]
        public void ChaqueEcouteDuClient_CorrespondAUnEvenementDuServeur()
        {
            if (ClientRoot == null) return;
            var dead = ClientListeners().Except(ServerEvents()).ToList();
            Assert.True(dead.Count == 0, "Événements écoutés mais jamais envoyés : " + string.Join(", ", dead));
        }

        [Fact]
        public void PrixDeLaBoutique_IdentiquesCoteClientEtServeur()
        {
            if (ClientRoot == null) return;
            string items = File.ReadAllText(System.IO.Path.Combine(ClientRoot, "shared", "utils", "items.ts"));
            var client = Regex.Matches(items, @"name: '([^']+)',[^}]*price: (\d+), pocket: '(\w+)'")
                .ToDictionary(m => m.Groups[1].Value, m => (Price: int.Parse(m.Groups[2].Value), Pocket: m.Groups[3].Value));
            var server = new PlayerMongo().Items.ToDictionary(i => i.Name, i => (i.Price, Pocket: i.Type));

            Assert.Equal(server.Keys.OrderBy(k => k), client.Keys.OrderBy(k => k));
            foreach (var (name, value) in server) Assert.Equal(value, client[name]);
        }

        [Fact]
        public void EnvironnementsDuChemin_ConnusDuClient()
        {
            if (ClientRoot == null) return;
            string environments = File.ReadAllText(System.IO.Path.Combine(ClientRoot, "shared", "utils", "environment.ts"));
            foreach (string env in new[] { "Plaine", "Volcan", "Foret", "Grotte", "Centrale", "Eau", "Shop", "Centre" })
                Assert.Contains(env + ": {", environments);
        }

        [Fact]
        public void NombreDeCombatsSauvagesParMap_IdentiqueCoteClient()
        {
            if (ClientRoot == null) return;
            string game = File.ReadAllText(System.IO.Path.Combine(ClientRoot, "features", "game", "game.component.ts"));
            Assert.Contains($"WILD_FIGHTS_PER_MAP = {PkmnRaceBattle.API.Helpers.PathManager.PlayerPathHelper.WildFightsPerMap};", game);
        }
    }
}
