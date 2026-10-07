using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.PathManager
{
    public static class PlayerPathHelper
    {
        public const string StartEnvironment = "Default";
        public const string ShopEnvironment = "Shop";
        public const string CenterEnvironment = "Centre";

        // Sur une map de combat : WildFightsPerMap combats sauvages, puis un combat de dresseur
        public const int WildFightsPerMap = 5;
        public const int FightsPerMap = WildFightsPerMap + 1;

        private static readonly string[] FightEnvironments = ["Plaine", "Volcan", "Foret", "Grotte", "Centrale", "Eau"];

        // Le chemin est généré au fur et à mesure : InitialSteps étapes au départ, puis ChunkSize de plus
        // dès que le joueur n'a plus que ChunkSize étapes devant lui (en arrivant en 6 : 13 à 18, en 12 : 19 à 24…)
        public const int InitialSteps = 12;
        public const int ChunkSize = 6;

        // Place le joueur avant le début de son chemin (le premier GetNewTurn l'amène sur la première map)
        public static void InitPlayerPath(PlayerMongo player)
        {
            player.PlayerPath = GenerateNewPath();
            player.CurrentPath = new PathPoint { X = 0, Y = 0, EnvironmentName = StartEnvironment };
            player.MapFightCount = 0;
        }

        public static bool IsFightEnvironment(string environmentName)
        {
            return environmentName != StartEnvironment
                && environmentName != ShopEnvironment
                && environmentName != CenterEnvironment;
        }

        // Shop / Centre : terminés dès qu'on les quitte. Map de combat : terminée après le combat de dresseur.
        public static bool IsCurrentMapCompleted(PlayerMongo player)
        {
            return !IsFightEnvironment(player.CurrentPath.EnvironmentName)
                || player.MapFightCount >= FightsPerMap;
        }

        public static bool IsTrainerFightNext(PlayerMongo player)
        {
            return player.MapFightCount >= WildFightsPerMap;
        }

        public static List<PathPoint> GetNextPathPoints(PlayerMongo player)
        {
            return player.PlayerPath.PathPoints
                .Where(p => p.X == player.CurrentPath.X + 1)
                .OrderBy(p => p.Y)
                .ToList();
        }

        // Déplace le joueur sur la map donnée, grise les autres maps de la même étape et prolonge le chemin si besoin
        public static void MoveTo(PlayerMongo player, PathPoint destination)
        {
            foreach (PathPoint point in player.PlayerPath.PathPoints.Where(p => p.X == destination.X))
            {
                point.IsSkipped = point.Y != destination.Y;
            }

            player.CurrentPath = destination;
            player.MapFightCount = 0;
            ExtendPathIfNeeded(player);
        }

        // Ajoute ChunkSize étapes tant qu'il en reste ChunkSize ou moins devant le joueur
        public static bool ExtendPathIfNeeded(PlayerMongo player)
        {
            bool extended = false;
            while (LastStep(player.PlayerPath) - player.CurrentPath.X <= ChunkSize)
            {
                AppendSteps(player.PlayerPath, ChunkSize);
                extended = true;
            }
            return extended;
        }

        public static Domain.Models.PlayerMongo.Path GenerateNewPath()
        {
            var path = new Domain.Models.PlayerMongo.Path { PathPoints = new List<PathPoint>() };
            AppendSteps(path, InitialSteps);
            return path;
        }

        public static void AppendSteps(Domain.Models.PlayerMongo.Path path, int count)
        {
            path.PathPoints ??= new List<PathPoint>();
            for (int i = 0; i < count; i++) AppendStep(path);
        }

        private static int LastStep(Domain.Models.PlayerMongo.Path path) =>
            path.PathPoints.Count == 0 ? 0 : path.PathPoints.Max(p => p.X);

        // Règles du chemin :
        // - étape 1 : toujours la Plaine ;
        // - après chaque paire de maps de combat : un Centre Pokémon puis une Boutique collée derrière ;
        // - maps de combat : alternance embranchement (2 choix) / map unique, la première paire (Plaine
        //   puis map unique) mise à part ;
        // - une map de combat n'est jamais du même environnement que la map de combat précédente.
        private static void AppendStep(Domain.Models.PlayerMongo.Path path)
        {
            int x = LastStep(path) + 1;
            if (x == 1)
            {
                path.PathPoints.Add(new PathPoint { X = 1, Y = 1, EnvironmentName = "Plaine" });
                return;
            }

            var steps = path.PathPoints.GroupBy(p => p.X).OrderBy(g => g.Key).ToList();
            var last = steps[^1];

            if (last.Any(p => p.EnvironmentName == CenterEnvironment))
            {
                path.PathPoints.Add(new PathPoint { X = x, Y = 1, EnvironmentName = ShopEnvironment });
                return;
            }

            int fightsSinceService = steps.AsEnumerable().Reverse()
                .TakeWhile(step => step.All(p => IsFightEnvironment(p.EnvironmentName)))
                .Count();
            if (fightsSinceService >= 2)
            {
                path.PathPoints.Add(new PathPoint { X = x, Y = 1, EnvironmentName = CenterEnvironment });
                return;
            }

            var lastFight = steps.LastOrDefault(step => step.All(p => IsFightEnvironment(p.EnvironmentName)));
            bool hasChoice = lastFight != null && lastFight.Count() == 1 && lastFight.Key != 1;
            List<string> available = FightEnvironments
                .Where(env => lastFight == null || lastFight.All(p => p.EnvironmentName != env))
                .ToList();

            int options = hasChoice ? 2 : 1;
            for (int y = 1; y <= options; y++)
            {
                string env = available[GameRandom.Next(RandomPurpose.Generation, available.Count)];
                available.Remove(env);
                path.PathPoints.Add(new PathPoint { X = x, Y = y, EnvironmentName = env });
            }
        }
    }
}
