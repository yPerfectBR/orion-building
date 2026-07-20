using Orion.Api;
using Orion.Api.Items;
using Orion.Api.Math;
using Orion.Gameplay;

namespace OrionBuilding;

/// <summary>
/// S7 Api-only façade. Deep place/use logic previously depended on Orion.dll block/item types;
/// full placement returns in a follow-up once IBlock helpers cover facing/replaceable checks.
/// </summary>
public sealed class BuildingGameplayServices : IBuildingApi, IPlayerBlockUseHandler
{
    public IPlayerBlockUseHandler BlockUse => this;

    public bool TryUseOnBlock(IPlayer player, BlockPos blockPos, int face, BlockPos placePos, IItemStack? held)
    {
        _ = (player, blockPos, face, placePos, held);
        return false;
    }

    public bool TryUseOnAir(IPlayer player, IItemStack? held)
    {
        _ = (player, held);
        return false;
    }
}
