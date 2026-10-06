// Remplace les évolutions par échange de la 1re génération par une évolution au niveau 37.
// À exécuter manuellement, en connaissance de cause, sur la base voulue :
//   mongosh "mongodb://localhost:27017/PkmnRaceBattle" tools/trade-evolutions.mongodb.js
// Les Pokémon déjà présents dans les équipes (collection Player) gardent leurs anciennes EvolutionDetails.

const LEVEL = 37;
const tradeEvolutions = [
  ['kadabra', 'alakazam'],
  ['machoke', 'machamp'],
  ['graveler', 'golem'],
  ['haunter', 'gengar'],
];

for (const [from, to] of tradeEvolutions) {
  const result = db.Pokemon.updateOne(
    {Name: from, 'EvolutionDetails.PokemonName': to},
    {$set: {'EvolutionDetails.$.MinLevel': LEVEL, 'EvolutionDetails.$.EvolutionTrigger': 'level-up'}}
  );
  print(`${from} -> ${to} : ${result.modifiedCount} document(s) modifié(s)`);
}
