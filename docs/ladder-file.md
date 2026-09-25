# Ladder file

A ladder file is JSON, UTF-8, no byte-order mark. Version 1 stores the duel history and nothing else. Ratings are not in the file. Loading the file replays the duels, which rebuilds the strength cache described in [rating-model.md](rating-model.md).

```json
{
  "version": 1,
  "duels": [
    {
      "utc": "2024-01-01T00:00:00.0000000Z",
      "leftUrl": "C:\\Music\\a.flac",
      "leftTitle": "First",
      "leftArtist": "Ada",
      "leftAlbum": "Album",
      "rightUrl": "C:\\Music\\b.flac",
      "rightTitle": "Second",
      "rightArtist": "Bea",
      "rightAlbum": "Album",
      "outcome": "left",
      "mode": "place"
    }
  ]
}
```

`utc` is UTC, with seven fractional digits and a `Z`. `outcome` is `left`, `right`, or `draw`. `mode` is `place` or `sharpen`. URLs are the file URLs MusicBee reported, compared as ordinal strings. A backslash in a Windows path is escaped once in JSON, the way the example escapes `C:\Music`.

The two top-level fields and the fields inside a duel may appear in any order. A missing field, an unknown field, a trailing comma, or a `version` other than 1 is rejected. The previous file is left in place.

Saving writes a temporary file beside the destination and then replaces it. A missing file loads as an empty ladder and is not created until the first save. The MusicBee plugin keeps this file under MusicBee's persistent storage folder, in `ladder\ladder.json`. The engine does not choose that folder itself.
