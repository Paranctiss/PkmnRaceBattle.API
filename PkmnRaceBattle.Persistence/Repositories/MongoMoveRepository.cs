using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoMoveRepository : IMongoMoveRepository
    {

        IMongoCollection<MoveMongo> _moveCollection;
        public MongoMoveRepository(IMongoDatabase database, string collectionName)
        {
            _moveCollection = database.GetCollection<MoveMongo>(collectionName);
        }

        public async Task CreateAsync(MoveMongo newMove) =>
            await _moveCollection.InsertOneAsync(newMove);


        public async Task<List<MoveMongo>> GetAsync() =>
            await _moveCollection.Find(_ => true).ToListAsync();

        public async Task<MoveMongo> GetMoveMongoById(int id)
        {
            return await _moveCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<MoveMongo> GetMoveMongoByName(string name)
        {
            return await _moveCollection.Find(x => x.NameFr == name).FirstOrDefaultAsync();
        }
    }
}
