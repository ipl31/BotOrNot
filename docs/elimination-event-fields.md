# Elimination Event Fields

Investigation of what data is available on each elimination event from `FortniteReplayReader 2.4.0`.

## Event Type

`FortniteReplayReader.Models.Events.PlayerElimination`

## Fields

| Field | Type | Example | Description |
|---|---|---|---|
| `Time` | string | `"00:51"` | Match time (MM:SS) when the event occurred |
| `Knocked` | bool | `true` | Whether this is a knock (true) or a finish (false) |
| `GunType` | byte | `4`, `5` | Weapon category code |
| `IsSelfElimination` | bool | `false` | Self-inflicted (fall damage, storm, etc.) |
| `Distance` | float? | `1.875` | Distance between players |
| `IsValidLocation` | bool | `true` | Whether location data is populated |
| `Eliminated` | string | `5225584461D5...` | Eliminated player ID (legacy field) |
| `Eliminator` | string | `A4EBB9C31B1B...` | Eliminator player ID (legacy field) |
| `EliminatedInfo` | PlayerEliminationInfo | — | Detailed info about the eliminated player |
| `EliminatorInfo` | PlayerEliminationInfo | — | Detailed info about the eliminator |
| `Info` | EventInfo | — | Unreal engine event metadata |

## PlayerEliminationInfo Fields

Available on both `EliminatedInfo` and `EliminatorInfo`:

| Field | Type | Example | Description |
|---|---|---|---|
| `Id` | string | `5225584461D5...` | Player ID |
| `PlayerType` | PlayerTypes | `PLAYER` | Player type enum |
| `IsBot` | bool | `false` | Whether this player is a bot |
| `Location` | FVector | `X: 0, Y: 0, Z: 0` | Position on map (X, Y, Z) |
| `Rotation` | FQuat | — | Player rotation quaternion |
| `Scale` | FVector | — | Scale vector |

## Storm Circle

There is **no explicit storm phase/circle field** on elimination events. However, the storm phase could be inferred from the `Time` field since storm circles follow a known schedule per game mode (e.g., first circle closes at ~3:20 in standard modes).

## Currently Used by BotOrNot

- `Time` — 60-second knock/finish credit window
- `Knocked` — distinguish knocks from finishes
- `EliminatedInfo.Id` / `EliminatorInfo.Id` — player identification
- `Eliminated` / `Eliminator` — legacy fallback IDs

## Not Yet Used

- `GunType` — could enhance death cause display
- `IsSelfElimination` — could flag self-elims differently
- `Distance` — engagement distance
- `Location` — map position (could enable heatmaps)
- `EliminatedInfo.IsBot` / `EliminatorInfo.IsBot` — alternative bot detection source
- `Time` as a display column — show when each elimination happened
