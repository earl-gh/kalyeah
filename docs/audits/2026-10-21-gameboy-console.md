# Bottom of the screen as a Game Boy: round D-pad + console

Owner request: with no cards to deploy (unlike Clash Royale) the bottom part is empty, so make it a Game Boy: a circular D-pad for up, down, left, right and in between.

## What changed
- `scripts/ui/virtual_joystick.gd` -> `scripts/ui/dpad.gd` (`DPad`). Same input contract (a `value` vector, auto-run, left-handed mirror, control scale) but the angle snaps to **8 directions** (`STEP` 45 degrees) and the pressed arrow lights up. Inputs are always a full-speed run, as before.
- `scripts/ui/hud/console_panel.gd` (`ConsolePanel`): the grey shell, rounded top corners, round sockets (D-pad, A/B-style skill buttons) and speaker slots. All drawn in code (stand-in until the owner supplies art; per the asset rule it is a 2D image / sprite job if they want it painted).
- `MatchHud`: console 294 px tall; D-pad bottom left; weapon 2 staggered above weapon 1 like A and B; cancel zone and hints moved above the console.
- Camera: `CameraRules.frame(aspect, arena, bottom)` and `ArenaCamera.bottom_reserve_px` keep the console's height clear (in UI pixels, so a taller or wider phone still shows the whole 27 x 48 m street above it).

## Check on the live link
Hold a thumb on the D-pad: the arrow in the direction lights; diagonals work; drag past the pad to auto-run. Weapons: drag to aim, release to cast. The whole street must still be visible above the grey console.

## Not checked
Android touch feel and safe-area (home bar) spacing; the console draws to the screen edge, so a home indicator sits on grey. Left-handed mode mirrors the console contents.
