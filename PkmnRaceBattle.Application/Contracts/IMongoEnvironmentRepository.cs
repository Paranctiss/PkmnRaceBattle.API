using PkmnRaceBattle.Domain.Models.BracketMongo;
using PkmnRaceBattle.Domain.Models.EnvironmentMongo;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Application.Contracts
{
    public interface IMongoEnvironmentRepository
    {
        public Task CreateAsync(EnvironmentMongo environmentMongo);

        public Task<EnvironmentMongo> GetByEnvironmentName(string environmentName);
    }
}
