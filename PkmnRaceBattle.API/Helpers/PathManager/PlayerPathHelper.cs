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
        private static readonly string[] ServiceEnvironments = [ShopEnvironment, CenterEnvironment];

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

        // Déplace le joueur sur la map donnée et grise les autres maps de la même étape
        public static void MoveTo(PlayerMongo player, PathPoint destination)
        {
            foreach (PathPoint point in player.PlayerPath.PathPoints.Where(p => p.X == destination.X))
            {
                point.IsSkipped = point.Y != destination.Y;
            }

            player.CurrentPath = destination;
            player.MapFightCount = 0;
        }

        public static Domain.Models.PlayerMongo.Path GenerateNewPath()
        {
            Domain.Models.PlayerMongo.Path path = new Domain.Models.PlayerMongo.Path();
            path.PathPoints = new List<PathPoint>();


            bool hasChoice = false; // Alterne : choix / pas choix
            int environmentCounter = 0; // Compte uniquement les environnements (pas Shop/Centre)

            for (int step = 1; step <= 11; step++)
            {
                // Step 1 : toujours Plaine en (1,1), pas de choix
                if (step == 1)
                {
                    path.PathPoints.Add(new PathPoint { X = step, Y = 1, EnvironmentName = "Plaine" });

                    hasChoice = false;
                    environmentCounter = 1;
                }
                // Tous les 2 environnements : Shop ou Centre
                else if (environmentCounter > 0 && environmentCounter % 2 == 0)
                {
                    path.PathPoints.Add(new PathPoint
                    {
                        X = step,
                        Y = 1,
                        EnvironmentName = ServiceEnvironments[GameRandom.Next(RandomPurpose.Generation, ServiceEnvironments.Length)]
                    });
                    hasChoice = false;
                    environmentCounter = 0; // Reset le compteur après Shop/Centre
                }
                // Alternance choix / pas choix pour les environnements
                else
                {
                    if (hasChoice)
                    {
                        // Le joueur a 2 choix : créer (X,1) ET (X,2)
                        string env1 = FightEnvironments[GameRandom.Next(RandomPurpose.Generation, FightEnvironments.Length)];
                        string env2;

                        do
                        {
                            env2 = FightEnvironments[GameRandom.Next(RandomPurpose.Generation, FightEnvironments.Length)];
                        } while (env2 == env1);

                        path.PathPoints.Add(new PathPoint { X = step, Y = 1, EnvironmentName = env1 });
                        path.PathPoints.Add(new PathPoint { X = step, Y = 2, EnvironmentName = env2 });

                        hasChoice = false;
                    }
                    else
                    {
                        // Pas de choix : créer seulement (X,1)
                        path.PathPoints.Add(new PathPoint
                        {
                            X = step,
                            Y = 1,
                            EnvironmentName = FightEnvironments[GameRandom.Next(RandomPurpose.Generation, FightEnvironments.Length)]
                        });

                        hasChoice = true;
                    }
                    environmentCounter++;
                }
            }

            return path;
        }
    }
}
