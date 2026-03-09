# AutoCrafter (Alpha)

AutoCrafter upgrades regular storage chests into automated crafting stations.

Important: this is an alpha version.
Do not trust it 100 percent for critical saves. Bugs and edge cases are always possible.

## Current Status

- Version: `0.0.1a`
- State: `Alpha`
- Multiplayer: host executes crafting logic
- Compatibility: currently works with the AutoSorter mod

## What AutoCrafter Does

- Upgrade a chest up to level 3.
- Each level unlocks one crafting slot (max 3 slots).
- Each slot can:
  - use a selected recipe
  - run infinite or count-limited crafting
  - use a custom input chest
  - use a custom output chest
- If no custom input/output is assigned, the upgraded chest is used by default.
- Data is saved per world.

## Alpha Warning

This project is actively in development.

- It can contain unknown bugs.
- Save behavior can still change during development.
- Unexpected interactions with other mods are possible.

Use on your own risk and keep backups of important worlds.

## Quick Start

1. Place a `Small Storage` chest.
2. Open it and click `Upgrade` in the AutoCrafter UI.
3. Select a recipe for slot 1.
4. Put required ingredients into the input inventory.
5. Enable the slot (`Active`).
6. Wait for the craft tick (default 3 seconds).

Optional setup:
- Assign a dedicated input chest.
- Assign a dedicated output chest.

## Example Use Case: Aloe Vera

Goal: automatically craft Aloe Vera from resources provided through linked containers.

### Setup

- Chest A (Input resources): clay and palm leaves
- Chest B (AutoCrafter): upgraded chest with recipe slot configured
- Chest C (Output): destination chest for crafted Aloe Vera

### Flow

1. Upgrade Chest B and assign the Aloe Vera recipe.
2. Set Chest A as slot input.
3. Set Chest C as slot output.
4. Activate the slot.
5. AutoCrafter consumes from Chest A and places results in Chest C.

## Screenshots

- Placeholder: `docs/screenshots/aloe-vera-overview.png`
- Placeholder: `docs/screenshots/aloe-vera-input-output-setup.png`
- Placeholder: `docs/screenshots/aloe-vera-running.png`

## UI Help Wanted

I am looking for someone who wants to build a custom UI in the original Raft design style.

If you are interested, open an issue or a pull request.

## Contributing

Pull requests are welcome.

Please read `CONTRIBUTING.md` before opening a PR.

## License

Licensed under GNU AGPLv3.
See `LICENSE` for details.
