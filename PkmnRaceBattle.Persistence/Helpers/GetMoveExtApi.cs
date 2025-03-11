using Newtonsoft.Json;
using PkmnRaceBattle.Domain.Models.PokemonJson;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PkmnRaceBattle.Persistence.Helpers
{
    public static class GetMoveExtApi
    {
        public static async Task<MoveMongo> GetMetronomeMove()
        {
            MovesJson movesJson = new MovesJson();
            MoveJson move = new MoveJson();
            HttpClient _client = new HttpClient();

            int[] bannedMoves = [68, 102, 118, 119, 144, 165];



            Random rand = new Random();
            int rnd = rand.Next(1, 165);
            while (bannedMoves.Contains(rnd))
            {
                rnd = rand.Next(1, 165);
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
