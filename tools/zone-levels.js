// Génère la table des paliers de niveaux par zone (Helpers/PathManager/ZoneLevels.cs).
//
//   node tools/zone-levels.js
//
// Simule la progression d'un Pokémon de référence (starter niveau 5, courbe medium-slow) sur chaque
// zone (5 sauvages puis un dresseur qui a autant de Pokémon que le numéro de la zone, 6 au plus), avec
// les vraies données du jeu (PkmnRaceBattle.Tests/Fixtures) et la formule d'XP du serveur.
// La référence ne reçoit que EFFICIENCY de l'XP possible (fuites, captures, K.O., changements de
// Pokémon…) : un joueur réel progresse moins vite qu'un Pokémon qui gagne tout seul tous ses combats.
// Les niveaux d'une zone se calent sur le niveau de référence au moment de chaque combat :
//   - sauvages : référence − WILD_GAP (± 1), d'abord à l'entrée de la zone puis en montant jusqu'au 5e ;
//   - dresseur : référence − TRAINER_GAP à référence (le « boss » de la zone).
// Multi Exp désactivé : l'XP doit être répartie à la main entre les Pokémon, la référence ne
// reçoit que SHARE_WITHOUT_MULTI_XP de cette XP (zones un peu plus douces).
const path = require('path');
const fixtures = path.join(__dirname, '..', 'PkmnRaceBattle.Tests', 'Fixtures');
const pokemons = require(path.join(fixtures, 'pokemon.json'));
const environments = require(path.join(fixtures, 'environments.json'));

const ZONES = 24;            // au-delà, ZoneLevels prolonge la table au rythme de la dernière zone
const WILD_FIGHTS = 5;
const MAX_TRAINER_POKEMON = 6;
const EFFICIENCY = 0.8;
const WILD_GAP = 3;
const TRAINER_GAP = 2;
const START_LEVEL = 5;
const SHARE_WITHOUT_MULTI_XP = 0.8;
const MULTIPLIERS = [1, 2, 5];

const byId = Object.fromEntries(pokemons.map(p => [p.Id, p]));
const rarity = { 'Commun': 50, 'Peu commun': 30, 'Rare': 15, 'Très rare': 4, 'Légendaire': 1 };

// XP de base moyenne d'un sauvage de ce niveau (tirage pondéré, moyenne sur les environnements)
function wildBaseXp(level) {
  let sum = 0;
  for (const env of environments) {
    let candidates = env.PossiblePokemons.filter(p => p.MinimumLevel <= level);
    if (candidates.length === 0) {
      const lowest = Math.min(...env.PossiblePokemons.map(p => p.MinimumLevel));
      candidates = env.PossiblePokemons.filter(p => p.MinimumLevel === lowest);
    }
    const weight = candidates.reduce((a, p) => a + rarity[p.Rareté], 0);
    sum += candidates.reduce((a, p) => a + rarity[p.Rareté] * byId[p.PokemonId].BaseExperience, 0) / weight;
  }
  return sum / environments.length;
}

// Les dresseurs tirent leurs Pokémon dans tout le Pokédex
const trainerBaseXp = pokemons.reduce((a, p) => a + p.BaseExperience, 0) / pokemons.length;

const xpForLevel = n => Math.floor(6 * n ** 3 / 5) - 15 * n * n + 100 * n - 140; // medium-slow
const levelOf = xp => { let l = 1; while (l < 100 && xpForLevel(l + 1) <= xp) l++; return l; };
const gained = (baseXp, level, trainer) => Math.floor(baseXp * level * (trainer ? 1.5 : 1) / 7);
const cap = n => Math.min(100, n);

function simulate(multiplier, share) {
  let xp = xpForLevel(START_LEVEL);
  const zones = [];
  for (let z = 0; z < ZONES; z++) {
    const reference = [];
    for (let k = 0; k < WILD_FIGHTS; k++) {
      const ref = levelOf(xp);
      const level = Math.max(2, ref - WILD_GAP);
      reference.push(ref);
      xp += gained(wildBaseXp(level), level, false) * multiplier * share;
    }
    const ref = levelOf(xp);
    const wildMin = Math.max(2, reference[0] - WILD_GAP - 1);
    const wildMax = Math.max(wildMin, reference[WILD_FIGHTS - 1] - WILD_GAP + 1);
    const trainerMin = Math.max(wildMax, ref - TRAINER_GAP);
    const trainerMax = Math.max(trainerMin + 1, ref);
    zones.push([cap(wildMin), cap(wildMax), cap(trainerMin), cap(trainerMax)]);
    const trainerPokemon = Math.min(z + 1, MAX_TRAINER_POKEMON);
    xp += trainerPokemon * gained(trainerBaseXp, (trainerMin + trainerMax) / 2, true) * multiplier * share;
  }
  return zones;
}

const lines = [];
for (const multiXp of [true, false]) {
  for (const multiplier of MULTIPLIERS) {
    const zones = simulate(multiplier, EFFICIENCY * (multiXp ? 1 : SHARE_WITHOUT_MULTI_XP));
    lines.push(`            [(${multiXp ? 'true' : 'false'}, ${multiplier})] =`);
    lines.push('            [');
    zones.forEach(([a, b, c, d], i) => lines.push(`                new(${a}, ${b}, ${c}, ${d}),`));
    lines.push('            ],');
  }
}
console.log(lines.join('\n'));
