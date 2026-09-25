# Ladder

Ladder is a plugin for [MusicBee](https://www.getmusicbee.com/) that ranks tracks by asking which of two songs you prefer.

A score such as 8.7 only means something if you can already remember where hundreds of other songs sit. Ladder never asks for that number. It asks for one comparison at a time, updates a strength estimate, and shows how sure that estimate is.

The ranking method is specified in [docs/rating-model.md](docs/rating-model.md).

## Status

A Glicko-2 strength update, including how a rating grows uncertain over time, is implemented and covered by tests. Choosing which pair to ask, saving the ladder, and the MusicBee panel are not built yet.

## Requirements

- Windows
- MusicBee 3.5 or newer, running on .NET Framework 4.8
- .NET 8 SDK, to build

Install steps will be added with the plugin project.
