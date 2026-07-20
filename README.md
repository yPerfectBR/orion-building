# Orion Building

Opt-in vanilla block placement and item use on block/air.

- **Manifest id:** `orion:building`
- **Provides:** `orion:building`
- **Soft depend:** `orion:inventory` (survival held item + consume)

## Build

```bash
dotnet build OrionBuilding.csproj -c Release
```

## API

Registered services: `IBuildingApi`, `IPlayerBlockUseHandler`.

The core dispatches `UseItem` (`InventoryTransaction` + `AuthInput`) to this plugin. Mining/crack animation stays in **orion:mining**.

## CI

GitHub Actions smoke-boots the server with this plugin loaded after a Release build.
