using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoRoomRepository : IMongoRoomRepository
    {
        private readonly IMongoCollection<RoomMongo> _collection;
        public MongoRoomRepository(IMongoDatabase database, string collectionName)
        {
            _collection = database.GetCollection<RoomMongo>(collectionName);
        }
        public async Task<RoomMongo> GetByRoomIdAsync(string gameRoom)
        {
            return await _collection.Find(x => x.roomId == gameRoom).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(RoomMongo room)
        {
            await _collection.InsertOneAsync(room);
        }
        public async Task UpdateAsync(string roomCode, RoomMongo updatedRoom) =>
    await _collection.ReplaceOneAsync(x => x.roomId == roomCode, updatedRoom);
    }
}
