using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TrailMod
{

    //////////////////////////////////////////////////////////////////////////////////////
    ///PATCHING TO ADD A UNIVERAL SET LOCATION FOR LAST ENTITY TO ATTACK ON ENTITY AGENT//
    //////////////////////////////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(Block))]
    public class OverrideOnEntityCollide
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }
        [HarmonyPatch(nameof(Block.OnEntityCollide))]
        [HarmonyPostfix]
        static void OnEntityCollideOverride(
    Block __instance,
    IWorldAccessor world,
    Entity entity,
    BlockPos pos,
    BlockFacing facing,
    Vec3d collideSpeed,
    bool isImpact
)
        {
            if (world.Side.IsClient())
                return;

            if (entity == null || !entity.Alive)
                return;

            if (entity is not EntityAgent)
                return;

            ModSystemTrampleProtection modTramplePro =
                entity.Api.ModLoader.GetModSystem<ModSystemTrampleProtection>();

            if (modTramplePro.IsTrampleProtected(pos))
                return;

            // Player / creative check
            if (entity is EntityPlayer playerEntity)
            {
                EnumGameMode gameMode = playerEntity.Player.WorldData.CurrentGameMode;

                if (gameMode != EnumGameMode.Survival)
                {
                    if (!TMGlobalConstants.creativeTrampling ||
                        gameMode != EnumGameMode.Creative)
                    {
                        return;
                    }
                }
            }

            TrailChunkManager trailChunkManager =
                TrailChunkManager.GetTrailChunkManager();


            // =========================
            // SNOW TRAMPLING
            // =========================
            if (__instance?.Code?.Path?.Contains("snowlayer") == true)
            {
                Block currentBlock = world.BlockAccessor.GetBlock(pos);

                if (currentBlock?.Variant?.TryGetValue("height", out string heightStr) == true)
                {
                    int currentSnowLevel = int.Parse(heightStr);
                    int newLevel = currentSnowLevel - 1;

                    if (newLevel <= 0)
                    {
                        Block baseBlock = world.GetBlock(
                            currentBlock.CodeWithVariant("height", "0")
                        );

                        if (baseBlock != null)
                        {
                            world.BlockAccessor.SetBlock(baseBlock.Id, pos);
                        }
                    }
                    else
                    {
                        Block nextBlock = world.GetBlock(
                            currentBlock.CodeWithVariant("height", newLevel.ToString())
                        );

                        if (nextBlock != null)
                        {
                            world.BlockAccessor.SetBlock(nextBlock.Id, pos);
                        }
                    }
                }

                return;
            }


            // =========================
            // STANDARD TRAIL SYSTEM
            // =========================
            Block groundBlock = world.BlockAccessor.GetBlock(pos);

            if (trailChunkManager.ShouldTrackBlockTrailData(groundBlock))
            {
                if (!trailChunkManager.BlockCenterHorizontalInEntityBoundingBox(entity, pos))
                    return;

                trailChunkManager.AddOrUpdateBlockPosTrailData(
                    world,
                    groundBlock,
                    pos,
                    entity
                );

                return;
            }


            // =========================
            // ICE BREAKING
            // =========================
            if (__instance.BlockMaterial == EnumBlockMaterial.Ice)
            {
                Block block = world.BlockAccessor.GetBlock(pos);

                if (block?.BlockMaterial != EnumBlockMaterial.Ice)
                    return;

                if (!block.Code.Path.StartsWith("lakeice"))
                    return;

                if (world.Rand.NextDouble() >= 0.001)
                    return;

                foreach (BlockFacing face in BlockFacing.HORIZONTALS)
                {
                    BlockPos checkPos = pos.AddCopy(face);
                    Block neighbor = world.BlockAccessor.GetBlock(checkPos);

                    if (neighbor?.BlockMaterial != EnumBlockMaterial.Ice)
                        continue;

                    if (neighbor.Code.Path.StartsWith("lakeice"))
                    {
                        world.BlockAccessor.BreakBlock(checkPos, null);
                    }
                }

                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

    }
}