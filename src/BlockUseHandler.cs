using Orion;
using Orion.Block.Traits.Types;
using Orion.Events;
using Orion.Gameplay;
using Orion.Item;
using Orion.Item.Traits.Types;
using Orion.Plugins;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.World;

namespace OrionBuilding;

/// <summary>
/// Place / use-on-block / use-on-air. AuthInput paths often send empty inventory actions — do not gate on that.
/// Survival held/consume requires VanillaInventory; creative can place from packet HeldItem without inventory.
/// </summary>
internal static class BlockUseHandler
{
    const uint UseItemTriggerInitial = 1;
    const uint UseItemTriggerRepeat = 2;
    const uint UseItemClientPredictionPlace = 1;
    const UpdateBlockFlagsType PlaceBlockUpdateFlags =
        UpdateBlockFlagsType.Network | UpdateBlockFlagsType.Neighbors;

    static readonly HashSet<string> ReplaceableBlocks =
    [
        "minecraft:air",
        "minecraft:cave_air",
        "minecraft:void_air",
        "minecraft:water",
        "minecraft:flowing_water",
        "minecraft:lava",
        "minecraft:flowing_lava",
        "minecraft:short_grass",
        "minecraft:tall_grass",
        "minecraft:fern",
        "minecraft:large_fern",
        "minecraft:dead_bush",
        "minecraft:vine",
        "minecraft:seagrass",
        "minecraft:tall_seagrass",
        "minecraft:snow_layer",
        "minecraft:fire"
    ];

    public static bool TryUseOnAir(global::Orion.Player.Player player, UseItemInventoryTransactionData transaction)
    {

        if (!TryResolveHeldItem(player, transaction, out ItemStack? heldItem, out IPlayerInventoryAccess? _)
            || heldItem is null)
        {
            return false;
        }

        if (player.Dimension is not null)
        {
            BlockPos blockPosition = transaction.BlockPosition;
            int blockFace = transaction.BlockFace;

            if (IsEmptyPosition(blockPosition) && transaction.BlockRuntimeId == 0 && player.LastActionBlockPosition.HasValue)
            {
                blockPosition = player.LastActionBlockPosition.Value;

                if (player.LastActionFace is >= 0 and <= 5)
                {
                    blockFace = player.LastActionFace;
                }
            }

            Orion.Block.BlockPermutation clickedBlock =
                player.Dimension.GetGameplayPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z);

            if (clickedBlock.Type.Identifier is not "minecraft:air" and not "minecraft:cave_air" and not "minecraft:void_air")
            {
                heldItem.OnUseOnBlock(new ItemUseOnBlockDetails(
                    player,
                    transaction.HotBarSlot,
                    blockPosition,
                    blockFace,
                    transaction.Position,
                    transaction.ClickedPosition));
                return true;
            }
        }

        heldItem.OnUseOnAir(new ItemUseOnAirDetails(player, transaction.HotBarSlot, transaction.Position));
        return true;
    }

    public static bool TryUseOnBlock(global::Orion.Player.Player player, UseItemInventoryTransactionData transaction)
    {

        if (transaction.TriggerType == UseItemTriggerRepeat)
        {
            return false;
        }

        if (transaction.TriggerType == UseItemTriggerInitial &&
            transaction.ClientPrediction != UseItemClientPredictionPlace &&
            player.Gamemode != Gamemode.Creative)
        {
            return false;
        }

        // AuthInput never sends inventory actions; do not reject survival when actions are empty.
        if (!TryResolveHeldItem(player, transaction, out ItemStack? heldItem, out IPlayerInventoryAccess? inventory)
            || heldItem is null)
        {
            return false;
        }

        // Survival place needs inventory for consume / cancel rollback.
        if (player.Gamemode == Gamemode.Survival && inventory is null)
        {
            return false;
        }

        return UseItemOnBlock(player, inventory, heldItem, transaction);
    }

    static bool UseItemOnBlock(
        global::Orion.Player.Player player,
        IPlayerInventoryAccess? inventory,
        ItemStack heldItem,
        UseItemInventoryTransactionData transaction)
    {
        if (player.Dimension is null)
        {
            return false;
        }

        BlockPos clickedPosition = transaction.BlockPosition;
        int clickedFace = transaction.BlockFace;

        if (IsEmptyPosition(clickedPosition) && transaction.BlockRuntimeId == 0 && player.LastActionBlockPosition.HasValue)
        {
            clickedPosition = player.LastActionBlockPosition.Value;

            if (player.LastActionFace is >= 0 and <= 5)
            {
                clickedFace = player.LastActionFace;
            }
        }

        Orion.Block.BlockPermutation clickedBlock =
            player.Dimension.GetGameplayPermutation(clickedPosition.X, clickedPosition.Y, clickedPosition.Z);

        BlockPos placePosition = GetPlacedBlockPosition(clickedPosition, clickedFace);

        Orion.Block.BlockPermutation existingBlock =
            player.Dimension.GetGameplayPermutation(placePosition.X, placePosition.Y, placePosition.Z);

        Orion.Block.BlockType? blockType = heldItem.Type.BlockType ?? Orion.Block.BlockType.Get(heldItem.Identifier);
        bool placingBlock = blockType is not null &&
                            blockType.Identifier != "minecraft:air" &&
                            existingBlock.Type.Identifier != blockType.Identifier &&
                            ReplaceableBlocks.Contains(existingBlock.Type.Identifier);

        if (blockType is null || blockType.Identifier == "minecraft:air")
        {
            heldItem.OnUseOnBlock(new ItemUseOnBlockDetails(
                player,
                transaction.HotBarSlot,
                clickedPosition,
                clickedFace,
                transaction.Position,
                transaction.ClickedPosition));

            SendBlockUpdate(player, placePosition, existingBlock.NetworkId);
            return true;
        }

        if (!placingBlock && existingBlock.Type.Identifier == blockType.Identifier)
        {
            if (player.Gamemode != Gamemode.Creative)
            {
                SendBlockUpdate(player, placePosition, existingBlock.NetworkId);
            }

            return false;
        }

        if (!placingBlock && !ReplaceableBlocks.Contains(existingBlock.Type.Identifier))
        {
            SendBlockUpdate(player, placePosition, existingBlock.NetworkId);
            return false;
        }

        if (!placingBlock)
        {
            Orion.Block.Block? blockEntity =
                player.Dimension.GetBlock(clickedPosition.X, clickedPosition.Y, clickedPosition.Z);

            if (blockEntity is not null)
            {
                blockEntity.OnInteract(new BlockInteractDetails(
                    player,
                    clickedPosition,
                    clickedFace,
                    transaction.ClickedPosition));

                SendBlockUpdate(player, clickedPosition, clickedBlock.NetworkId);
                return true;
            }

        }

        Server? server = player.Dimension.World?.Server as Server;
        if (server is not null)
        {
            PlayerPlaceBlockSignal signal = new(player, placePosition, clickedFace);
            server.Emit(signal);
            if (!signal.Emit())
            {
                SendBlockUpdate(player, placePosition, existingBlock.NetworkId);
                if (inventory is not null)
                {
                    ItemStack? rollbackItem = inventory.Container.GetItem(transaction.HotBarSlot);
                    if (rollbackItem is not null)
                    {
                        inventory.Container.SetItem(transaction.HotBarSlot, rollbackItem.Clone());
                    }

                    inventory.Container.UpdateSlot(transaction.HotBarSlot);
                    inventory.Container.Update();
                    inventory.SyncToPlayer(player);
                }

                return false;
            }

        }

        Orion.Block.BlockPermutation placedPermutation = blockType.Permutations.Count > 0
            ? blockType.Permutations[0]
            : blockType.GetPermutation();

        player.Dimension.SetGameplayPermutation(placePosition.X, placePosition.Y, placePosition.Z, placedPermutation);

        Orion.Block.Block? placedBlock =
            player.Dimension.GetBlock(placePosition.X, placePosition.Y, placePosition.Z);

        placedBlock?.OnPlace(new BlockPlaceDetails(
            player,
            placePosition,
            clickedFace,
            transaction.ClickedPosition));

        if (placedBlock is not null && placedBlock.Permutation.NetworkId != placedPermutation.NetworkId)
        {
            placedPermutation = placedBlock.Permutation;
            player.Dimension.SetGameplayPermutation(placePosition.X, placePosition.Y, placePosition.Z, placedPermutation);
        }

        UpdateBlockPacket placedBlockUpdate = new()
        {
            Position = placePosition,
            NetworkBlockId = placedPermutation.NetworkId,
            Flags = PlaceBlockUpdateFlags,
            Layer = UpdateBlockLayerType.Normal
        };

        // Creative clients already predict placement locally; a server UpdateBlock to the placer
        // can overwrite that prediction and make the block vanish until the next click.
        if (player.Gamemode != Gamemode.Creative)
        {
            SendBlockUpdate(player, placePosition, placedPermutation.NetworkId);
        }
        else
        {
        }

        player.Dimension.Broadcast(
            placedBlockUpdate,
            new BroadcastOptions { Except = [player] });

        player.Dimension.Broadcast(new LevelSoundEventPacket
        {
            Event = LevelSoundEvent.Place,
            Position = new Vec3f
            {
                X = placePosition.X + 0.5f,
                Y = placePosition.Y + 0.5f,
                Z = placePosition.Z + 0.5f
            },
            Data = placedPermutation.NetworkId,
            ActorIdentifier = string.Empty,
            BabyMob = false,
            DisableRelativeVolume = false,
            UniqueActorId = 0,
            FireAtPosition = new Optional<Vec3f> { HasValue = false, Value = default }
        });

        heldItem.OnPlace(new ItemPlaceDetails(
            player,
            transaction.HotBarSlot,
            clickedPosition,
            clickedFace,
            transaction.Position,
            transaction.ClickedPosition));

        if (player.Gamemode != Gamemode.Survival || inventory is null)
        {
            Orion.Block.BlockPermutation verifyCreative =
                player.Dimension.GetGameplayPermutation(placePosition.X, placePosition.Y, placePosition.Z);
            return true;
        }

        heldItem.DecrementStack();

        if (heldItem.StackSize == 0)
        {
            inventory.Container.ClearSlot(inventory.SelectedSlot);
        }
        else
        {
            inventory.Container.UpdateSlot(inventory.SelectedSlot);
        }

        Orion.Block.BlockPermutation verify =
            player.Dimension.GetGameplayPermutation(placePosition.X, placePosition.Y, placePosition.Z);

        return true;
    }

    static bool TryResolveHeldItem(
        global::Orion.Player.Player player,
        UseItemInventoryTransactionData transaction,
        out ItemStack? heldItem,
        out IPlayerInventoryAccess? inventory)
    {
        heldItem = null;
        inventory = null;

        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventoryService)
            && inventoryService is not null
            && inventoryService.TryGetAccess(player, out inventory)
            && inventory is not null)
        {
            heldItem = GetHeldItem(inventory, transaction.HotBarSlot);
            return heldItem is not null;
        }

        // Creative without inventory: use packet-declared held stack for placement.
        if (player.Gamemode == Gamemode.Creative
            && transaction.HeldItem.NetworkId != 0
            && transaction.HeldItem.Count > 0)
        {
            try
            {
                heldItem = ItemStack.FromNetworkStack(transaction.HeldItem);
                return heldItem is not null && heldItem.StackSize > 0;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    static ItemStack? GetHeldItem(IPlayerInventoryAccess inventory, int hotBarSlot)
    {
        if (hotBarSlot is < 0 or >= 9)
        {
            hotBarSlot = 0;
        }

        inventory.SetHeldSlot(hotBarSlot);

        ItemStack? heldItem = inventory.GetHeldItem();
        return heldItem is null || heldItem.StackSize == 0 ? null : heldItem;
    }

    static void SendBlockUpdate(global::Orion.Player.Player player, BlockPos position, int networkId)
    {
        player.Send(new UpdateBlockPacket
        {
            Position = position,
            NetworkBlockId = networkId,
            Flags = PlaceBlockUpdateFlags,
            Layer = UpdateBlockLayerType.Normal
        });
    }

    static BlockPos GetPlacedBlockPosition(BlockPos position, int face)
    {
        return face switch
        {
            0 => new BlockPos { X = position.X, Y = position.Y - 1, Z = position.Z },
            1 => new BlockPos { X = position.X, Y = position.Y + 1, Z = position.Z },
            2 => new BlockPos { X = position.X, Y = position.Y, Z = position.Z - 1 },
            3 => new BlockPos { X = position.X, Y = position.Y, Z = position.Z + 1 },
            4 => new BlockPos { X = position.X - 1, Y = position.Y, Z = position.Z },
            5 => new BlockPos { X = position.X + 1, Y = position.Y, Z = position.Z },
            _ => position
        };
    }

    static bool IsEmptyPosition(BlockPos position)
        => position.X == 0 && position.Y == 0 && position.Z == 0;
}
