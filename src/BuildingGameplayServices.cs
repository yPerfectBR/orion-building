using Orion.Gameplay;

namespace OrionBuilding;

/// <summary>
/// Host facade for block place / item-use-on-block.
/// </summary>
public sealed class BuildingGameplayServices : IBuildingApi, IPlayerBlockUseHandler
{
    public IPlayerBlockUseHandler BlockUse => this;

    public bool TryUseOnBlock(global::Orion.Player.Player player, Orion.Protocol.Types.UseItemInventoryTransactionData data)
        => BlockUseHandler.TryUseOnBlock(player, data);

    public bool TryUseOnAir(global::Orion.Player.Player player, Orion.Protocol.Types.UseItemInventoryTransactionData data)
        => BlockUseHandler.TryUseOnAir(player, data);
}
