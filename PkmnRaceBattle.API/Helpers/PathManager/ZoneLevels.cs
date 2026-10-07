using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.PathManager
{
    // Niveaux d'une zone de combat : sauvages entre WildMin et WildMax, dresseur entre TrainerMin et TrainerMax
    public record ZoneLevelRange(int WildMin, int WildMax, int TrainerMin, int TrainerMax);

    // Paliers de niveaux des zones de combat. Seules les maps de combat comptent (Shop / Centre n'avancent pas
    // le palier). Le chemin est sans fin : au-delà de la table, les niveaux sont prolongés (GetRange).
    // Les paliers suivent la progression d'XP attendue, qui dépend du Multi Exp et du multiplicateur d'XP :
    // table générée par tools/zone-levels.js (à relancer si la formule d'XP ou les données changent).
    public static class ZoneLevels
    {
        private static readonly Dictionary<(bool MultiXp, int Multiplier), ZoneLevelRange[]> Table = new()
        {
            [(true, 1)] =
            [
                new(2, 4, 4, 6),
                new(4, 7, 8, 10),
                new(8, 11, 11, 13),
                new(11, 14, 14, 16),
                new(14, 17, 17, 19),
                new(17, 19, 20, 22),
                new(20, 22, 22, 24),
                new(22, 25, 25, 27),
                new(25, 27, 27, 29),
                new(27, 29, 29, 31),
                new(29, 31, 31, 33),
                new(30, 33, 33, 35),
                new(32, 35, 35, 37),
                new(34, 36, 36, 38),
                new(36, 38, 38, 40),
                new(37, 39, 39, 41),
                new(39, 41, 41, 43),
                new(40, 42, 42, 44),
                new(41, 44, 44, 46),
                new(43, 45, 45, 47),
                new(44, 46, 46, 48),
                new(45, 48, 48, 50),
                new(47, 49, 49, 51),
                new(48, 50, 50, 52),
            ],
            [(true, 2)] =
            [
                new(2, 5, 6, 8),
                new(6, 10, 11, 13),
                new(11, 15, 15, 17),
                new(16, 19, 19, 21),
                new(20, 23, 23, 25),
                new(24, 27, 27, 29),
                new(28, 31, 31, 33),
                new(32, 34, 35, 37),
                new(35, 38, 38, 40),
                new(38, 41, 41, 43),
                new(41, 44, 44, 46),
                new(44, 46, 46, 48),
                new(46, 49, 49, 51),
                new(48, 51, 51, 53),
                new(51, 53, 53, 55),
                new(53, 55, 55, 57),
                new(55, 57, 58, 60),
                new(57, 59, 60, 62),
                new(59, 61, 61, 63),
                new(61, 63, 63, 65),
                new(63, 65, 65, 67),
                new(64, 67, 67, 69),
                new(66, 68, 69, 71),
                new(68, 70, 70, 72),
            ],
            [(true, 5)] =
            [
                new(2, 9, 10, 12),
                new(11, 16, 17, 19),
                new(19, 23, 24, 26),
                new(26, 30, 30, 32),
                new(33, 36, 37, 39),
                new(39, 43, 43, 45),
                new(46, 49, 49, 51),
                new(51, 55, 55, 57),
                new(56, 60, 60, 62),
                new(61, 64, 64, 66),
                new(66, 69, 69, 71),
                new(70, 73, 73, 75),
                new(74, 76, 77, 79),
                new(77, 80, 80, 82),
                new(81, 84, 84, 86),
                new(84, 87, 87, 89),
                new(87, 90, 90, 92),
                new(91, 93, 93, 95),
                new(94, 96, 96, 98),
                new(96, 98, 98, 100),
                new(96, 98, 98, 100),
                new(96, 98, 98, 100),
                new(96, 98, 98, 100),
                new(96, 98, 98, 100),
            ],
            [(false, 1)] =
            [
                new(2, 4, 4, 6),
                new(3, 6, 7, 9),
                new(7, 9, 10, 12),
                new(10, 12, 12, 14),
                new(12, 15, 15, 17),
                new(15, 17, 17, 19),
                new(17, 20, 20, 22),
                new(20, 22, 22, 24),
                new(22, 24, 24, 26),
                new(24, 26, 26, 28),
                new(25, 28, 28, 30),
                new(27, 29, 29, 31),
                new(29, 31, 31, 33),
                new(30, 32, 32, 34),
                new(32, 34, 34, 36),
                new(33, 35, 35, 37),
                new(34, 37, 37, 39),
                new(36, 38, 38, 40),
                new(37, 39, 39, 41),
                new(38, 40, 40, 42),
                new(39, 41, 41, 43),
                new(40, 43, 43, 45),
                new(41, 44, 44, 46),
                new(42, 45, 45, 47),
            ],
            [(false, 2)] =
            [
                new(2, 5, 5, 7),
                new(5, 9, 10, 12),
                new(10, 13, 14, 16),
                new(14, 17, 17, 19),
                new(18, 21, 21, 23),
                new(22, 24, 24, 26),
                new(25, 28, 28, 30),
                new(28, 31, 31, 33),
                new(31, 34, 34, 36),
                new(34, 36, 37, 39),
                new(36, 39, 39, 41),
                new(39, 41, 41, 43),
                new(41, 44, 44, 46),
                new(43, 46, 46, 48),
                new(45, 48, 48, 50),
                new(47, 50, 50, 52),
                new(49, 51, 52, 54),
                new(51, 53, 53, 55),
                new(53, 55, 55, 57),
                new(54, 57, 57, 59),
                new(56, 58, 58, 60),
                new(58, 60, 60, 62),
                new(59, 61, 62, 64),
                new(61, 63, 63, 65),
            ],
            [(false, 5)] =
            [
                new(2, 8, 9, 11),
                new(10, 15, 15, 17),
                new(17, 21, 21, 23),
                new(23, 27, 27, 29),
                new(29, 32, 33, 35),
                new(35, 38, 39, 41),
                new(41, 44, 44, 46),
                new(46, 49, 49, 51),
                new(50, 53, 54, 56),
                new(55, 58, 58, 60),
                new(59, 61, 62, 64),
                new(62, 65, 65, 67),
                new(66, 69, 69, 71),
                new(69, 72, 72, 74),
                new(72, 75, 75, 77),
                new(75, 78, 78, 80),
                new(78, 81, 81, 83),
                new(81, 84, 84, 86),
                new(84, 86, 86, 88),
                new(86, 89, 89, 91),
                new(89, 91, 92, 94),
                new(91, 94, 94, 96),
                new(94, 96, 96, 98),
                new(96, 98, 98, 100),
            ],
        };

        public const int MaxTrainerPokemon = 6;

        // Le dresseur de la zone N a N Pokémon (6 au plus)
        public static int TrainerTeamSize(int zone) => Math.Clamp(zone, 1, MaxTrainerPokemon);

        public static ZoneLevelRange GetRange(int zone, XpSettings settings)
        {
            ZoneLevelRange[] zones = Table[(settings.MultiXp, XpSettings.NormalizeMultiplier(settings.Multiplier))];
            int index = Math.Max(1, zone) - 1;
            if (index < zones.Length) return zones[index];

            // Au-delà de la table : on prolonge au rythme de la dernière zone
            ZoneLevelRange last = zones[^1];
            int step = Math.Max(1, last.WildMin - zones[^2].WildMin) * (index - zones.Length + 1);
            return new ZoneLevelRange(
                Math.Min(100, last.WildMin + step), Math.Min(100, last.WildMax + step),
                Math.Min(100, last.TrainerMin + step), Math.Min(100, last.TrainerMax + step));
        }

        // Numéro (à partir de 1) de la zone de combat où se trouve le joueur
        public static int GetZone(PlayerMongo player)
        {
            int currentX = player.CurrentPath?.X ?? 0;
            int fightZones = player.PlayerPath?.PathPoints?
                .Where(p => p.X <= currentX && PlayerPathHelper.IsFightEnvironment(p.EnvironmentName))
                .Select(p => p.X)
                .Distinct()
                .Count() ?? 0;
            return Math.Max(1, fightZones) + player.PathLoopCount;
        }

        // Le niveau des sauvages monte au fil des combats de la zone : du bas de la fourchette au 1er combat
        // jusqu'au haut au dernier, à ± 1 niveau près
        public static int WildLevel(ZoneLevelRange range, int fightIndex)
        {
            int lastFight = PlayerPathHelper.WildFightsPerMap - 1;
            double progress = Math.Clamp(fightIndex, 0, lastFight) / (double)lastFight;
            int center = range.WildMin + (int)Math.Round((range.WildMax - range.WildMin) * progress);
            int min = Math.Max(range.WildMin, center - 1);
            int max = Math.Min(range.WildMax, center + 1);
            return GameRandom.Next(RandomPurpose.Generation, min, max + 1);
        }

        // Fourchette affichée sur la carte pour chaque map de combat (des sauvages au dresseur)
        public static void AnnotatePath(PlayerMongo player, XpSettings settings)
        {
            if (player.PlayerPath?.PathPoints == null) return;

            // Chemin généré au fur et à mesure (sans fin) : un seul parcours, étape par étape
            int zone = 0;
            foreach (var step in player.PlayerPath.PathPoints.GroupBy(p => p.X).OrderBy(g => g.Key))
            {
                if (!step.Any(p => PlayerPathHelper.IsFightEnvironment(p.EnvironmentName))) continue;
                zone++;
                ZoneLevelRange range = GetRange(zone, settings);
                foreach (PathPoint point in step.Where(p => PlayerPathHelper.IsFightEnvironment(p.EnvironmentName)))
                    SetLevels(point, range);
            }

            if (player.CurrentPath != null && PlayerPathHelper.IsFightEnvironment(player.CurrentPath.EnvironmentName))
                SetLevels(player.CurrentPath, GetRange(GetZone(player), settings));
        }

        public static void SetLevels(PathPoint point, ZoneLevelRange range)
        {
            point.MinLevel = range.WildMin;
            point.MaxLevel = range.TrainerMax;
        }
    }
}
