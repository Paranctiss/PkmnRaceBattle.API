namespace PkmnRaceBattle.API.Helper
{
    public static class UserConnectionManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> userConnections = new Dictionary<string, Dictionary<string, string>>();

        private static readonly Dictionary<string, (string roomId, string userId)> connectionDetails = new Dictionary<string, (string, string)>();

        public static void AddUserToRoom(string userId, string roomId, string connectionId)
        {

            if (connectionDetails.ContainsKey(connectionId))
            {
                var (oldRoomId, oldUserId) = connectionDetails[connectionId];

                if (oldRoomId != roomId)
                {
                    RemoveUserFromRoom(oldUserId, oldRoomId);
                }
            }


            if (!userConnections.ContainsKey(roomId))
            {
                userConnections[roomId] = new Dictionary<string, string>();
            }
            userConnections[roomId][userId] = connectionId;


            connectionDetails[connectionId] = (roomId, userId);
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
                string connectionId = userConnections[roomId][userId];
                userConnections[roomId].Remove(userId);


                if (connectionDetails.ContainsKey(connectionId))
                {
                    connectionDetails.Remove(connectionId);
                }

                if (userConnections[roomId].Count == 0)
                {
                    userConnections.Remove(roomId);
                }
            }
        }

        public static (string roomId, string userId) GetUserRoomByConnectionId(string connectionId)
        {
            if (connectionDetails.TryGetValue(connectionId, out var details))
            {
                return details;
            }
            return (null, null);
        }
    }

}
