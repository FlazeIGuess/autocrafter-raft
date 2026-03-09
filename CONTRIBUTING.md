# Contributing

Thanks for your interest in AutoCrafter.

## First Steps

1. Fork the repository.
2. Create a feature branch.
3. Implement your change with clear commit messages.
4. Build the mod with `build.bat`.
5. Open a pull request.

## Pull Request Guidelines

- Keep changes focused and small when possible.
- Do not include unrelated refactors.
- Add a clear description of the change.
- Include reproduction steps for bug fixes.
- Include screenshots or short clips for UI changes.
- Mention compatibility notes when touching crafting logic.

## Coding Notes

- Keep code and comments in English.
- Preserve current mod architecture and Raft modding patterns.
- Avoid touching unrelated mods or APIs.

## Testing Checklist

Before opening a PR, verify at least:

- Mod builds successfully with `build.bat`
- Upgrade and downgrade still work
- Crafting works on host
- Input/output container assignment works
- Save and reload preserves expected state

## Custom UI Contributions

Custom UI work in Raft style is explicitly welcome.

If you work on UI, include:

- before and after screenshots
- resolution checks (desktop and lower resolutions)
- notes on readability and interaction flow
