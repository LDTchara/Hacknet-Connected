# ReplaceSystem

Runtime, eager text replacement for Hacknet + Pathfinder.

## Pathfinder action

```xml
<Replace Org="#ASDFASFF#" Curr="asdfassdf" />
```

`Replace` inherits Pathfinder's `DelayablePathfinderAction`, so the standard
`Delay` and `DelayHost` attributes are also accepted.

The action adds one immutable mapping, immediately rewrites loaded gameplay
objects, and leaves the mapping active for content loaded later. Reassigning an
existing token to a different value is an error.

## Contract

- Tokens must use `#...#` and cannot contain another `#` or a newline.
- Values may not contain a token. Recursive replacement is forbidden.
- Replacement is case-sensitive and non-recursive.
- Unknown tokens remain unchanged.

## JSON files

Global mappings are loaded from `BepInEx/config/ReplaceSystem/replacements.json`.
An extension can optionally provide `RuntimeReplacements.json` at its root;
extension mappings override global mappings before the OS session loads.

```json
{
  "#PLAYERNAME1#": "ASDF",
  "#PLAYERNAME2#": "QWER",
  "#FORK_1#": "P1 triggered fork 1!"
}
```
