using PkmnRaceBattle.Domain.Models;
using PkmnRaceBattle.Domain.Models.BracketMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoBracketRepository
    {
        public Task<BracketMongo> GetByPlayerId(string id);
        public Task<BracketMongo> GetByRoomId(string gameCode);

        public Task<List<BracketMongo>> GetAsync();

        public Task CreateAsync(BracketMongo bracketMongo);

        public Task<BracketMongo> AddWinnerToNextRound(BracketMongo bracket, int round, string userId);
    }
}
