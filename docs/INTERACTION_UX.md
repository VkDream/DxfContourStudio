# Interaction UX Problem Map (v0.3.0 CAD Interaction UX Overhaul)

Created: 2026-08-12 by V030_CAD_INTERACTION_UX_OVERHAUL round (static phase).

Scope: mouse/pencil interaction feel of the CAD viewport (zoom, pan, hover,
select, snap, grip, trimming tools), keyboard focus safety, status feedback.
Out of scope: new geometry features, schema changes, Git, GUI automation.

Priority legend: P0 = must fix for acceptance, P1 = strong polish, P2 = minor,
P3 = cosmetic.

## P0 problems

1. **Snap has no hysteresis.** Single acquire radius (8px, SnapSettings.PixelTolerance)
   toggles markers hard at the boundary; a cursor jitter around the boundary
   flips markers every frame; equal-priority candidates switch by pure
   distance so hovering near a shared endpoint flickers between the two.
2. **Tools return to Select after every successful commit** (EditToolSession
   NotifyCommandCompleted). CAD precedent is continuous mode: the tool stays
   active so the user can chain joins/breaks/trims without re-clicking the
   toolbar. Test assertions at EditToolSessionTests L116/L327 and
   MainViewModelToolTests L88/L183 encode the current (to-be-changed) behaviour.
3. **Zoom has no clamp.** Viewport.ZoomAt clamps only at 1e-9 (lower bound);
   repeated wheel-up never stops; ZoomToFit has a fixed 0.95 factor and
   degenerates for zero-width/zero-height entities (vertical/horizontal lines,
   points) — the collapsed dimension falls back to scale 1.0 and picks the
   wrong dominant scale.
4. **Grip drag has no threshold.** CadViewport starts node drag on the first
   pixel of movement after MouseLeftButtonDown even inside the pick radius, so
   every single click on a node replays a full NodeEdit preview compute.
5. **Tool switch clears the selection** (MainViewModel.ActivateToolCore →
   Selection.Clear()). The spec says selection should normally survive a tool
   switch; grips are just hidden while a tool owns the mouse.

## P1 problems

6. **Trim hover flicker near boundaries.** TrimHover recomputes
   PlanSectionTrim on every pixel; near a boundary the plan flips back and
   forth between two sections, so the "shaded section" can disagree with the
   section removed at click time (Preview != Commit risk at the boundaries).
7. **Break has no hover "invalid endpoint" state.** Endpoint clicks are
   refused only at click time (status text); hovering directly over an
   endpoint shows the same "ready to cut" preview as a valid interior point.
8. **Join step 1 has no hover marker.** The first picked endpoint is drawn,
   but hovering a second endpoint never previews until the first pick exists;
   Join first-step hover also does not highlight the endpoint under the cursor.
9. **Extend step 1 decides the side only at click time** with no hover
   feedback which end will be extended or whether the hovered side has a
   valid boundary.
10. **No fixed hover highlight in Select mode** (no "entity under the cursor"
    feedback), and tool hover draws the whole entity solid (e.g. Trim Remove
    preview paints the full target red, not just the doomed section).
11. **No overlap cycling.** PickClosest always returns the lowest Id among
    equally-near entities; an entity fully covered by another can never be
    picked without hiding layers.
12. **Shortcuts ignore keyboard focus.** Window.InputBindings (Delete,
    Ctrl+A/Z/Y) fire while a TextBox (e.g. properties panel) holds focus,
    deleting document entities while the user just wanted to delete a
    character in the box.
13. **Toolbar has no active-tool indication** (the six tool buttons look
    identical whether active or not); tool groups lack a separator between the
    six tools and Delete.

## P2 problems

14. **Magic numbers scattered** across CadViewport / SnapSettings /
    MainViewModel: 6px pick, 8px snap, 3px drag threshold, 1.15 wheel factor,
    0.95 fit margin, 8px grip radius, grip sizes 3.5/4.5, "within 3px" of node
    decisions — no single settings owner.
15. **No cursor feedback**: pan glove / size-all while panning, crosshair
    while a tool is active, size-all over a grip.
16. **Status text storms**: repeated identical status keys re-publish every
    pixel move (e.g. "EditTools.Join.PickSecond" re-requested on every move);
    no dedup.
17. **No box selection** (the ToolMode.Select doc-comment claims drag on empty
    space starts a window selection; there is none). P2 enhancement, not
    blocking.

## P3 problems

18. **Tool activation gives no hint text** ("join: pick the first endpoint").
    Only per-step status keys exist (PickSecond etc.), no activation hint.
19. **Overlay ink is heavy**: remove/extend previews draw solid 2.2px pens with
    no translucent fill; hover highlight pen equals selection pen.

## Design decisions (recorded for implementation)

- New `InteractionSettings` (Application/Interaction, non-static, one default
  instance `InteractionSettings.Default`): HitTolerancePx=6, SnapAcquireRadiusPx=8,
  SnapReleaseRadiusPx=12, StickyMinDeltaPx=1.0, DragThresholdPx=4,
  GripPickupRadiusPx=8, GripSizePx=7, GripHoverSizePx=9, ZoomFactorPerNotch=1.15,
  MinPixelsPerWorld=1e-4, MaxPixelsPerWorld=1e6, FitMarginRatio=0.05,
  ClickStableRadiusPx=4, BoxSelectionMinSizePx=4.
- `Viewport` gains: `ZoomAtScreen(factor, screenPoint, w, h)` (cursor-anchored
  zoom moved into pure math), `PanByScreen(deltaScreen, w, h)`, min/max clamp
  inside ZoomAt, ZoomToFit margin parameter + zero-dimension handling.
- New `SnapHoverController` (Application/Interaction): acquire/release
  hysteresis, sticky candidate (stay unless another candidate beats it by
  StickyMinDeltaPx), priority-aware switch, clear-on-invalid.
- `EditToolSession`: continuous mode (stay in tool after success; still clear
  pending gesture), status-key dedup, Trim sticky plan cache (preview == the
  exact plan committed when cursor moved < ClickStableRadiusPx), Break hover
  invalid-at-endpoint state, Join first-step hover endpoint marker + hint key,
  Extend first-step hover side preview + NoBoundary hint, activation hint
  status keys.
- `MainViewModel`: ActivateToolCore keeps the selection (viewport hides grips
  while a tool is active), snap pipeline routes through SnapHoverController and
  uses world-space distances via PixelsToWorld, ClearSnap resets the
  controller, hover highlight id with stability tolerance (HoveredEntityId),
  box selection handling (window vs crossing).
- `CadViewport`: all magic numbers come from InteractionSettings; wheel →
  Viewport.ZoomAtScreen; pan → Viewport.PanByScreen; grip drag threshold;
  tool hover weak (dashed/thin) except the planned section itself; overlap
  cycling via HitTester.PickAll + last-click context; box selection state
  machine (P2, after core items); cursor updates (pan = SizeAll, grip = SizeAll,
  tool = Cross).
- `MainWindow.xaml(.cs)`: remove Delete/Ctrl+A/Ctrl+Z/Ctrl+Y KeyBindings →
  PreviewKeyDown + ShortcutRouter + ShortcutFocusPolicy (text-input guard);
  tool buttons get active-state DataTriggers; one more separator between the
  six tool buttons and Delete.
- Tests: ZoomAnchorTests, PanTests, DragThresholdTests, SelectionInteractionTests,
  SnapHysteresisTests, GripUxTests, ToolUxTests (JOIN/BREAK/TRIM/EXTEND UX),
  UndoUxTests, ShortcutFocusTests, BoxSelectionTests, SelectionCycleTests,
  InteractionPerformanceTests (100k-entity pointer trace), plus updates to the
  four existing assertion sites listed in P0-2.

## Verification gates

- Whole-solution Debug + Release builds: 0 warnings / 0 errors.
- `dotnet test` (all three test projects): every new interaction test green,
  existing 456-test baseline stays green.
- No window/GUI automation; offscreen RenderTargetBitmap proof where inks are
  involved (existing ViewportOffscreenRenderTests pattern).
- Final manual UX acceptance by the user (GUI feel can only be judged
  interactively).

Exit code: DXF_CONTOUR_STUDIO_V030_CAD_INTERACTION_UX_OVERHAUL_STATIC_PASS
(validated by tests), _PARTIAL, or _BLOCKED_<REASON>.