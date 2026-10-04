# Audio and motion

## Sound: every sound is a recording

**The playtests said the sounds were harsh, then that they were still bad on a phone.** The first
round replaced the synthesised effects heard every turn with Kenney recordings; the second replaced
everything the synthesiser still made — Burn, Overheat, the relic chime, the end-of-fight stings and
all three music loops — so nothing in the game is generated any more. The synthesiser
(`art/audio/synth.py`) is gone; git history has it.

Everything is **CC0**: public domain, commercial use allowed, no attribution required (given anyway,
in `docs/third-party/`).

```bash
./art/audio/install_kenney.sh <folder the four Kenney packs were unzipped into>
./art/audio/install_cc0.sh                  # downloads the rest itself
```

| sound | source |
|---|---|
| cards dealt, lifted, thrown, discarded, shuffled | Kenney Casino Audio |
| hits, blocked hits, block, deaths, hurt, upgrade, potion | Kenney Impact Sounds |
| debuff | Kenney RPG Audio |
| clicks, map select, reward | Kenney Interface Sounds |
| burn | a fire crackle by AntumDeluge (OpenGameArt), cut into four 0.8 s takes |
| overheat, relic, buff | "80 CC0 RPG SFX" by rubberduck (OpenGameArt): fire spells, gems, spells |
| victory, defeat | Kenney Music Jingles, pizzicato: a rising run and a falling one |
| combat music | "Cynic Battle Loop" by Ferk (OpenGameArt) |
| boss music | "Epic Boss Battle" by Juhani Junkala (OpenGameArt) |
| map music | "Dark Shrine Loop" by qubodup and yd (OpenGameArt) |

- **Several takes per effect** (`sfx_hit_1` … `sfx_hit_5`), and `AudioDirector` never plays the same
  take twice running. Five cards dealt are five different slides.
- **Levelled for a phone speaker.** The three music files arrived 15 LU apart (map −22 LUFS, boss
  −7); `install_cc0.sh` sets combat to −16, boss to −15 and the map, which should sit behind, to −18,
  by plain gain so each loop's seam is untouched. Effects from the new sources are peak-normalised to
  −1 dBFS. The old buff take (a drawn knife) peaked at −24 dBFS and was close to inaudible.
- **Heat is heard only when it crosses the Overheat line.** It used to sound on every gain, and a
  Heat deck gains it three times a turn. `sfx_heat` no longer exists; nothing plays it.
- Files are stored as WAV, decoded from the sources once. `AudioImportSettings` compresses music to
  Vorbis and streams it, and keeps effects as decompressed PCM so nothing is decoded the moment a
  card lands.

### Finding more sounds

Free sources that allow a commercial release, checked before anything here was used:

- **kenney.nl** — CC0 packs, no account.
- **opengameart.org** — filter by licence; take CC0 only, or CC-BY with a credit line. Avoid
  anything "NC" (non-commercial) or GPL for game assets.
- **freesound.org** — free account; filter the licence to "Creative Commons 0".
- **pixabay.com/sound-effects** and **pixabay.com/music** — Pixabay licence, commercial use allowed.
- **Sonniss GDC bundles** (sonniss.com/gameaudiogdc) — tens of gigabytes of professional effects,
  royalty free for commercial games.
- **Audacity** (audacityteam.org) for trimming, fades and loudness, all free.

## AudioDirector

- Loads clips by name from `Resources/Audio`, so the generated scene has nothing to wire up. A
  missing clip is silent, not an error.
- **The same effect cannot retrigger within 35 ms.** Five cards drawn at once are one draw, not
  a phaser.
- Effects round-robin over ten voices with ±4% pitch jitter, so the tenth identical hit does not
  sound like a recording of the first.
- Music crossfades over 1.4 s: map, combat, and boss (which is faster and heavier).
- **M** mutes. Volume settings belong to the settings screen, which does not exist yet.

## Motion: a fight told in beats

The second complaint from the same playtest was that drawing, attacking and blocking all felt weak.
They did, because they were over before they could be seen: the engine resolves a card or a whole
enemy turn instantly, and the view showed the result in the same frame. `CombatFeedback` now keeps a
**timeline** and replays the fight on it. Every duration is in `Pace`, and Settings has
**Animation speed: Normal / Fast** (Fast halves all of it).

| beat | what the player sees | Normal |
|---|---|---|
| a card played | lifted out of the hand and held up, then thrown, accelerating, at what it affects — the enemy it hits, every enemy, or the player it shields — and bursts on arrival | 0.22 s lift, 0.30 s throw |
| its effects | land when the card does: the hit, the shield, the status; several hits of one card follow each other | 0.16 s apart |
| an enemy attacking | the whole enemy card draws back, then lunges at the player, and the blow lands at the end of the lunge | 0.26 s |
| the next enemy | waits its turn | 0.85 s |
| a hand dealt | after the enemy turn's last blow, one card at a time, each arcing up from the draw pile and turning face-up on the way | 0.17 s apart, 0.45 s flight |
| Block gained | a shield swells over whoever raised it, holds, and fades; the Block badge pops when it arrives | 0.75 s |
| a blow on Block | the shield takes it first, visibly, with "Blocked N" and a clang | — |

**Numbers wait for the blows.** The model has already applied a card's damage when the card leaves
the hand, so health bars would drop before anything had landed. `ShownHp` is the model's health plus
every blow still in the air; each blow releases its share as it lands, and the bars redraw then. The
player's Block during an enemy turn is the Block they ended the turn with, spent hit by hit.

**Input waits too.** While an enemy turn is being shown, an invisible blocker covers the board and
End Turn is disabled, and the pad cannot play a card — a card played into a board still being
resolved on screen is a card played blind. Rewards and the end-of-run screen wait for the killing
blow to finish landing.

The engine gained one event for this, `EnemyActionEvent`, published as each enemy begins its action:
nothing else marked where one attacker stops and the next begins. The rules never read it.

## Motion (the tween runner)

`View/Motion.cs` is a small tween runner, written instead of importing DOTween: the game needs
move, punch, shake, flash, floating text and delay, and nothing else. It is view-only and never
touches combat state, so rules and simulations are identical whether anything animates.

**The staggered replay.** The engine resolves a whole enemy turn inside one call, so every hit
arrives in the same frame. `CombatFeedback` spaces events from one frame 0.16 s apart. The rules
have already finished; the view replays what happened at a pace a person can read. The turn-start
chime is queued after the replay, not on top of it.

| moment | motion | sound |
|---|---|---|
| card drawn | flies in from the draw pile | riffle, each card slightly higher |
| card hovered | lifts and comes to the front | — |
| card played | rises toward the board and fades | whoosh |
| card discarded | shrinks into the discard pile | — |
| damage | number pops and rises, target shakes in proportion, red flash | hit or heavy hit; grunt if the player |
| damage fully blocked | "Blocked", small shake | metallic clank |
| burn or overheat damage | orange number and flash | sizzle |
| block gained | "+N Block" | shield shimmer |
| status applied | "+N Burn/Weak/…" | sizzle, debuff or buff stab |
| enemy dies | shrinks | collapse |
| intent changes | label pops | — |
| idle | every enemy portrait breathes, out of phase with the others | — |
| rewards | cards dealt up from below | bells; relic chime |
| rest | — | anvil ring on upgrade, bells on heal |

**The hand keeps its identity.** `SyncHand` used to destroy and rebuild every card whenever the
hand changed. With motion, that would make the whole hand re-deal itself on every play, so views
are now matched to cards by instance and only the cards that actually arrived or left move.
