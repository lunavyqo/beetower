# Changelog

## Unreleased

- Ranking model chosen: comparisons update a Glicko-2 strength, placement questions narrow the current ladder, and later questions revisit close uncertain pairs. See [docs/rating-model.md](docs/rating-model.md).
- A ladder is stored as version 1 JSON containing the comparison history. See [docs/ladder-file.md](docs/ladder-file.md).
- MusicBee can place a track and sharpen close pairs from the Ladder panel, the Tools menu, or a track's right-click menu.
- Tools → Ladder opens straight onto the next library pair. The listener does not pick the songs.
- The comparison shows how many library tracks are placed, how settled each song is, and how far the current placement has gotten.
