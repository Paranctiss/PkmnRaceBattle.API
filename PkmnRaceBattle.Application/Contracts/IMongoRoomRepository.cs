using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoRoomRepository
    {
        public Task<RoomMongo> GetByRoomIdAsync(string gameRoom);
        public Task CreateAsync(RoomMongo room);

        public Task UpdateAsync(string roomCode, RoomMongo updatedRoom);
    }
}
