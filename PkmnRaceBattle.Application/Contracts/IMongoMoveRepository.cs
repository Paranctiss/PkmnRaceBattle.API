using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoMoveRepository
    {
        public Task<MoveMongo> GetMoveMongoById(int id);
        public Task<MoveMongo> GetMoveMongoByName(string name);

        public Task<List<MoveMongo>> GetAsync();

        public Task CreateAsync(MoveMongo moveMongo);

    }
}
