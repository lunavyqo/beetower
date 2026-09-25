# Ladder

Ladder is a plugin for [MusicBee](https://www.getmusicbee.com/) that ranks tracks by asking which of two songs you prefer.

A score such as 8.7 only means something if you can already remember where hundreds of other songs sit. Ladder never asks for that number. It asks for one comparison at a time, updates a strength estimate, and shows how sure that estimate is.

Two tracks are enough to start. The rules are in [docs/rating-model.md](docs/rating-model.md). The saved file is in [docs/ladder-file.md](docs/ladder-file.md).

## Install

MusicBee 3.5 or newer, on .NET Framework 4.8. Building needs the .NET 8 SDK.

```text
dotnet build -c Release
```

Copy these two files into MusicBee's Plugins folder (for a normal install, `C:\Program Files (x86)\MusicBee\Plugins`):

- `src/Ladder.Plugin/bin/Release/net48/mb_Ladder.dll`
- `src/Ladder.Plugin/bin/Release/net48/Ladder.Engine.dll`

Restart MusicBee, then enable Ladder under Preferences, Plugins. Open it from **Tools → Ladder**. The window fills the screen with the next two songs from the library. The left and right arrow keys switch which one is playing. **Prefer this** records the choice, and the next pair appears on its own.

MusicBee does not let a plugin add a tab beside Music or Playlists. Tools → Ladder is the way in.

Answers are stored in `ladder\ladder.json` under MusicBee's persistent storage folder. Removing the plugin leaves that file where it is.

## Asking

The two covers take the screen. Ladder picks them from the library. Click a cover, or press the left and right arrows, to play that song. **Prefer this** under a cover is the rating, and the next pair follows immediately.

While a track is being placed, the song on the left stays put and the one on the right is the next comparison from the middle of the ladder, then half of what remains.

Sharpen mode asks about two tracks whose ratings are close and still uncertain. **About the same** is a real answer. Skip does not record one.

The number on a track looks like `1640 ± 80`. The first figure is the strength. The `±` is how wide that estimate still is. A new track says "not placed" until it has been compared. The bar under a song fills as that width shrinks. The bar at the top is how many library tracks have been placed. While one song is being slotted in, a second bar shows how far that placement has gotten.
