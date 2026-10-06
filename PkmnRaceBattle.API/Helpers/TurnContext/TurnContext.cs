using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Hub;

namespace PkmnRaceBattle.API.Hub
{
    public class TurnContext
    {
        public string ActionName { get; set; } = "";
        public PokemonChanges Opponent { get; set; } = new PokemonChanges();
        public PokemonChanges Player { get; set; } = new PokemonChanges();
        public List<string> PrioMessages { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();

        public void AddMessage(string message)
        {
            Messages.Add(message);
        }

        public void DeleteLastMessage()
        {
            Messages.RemoveAt(Messages.Count - 1);
        }

        public void DeleteLastPrioMessage()
        {
            PrioMessages.RemoveAt(PrioMessages.Count - 1);
        }

        public void AddPrioMessage(string message)
        {
            PrioMessages.Add(message);
        }

        public int CalculateDelay()
        {
            int delay = 0;
            delay += Messages.Count * GameDelay.Message;
            delay += PrioMessages.Count * GameDelay.Message;
            delay += Player.Hp.Count * GameDelay.HpChange;
            delay += Opponent.Hp.Count * GameDelay.HpChange;
            return delay;
        }
    }
}
