# Ranking model

Ladder ranks a music library from pairwise answers. Each track carries a strength: a rating, a deviation (how wide the estimate still is), and a volatility (how jumpy that track's results have been). The listener never types a score.

The math is the Glicko-2 system published by Mark E. Glickman, "Example of the Glicko-2 system" (22 March 2022, https://glicko.net/glicko/glicko2.pdf). This document records the choices around that system. The formulas below are the ones the engine implements.

## Why a strength, plus a separate way to choose questions

Three ways to turn "which song is better?" into an order were considered.

**Binary insertion.** Compare the track with the one in the middle of the current order, then with the middle of the remaining half, until a slot is left. A library of a thousand tracks needs about ten answers, and each answer is easy to explain. The slot is only as good as every earlier answer. A misclick, a mood, or a changed opinion stays in the order, and two tracks that landed far apart are never asked about again. The position also has no width: track 40 and track 41 look equally decided whether they were a coin flip or a blowout.

**Elo.** Each answer nudges both ratings. Later answers can repair an earlier one. There is no separate notion of "this number is still a guess", so the system cannot prefer an informative question over a useless one, and a displayed 8.7 is precise-looking for a song you have compared twice.

**Glicko-2.** Each track has a rating, a deviation, and a volatility. A new track starts at rating 1500, deviation 350, volatility 0.06. Wins against strong tracks move a wide rating a long way. Wins that were already expected move a narrow rating a little. A draw is a real outcome (score 0.5), which matters for songs that feel the same. The 95% interval is the rating plus or minus twice the deviation, which is the honest replacement for a hand-typed decimal.

Ladder uses both of the useful parts. Binary search chooses the questions while a track is being placed, because those questions halve the library and stay understandable. Glicko-2 records every answer, so the stored result is a strength with a width, and a later answer can move the track. A second mode then asks about pairs whose ratings are close and whose deviations are still wide. That is the part binary insertion never does: checking the songs that actually sit next to each other.

A full Bradley-Terry refit of the whole history was set aside. It is a good future check, which is why every duel is stored, and it is more machinery than the plugin needs in order to update one pair at a time.

## Constants

| Name | Value | Meaning |
| --- | --- | --- |
| Scale | 173.7178 | Converts a Glicko rating or deviation onto the Glicko-2 scale |
| Tau | 0.5 | Limits how fast volatility can change. The paper's worked example uses 0.5, inside the suggested 0.3–1.2 range |
| Initial rating | 1500 | Strength of a track with no answers |
| Initial deviation | 350 | Width of a track with no answers |
| Initial volatility | 0.06 | Starting volatility from the paper |
| Deviation floor | 30 | Lowest deviation after an update, so a heavily compared track can still move |
| Deviation cap | 350 | Highest deviation, matching an unrated track |
| Neighbor window | 10 | Sharpening only considers a track against the ten tracks just above it |
| Recent pairs | 24 | How many latest duels count as "just asked" |
| Recent weight | 0.1 | A just-asked pair keeps a tenth of its information score |
| Convergence | 0.000001 | Stop tolerance for the volatility solver |

The deviation floor is an extra constraint on top of the paper. The worked example finishes at deviation 151.52, so the floor does not change that result. It exists so a song you have compared dozens of times is not frozen if your taste shifts.

## Time since the last answer

Glicko-2's own volatility term barely widens the deviation across calendar time, because one quiet rating period only adds `σ²` on the Glicko-2 scale. A song you were sure about in January should be questionable again a year later.

Before a duel is scored, and whenever a deviation is shown or used to pick a question, replace the stored deviation with

```text
RD' = min(350, sqrt(RD² + d · days))
```

`days` is the number of UTC days since that track's last comparison. A track that has never been compared uses its stored deviation unchanged. `d` is chosen so a confident deviation of 50 returns to 350 after 730 days:

```text
d = (350² − 50²) / 730 = 120000 / 730
```

The rating and the volatility stay as stored. The widened deviation is what the Glicko-2 update then sees. It is not written back on its own. Opening the panel does not change the file. The next real answer does, because the update starts from the widened value.

Fractional days count. A negative elapsed time, from a clock step, counts as zero.

## One duel

Both tracks are updated from the strengths they had at the start of the answer, after the calendar widening above. Updating the second track from the first track's already-new rating would count the answer twice.

Scores are 1 for a win, 0 for a loss, and 0.5 for "about the same". Glicko-2 takes that score directly.

For a player with Glicko-2 rating `μ`, deviation `φ`, and volatility `σ`, against opponents `j` with scores `sⱼ`:

```text
μ  = (r − 1500) / 173.7178
φ  = RD / 173.7178

g(φ) = 1 / sqrt(1 + 3φ² / π²)
E(μ, μⱼ, φⱼ) = 1 / (1 + exp(−g(φⱼ) (μ − μⱼ)))

v  = 1 / Σⱼ g(φⱼ)² Eⱼ (1 − Eⱼ)
Δ  = v Σⱼ g(φⱼ) (sⱼ − Eⱼ)
```

The new volatility `σ'` is the paper's Illinois solve for `f(x) = 0`, with `a = ln(σ²)` and tolerance 0.000001. Then

```text
φ* = sqrt(φ² + σ'²)
φ' = 1 / sqrt(1/φ*² + 1/v)
μ' = μ + φ'² Σⱼ g(φⱼ) (sⱼ − Eⱼ)

r'  = 173.7178 μ' + 1500
RD' = 173.7178 φ'
```

Clamp `RD'` into [30, 350]. The worked example (rating 1500, deviation 200, volatility 0.06, τ = 0.5, results 1, 0, 0 against 1400/30, 1550/100, and 1700/300) finishes at rating 1464.06, deviation 151.52, volatility 0.05999. The engine test follows that example.

The expected score `E` is also the chance that the player beats that opponent. Pairing uses it.

## Place mode

The listener picks the song being rated. Ladder chooses the other song. With an empty ladder, the first pick waits for a second song, and that pair is the first duel. After that, each picked song is placed by binary search.

Place mode inserts one focus track into the ladder with as few questions as a binary search needs. The opponents are the other tracks that already have at least one comparison, sorted from weakest rating to strongest, frozen at the moment the session starts. Later answers update strengths immediately, but they do not reshuffle the list the search is walking, so the questions keep halving the same range.

Let the range be the half-open index interval `[low, high)`.

- No opponents: there is nothing to ask. The focus stays unplaced until a second track exists.
- Otherwise ask about the opponent at `mid = (low + high) / 2`. The focus is always presented as the track being placed.
- "Focus is better" sets `low = mid + 1` and records a win.
- "Focus is worse" sets `high = mid` and records a loss.
- "About the same" records a draw and ends the search.
- The search also ends when `low >= high`.

Skip removes that opponent from the frozen list and shifts `low` and `high` down for every index that moved. No duel is stored.

When the search ends, ask up to two edge questions: the track now immediately stronger than the focus, and the track now immediately weaker, skipping either one if this session already compared them. The closer rating is asked first. These are real duels. They exist because the last binary-search pivot is a midpoint, and the song directly beside the new slot may never have been played against the focus.

Every stored duel from this session uses mode `place`. Cancelling before the first answer writes nothing.

## Sharpen mode

Sharpen mode repairs the local order. Consider every track that has at least one comparison, and pair it with each of the next 10 stronger tracks. Score a pair by

```text
info = (RDᵢ² + RDⱼ²) · p · (1 − p)
```

using calendar-widened deviations. `p` is the expected score of either track against the other. `p · (1 − p)` is largest when the answer is a coin flip, and the squared deviations prefer tracks the ladder is still unsure about.

If that unordered pair appears in the last 24 stored duels, multiply `info` by 0.1. The pair with the largest score is the question. Ties break by the two URLs in ordinal order, so the choice is stable.

Skip remembers the pair for the current session only. It is not a draw, and it is not written to the file. If every candidate has been skipped, there is no question.

Side of the screen alternates with the number of stored duels, so the stronger track is not always on the left. Even duel counts put the stronger track on the left. The buttons name the songs. They do not say only "left" and "right".

## What is stored, and what is shown

The identity of a track is the file URL MusicBee reports, compared as an ordinal string. Ladder does not rewrite paths.

A track with no comparisons is unplaced. It is absent from the ranked list, and the UI says "not placed" rather than showing 1500 ± 700.

Rank is a view: higher rating first. Equal ratings break by more comparisons, then by URL ordinal.

The number shown is the rating rounded to an integer, then `±` the integer rounding of twice the calendar-widened deviation. That `±` is half the width of the 95% interval from the paper (rating ± 2 RD). Example shape: `1640 ± 80`.

Against the next-weaker ranked track, the gap is "clear of the next track" when the rating gap is larger than twice the sum of the two widened deviations. Otherwise the gap is "overlapping the next track". The strongest track has no track above it. The weakest has no next-weaker track, so the hint is omitted there.

Duels are the source of truth. Each one stores the UTC time, both URLs, the display title, artist, and album heard at the time, the outcome, and the mode (`place` or `sharpen`). The track table is a cache of strengths rebuilt from those duels. Replaying the duel list from unrated strengths must reproduce the cache. A later comparison replaces a cached title, artist, or album only when the new value is non-empty, so a missing tag does not erase a name already known for that URL.

Opening a view does not write the file. An answer does.

## Out of scope for the model

Ladder does not write ratings into audio tags. It does not invent wins against the songs a binary search stepped over. It does not turn the rating back into a 0–10 score.
