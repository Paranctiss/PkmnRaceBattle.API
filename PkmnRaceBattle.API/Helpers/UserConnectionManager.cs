namespace PkmnRaceBattle.API.Helper
{
    public static class UserConnectionManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> userConnections = new Dictionary<string, Dictionary<string, string>>();

        public static void AddUserToRoom(string userId, string roomId, string connectionId)
        {
            if (!userConnections.ContainsKey(roomId))
            {
                userConnections[roomId] = new Dictionary<string, string>();
            }
            userConnections[roomId][userId] = connectionId;
        }

        public static string GetConnectionId(string userId, string roomId)
        {
            if (userConnections.ContainsKey(roomId) && userConnections[roomId].ContainsKey(userId))
            {
                return userConnections[roomId][userId];
            }
            return null;
        }

        public static void RemoveUserFromRoom(string userId, string roomId)
        {
            if (userConnections.ContainsKey(roomId) && userConnections[roomId].ContainsKey(userId))
            {
                userConnections[roomId].Remove(userId);
                if (userConnections[roomId].Count == 0)
                {
                    userConnections.Remove(roomId);
                }
            }
        }
    }

}
