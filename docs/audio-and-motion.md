# Audio and motion

## Sound: recorded where it counts, synthesised where nothing fits

**The first playtest said the sounds were harsh and grating**, and the table below said why before
anyone listened: every effect came from a synthesiser, and nobody had heard them. The effects a
player hears every turn — cards dealt, lifted and thrown, hits, blocks, deaths, clicks — are now
recorded sounds from four of Kenney's packs (casino, impact, RPG and interface audio), all **CC0**:
public domain, commercial use allowed, no attribution required (it is given anyway, in
`docs/third-party/`).

```bash
./art/audio/install_kenney.sh <folder the four packs were unzipped into>
```

- **Several takes per effect** (`sfx_hit_1` … `sfx_hit_5`), and `AudioDirector` never plays the same
  take twice running. Five cards dealt are five different slides.
- **Heat is heard only when it crosses the Overheat line.** It used to sound on every gain, and a
  Heat deck gains it three times a turn — the sound singled out as the one that grated.
- What stays synthesised is what no recording fits: Burn, Heat, Overheat, the relic chime and the
  two end-of-fight stings. `synth.py` no longer writes the replaced effects (its `RECORDED` set), so a
  re-run cannot put a stale synthesised take back among the recorded ones.

## The synthesiser

Every sound effect and all three music loops come from `art/audio/synth.py`, using only the
Python standard library, with ffmpeg for measurement.

```bash
python3 -u art/audio/synth.py            # everything, about 25 seconds
python3 -u art/audio/synth.py --only hit # one sound
```

**Why not a model.** The one audio model on this machine is `facebook/musicgen-small`, in the
Hugging Face cache. Its weights are licensed CC-BY-NC, non-commercial only, so its output cannot
ship in a game sold on Steam. The Stable Audio templates in ComfyUI have no model downloaded. A
synthesiser in the repo means the game owns every sound outright, and changing one is an edit and
a re-run rather than a hunt for a replacement sample.

| layer | technique |
|---|---|
| hits, thumps | sine with an exponential pitch drop, plus lowpassed noise, saturated |
| block, anvil, upgrade | inharmonic partials with separate decays (the difference between "metal" and "tone") |
| bells: turn start, reward, relic | two-operator FM, with modulation fading faster than the tone |
| burn | sparse filtered crackles over a hiss |
| music pads and bass | detuned lowpassed sawtooth pairs |
| music arpeggios | Karplus-Strong plucked strings |
| space | Schroeder reverb, offset per channel for stereo width |

**The loops have no seam.** Each track renders past its loop point, then adds that tail back onto
the start, so the reverb and the pads ringing over the end become the sound the next pass begins
in. All three tracks are in D, so the crossfade between the map and a fight never clashes.

Music is stored as WAV because this machine's ffmpeg has no Vorbis encoder. The repository size
is the only cost: `AudioImportSettings` compresses music to Vorbis and streams it at import, and
keeps effects as decompressed PCM so that nothing is decoded at the moment a card lands.

### Checked without listening

Nobody has listened to these yet, so they were checked by measurement:

- **Levels.** Every file peaks between −0.4 and −9.9 dB: nothing clips, nothing is silent.
- **Loop seams.** At each wrap point, the jump between the last and first sample is smaller than
  the 99th-percentile step inside the track (e.g. combat: 82 against 706), so there is no click.
- **Shapes.** Waveforms were drawn with ffmpeg: hits have sharp attacks and decays, bells ring
  long, and the combat loop pulses regularly on the kick.
- **Import.** Music `.meta` shows streaming and Vorbis; effects show decompress-on-load and mono.

What measurement cannot say is whether any of it sounds *good*. That needs ears, and it is the
first thing to check in a play session.

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
