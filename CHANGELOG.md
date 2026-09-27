# Changelog

## Unreleased

- Ranking model chosen: comparisons update a Glicko-2 strength, placement questions narrow the current ladder, and later questions revisit close uncertain pairs. See [docs/rating-model.md](docs/rating-model.md).
- A ladder is stored as version 1 JSON containing the comparison history. See [docs/ladder-file.md](docs/ladder-file.md).
- MusicBee can place a track and sharpen close pairs from the Ladder panel, the Tools menu, or a track's right-click menu.
- Each comparison is a cover with the title, rank, playback bar, and Pick this directly under it. About the same lines up with the two Pick this buttons. Drag a bar to move through that song.
- You pick the song to rate. Ladder chooses the comparison, and the cards are the choice.
- A song joins the ladder only after its placement run finishes, not after the first comparison.
- When that run finishes, Ladder shows the song's rank and stays there until you rate another song.
