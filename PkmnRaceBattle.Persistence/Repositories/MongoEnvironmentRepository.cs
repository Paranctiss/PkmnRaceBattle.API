using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.BracketMongo;
using PkmnRaceBattle.Domain.Models.EnvironmentMongo;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoEnvironmentRepository : IMongoEnvironmentRepository
    {
        IMongoCollection<EnvironmentMongo> _environmentCollection;

        public MongoEnvironmentRepository(IMongoDatabase database, string collectionName)
        {
            _environmentCollection = database.GetCollection<EnvironmentMongo>(collectionName);
        }

        public async Task CreateAsync(EnvironmentMongo environmentMongo) =>
            await _environmentCollection.InsertOneAsync(environmentMongo);

        public async Task<EnvironmentMongo> GetByEnvironmentName(string environmentName)
        {
            return await _environmentCollection.Find(x => x.Name == environmentName).FirstOrDefaultAsync();
        }
    }
}
