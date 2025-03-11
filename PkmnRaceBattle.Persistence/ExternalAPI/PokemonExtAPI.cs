using Newtonsoft.Json;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonJson;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using PkmnRaceBattle.Persistence.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.ExternalAPI
{
    public class PokemonExtAPI
    {
        private readonly IMongoPokemonRepository _dbService;
        public PokemonExtAPI(IMongoPokemonRepository dbService)
        {
            _dbService = dbService;
        }
        public async Task<bool> GetPokemonTest()
        {
            
            for(int i=1; i<=151; i++)
            {
                PokemonJson pokemonJson = new PokemonJson();
                HttpClient _client = new HttpClient();
                HttpResponseMessage response = await _client.GetAsync($"https://pokeapi.co/api/v2/pokemon/{i}");
                var responseContent = await response.Content.ReadAsStringAsync();

                pokemonJson = JsonConvert.DeserializeObject<PokemonJson>(responseContent);

                foreach (MovesJson move in pokemonJson.moves)
                {
                    HttpResponseMessage moveDetailsResponse = await _client.GetAsync(move.move.url);
                    move.move.moveDetails = JsonConvert.DeserializeObject<MoveDetailsJson>(await moveDetailsResponse.Content.ReadAsStringAsync());
                }

                HttpResponseMessage speciesDetailsResponse = await _client.GetAsync(pokemonJson.species.url);
                pokemonJson.species.PokemonSpecies = JsonConvert.DeserializeObject<PokemonSpeciesJson>(await speciesDetailsResponse.Content.ReadAsStringAsync());

                HttpResponseMessage evolutionDetailsResponse = await _client.GetAsync(pokemonJson.species.PokemonSpecies.evolution_chain.url);
                pokemonJson.species.PokemonSpecies.evolution_chain.evolutions = JsonConvert.DeserializeObject<Evolutions>(await evolutionDetailsResponse.Content.ReadAsStringAsync());

                string json = JsonConvert.SerializeObject(pokemonJson);

                PokemonMongo pokemon = new PokemonMongo(pokemonJson);

                if(pokemon.Id == 19)
                {
                    Console.WriteLine("dsdsqd");
                }

                await _dbService.CreateAsync(pokemon);
            }
            

            return true;
        }

        public async Task<bool> GetGoldy()
        {

            PokemonMongo goldy = await _dbService.GetPokemonMongoById(150);

            List<MovesJson> moves = new List<MovesJson>();

            for(int i = 1; i <= 165; i++)
            {
                MovesJson movesJson = new MovesJson();
                MoveJson move = new MoveJson();
                HttpClient _client = new HttpClient();

                HttpResponseMessage moveDetailsResponse = await _client.GetAsync("https://pokeapi.co/api/v2/move/"+i);
                move.name = "osef";
                move.moveDetails = JsonConvert.DeserializeObject<MoveDetailsJson>(await moveDetailsResponse.Content.ReadAsStringAsync());
                movesJson.move = move;
                moves.Add(movesJson);
            }
            MovesJson[] movesArray = moves.ToArray();
            goldy._id = null;
            goldy.NameFr = "Goldy";
            goldy.Sprites.BackDefault = "/assets/goldy.png";
            goldy.Moves = movesArray
                 .Select(m => new MoveMongo(m))
                 .ToArray();

            Console.WriteLine(goldy);
            await _dbService.CreateAsync(goldy);

            return true;
        }

        public async Task<MoveMongo> GetMetronomeMove()
        {
            MovesJson movesJson = new MovesJson();
            MoveJson move = new MoveJson();
            HttpClient _client = new HttpClient();

            int[] bannedMoves = [68, 102, 118, 119, 144, 165];



            Random rand = new Random();
            int rnd = rand.Next(1,165);
            while (bannedMoves.Contains(rnd))
            {
                rnd = rand.Next(1,165);
            }

            HttpResponseMessage moveDetailsResponse = await _client.GetAsync("https://pokeapi.co/api/v2/move/" + rnd);
            move.name = "osef";
            move.moveDetails = JsonConvert.DeserializeObject<MoveDetailsJson>(await moveDetailsResponse.Content.ReadAsStringAsync());
            movesJson.move = move;

            MoveMongo moveMongo = new MoveMongo(movesJson);

            return moveMongo;
        }
    }
}
