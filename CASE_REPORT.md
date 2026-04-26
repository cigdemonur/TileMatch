# Case Report — Gameplay Polish & Race-Condition Fixes

Date: 2026-04-26
Branch: `data-and-tile-model`

This document captures the issues investigated this session, the root cause of
each, the fix applied, and where the change lives.

---

## 1. Win / Fail popup rendered behind board tiles

**Symptom.** When the level ended, remaining tile sprites drew on top of the
Win / Fail panel.

**Root cause.** Tile sprites are on sorting layer `UI` at order `32000` during
flight (`TileView.flyingSortingLayer` / `flyingSortingOrder`). The popup lived
on the same world-space Canvas as the rack and orders, so its sort order was
below the flying-tile order.

**Fix.** Scene-side. Add a nested `Canvas` to the Win and Fail panels, enable
**Override Sorting**, set Sorting Layer to a popup-dedicated layer (or `UI`)
with **Order in Layer = 32001** or higher. Add a `GraphicRaycaster` so buttons
still receive clicks. Avoids touching the parent Canvas, so rack/order tray
keep their existing sort order.

No code change required.

---

## 2. Rack stayed full after Restart

**Symptom.** On `RestartLevel`, the rack visuals still showed the previous
run's tiles.

**Root cause.** `RackModel.Clear()` fires `OnCleared`, but `RackView` only
subscribed to `OnSlotFilled` / `OnSlotCleared`. Visuals never refreshed.

**Fix.** Subscribe `RackView` to `OnCleared` and route it to
`ClearAllVisuals`.

Files: `Assets/_TileMatch/Scripts/Views/RackView.cs`.

---

## 3. Tile tapped during order activation got stuck in rack

**Symptom.** Tapping a tile right as a new order appeared sent it to the rack
even though it matched the new order. The level could no longer complete.

**Root cause.** Race between two timed flows:
1. `OrderController` activates an order → `GameController` schedules
   `FlyRackTilesAfterDelay` (waits ~0.32 s for slot grow-in, then snapshots
   rack and flies matches).
2. The user's tap-to-rack flight (~0.32 s) launched before the order was
   considered active by `FindMatchingOrder`. The snapshot ran against an
   empty rack slot. The tile landed *after* the snapshot, with no further
   re-check.

**Fix.** Subscribe `GameController` to `RackModel.OnSlotFilled`. When a tile
lands in the rack, re-evaluate active orders; if a match exists, dispatch a
rack→order flight via the shared `StartRackToOrderFlight` helper. A
`HashSet<TileModel> _rackTilesFlyingToOrder` prevents the snapshot scan and
the OnSlotFilled handler from double-dispatching the same tile.

Files: `Assets/_TileMatch/Scripts/Controllers/GameController.cs`.

---

## 4. Two fast taps both flew to the same rack slot

**Symptom.** Tapping two non-matching tiles in rapid succession sent both
icons to slot 0 of the rack.

**Root cause.** Both `RouteTile` calls computed `PeekNextFreeSlot()` before
either flight had landed (and thus before either tile was actually placed in
the model). `FilledCount` was still the same, so both got slot 0.

**Fix.** Slot-reservation pattern, mirroring the existing
`_inFlightByOrder` reservation for order icons.

- New `RackModel.TryAddAt(slotIndex, tile)` — direct placement at a known
  index.
- `RackController` gains `_reservedSlots`, `ReserveNextFreeSlot`,
  `ReleaseReservation`, `CommitReserved`. Reservation skips both filled and
  already-reserved slots.
- `GameController.RouteTile` now reserves at tap, flies to the reserved
  slot's world position, and commits on arrival. If reservation fails (every
  slot taken or reserved), Fail fires at tap time instead of overflow.

Files:
- `Assets/_TileMatch/Scripts/Models/RackModel.cs`
- `Assets/_TileMatch/Scripts/Controllers/RackController.cs`
- `Assets/_TileMatch/Scripts/Controllers/GameController.cs`

---

## 5. Untappable tile gave no feedback when tapped

**Symptom.** Tapping a blocked tile was a silent no-op.

**Fix.** Added a side-to-side shake.

- `TileView.AnimateBlockedShake()` uses `DOShakePosition` with
  `randomness: 0` and `fadeOut: true` for a smooth decaying horizontal
  sine wiggle that cleanly returns to origin. Inspector knobs:
  `blockedShakeAmplitude`, `blockedShakeDuration`, `blockedShakeVibrato`.
  An `_isShaking` guard prevents re-trigger mid-animation.
- `TileController.OnBlockedTapped()` calls into the view.
- `InputController.HandleTap` now also tracks the topmost *blocked*
  candidate. If no unblocked tile is under the tap, it shakes the blocked
  one.

Files:
- `Assets/_TileMatch/Scripts/Views/TileView.cs`
- `Assets/_TileMatch/Scripts/Controllers/TileController.cs`
- `Assets/_TileMatch/Scripts/Controllers/InputController.cs`

---

## 6. Animation timings moved off `GameController`

`rackToOrderDuration` and `rackToOrderStartDelay` were `[SerializeField]`s on
`GameController`. They're animation/timing concerns that belong with the
views.

- `RackView.RackToOrderDuration` (rack-side flight duration).
- `OrderView.RackFlightStartDelay` (wait for slot grow-in).

`GameController` now reads both via the public properties. Re-set the
inspector values on the new fields if non-default tuning was applied.

Files:
- `Assets/_TileMatch/Scripts/Views/RackView.cs`
- `Assets/_TileMatch/Scripts/Views/OrderView.cs`
- `Assets/_TileMatch/Scripts/Controllers/GameController.cs`

---

## Verification checklist

- [ ] Win panel and Fail panel render above all tiles, including tiles
      mid-flight.
- [ ] Restart from Win and from Fail clears the rack, re-activates orders,
      and resets reserved slots.
- [ ] Two rapid same-type taps land on consecutive order icons (not both on
      icon 0).
- [ ] Two rapid no-match taps land on consecutive rack slots (not both on
      slot 0).
- [ ] Tapping a tile while an order is activating still resolves to the
      order (not stranded in rack).
- [ ] Tapping a blocked tile produces a smooth horizontal shake; rapid
      retaps don't restart the shake mid-animation.
- [ ] Filling the rack to capacity triggers Fail (not overflow).
