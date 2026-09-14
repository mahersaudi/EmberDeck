# Audio and motion

## Sound: synthesised, not sampled or generated

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

## Motion

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
