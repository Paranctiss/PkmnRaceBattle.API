# Fixtures

Export JSON (format Extended JSON relaxed de MongoDB) des collections de référence du jeu, utilisé par les tests serveur (`Support/GameData.cs`) et par les tests E2E du client (`PokemonRaceBattle/e2e/support/global-setup.ts`, qui en remplit la base locale `PkmnRaceBattle_Test`).

| Fichier | Source | Contenu |
|---|---|---|
| `pokemon.json` | collection `Pokemon` (base locale, 06/10/2026) | 151 Pokémon de la Gen 1 avec stats, capacités apprises par niveau, évolutions |
| `moves.json` | collection `Move` (base locale) | 165 capacités (utilisées par Métronome et les attaques en deux tours) |
| `environments.json` | collection `Environment` (base de production, lecture seule) | Pokémon possibles par map (`Plaine`, `Foret`, `Volcan`, `Grotte`, `Centrale`, `Eau`) avec rareté et niveau minimum |

## Différence volontaire avec la base

Les évolutions par échange de la Gen 1 sont remplacées par une évolution au **niveau 37** (choix de design, ajustable) :
Kadabra → Alakazam, Machopeur → Mackogneur, Gravalanch → Grolem, Spectrum → Ectoplasma
(`EvolutionDetails[x].MinLevel = 37`, `EvolutionTrigger = "level-up"`).
Appliqué le 06/10/2026 aux bases locale et de production (collection `Pokemon`). Script réutilisable : `../../tools/trade-evolutions.mongodb.js`.

## Régénérer

Exporter les collections avec `mongoexport --jsonArray` (ou un script Node `BSON.EJSON.stringify`) en conservant les noms de champs Mongo (PascalCase), puis réappliquer la modification ci-dessus. Les tests vérifient 151 Pokémon / 165 capacités / 6 environnements.
