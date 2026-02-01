using Newtonsoft.Json;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.EnvironmentMongo;
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
        private readonly IMongoMoveRepository _moveDbService;
        private readonly IMongoEnvironmentRepository _environmentDbService;
        public PokemonExtAPI(IMongoPokemonRepository dbService, IMongoMoveRepository moveDbService, IMongoEnvironmentRepository environmentDbService)
        {
            _dbService = dbService;
            _moveDbService = moveDbService;
            _environmentDbService = environmentDbService;
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

                await _dbService.CreateAsync(pokemon);
            }
            

            return true;
        }

        public async Task<bool> InsertAllMoves()
        {
            for (int i = 1; i<= 165; i++) 
            { 
                MovesJson moveJson = new MovesJson();
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync($"https://pokeapi.co/api/v2/move/{i}");
                moveJson.move = new MoveJson();
                moveJson.move.moveDetails = JsonConvert.DeserializeObject<MoveDetailsJson>(await response.Content.ReadAsStringAsync());

                string json = JsonConvert.SerializeObject(moveJson);

                MoveMongo move = new MoveMongo(moveJson);

                await _moveDbService.CreateAsync(move);
            }

            return true;
        
        }

        public async Task<bool> InsertEnvironments()
        {
            List<EnvironmentMongo> environments = new List<EnvironmentMongo>();
            environments.Add(GetPlaine());
            environments.Add(GetForet());
            environments.Add(GetGrotte());
            environments.Add(GetVolcan());
            environments.Add(GetCentrale());
            environments.Add(GetEau());

            foreach (var environment in environments) 
            {
               await _environmentDbService.CreateAsync(environment);
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

        public EnvironmentMongo GetPlaine()
        {
            EnvironmentMongo environmentPlaine = new EnvironmentMongo()
            {
                Name = "Plaine",
            };

            List<PokemonSpawn> plaineSpawns = new List<PokemonSpawn>();

            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 1, MinimumLevel = 1, Rareté = "Rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 2, MinimumLevel = 16, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 3, MinimumLevel = 32, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 16, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 17, MinimumLevel = 18, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 18, MinimumLevel = 36, Rareté = "Rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 19, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 20, MinimumLevel = 20, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 21, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 22, MinimumLevel = 20, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 23, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 24, MinimumLevel = 22, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 25, MinimumLevel = 1, Rareté = "Rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 26, MinimumLevel = 20, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 29, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 30, MinimumLevel = 16, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 31, MinimumLevel = 50, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 32, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 33, MinimumLevel = 16, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 34, MinimumLevel = 50, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 43, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 44, MinimumLevel = 21, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 45, MinimumLevel = 45, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 52, MinimumLevel = 28, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 69, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 70, MinimumLevel = 21, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 71, MinimumLevel = 50, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 83, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 84, MinimumLevel = 1, Rareté = "Commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 85, MinimumLevel = 31, Rareté = "Rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 106, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 107, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 108, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 111, MinimumLevel = 1, Rareté = "Peu commun" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 112, MinimumLevel = 42, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 113, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 114, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 115, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 122, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 128, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 132, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 133, MinimumLevel = 1, Rareté = "Rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 143, MinimumLevel = 1, Rareté = "Très rare" });
            plaineSpawns.Add(new PokemonSpawn() { PokemonId = 151, MinimumLevel = 1, Rareté = "Légendaire" });

            environmentPlaine.PossiblePokemons = plaineSpawns;
            return environmentPlaine;
        }

        public EnvironmentMongo GetForet()
        {
            EnvironmentMongo environmentForet = new EnvironmentMongo()
            {
                Name = "Forêt",
            };

            List<PokemonSpawn> foretSpawns = new List<PokemonSpawn>();

            foretSpawns.Add(new PokemonSpawn() { PokemonId = 10, MinimumLevel = 1, Rareté = "Commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 11, MinimumLevel = 7, Rareté = "Peu commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 12, MinimumLevel = 10, Rareté = "Rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 13, MinimumLevel = 1, Rareté = "Commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 14, MinimumLevel = 10, Rareté = "Peu commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 15, MinimumLevel = 10, Rareté = "Rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 46, MinimumLevel = 1, Rareté = "Peu commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 47, MinimumLevel = 24, Rareté = "Rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 48, MinimumLevel = 1, Rareté = "Peu commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 49, MinimumLevel = 31, Rareté = "Rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 56, MinimumLevel = 1, Rareté = "Peu commun" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 57, MinimumLevel = 28, Rareté = "Rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 102, MinimumLevel = 1, Rareté = "Rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 103, MinimumLevel = 50, Rareté = "Très rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 123, MinimumLevel = 1, Rareté = "Très rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 124, MinimumLevel = 1, Rareté = "Très rare" });
            foretSpawns.Add(new PokemonSpawn() { PokemonId = 127, MinimumLevel = 1, Rareté = "Très rare" });

            environmentForet.PossiblePokemons = foretSpawns;
            return environmentForet;
        }

        public EnvironmentMongo GetGrotte()
        {
            EnvironmentMongo environmentGrotte = new EnvironmentMongo()
            {
                Name = "Grotte",
            };

            List<PokemonSpawn> grotteSpawns = new List<PokemonSpawn>();

            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 27, MinimumLevel = 1, Rareté = "Peu commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 28, MinimumLevel = 22, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 35, MinimumLevel = 1, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 36, MinimumLevel = 30, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 41, MinimumLevel = 1, Rareté = "Commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 42, MinimumLevel = 22, Rareté = "Peu commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 50, MinimumLevel = 1, Rareté = "Commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 51, MinimumLevel = 26, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 63, MinimumLevel = 1, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 64, MinimumLevel = 16, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 65, MinimumLevel = 40, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 66, MinimumLevel = 1, Rareté = "Commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 67, MinimumLevel = 28, Rareté = "Peu commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 68, MinimumLevel = 40, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 74, MinimumLevel = 1, Rareté = "Commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 75, MinimumLevel = 25, Rareté = "Peu commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 76, MinimumLevel = 40, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 88, MinimumLevel = 1, Rareté = "Peu commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 89, MinimumLevel = 38, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 92, MinimumLevel = 1, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 93, MinimumLevel = 25, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 94, MinimumLevel = 40, Rareté = "Très rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 95, MinimumLevel = 1, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 96, MinimumLevel = 1, Rareté = "Peu commun" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 97, MinimumLevel = 26, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 104, MinimumLevel = 1, Rareté = "Rare" });
            grotteSpawns.Add(new PokemonSpawn() { PokemonId = 105, MinimumLevel = 28, Rareté = "Très rare" });

            environmentGrotte.PossiblePokemons = grotteSpawns;
            return environmentGrotte;
        }

        public EnvironmentMongo GetVolcan()
        {
            EnvironmentMongo environmentVolcan = new EnvironmentMongo()
            {
                Name = "Volcan",
            };

            List<PokemonSpawn> volcanSpawns = new List<PokemonSpawn>();

            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 4, MinimumLevel = 1, Rareté = "Rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 5, MinimumLevel = 16, Rareté = "Très rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 6, MinimumLevel = 36, Rareté = "Très rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 37, MinimumLevel = 1, Rareté = "Peu commun" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 38, MinimumLevel = 40, Rareté = "Très rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 58, MinimumLevel = 1, Rareté = "Rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 59, MinimumLevel = 40, Rareté = "Très rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 77, MinimumLevel = 1, Rareté = "Peu commun" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 78, MinimumLevel = 78, Rareté = "Rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 126, MinimumLevel = 1, Rareté = "Très rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 136, MinimumLevel = 30, Rareté = "Très rare" });
            volcanSpawns.Add(new PokemonSpawn() { PokemonId = 146, MinimumLevel = 1, Rareté = "Légendaire" });

            environmentVolcan.PossiblePokemons = volcanSpawns;
            return environmentVolcan;
        }

        public EnvironmentMongo GetCentrale()
        {
            EnvironmentMongo environmentCentrale = new EnvironmentMongo()
            {
                Name = "Centrale",
            };

            List<PokemonSpawn> centraleSpawns = new List<PokemonSpawn>();

            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 25, MinimumLevel = 1, Rareté = "Rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 26, MinimumLevel = 30, Rareté = "Très rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 81, MinimumLevel = 1, Rareté = "Peu commun" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 82, MinimumLevel = 30, Rareté = "Rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 100, MinimumLevel = 1, Rareté = "Peu commun" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 101, MinimumLevel = 30, Rareté = "Rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 125, MinimumLevel = 1, Rareté = "Très rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 135, MinimumLevel = 30, Rareté = "Très rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 137, MinimumLevel = 1, Rareté = "Très rare" });
            centraleSpawns.Add(new PokemonSpawn() { PokemonId = 145, MinimumLevel = 1, Rareté = "Légendaire" });

            environmentCentrale.PossiblePokemons = centraleSpawns;
            return environmentCentrale;
        }

        public EnvironmentMongo GetEau()
        {
            EnvironmentMongo environmentEau = new EnvironmentMongo()
            {
                Name = "Eau",
            };

            List<PokemonSpawn> eauSpawns = new List<PokemonSpawn>();

            eauSpawns.Add(new PokemonSpawn() { PokemonId = 7, MinimumLevel = 1, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 8, MinimumLevel = 16, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 9, MinimumLevel = 36, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 54, MinimumLevel = 1, Rareté = "Peu commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 55, MinimumLevel = 33, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 60, MinimumLevel = 1, Rareté = "Commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 61, MinimumLevel = 25, Rareté = "Peu commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 62, MinimumLevel = 40, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 72, MinimumLevel = 1, Rareté = "Commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 73, MinimumLevel = 30, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 79, MinimumLevel = 1, Rareté = "Peu commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 80, MinimumLevel = 37, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 86, MinimumLevel = 1, Rareté = "Peu commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 87, MinimumLevel = 34, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 90, MinimumLevel = 1, Rareté = "Peu commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 91, MinimumLevel = 40, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 98, MinimumLevel = 1, Rareté = "Commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 99, MinimumLevel = 28, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 116, MinimumLevel = 1, Rareté = "Commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 117, MinimumLevel = 32, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 118, MinimumLevel = 1, Rareté = "Commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 119, MinimumLevel = 33, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 120, MinimumLevel = 1, Rareté = "Peu commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 121, MinimumLevel = 30, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 129, MinimumLevel = 1, Rareté = "Commun" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 130, MinimumLevel = 20, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 131, MinimumLevel = 1, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 134, MinimumLevel = 30, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 138, MinimumLevel = 1, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 139, MinimumLevel = 40, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 140, MinimumLevel = 1, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 141, MinimumLevel = 40, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 142, MinimumLevel = 1, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 144, MinimumLevel = 1, Rareté = "Légendaire" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 147, MinimumLevel = 1, Rareté = "Rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 148, MinimumLevel = 30, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 149, MinimumLevel = 55, Rareté = "Très rare" });
            eauSpawns.Add(new PokemonSpawn() { PokemonId = 150, MinimumLevel = 1, Rareté = "Légendaire" });

            environmentEau.PossiblePokemons = eauSpawns;
            return environmentEau;
        }

    }
}
