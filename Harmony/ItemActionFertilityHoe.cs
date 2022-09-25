// Decompiled with JetBrains decompiler
// Type: ItemActionMakeFertile
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 121D1E4A-719C-47F4-9C22-4FA4FC9D63B1
// Assembly location: G:\steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed\Assembly-CSharp.dll

using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemActionFertilityHoe : ItemActionDynamicMelee
{

    public enum FertilityAction : byte
    {
        FertilizeTerrainOld,
        FertilizeTerrainNew
    }

    protected BlockValue FertileBlock;
    protected BlockValue AdjacentBlock;

    protected Tuple<string, int>[] ResourcesForUpgrade;
    protected Tuple<string, int>[] ResourcesFromDowngrade;

    protected ItemStack[] ItemsForUprade;
    protected ItemStack[] ItemsFromDowngrade;

    private string SoundHoe = string.Empty;

    private FertilityAction ActionType = FertilityAction.FertilizeTerrainOld;

    public float GetBlockRange() => BlockRange;

    public override void ReadFrom(DynamicProperties _props)
    {
        _props.ParseString("SoundHoe", ref SoundHoe);
        ActionType = EnumUtils.Parse(_props.GetString("ActionType"), FertilityAction.FertilizeTerrainOld);
        ResourcesForUpgrade = OCB.ParseHelper.ParseResources(_props.GetString("ResourcesForUpgrade"));
        ResourcesFromDowngrade = OCB.ParseHelper.ParseResources(_props.GetString("ResourcesFromDowngrade"));
        base.ReadFrom(_props);
        string _itemName1 = _props.Values.ContainsKey("Fertileblock") ? _props.Values["Fertileblock"]
            : throw new Exception("Missing attribute 'fertileblock' in use_action 'MakeFertile'");
        this.FertileBlock = ItemClass.GetItem(_itemName1).ToBlockValue();
        if (this.FertileBlock.Equals(BlockValue.Air))
            throw new Exception("Unknown block name '" + _itemName1 + "' in use_action 'MakeFertile'!");
        string _itemName2 = _props.Values.ContainsKey("Adjacentblock") ? _props.Values["Adjacentblock"]
            : throw new Exception("Missing attribute 'adjacentblock' in use_action 'MakeFertile'");
        this.AdjacentBlock = ItemClass.GetItem(_itemName2).ToBlockValue();
        if (this.AdjacentBlock.Equals(BlockValue.Air))
            throw new Exception("Unknown block name '" + _itemName2 + "' in use_action 'MakeFertile'!");
    }

    protected override void hitTarget(
        ItemActionData action,
        WorldRayHitInfo hitInfo,
        bool isGrazingHit)
    {
        var invData = action.invData;
        if (IsHitValid(invData))
            if (ExecuteFertilityHoe(invData)) return;
        base.hitTarget(action, hitInfo, isGrazingHit);
    }

    public override RenderCubeType GetFocusType(ItemActionData action) =>
        IsHitValid(action) ? RenderCubeType.FaceTop : base.GetFocusType(action);

    public override ItemClass.EnumCrosshairType GetCrosshairType(ItemActionData action) =>
        IsHitValid(action) ? ItemClass.EnumCrosshairType.Plus : base.GetCrosshairType(action);

    protected override bool isShowOverlay(ItemActionData action) =>
        IsHitValid(action) || base.isShowOverlay(action);

    protected override void getOverlayData(
        ItemActionData action,
        out float _perc,
        out string _text)
    {
        var invData = action.invData;
        if (IsHitValid(invData))
        {
            var hitInfo = invData.hitInfo;
            // int clrIdx = hitInfo.hit.clrIdx;
            // Vector3i pos = hitInfo.hit.blockPos;
            BlockValue BV = hitInfo.hit.blockValue;
            // Works with any terrain block
            if (BV.Block.shape.IsTerrain())
			{
				// Check current fertility level if upgrade is void
				var fertility = BV.Block.blockMaterial.FertileLevel;
				if (fertility <= 15)
                {
					int state = HasFertileNeighbor(invData.world, hitInfo);
                    // Primary action can safely execute (nothing nearby)
					if (state == 0) _text = Localization.Get("ttFertilityHoeAllGood"); 
					// Primary action may destroy fertile terrain
					else if (state == 1) _text = Localization.Get("ttFertilityHoeFertileNearby");
                    // Primary action will destroy plants
                    else _text = Localization.Get("ttFertilityHoePlantsNearby");
                    _perc = fertility / 30f; // Make it half full
                }
                else
                {
                    _text = Localization.Get("ttFertilityHoeAlreadyFertile");
                    _perc = 1f;
                }
                return;
            }
        }
        base.getOverlayData(action, out _perc, out _text);
    }

    private static bool IsPlayerCrouching(ItemInventoryData invData)
    {
        var player = invData.holdingEntity as EntityPlayerLocal;
        return (bool)player?.vp_FPController?.Player?.Crouch.Active;
    }

    private bool CanRemoveRequiredItem(ItemInventoryData data, ItemStack stack) =>
        data.holdingEntity.inventory.GetItemCount(stack.itemValue) >= stack.count
            || data.holdingEntity.bag.GetItemCount(stack.itemValue) >= stack.count;

    private bool RemoveRequiredItem(ItemInventoryData data, ItemStack stack) =>
        data.holdingEntity.inventory.DecItem(stack.itemValue, stack.count) == stack.count
            || data.holdingEntity.bag.DecItem(stack.itemValue, stack.count) == stack.count;

    public static int HasFertileNeighbor(World world, WorldRayHitInfo hitInfo)
    {
        int clrIdx = hitInfo.hit.clrIdx;
        Vector3i pos = hitInfo.hit.blockPos;
        // Check all neighbors and get maximum return state
        return Math.Max(Math.Max(Math.Max(
            IsFertileNeighbor(world, clrIdx, pos + Vector3i.left),
            IsFertileNeighbor(world, clrIdx, pos + Vector3i.forward)),
            IsFertileNeighbor(world, clrIdx, pos + Vector3i.right)),
            IsFertileNeighbor(world, clrIdx, pos + Vector3i.back));
    }

    // Check if above the position is a plant (and not grass etc.)
    public static bool HasPlantAtBlock(World world, int clrIdx, Vector3i pos)
    {
        // Get the block value above our position (is it a plant?)
        BlockValue BV = world.GetBlock(clrIdx, pos + Vector3i.up);
        // Another check that might work is "material.IsCollidable"
        return !BV.Block.blockMaterial.IsGroundCover && BV.Block.IsPlant();
    }

    // Check if neighbor at position is fertile (and has a plant already)
    public static int IsFertileNeighbor(World world, int clrIdx, Vector3i pos)
    {
        BlockValue BV = world.GetBlock(clrIdx, pos);
        if (!BV.Block.shape.IsTerrain()) return 0;
        if (BV.Block.blockMaterial.FertileLevel < 2) return 0;
        // Check fertility level and if we have a plant above
        return BV.Block.blockMaterial.FertileLevel > 15 ?
            HasPlantAtBlock(world, clrIdx, pos) ? 2 : 1 : 0;
    }

    // Execute the level averaging/filling action
    // Return `true` if action was executed
    private bool ExecuteFertilityHoe(
        ItemInventoryData invData)
    {
        var hitInfo = invData.hitInfo;

        // Accumulate all block changes
        List<BlockChangeInfo> changes =
            new List<BlockChangeInfo>();

        int clrIdx = hitInfo.hit.clrIdx;
        Vector3i pos = hitInfo.hit.blockPos;
        BlockValue BV = hitInfo.hit.blockValue;

        // Report "success" if the action is not really needed
        if (BV.Block.blockMaterial.FertileLevel > 15) return true;

        // Old code adds additional blocks around us
        if (ActionType == FertilityAction.FertilizeTerrainOld)
        {
            // Collect changes, but abort if a plant is found somewhere
            if (CollectNeighbor(invData.world, clrIdx, pos + Vector3i.left, changes) ||
                CollectNeighbor(invData.world, clrIdx, pos + Vector3i.forward, changes) ||
                CollectNeighbor(invData.world, clrIdx, pos + Vector3i.right, changes) ||
                CollectNeighbor(invData.world, clrIdx, pos + Vector3i.back, changes))
            {
                GameManager.ShowTooltip(
                    XUiM_Player.GetPlayer() as EntityPlayerLocal,
                    Localization.Get("ttFertilityHoePlantTooClose"));
                return true;
            }
        }

        // Try to consume all items needed for the upgrade
        // Will announce the consumed items in player UI
        if (!ConsumeItemsForUpgrade(invData))
        {
            GameManager.ShowTooltip(
                XUiM_Player.GetPlayer() as EntityPlayerLocal,
                Localization.Get("ttFertilityHoeMissingResources"));
            return true;
        }

        // Replace focused block with a (hopefully) fertile one
        changes.Add(new BlockChangeInfo(clrIdx, pos,
            FertileBlock, MarchingCubes.DensityTerrain));

        // Add some experience for doing that action
        // ToDo: why not preserving existing density!?
        if (invData.holdingEntity is EntityPlayerLocal player)
            player.Progression.AddLevelExp((int)FertileBlock
                .Block.blockMaterial.Experience);

        // Commit changes to the world
        if (changes.Count > 0) invData.
            world.SetBlocksRPC(changes);

        if (changes.Count > 0 && SoundHoe != null)
            invData.holdingEntity.PlayOneShot(SoundHoe);

        return true;
    }

    private bool ConsumeItemsForUpgrade(ItemInventoryData invData)
    {
        // Call converters (will only do this once, then re-use ItemStack array
        OCB.ParseHelper.ConvertResources(ResourcesForUpgrade, ref ItemsForUprade);
        // OCB.ParseHelper.ConvertResources(ResourcesFromDowngrade, ref ItemsFromDowngrade);
        var player = invData.holdingEntity as EntityPlayerLocal;
        // Check if we can consume all required items first
        bool satisfied = true;
        foreach (ItemStack stack in ItemsForUprade)
        {
            if (CanRemoveRequiredItem(invData, stack)) continue;
            var missing = new ItemStack(stack.itemValue, 0);
            player.AddUIHarvestingItem(missing, true);
            satisfied = false;
        }
        // Check if anything is not satisfied
        if (satisfied == false) return false;

        // Now consume all items (weird check is from vanilla)
        // Note: we should make sure to have a unique list
        foreach (ItemStack stack in ItemsForUprade)
            if (!RemoveRequiredItem(invData, stack)) return false;
            // Advertise that we took resources
            else player.AddUIHarvestingItem(
                new ItemStack(stack.itemValue,
                    - stack.count), true);
        // All good
        return true;
    }

    // Collect neighboring blocks according to offset
    // Returns `true` if it found an already fertile block
    private bool CollectNeighbor(World world, int clrIdx,
        Vector3i pos, List<BlockChangeInfo> changes)
    {
        BlockValue BV = world.GetBlock(clrIdx, pos);
        if (!BV.Block.shape.IsTerrain()) return false;
        if (BV.Block.blockMaterial.FertileLevel < 2) return false;
        // Check for plant only if soil is fertile enough
        if (BV.Block.blockMaterial.FertileLevel > 15 &&
            HasPlantAtBlock(world, clrIdx, pos)) return true;
        // Otherwise collect this neighbor to be changed
        changes.Add(new BlockChangeInfo(clrIdx, pos, AdjacentBlock,
            (sbyte)(MarchingCubes.DensityTerrain / 3)));
        // All good
        return false;
    }

    // Code from original make fertile action
    // This ensures a necessary minimum distance
    private static Vector3i GetFertilationSide(
        EntityAlive entity, WorldRayHitInfo hitInfo)
    {
        Vector3 position = entity.GetPosition();
        float f1 = position.x - hitInfo.hit.pos.x;
        float f2 = position.z - hitInfo.hit.pos.z;
        return Mathf.Abs(f1) > Mathf.Abs(f2) ?
            Vector3i.forward : Vector3i.right;
    }

    private static Vector3i GetFertilationSide(ItemInventoryData invData)
        => GetFertilationSide(invData.holdingEntity, invData.hitInfo);

    private bool IsHitValid(ItemInventoryData invData)
    {
        WorldRayHitInfo hitInfo = invData.hitInfo;
        // Check for overall hit validity
        if (!hitInfo.bHitValid) return false;
        // Only works for terrain and blocks (with density)
        if (!GameUtils.IsBlockOrTerrain(hitInfo.tag)) return false;
        // Get additional info from structs
        // int clrIdx = hitInfo.hit.clrIdx;
        // Vector3i pos = hitInfo.hit.blockPos;
        BlockValue BV = hitInfo.hit.blockValue;
        // Check if terrain is fertile enough to fertilize further
        if (BV.Block.blockMaterial.FertileLevel < 2) return false;
        // Allow action for all terrain (no blocks!?)
        if (!BV.Block.shape.IsTerrain()) return false;
        // Check distance for this hit to be within our range
        if (hitInfo.hit.distanceSq > BlockRange * BlockRange) return false;
        // Hit is valid
        return true;
    }

    private bool IsHitValid(ItemActionData action)
        => IsHitValid(action.invData);

}
