using HarmonyLib;
using System.Reflection;
using UnityEngine;
using static ItemActionFertilityHoe;

public class FertilityHoe : IModApi
{


	public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

	// Give users some feedback if placing is blocked by missing
	// info from the PipeGridManager (applies for all clients)
	[HarmonyPatch(typeof(RenderDisplacedCube))]
	[HarmonyPatch("update0")]
	public class RenderDisplacedCube_Update0
	{

		private static readonly MethodInfo MethodDestroyPreview =
			AccessTools.Method(typeof(RenderDisplacedCube), "DestroyPreview");

		private static Color ColorOrange =
			new Color32(255, 104, 32, 255);
		private static Color ColorYellow =
			new Color32(170, 228, 0, 255);
		
		public static bool Prefix(
			ref World _world,
			ref EntityAlive _player,
			ref WorldRayHitInfo _hitInfo,
			RenderDisplacedCube __instance,
			ref Bounds ___myBounds,
			ref Vector3 ___multiDim,
			ref float ___lastTimeFocusTransformMoved,
			ref Transform ___transformFocusCubePrefab,
			ref Transform ___transformWireframeCube,
			ref Material ___previewMaterial)
		{

			// Do some cleanups we seen on original code
			// Not exactly sure what it does, but seems to
			// be safer to keep these than to skip them
			MethodDestroyPreview.Invoke(__instance, null);
			Object.DestroyImmediate(___previewMaterial);

			// int clrIdx = _hitInfo.hit.clrIdx;
			Vector3i blockPos = _hitInfo.hit.blockPos;
			BlockValue BV = _hitInfo.hit.blockValue;

			// Assertion used when debugging (never happened so far)
			// if (BV.rawData != _world.GetBlock(blockPos).rawData)
			// 	Log.Warning("Raw Data of Hit Block differs");

			// Check if distance to hit is within our reached range
			float blockRangeSq = GetHoeActionRangeSq(_player?.inventory?
					.holdingItemItemValue?.ItemClass?.Actions);
			if (blockRangeSq < _hitInfo.hit.distanceSq) return true;

			// Update to avoid other code from thinking it's stale
			___lastTimeFocusTransformMoved = Time.time;

			// Play safe to check for existence first
			if (___transformWireframeCube != null)
			{
				___transformWireframeCube.position = blockPos - Origin.position
					- new Vector3(0.05f, 0.25f, 0.05f); // Adjust for paddings
				___transformWireframeCube.localScale = new Vector3(1.1f, 1.5f, 1.1f);
				___transformWireframeCube.rotation = BV.Block.shape.GetRotation(BV);
			}

			// Play safe to check for existence first
			if (___transformFocusCubePrefab != null)
			{
				___transformFocusCubePrefab.localPosition = new Vector3(0.5f, 0.5f, 0.5f);
				___transformFocusCubePrefab.localScale = new Vector3(2.54f, 2.54f, 2.54f);
				___transformFocusCubePrefab.parent = ___transformWireframeCube;
			}

			// Update some states, which are static for our use case
			// We only target terrain blocks, so block size is fixed
			___myBounds = new Bounds(new Vector3(0.5f, 0.5f, 0.5f), Vector3.one);
			___multiDim = Vector3i.one; // Terrain blocks are never multi-dims?

			// Works with any terrain block
			if (BV.Block.shape.IsTerrain())
			{
				Color color = Color.red;
				// Enable the two transforms (GameObjects) to show
				___transformWireframeCube?.gameObject.SetActive(true);
				___transformFocusCubePrefab?.gameObject.SetActive(true);
				// Check current fertility level if upgrade is void
				var fertility = BV.Block.blockMaterial.FertileLevel;
				if (fertility <= 15)
                {
					int state = HasFertileNeighbor(_world, _hitInfo);
					// Primary action will destroy plants
					if (state == 2) color = ColorOrange;
					// Primary action may destroy fertile terrain
					else if (state == 1) color = ColorYellow;
					else color = Color.green; // All good
				}
				// Update the color for the wire-frame
				// For now we always have the same color
				// Might see some use-case in the future
				foreach (Renderer renderer in ___transformFocusCubePrefab?
							.GetComponentsInChildren<Renderer>())
					renderer.material.SetColor("_Color", color);
			}
			else
			{
				// Disable the two transforms to hide the helpers
				___transformWireframeCube?.gameObject.SetActive(false);
				___transformFocusCubePrefab?.gameObject.SetActive(false);
			}

			// Skip original
			return false;
		}

		private static float GetHoeActionRangeSq(ItemAction[] actions)
		{
			float range = 0;
			foreach (ItemAction action in actions)
				if (action is ItemActionFertilityHoe item)
					range = Mathf.Max(range, item.GetBlockRange());
			// range = Mathf.Max(range, item.GetBlockRange());
			return range * range;
		}

    }

}
