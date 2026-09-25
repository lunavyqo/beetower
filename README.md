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

Restart MusicBee, then enable Ladder under Preferences, Plugins. Open it from **View → Ladder**. That window fills the player: two songs side by side, and the left and right arrow keys switch which one is playing.

To keep it as its own tab, click the **+** on the tab bar, open **View → Arrange Panels**, and drag **Ladder** into the main panel of that tab. Leave the other elements of that tab empty.

Answers are stored in `ladder\ladder.json` under MusicBee's persistent storage folder. Removing the plugin leaves that file where it is.

## Asking

The two covers take the screen. Click a cover, or press the left and right arrows, to play that song. **Prefer this** under a cover is the rating. Listening and choosing are separate, so you can flip between the pair before you decide.

Place mode keeps the track you chose on the left and compares it with one track from the middle of the current ladder, then half of what remains, until the slot is narrow. It then asks about the songs that ended up directly beside it.

Sharpen mode asks about two tracks whose ratings are close and still uncertain. **About the same** is a real answer. Skip does not record one.

The number on a track looks like `1640 ± 80`. The first figure is the strength. The `±` is how wide that estimate still is. A new track says "not placed" until it has been compared.
