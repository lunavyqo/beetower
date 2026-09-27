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

Restart MusicBee, then enable Ladder under Preferences, Plugins. Open it from **Tools → Ladder**, or right-click a track and choose **Ladder: Rate this track**.

You pick the song you want to rate. Ladder chooses the song on the other side. Click **Pick this** on a card when you have decided. The song stays off the ladder until that whole run is finished. Ladder then shows its rank and waits until you rate another song.

MusicBee does not let a plugin add a tab beside Music or Playlists. Tools → Ladder is the way in.

Answers are stored in `ladder\ladder.json` under MusicBee's persistent storage folder. Removing the plugin leaves that file where it is.

## Asking

The song you picked stays on the left. Click either cover to listen. The title, rank, playback bar, and **Pick this** sit under that cover. **About the same** lines up with the two buttons. Drag a bar to move through that song.

While a track is being placed, the song on the left stays put and the one on the right is the next comparison from the middle of the ladder, then half of what remains.

Sharpen mode asks about two tracks whose ratings are close and still uncertain. **About the same** is a real answer. Skip does not record one.

The number on a track looks like `1640 ± 80`. The first figure is the strength. The `±` is how wide that estimate still is. A new track says "not placed" until it has been compared. The bar under each song is its playback position. Drag it to move through that song.
