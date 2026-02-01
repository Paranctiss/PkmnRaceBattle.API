using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.PathManager
{
    public static class PlayerPathHelper
    {
        public static Domain.Models.PlayerMongo.Path GenerateNewPath()
        {
            Domain.Models.PlayerMongo.Path path = new Domain.Models.PlayerMongo.Path();
            path.PathPoints = new List<PathPoint>();

            Random random = new Random();
            string[] possibleEnvironments = ["Plaine", "Volcan", "Forêt", "Grotte", "Centrale", "Eau"];
            string[] possibleOther = ["Shop", "Center"];

            bool hasChoice = false; // Alterne : choix / pas choix
            int environmentCounter = 0; // Compte uniquement les environnements (pas Shop/Center)

            for (int step = 1; step <= 11; step++)
            {
                // Step 1 : toujours Plaine en (1,1), pas de choix
                if (step == 1)
                {
                    path.PathPoints.Add(new PathPoint
                    {
                        X = step,
                        Y = 1,
                        EnvironmentName = "Plaine"
                    });

                    hasChoice = false;
                    environmentCounter = 1;
                }
                // Tous les 2 environnements : Shop ou Center
                else if (environmentCounter > 0 && environmentCounter % 2 == 0)
                {
                    path.PathPoints.Add(new PathPoint
                    {
                        X = step,
                        Y = 1,
                        EnvironmentName = possibleOther[random.Next(possibleOther.Length)]
                    });
                    hasChoice = false;
                    environmentCounter = 0; // Reset le compteur après Shop/Center
                }
                // Alternance choix / pas choix pour les environnements
                else
                {
                    if (hasChoice)
                    {
                        // Le joueur a 2 choix : créer (X,1) ET (X,2)
                        string env1 = possibleEnvironments[random.Next(possibleEnvironments.Length)];
                        string env2;

                        do
                        {
                            env2 = possibleEnvironments[random.Next(possibleEnvironments.Length)];
                        } while (env2 == env1);

                        path.PathPoints.Add(new PathPoint
                        {
                            X = step,
                            Y = 1,
                            EnvironmentName = env1
                        });

                        path.PathPoints.Add(new PathPoint
                        {
                            X = step,
                            Y = 2,
                            EnvironmentName = env2
                        });

                        hasChoice = false;
                        environmentCounter++;
                    }
                    else
                    {
                        // Pas de choix : créer seulement (X,1)
                        path.PathPoints.Add(new PathPoint
                        {
                            X = step,
                            Y = 1,
                            EnvironmentName = possibleEnvironments[random.Next(possibleEnvironments.Length)]
                        });

                        hasChoice = true;
                        environmentCounter++;
                    }
                }
            }

            return path;
        }
    }
}
