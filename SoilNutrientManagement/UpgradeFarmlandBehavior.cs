using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace SoilNutrientManagement;

class UpgradeFarmlandBehavior : BlockBehavior
{
    public UpgradeFarmlandBehavior(Block block) : base(block)
    {

    }
    static public bool isValidFarmland(string name)
    {
        //SNMCore.ClientAPI.Logger.Notification("Picked block: " + name);
        bool ret = false;
        if (name.StartsWith("farmland-moist-") && !name.EndsWith("high"))
        {
            ret = true;
        }
        return ret;
    }

    static public bool hasEnoughFertilizer(TreeAttribute farmlandAttributes)
    {
        if (farmlandAttributes.HasAttribute("slowN") && farmlandAttributes.HasAttribute("slowP") && farmlandAttributes.HasAttribute("slowK"))
        {
            if (farmlandAttributes.GetFloat("slowN") >= ModConfig.configData.requiredN &&
                farmlandAttributes.GetFloat("slowP") >= ModConfig.configData.requiredP &&
                farmlandAttributes.GetFloat("slowK") >= ModConfig.configData.requiredK)
            {
                return true;
            }
        }
        return false;
    }

    static public int getUpgradedFertility(int originalFertility)
    {
        //TODO: figure out how to get this from farmland.json
        var fertilities = new List<int> { 5, 25, 50, 65, 80 };

        for (int i = 0; i < fertilities.Count; i++)
        {
            var diff = fertilities[i] - originalFertility;
            if (originalFertility < (fertilities[i]))
            {
                return fertilities[i];
            }
        }
        return originalFertility;
    }

    static public float downgradeFertilizerOverlay(float overlay)
    {
        if (overlay > 100) overlay = 100;
        return (float) Math.Floor(overlay / 15);
    }

    private void upgradeFarmland(Block block, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, int requiredBioChar)
    {
        var pos = blockSel.Position;
        var farmland = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFarmland;

        var farmlandAttributes = new TreeAttribute();
        farmland.ToTreeAttributes(farmlandAttributes);

        if (hasEnoughFertilizer(farmlandAttributes))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(requiredBioChar);

            //upgrade original fertility values
            farmlandAttributes.SetInt("originalFertilityN", getUpgradedFertility(farmlandAttributes.GetInt("originalFertilityN")));
            farmlandAttributes.SetInt("originalFertilityP", getUpgradedFertility(farmlandAttributes.GetInt("originalFertilityP")));
            farmlandAttributes.SetInt("originalFertilityK", getUpgradedFertility(farmlandAttributes.GetInt("originalFertilityK")));

            //Subtract configured slowNPK values
            farmlandAttributes.SetFloat("slowN", farmlandAttributes.GetFloat("slowN") - ModConfig.configData.requiredN);
            farmlandAttributes.SetFloat("slowP", farmlandAttributes.GetFloat("slowP") - ModConfig.configData.requiredP);
            farmlandAttributes.SetFloat("slowK", farmlandAttributes.GetFloat("slowK") - ModConfig.configData.requiredK);

            //Reduce strength of all fertilizer overlays
            ITreeAttribute fertilizerOverlay = (ITreeAttribute)farmlandAttributes["fertilizerOverlayStrength"];
            if (fertilizerOverlay != null)
            {
                var fertilizerOverlayStrength = new Dictionary<string, float>();
                foreach (KeyValuePair<string, IAttribute> keyValuePair in (IEnumerable<KeyValuePair<string, IAttribute>>)fertilizerOverlay)
                {
                    fertilizerOverlayStrength[keyValuePair.Key] = downgradeFertilizerOverlay(((ScalarAttribute<float>)(keyValuePair.Value as FloatAttribute)).value);
                }

                TreeAttribute overlayAttribute = new TreeAttribute();
                farmlandAttributes["fertilizerOverlayStrength"] = (IAttribute) overlayAttribute;
                foreach (KeyValuePair<string, float> keyValuePair in fertilizerOverlayStrength)
                    overlayAttribute.SetFloat(keyValuePair.Key, keyValuePair.Value);
            }

            //clear permaboosts. This will do until I can figure out the math to make permaboosts carry over between upgrades
            farmlandAttributes.SetStringArray("permaBoosts", []);

            //apply attribute changes
            farmland.FromTreeAttributes(farmlandAttributes, world);
            farmland.MarkDirty();

            long randomsound = world.Rand.Next(1, 4);
            world.PlaySoundAt(world.BlockAccessor.GetBlock(pos).Sounds.Hit, (double)pos.X + 0.5, (double)pos.Y + 0.75, (double)pos.Z + 0.5, byPlayer, true, 12f, 1f);
            //world.PlaySoundAt(new AssetLocation("sounds/block/dirt" + randomsound), byPlayer, byPlayer, true, 8);

            //SNMCore.ClientAPI.Logger.Debug(String.Format("Farmland base fertilities upgraded to {0}/{1}/{2}",
            //    farmland.OriginalFertility[0], farmland.OriginalFertility[1], farmland.OriginalFertility[2]));
        }
    }

    private void defaultBehavior(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        ItemStack heldItemstack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
        JsonObject attribute = heldItemstack?.Collectible?.Attributes?["bioCharFill"];
        if (attribute != null || attribute.Exists)
        {
            var requiredBioChar = (int) Math.Ceiling(ModConfig.configData.requiredBioChar / attribute.AsFloat());
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            if (isValidFarmland(block.Code.GetName()) && heldItemstack.StackSize >= requiredBioChar)
            {
                upgradeFarmland(block, world, byPlayer, blockSel, requiredBioChar);
                handling = EnumHandling.Handled;
            }
            else
            {
                handling = EnumHandling.PassThrough;
            }
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        Block block = world.BlockAccessor.GetBlock(blockSel.Position);
        if(block is not null)
        {
            defaultBehavior(world, byPlayer, blockSel, ref handling);
        }
        return true;
    }
}
