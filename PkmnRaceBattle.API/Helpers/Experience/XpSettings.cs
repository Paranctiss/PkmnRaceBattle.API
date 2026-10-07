using PkmnRaceBattle.Domain.Models.RoomMongo;

namespace PkmnRaceBattle.API.Helpers.Experience
{
    // Réglages d'XP de la partie, choisis par l'hôte avant le lancement (RoomMongo)
    public record XpSettings(bool MultiXp, int Multiplier)
    {
        public static readonly int[] AllowedMultipliers = [1, 2, 5];

        // Multi Exp activé, XP normale
        public static readonly XpSettings Default = new(true, 1);

        // Valeur inconnue (ancienne salle, appel trafiqué) : XP normale
        public static int NormalizeMultiplier(int multiplier) =>
            AllowedMultipliers.Contains(multiplier) ? multiplier : 1;

        public static XpSettings FromRoom(RoomMongo? room) =>
            room == null ? Default : new XpSettings(room.MultiXp, NormalizeMultiplier(room.XpMultiplier));
    }
}
