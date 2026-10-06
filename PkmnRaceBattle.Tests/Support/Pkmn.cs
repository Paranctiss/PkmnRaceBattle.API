using PkmnRaceBattle.API.Helpers.MoveManager;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;

namespace PkmnRaceBattle.Tests.Support
{
    // Fabrique de Pokémon d'équipe à partir des vraies données du jeu
    public static class Pkmn
    {
        // Pokémon généré comme en jeu (PokemonBaseToTeam), jamais shiny ; moves = capacités imposées (sinon celles du niveau)
        public static PokemonTeam Create(string nameFr, int level, params string[] moves)
        {
            PokemonMongo pokemonBase = GameData.Pokemon(nameFr);
            PokemonTeam pokemon;
            using (GameRandom.Use(new TestRandom().Set(RandomPurpose.Generation, TestRandom.Highest)))
            {
                pokemon = PokemonBaseToTeam.ConvertBaseToTeam(pokemonBase, level);
            }
            if (moves.Length > 0) pokemon.Moves = moves.Select(Move).ToArray();
            return pokemon;
        }

        public static PokemonTeamMove Move(string nameFr) => PokemonMoveSelector.ConvertToTeamMove(GameData.Move(nameFr));

        // Pseudo-capacité d'objet, telle que le client l'envoie ("item:<nom>:<type>")
        public static PokemonTeamMove Item(string name, string type, int index = 0) =>
            PokemonMoveSelector.ConvertToActionMove("item:" + name + ":" + type, index);

        public static PokemonTeam WithStats(this PokemonTeam pokemon, int? hp = null, int? atk = null, int? def = null, int? atkSpe = null, int? defSpe = null, int? speed = null)
        {
            if (hp != null) { pokemon.BaseHp = hp.Value; pokemon.CurrHp = hp.Value; }
            if (atk != null) pokemon.Atk = atk.Value;
            if (def != null) pokemon.Def = def.Value;
            if (atkSpe != null) pokemon.AtkSpe = atkSpe.Value;
            if (defSpe != null) pokemon.DefSpe = defSpe.Value;
            if (speed != null) pokemon.Speed = speed.Value;
            return pokemon;
        }

        public static PokemonTeam WithHp(this PokemonTeam pokemon, int currHp)
        {
            pokemon.CurrHp = currHp;
            return pokemon;
        }

        public static PokemonTeam WithTypes(this PokemonTeam pokemon, params string[] types)
        {
            pokemon.Types = types.Select((t, i) => GameData.Clone(GameData.Pokemons.First().Types[0]).Also(x => { x.Name = t; x.Slot = i + 1; })).ToArray();
            return pokemon;
        }

        public static T Also<T>(this T value, Action<T> action)
        {
            action(value);
            return value;
        }
    }
}
