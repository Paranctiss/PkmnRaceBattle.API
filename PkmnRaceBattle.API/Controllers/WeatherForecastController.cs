using Microsoft.AspNetCore.Mvc;
using PkmnRaceBattle.Persistence.ExternalAPI;
using PkmnRaceBattle.Persistence.Models;

namespace PkmnRaceBattle.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly PokemonExtAPI _api;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, PokemonExtAPI api)
        {
            _logger = logger;
            _api = api;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task<bool> Get()
        {
            return await _api.GetPokemonTest();
            //return await _api.InsertAllMoves();
           //return await _api.GetGoldy();
        }
    }
}
