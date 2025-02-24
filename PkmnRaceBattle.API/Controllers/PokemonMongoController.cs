using Microsoft.AspNetCore.Mvc;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Persistence.Services;

namespace PkmnRaceBattle.API.Controllers
{

    [ApiController]
    [Route("Pokemon")]
    public class PokemonMongoController : ControllerBase
    {

        private readonly IMongoPokemonRepository _pokemonRepository;
        public PokemonMongoController(IMongoPokemonRepository pokemonRepository) 
        {
            _pokemonRepository = pokemonRepository;
        }

        [Route("{id}"), HttpGet]
        public async Task<PokemonMongo> GetById(int id)
        {
            return await _pokemonRepository.GetPokemonMongoById(id);
        }
    }
}
