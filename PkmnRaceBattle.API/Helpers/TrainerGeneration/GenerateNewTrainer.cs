using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Numerics;

namespace PkmnRaceBattle.API.Helpers.TrainerGeneration
{
    public static class GenerateNewTrainer
    {

        public static async Task<PlayerMongo> GenerateNewTrainerTeam(int nbPokemon, int levelAvg, IMongoPokemonRepository _mongoPokemonRepository)
        {

            PlayerMongo trainer = new()
            {
                Team = []
            };

            List<PokemonTeam> teamList = [];

            for (int i = 0; i < nbPokemon; i++)
            {
                PokemonMongo rndPokemon = await _mongoPokemonRepository.GetRandom();
                //PokemonMongo rndPokemon = await _mongoPokemonRepository.GetPokemonMongoById(58);
                PokemonTeam generatedPokemon = GenerateNewPokemon.GenerateNewPokemonTeam(rndPokemon, levelAvg-5, levelAvg-2);
                teamList.Add(generatedPokemon);
            }
            trainer = GenerateTrainerInfos(trainer);

            trainer.Team = [.. teamList];
            return trainer;
        }

        private static PlayerMongo GenerateTrainerInfos(PlayerMongo trainer)
        {
            Random rnd = new Random();
            string[] names = ["Alex", "Bruno", "Clara", "Dylan", "Eva", "Fabien", "Gaëlle", "Hugo", "Inès", "Julien", "Katia", "Léo", "Morgane", "Nolan", "Océane", "Paul", "Quentin", "Romy", "Sacha", "Théo", "Ugo", "Valentine", "William", "Xavier", "Yasmine", "Zacharie", "Aurore", "Bastien", "Célia", "Damien", "Elise", "Florian", "Glod", "Gwendoline", "Henri", "Isabelle", "Joris", "Kelly", "Ludovic", "Manon", "Nathan", "Olivier", "Perrine", "Raphaël", "Soline", "Tanguy", "Ulysse", "Violette", "Wendy", "Xéna", "Zoé"];
            string[] sprites = [
      "aaron", "aarune", "acerola", "acetrainer", "acetrainer1", "acetrainer2", "acetrainer3", "acetrainerf", "acetrainerf1", "acetrainerf2", "acetrainerf3",
      "acetrainersnow", "acetrainersnowf", "adaman", "akari", "akari-isekai", "alder", "alec", "allister", "anthea", "aquagrunt", "aquagrunt1", "aquagruntf",
      "archie", "arezu", "ariana", "artistf", "arven", "ash", "ballguy", "barry", "bea", "beauty", "bede", "birch", "blue", "brendan", "brock", "buck", "burnet",
      "cynthia", "dawn", "delinquent", "elesa", "erika", "gardenia", "geeta", "ghetsis", "gladion", "gold", "hala", "iono", "iris", "leon", "lillie", "lucy", "misty", "n", "oak", "ogreclan",
      "red", "ryuki", "swimmer", "volo", "youngn", "zinnia"
    ];
            trainer.Name = names[rnd.Next(names.Length)];
            trainer.Sprite = sprites[rnd.Next(sprites.Length)];
            trainer.IsHost = false;
            trainer.IsPlayer = false;
            trainer.IsTrainer = true;
            trainer.Credits = 0;
            trainer.Items = [];


            return trainer;
        }


    }
}
