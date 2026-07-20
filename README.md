# VanillaBuilding

Opt-in vanilla block placement and item-use-on-block/air.

- **Provides:** `orion:building`
- **Softdepend:** `VanillaInventory` (required for survival held item + consume)
- **API:** `IVanillaBuildingApi` / `IPlayerBlockUseHandler`

Core dispatches `UseItem` (InventoryTransaction + AuthInput) to this plugin; mining/crack stays in core.
