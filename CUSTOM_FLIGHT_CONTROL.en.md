# Custom Flight Control Guide

[简体中文](CUSTOM_FLIGHT_CONTROL.md) · [Back to README](README.en.md)

[Save a craft tune](#create-and-save-a-craft-tune) · [Parameter sources](#parameter-sources) · [Parameter reference](#parameter-reference) · [Symptoms](#start-with-the-symptom) · [Manual syntax](#manual-syntax-and-rejected-parameters)

For **0.2.19**. This guide explains how to save a tune with a craft and what all 40 Tuning fields do. Defaults below are for a fresh configuration in this version. Retained upgrade settings, craft parameters and session changes may differ; check the actual panel values.

## Three movements

- **Pitch:** the nose moves up or down, primarily through the horizontal tail.
- **Roll:** the wings bank to enter a turn or return to level.
- **Yaw:** the nose turns left or right, primarily through the rudder.

Parameters change how the plugin uses the aircraft's existing controls. They do not increase thrust, enlarge surfaces or change aerodynamics. A rate means degrees of rotation per second, rather than engine RPM. A control amount of magnitude `1` is full input in that direction; `0` means no input.

## Create and save a craft tune

![Tuning page](assets/screenshots/tuning-en.png)

1. Install the plugin, load your aircraft into flight and open Tuning. Check whether the source is the default/local settings or existing craft parameters.
2. Enable mouse flight and test at representative speeds. Change one field, or a closely related group, at a time using small adjustments.
3. Use the session-save button to apply your changes, then repeat the same maneuver. Editing a field does not immediately change flight control.
4. When satisfied, copy the full parameters. Export uses the panel draft: apply and test any unapplied changes before exporting.
5. Return to the designer and paste the copied `MF;...` text into the **name of one part**. Use only one parameter-carrying part, rather than duplicating it across parts.
6. Save the craft, reload it into flight, and check the source and values on Tuning before sharing.

You can keep an existing part name if whitespace separates it from `MF`, for example `Flight control MF;MPR=0;MRR=100;MLR=35;MYR=15`. The simplest option is to replace the entire name with the copied text. Keep parameters at the end; do not append ordinary descriptive text after them.

Session save applies the tune to the current flight. **It does not save into the craft or rewrite the configuration file.** Changing craft or reloading flight reads the sources again. To keep a tune with a craft, paste it into the part name and save in the designer.

Discarding unsaved changes restores the last applied values. Restoring default/craft parameters loads the source into the draft; use session save to apply it. Collapsing the panel neither saves nor applies edits.

## Parameter sources

Priority, highest first:

1. **Parameters applied from the panel for this flight.**
2. **Parameters explicitly supplied in the craft's part name.**
3. **The player's local plugin configuration.**
4. **Built-in defaults**, used for missing configuration entries.

Craft overrides are per field. For example, `MF;MYR=15` changes only the yaw target-rate limit to `15`; other fields use the downloader's local configuration. Full export includes all current 40 fields and reduces differences caused by local configurations. Future new fields still follow the newer version's reading rules.

| Export | Contents | Best use |
|---|---|---|
| Full parameters | All 40 current draft values | Saving and sharing a complete craft tune; recommended |
| Difference parameters | Fields differing from this player's local plugin configuration | Intentionally overriding only a few fields |

Differences are compared against **local plugin configuration**, not built-in defaults or the craft's original tune. Export does not apply the draft or save the craft automatically.

Players with the plugin can use their own mouse flight in multiplayer rooms. Players without it can still download, load and fly these craft using normal game controls; the parameters in the part name do not provide flight-control behavior for them.

## Parameter reference

[Pitch](#pitch-moving-the-nose-updown-and-settling-on-target) · [Roll](#roll-entering-turns-and-levelling) · [Yaw](#yaw-rudder-participation-and-leftright-alignment) · [General](#general-response-smoothing-and-direction)

Each field lists its full name and export alias. Use the panel for routine tuning; typing names manually is optional. Angles use degrees, `°/s` means degrees per second, `s` means seconds, and `°/s²` describes how much the requested rotation rate may increase each second.

**Setting a rate limit to `0` disables that limit; it does not command zero rotation.** Aircraft capability, other parameters and maximum control input still matter. These are requested rotation-rate limits, not angle-of-attack limits or stall protection. Pitch is unlimited by default; banking, levelling and yaw retain their default limits.

### Pitch: moving the nose up/down and settling on target

#### pitch rate limit

`MaxPitchRate` / `MPR`

**Default:** 0 · **Range:** 0–150 °/s

Positive values cap requested nose-up/down speed; smaller is more conservative. `0` disables the cap. Increasing it cannot guarantee exceeding the aircraft's own capability.

#### pitch settling radius

`PitchArrivalRadius` / `PAR`

**Default:** 2 · **Range:** 0.5–5°

Requested rotation slows near the target. Smaller keeps stronger turning closer to the reticle; larger gives a wider, softer finish. A 2° radius spans about 4° across; it is not a no-control dead zone.

#### pitch braking lead

`PitchBrakingLead` / `PBL`

**Default:** 0.15 · **Range:** 0–0.4 s

Uses current nose rotation to ease off early and reduce overshoot. Larger brakes earlier, potentially making final convergence slow. `0` disables this prediction. Braking can begin outside the settling radius.

#### pitch demand buildup

`PitchDemandAcceleration` / `PDA`

**Default:** 120 · **Range:** 0–1000 °/s²

Controls how quickly requested pitch rate increases. Smaller gives a softer start; larger builds faster. `0` disables gradual buildup. Reductions and reversals bypass it.

#### pitch speed correction

`PitchRateTrackingGain` / `PTG`

**Default:** 0.5 · **Range:** 0.1–2

Corrects differences between actual and requested pitch speed. Larger tracks more firmly; smaller is gentler. Excessive values can cause repeated corrections or shaking.

#### sustained pitch input

`PitchRateIntegralGain` / `PIG`

**Default:** 0.3 · **Range:** 0–1

Gradually adds the input needed when the aircraft consistently turns too slowly. Larger adds it faster but may leave more to remove later. `0` disables this accumulation.

#### abrupt pitch acceleration restraint

`PitchAccelerationDamping` / `PAD`

**Default:** 0.05 · **Range:** 0–0.2

Opposes sudden changes in pitch speed. Increasing may soften abrupt motion, but can also cause lag or shaking; test in small steps. `0` disables it.

#### leftover input removal

`PitchIntegralReleaseGain` / `PIR`

**Default:** 2 · **Range:** 0–5

Removes accumulated input faster when it obstructs substantial braking. Larger removes it faster. `0` removes only this extra boost; normal adjustment of accumulated input remains.

#### fast-response protection

`PitchFastResponseProtection` / `PFP`

**Default:** On (1) · **Range:** 0 / 1

After detecting a very quick aircraft response, reduces parts of pitch correction sensitivity for this engagement. Can help overly reactive craft but soften correction. It is not AoA, stall or G protection.

#### steady alignment learning

`PitchBalanceGain` / `PBG`

**Default:** 0.035 · **Range:** 0–0.1

Near the reticle in stable flight, gradually finds a small sustained push/pull needed to stay aligned. Larger learns faster. `0` disables it. It does not hold altitude automatically.

#### steady alignment input limit

`PitchBalanceLimit` / `PBC`

**Default:** 0.25 · **Range:** 0–0.4

Caps the push/pull retained by that compensation. Larger allows more; smaller constrains it. `0` disables the compensation. This is separate from the game's native trim channel.

#### steady alignment adjustment speed

`PitchBalanceSlew` / `PBS`

**Default:** 0.025 · **Range:** 0.001–0.1 /s

Maximum change in compensation input each second. Smaller finds balance slowly; larger adjusts faster. Consider it together with learning speed and maximum input.

With the pitch limit set to `0`, requested rate grows with remaining pitch error. The settling radius also affects demand farther from the reticle: smaller values make the overall pitch request more assertive. With the default unlimited pitch setting, changing this radius therefore affects more than the final few degrees.

Steady alignment compensation learns only under conditions such as small pointing error, relatively level wings and slow rotation. It does not correct every maneuver or save an automatic aircraft measurement for the next flight.

### Roll: entering turns and levelling

Levelling has two stages: the aircraft must first be close enough to the target with sufficiently slow nose motion; then the plugin blends levelling with continued banking toward the target. Entering an angular region alone does not immediately force levelling.

#### levelling strength

`WingsLevelGain` / `WLG`

**Default:** 1.33 · **Range:** 0.5–3

How actively the plugin corrects wing tilt during levelling. Larger is more assertive; smaller gentler. Levelling-rate and output-change limits still apply.

#### levelling rate limit

`MaxLevelRollRate` / `MLR`

**Default:** 45 · **Range:** 0–200 °/s

Caps requested roll speed while levelling. Smaller slows it down; `0` disables this cap. Separate from the banking rate limit.

#### banking rate limit

`MaxRollRate` / `MRR`

**Default:** 120 · **Range:** 0–400 °/s

Caps requested roll speed toward the target. Smaller is slower; larger allows faster banking. `0` disables this cap.

#### roll braking strength

`KdRoll` / `KDR`

**Default:** 0.15 · **Range:** 0–0.3

Opposes existing roll motion to reduce rolling past the desired bank. Larger brakes more strongly but can weaken roll response. `0` disables this braking term.

#### roll demand buildup

`RollDemandAcceleration` / `RDA`

**Default:** 600 · **Range:** 0–2000 °/s²

Smaller builds requested roll speed gradually; larger starts faster. `0` disables buildup limiting. Reductions and reversals act immediately.

#### levelling blend start

`BankBlendStart` / `BBS`

**Default:** 0.3 · **Range:** 0.2–90°

Once capture conditions are met, errors below this favor levelling. Increasing it favors levelling at larger errors. Adjust together with the blend end.

#### banking blend end

`BankBlendEnd` / `BBE`

**Default:** 2 · **Range:** 0.5–120°

In the blend calculation, this error fully favors banking toward the target; values between start and end transition gradually. Increasing can widen the transition. Must exceed start by at least 1°.

#### vertical-target inner boundary

`VerticalFadeStart` / `VFS`

**Default:** 10 · **Range:** 2–45°

When the target is closer than this angle to straight above/below the aircraft, levelling gives way most strongly. Larger expands that region. Direction is relative to the aircraft, not the ground horizon.

#### vertical-target outer boundary

`VerticalFadeEnd` / `VFE`

**Default:** 30 · **Range:** 5–90°

Beyond this angle, ordinary levelling calculation returns; the area between boundaries transitions gradually. Larger extends the transition. Must exceed the inner boundary by at least 1°.

#### roll-mode transition time

`BlendSmoothing` / `BS`

**Default:** 0.15 · **Range:** 0–0.5 s

Smooths switching between levelling and target-directed banking. Larger is softer but slower; smaller more direct. `0` disables smoothing. It does not independently cap every roll movement.

#### levelling entry angle

`RollCaptureAngle` / `RCA`

**Default:** 0.8 · **Range:** 0.2–2°

The nose must be this close to the reticle before levelling is allowed. Smaller delays entry; larger allows it earlier. Rate and confirmation-time conditions must also be met.

#### levelling exit angle

`RollCaptureExitAngle` / `RCE`

**Default:** 3 · **Range:** 0.7–10°

After entering levelling, error must exceed this to exit. Larger reduces repeated switching but can hold levelling longer. Must exceed the entry angle by at least 0.5°.

#### allowed nose rate for levelling

`RollCaptureRate` / `RCR`

**Default:** 6 · **Range:** 1–20 °/s

Combined up/down and left/right nose rotation must be below this to allow levelling. Smaller waits for more settled pointing; larger permits earlier entry. **Not a wing roll-rate limit.**

#### levelling confirmation time

`RollCaptureDwell` / `RCD`

**Default:** 0.08 · **Range:** 0–0.5 s

How long proximity and slow nose motion must continuously hold. Larger avoids levelling during a brief pass across the reticle; smaller confirms sooner. `0` removes the wait.

### Yaw: rudder participation and left/right alignment

#### yaw rate limit

`MaxYawRate` / `MYR`

**Default:** 20 · **Range:** 0–60 °/s

Caps requested left/right nose rotation. Smaller is conservative; larger allows faster turning. `0` disables the cap.

#### yaw braking strength

`KdYaw` / `KDY`

**Default:** 0.3 · **Range:** 0–0.5

Opposes existing yaw motion to reduce swaying. Larger brakes more firmly but may slow turning. `0` disables this braking term.

#### rudder aiming share in turns

`YawInTurnShare` / `YTS`

**Default:** 0.75 · **Range:** 0–1

How much rudder aiming remains while banking toward the target. Larger adds participation; smaller gives more work to banking. `0` removes only this aiming contribution, not all rudder movement.

#### motion-change rudder assistance

`YawCoordStrength` / `YCS`

**Default:** 0.6 · **Range:** 0–1.5

Adds assistance based on changes in lateral travel direction relative to the body. Larger contributes more but may cause swaying; `0` disables it. Does not directly force nose and travel directions to coincide.

#### larger-error aiming strength

`YawAimGain` / `YAG`

**Default:** 1.6 · **Range:** 0.5–2

How actively the rudder pursues a target farther from the nose. Larger is more assertive; smaller gentler. Blends smoothly with fine aiming strength.

#### fine aiming strength

`YawFineGain` / `YFG`

**Default:** 2 · **Range:** 0.5–2.5

Rudder strength near the reticle. Larger can improve small left/right corrections but cause swaying; smaller is steadier but slower. Transitions to larger-error strength over roughly 0.3°–6°.

#### yaw braking lead

`YawBrakingLead` / `YBL`

**Default:** 0.05 · **Range:** 0–0.4 s

Uses current yaw speed to ease off early. Larger brakes earlier; smaller waits until closer. `0` disables this prediction.

#### yaw input strength

`YawDemandGain` / `YDG`

**Default:** 0.8 · **Range:** 0.2–1.5

Converts requested yaw speed into control input. Larger commands more rudder; smaller is conservative. Affects the combined aiming and motion-assistance request. Excessive values can cause swaying.

### General: response, smoothing and direction

#### roll/yaw response time

`AttitudeResponse` / `AR`

**Default:** 0.35 · **Range:** 0.05–2 s

Smaller corrects roll/yaw errors more urgently; larger is gentler. **Does not currently control pitch response.** Very small values may be overly aggressive.

#### measured-rate smoothing time

`RateSmoothing` / `RS`

**Default:** 0.06 · **Range:** 0–0.3 s

Smooths rotation data used by flight control. Larger filters more rapid variation but adds lag; smaller is more immediate. `0` disables smoothing. Not a visual smoothing setting.

#### three-axis input change speed

`OutputSlew` / `OS`

**Default:** 6 · **Range:** 0.5–30 /s

Caps how much automatic input can change each second. Smaller is softer but may fall behind; larger is more direct. `6` means up to six input units per second. Does not rewrite the sensitivity of raw manual player input.

#### roll direction

`RollSign` / `RSG`

**Default:** 1 · **Range:** -1 or 1

Flip only when the aircraft banks away from the required direction. Does not adjust strength; `0` is invalid.

#### pitch direction

`PitchSign` / `PSG`

**Default:** -1 · **Range:** -1 or 1

Flip only when nose-up commands cause nose-down or vice versa. The negative default matches the game's pitch input direction, rather than indicating negative strength.

#### yaw direction

`YawSign` / `YSG`

**Default:** 1 · **Range:** -1 or 1

Flip only when right commands turn left or vice versa. Not a way to increase rudder strength.

## Start with the symptom

These are investigation starting points, not universal recommended values. Check speed, control surfaces, center of mass and whether the game's autopilot is also active.

| Symptom | First checks |
|---|---|
| Pitching toward a distant target is too slow | A low pitch limit; then pitch speed correction, demand buildup and input change speed |
| Final convergence near the reticle takes too long | Excessive settling radius or braking lead; insufficient sustained pitch input |
| Early levelling and pitch overshoot at turn completion | Reduce entry angle or allowed nose rate, or increase confirmation time; then check levelling speed and pitch braking |
| Levelling is too quick or rigid | Lower levelling speed or strength; inspect roll-mode transition time |
| Left/right swaying in large turns | Slightly reduce turn rudder share or motion-change assistance, then inspect yaw input strength and braking |
| Small left/right corrections are too slow | Fine aiming strength, yaw limit and input strength; avoid raising everything at once |
| High-speed pitch shaking | Keep fast-response protection on; first slightly reduce pitch speed correction, then separately inspect acceleration restraint and rate smoothing. Larger is not automatically more stable. |

Repeat tests with the same aircraft at similar speed and altitude: small pointing changes, larger turns, final settling, and automatic return after manual input. Finally check low, typical and high speeds. Diagnostic recordings can help comparisons, but collecting large datasets is not a prerequisite for manual tuning.

## Manual syntax and rejected parameters

For example, to limit banking, levelling and yaw while disabling the pitch limit:

```text
MF;MPR=0;MRR=100;MLR=35;MYR=15
```

This is a syntax example, not a tune recommended for every aircraft. Full names also work, for example `MF;MaxYawRate=15`.

- The prefix must be uppercase `MF`, at the start of the name or after whitespace, followed by a semicolon or the end. Field names and aliases are case-insensitive.
- Use ASCII semicolons `;`, equals signs `=` and decimal points `.`; omit units. Switches accept `0`/`1` or `false`/`true`.
- Only one part per craft may have the `MF` prefix. Multiple parameter parts reject the entire craft tune and fall back to local configuration.
- Do not repeat a field; its full name and alias count as the same field. Invalid syntax, out-of-range values or invalid paired relationships reject the entire craft tune.
- Unknown fields are ignored with a warning. Misspelling a field is not a way to disable it.
- Blend end must exceed blend start by at least 1°; vertical outer boundary must exceed inner boundary by at least 1°; levelling exit angle must exceed entry angle by at least 0.5°. These relationships must also hold after unspecified fields inherit local values.

If the panel does not show craft parameters as the source, check that part's name, duplicate parameter parts, and whether the craft was saved and reloaded. For more detail, inspect craft-parameter messages in the BepInEx log.
