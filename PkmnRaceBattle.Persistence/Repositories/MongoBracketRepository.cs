using MongoDB.Driver;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models;
using PkmnRaceBattle.Domain.Models.BracketMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Repositories
{
    public class MongoBracketRepository : IMongoBracketRepository
    {
        IMongoCollection<BracketMongo> _bracketCollection;

        public MongoBracketRepository(IMongoDatabase database, string collectionName)
        {
            _bracketCollection = database.GetCollection<BracketMongo>(collectionName);
        }

        public async Task CreateAsync(BracketMongo bracketMongo) =>
            await _bracketCollection.InsertOneAsync(bracketMongo);

        public async Task<List<BracketMongo>> GetAsync() =>
            await _bracketCollection.Find(_ => true).ToListAsync();

        public async Task<BracketMongo> GetByPlayerId(string id)
        {
            return await _bracketCollection.Find(x => x.Players.Any(s => s._id == id)).FirstOrDefaultAsync();
        }

        public async Task<BracketMongo> GetByRoomId(string gameCode)
        {
            return await _bracketCollection.Find(x => x.GameCode == gameCode).FirstOrDefaultAsync();
        }

        public async Task<BracketMongo> AddWinnerToNextRound(BracketMongo bracket, int round, string userId)
        {
            bracket.Rounds[round-1].PlayersInRace.Add(userId);
            bracket.Rounds[round-1].PlayersInRace.Remove("?");
            await _bracketCollection.ReplaceOneAsync(x => x._id == bracket._id, bracket);
            return bracket;
        }
    }
}
