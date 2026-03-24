using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace SoilNutrientManagement;

public class SNMCore : ModSystem
{
    public static ICoreAPI CoreAPI;
    public static ICoreClientAPI ClientAPI;
    public static ICoreServerAPI ServerAPI;
    public Harmony harmony;
    // Called on server and client
    // Useful for registering block/entity classes on both sides
    public override void Start(ICoreAPI api)
    {
        CoreAPI = api;

        //if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        //{
        //    harmony = new Harmony(Mod.Info.ModID);
        //    harmony.PatchAll();
        //}

        CoreAPI.RegisterBlockBehaviorClass("UpgradeFarmland", typeof(UpgradeFarmlandBehavior));
        CoreAPI.Logger.Notification("Soil Nutrient Management Mod: Started.");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        ModConfig.tryToLoadConfig(api);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ClientAPI = api;
        ModConfig.tryToLoadConfig(api);
    }

    //public override void Dispose()
    //{
    //    harmony?.UnpatchAll("soilnutrientmanagement");
    //    harmony = null;
    //    ClientAPI = null;
    //    base.Dispose();
    //}
}

//[HarmonyPatch(typeof(Block), nameof(Block.GetPlacedBlockInteractionHelp))]
//internal class BlockFarmland_GetPlacedBlockInteractionHelp_Patch
//{
//    public static ItemStack[] GetBioCharStacks(ICoreAPI api)
//    {
//        List<ItemStack> bioCharMaterials = [];
//        foreach (var collectible in api.World.Collectibles.Where(c => c.Code != null))
//        {
//            var bioCharFill = collectible.Attributes?["bioCharFill"].AsFloat() ?? 0;
//            if (bioCharFill > 0)
//            {
//                var requiredBioChar = (int)Math.Ceiling(ModConfig.configData.requiredBioChar / bioCharFill);
//                bioCharMaterials.Add(new ItemStack(collectible, requiredBioChar));
//            }
//        }
//        return bioCharMaterials.ToArray();
//    }

//    private static List<ItemStack> biocharItems = new List<ItemStack>();
//    public static void PostFix(ref WorldInteraction[] __result, IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
//    {
//        Block block = world.BlockAccessor.GetBlock(blockSel.Position);
//        if (biocharItems.Count == 0)
//        {
//            Item[] biochars = BlockFarmland_GetPlacedBlockInteractionHelp_Patch.GetBioCharStacks(ICoreAPI api);
//        }
//        string ActionLangCode = "Upgrade farmland";

//        if (block != null && block.Code != null && UpgradeFarmlandBehavior.IsValidFarmland(block.Code.GetName()))
//        {
//            var farmland = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFarmland;

//            var farmlandAttributes = new TreeAttribute();
//            farmland.ToTreeAttributes(farmlandAttributes);
//            if (UpgradeFarmlandBehavior.HasEnoughFertilizer(farmlandAttributes))
//            {
//                __result = __result.Append(new WorldInteraction()
//                {
//                    ActionLangCode = ActionLangCode,
//                    MouseButton = EnumMouseButton.Right,
//                    Itemstacks = biocharItems.ToArray(),
//                });
                
//            }
//        } 
//    }
//}