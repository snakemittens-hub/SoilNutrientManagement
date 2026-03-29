using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FarmlandNutrientManagement;
#nullable disable

class UpgradeFarmlandBehavior : BlockBehavior
{
    private ICoreAPI Api;
    public const string PermaboostFertilizersCacheKey = "UpgradeFarmlandBehavior.permaboostFertilizers";
    private PermaFertilityBoost[] _permaboostFertilizerStacks = [];
    public const string FarmlandFertilitiesCacheKey = "UpgradeFarmlandBehavior.farmlandFertilities";

    public override void OnLoaded(ICoreAPI api)
    {
        this.Api = api;

        _permaboostFertilizerStacks = ObjectCacheUtil.GetOrCreate(api, PermaboostFertilizersCacheKey,
            (CreateCachableObjectDelegate<PermaFertilityBoost[]>)(() => GetPermaboostFertilizers(api))).ToArray();
    }

    public UpgradeFarmlandBehavior(Block block) : base(block) { }

    public static PermaFertilityBoost[] GetPermaboostFertilizers(ICoreAPI api)
    {
        List<PermaFertilityBoost> permaboostFertilizers = [];

        foreach (var collObj in api.World.Collectibles.Where(c => c.Code != null))
        {
            JsonObject attribute = collObj?.Attributes?["fertilizerProps"];
            if (attribute == null || !attribute.Exists) continue;
            FertilizerProps fertilizerProps = attribute.AsObject<FertilizerProps>((FertilizerProps)null);
            if (fertilizerProps == null) continue;
            if (fertilizerProps.PermaBoost != null)
            {
                api.Logger.Debug("Found permaboost: " + fertilizerProps.PermaBoost.Code);
                permaboostFertilizers.Add(fertilizerProps.PermaBoost);
            }
        }

        api.Logger.Debug($"Found {permaboostFertilizers.Count} fertility permaboosts");
        return permaboostFertilizers.ToArray();
    }

    static public bool IsValidFarmland(string name)
    {
        // Only moist farmland can be upgraded; dry farmland is excluded intentionally
        return name.StartsWith("farmland-moist-");
    }

    public bool HasEnoughFertilizer(TreeAttribute farmlandAttributes)
    {
        if (farmlandAttributes.HasAttribute("slowN") && farmlandAttributes.HasAttribute("slowP") && farmlandAttributes.HasAttribute("slowK"))
        {
            float slowN = farmlandAttributes.GetFloat("slowN");
            float slowP = farmlandAttributes.GetFloat("slowP");
            float slowK = farmlandAttributes.GetFloat("slowK");

            if (slowN >= ModConfig.configData.requiredN &&
                slowP >= ModConfig.configData.requiredP &&
                slowK >= ModConfig.configData.requiredK)
            {
                if (ModConfig.configData.limitP && slowP > ModConfig.configData.excessP)
                {
                    if (this.Api is ICoreClientAPI api)
                    {
                        api.TriggerIngameError((object)this, "high-p", Lang.Get("farmlandnutrientmanagement:high-p"));
                    }
                    return false;
                }
                return true;
            }
            else
            {
                // User feedback for which fertilizers to add
                if (this.Api is ICoreClientAPI api)
                {
                    if (slowN < ModConfig.configData.requiredN)
                    {
                        api.TriggerIngameError((object)this, "low-n", Lang.Get("farmlandnutrientmanagement:low-n"));
                    }
                    else if (slowP < ModConfig.configData.requiredP)
                    {
                        api.TriggerIngameError((object)this, "low-p", Lang.Get("farmlandnutrientmanagement:low-p"));
                    }
                    else if (slowK < ModConfig.configData.requiredK)
                    {
                        api.TriggerIngameError((object)this, "low-k", Lang.Get("farmlandnutrientmanagement:low-k"));
                    }
                }
            }
        }
        return false;
    }

    public TreeAttribute RemovePermaboosts(TreeAttribute farmlandAttributes)
    {
        var currentPermaboosts = farmlandAttributes.GetStringArray("permaBoosts");
        if (currentPermaboosts != null && currentPermaboosts.Length > 0)
        {
            foreach (PermaFertilityBoost permaboost in _permaboostFertilizerStacks)
            {
                if (currentPermaboosts.Contains(permaboost.Code))
                {
                    farmlandAttributes.SetInt("originalFertilityN", farmlandAttributes.GetInt("originalFertilityN") - permaboost.N);
                    farmlandAttributes.SetInt("originalFertilityP", farmlandAttributes.GetInt("originalFertilityP") - permaboost.P);
                    farmlandAttributes.SetInt("originalFertilityK", farmlandAttributes.GetInt("originalFertilityK") - permaboost.K);
                }
            }
        }
        return farmlandAttributes;
    }

    public TreeAttribute RestorePermaboosts(TreeAttribute farmlandAttributes)
    {
        var currentPermaboosts = farmlandAttributes.GetStringArray("permaBoosts");
        if (currentPermaboosts != null && currentPermaboosts.Length > 0)
        {
            foreach (PermaFertilityBoost permaboost in _permaboostFertilizerStacks)
            {
                if (currentPermaboosts.Contains(permaboost.Code))
                {
                    farmlandAttributes.SetInt("originalFertilityN", farmlandAttributes.GetInt("originalFertilityN") + permaboost.N);
                    farmlandAttributes.SetInt("originalFertilityP", farmlandAttributes.GetInt("originalFertilityP") + permaboost.P);
                    farmlandAttributes.SetInt("originalFertilityK", farmlandAttributes.GetInt("originalFertilityK") + permaboost.K);
                }
            }
        }
        return farmlandAttributes;
    }

    static public int GetUpgradedFertility(int originalFertility)
    {
        foreach (KeyValuePair<string, float> fertility in BlockEntityFarmland.Fertilities)
        {
            if ((int)fertility.Value > originalFertility)
                return (int)fertility.Value;
        }
        return originalFertility;
    }

    static public float DowngradeFertilizerOverlay(float overlay)
    {
        if (overlay > 100) overlay = 100;
        return MathF.Floor(overlay / 15);
    }

    private void UpgradeFarmland(Block block, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, int requiredBioChar)
    {
        // Inventory and world state changes must only run server-side
        if (world.Side != EnumAppSide.Server) return;

        var pos = blockSel.Position;
        var farmland = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFarmland;

        var farmlandAttributes = new TreeAttribute();
        farmland.ToTreeAttributes(farmlandAttributes);

        if (HasEnoughFertilizer(farmlandAttributes))
        {
            // Remove any permaboost original fertility modifiers
            farmlandAttributes = RemovePermaboosts(farmlandAttributes);

            // Cancel action if farmland is already base Terra Preta
            if (farmlandAttributes.GetInt("originalFertilityN") >= 80 &&
                farmlandAttributes.GetInt("originalFertilityP") >= 80 &&
                farmlandAttributes.GetInt("originalFertilityK") >= 80)
            {
                return;
            }

            // Spend biochar
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(requiredBioChar);

            // Upgrade original fertility values
            farmlandAttributes.SetInt("originalFertilityN", GetUpgradedFertility(farmlandAttributes.GetInt("originalFertilityN")));
            farmlandAttributes.SetInt("originalFertilityP", GetUpgradedFertility(farmlandAttributes.GetInt("originalFertilityP")));
            farmlandAttributes.SetInt("originalFertilityK", GetUpgradedFertility(farmlandAttributes.GetInt("originalFertilityK")));

            // Restore any permaboost original fertility modifiers
            farmlandAttributes = RestorePermaboosts(farmlandAttributes);

            // Subtract configured slowNPK values
            farmlandAttributes.SetFloat("slowN", farmlandAttributes.GetFloat("slowN") - ModConfig.configData.requiredN);
            farmlandAttributes.SetFloat("slowP", farmlandAttributes.GetFloat("slowP") - ModConfig.configData.requiredP);
            farmlandAttributes.SetFloat("slowK", farmlandAttributes.GetFloat("slowK") - ModConfig.configData.requiredK);

            // Reduce strength of all fertilizer visual overlays
            ITreeAttribute fertilizerOverlay = (ITreeAttribute)farmlandAttributes["fertilizerOverlayStrength"];
            if (fertilizerOverlay != null)
            {
                TreeAttribute overlayAttribute = new TreeAttribute();
                foreach (KeyValuePair<string, IAttribute> keyValuePair in (IEnumerable<KeyValuePair<string, IAttribute>>)fertilizerOverlay)
                {
                    overlayAttribute.SetFloat(keyValuePair.Key, DowngradeFertilizerOverlay(((ScalarAttribute<float>)(keyValuePair.Value as FloatAttribute)).value));
                }
                farmlandAttributes["fertilizerOverlayStrength"] = (IAttribute)overlayAttribute;
            }

            // Apply attribute changes
            farmland.FromTreeAttributes(farmlandAttributes, world);
            farmland.MarkDirty();

            try
            {
                world.PlaySoundAt(world.BlockAccessor.GetBlock(pos).Sounds.Hit, (double)pos.X + 0.5, (double)pos.Y + 0.75, (double)pos.Z + 0.5, byPlayer, true, 12f, 1f);
            }
            catch (Exception e)
            {
                this.Api.Logger.Debug(e.ToString());
            }

            this.Api.Logger.Debug(Lang.Get("farmlandnutrientmanagement:farmland-upgraded") + $"{farmland.OriginalFertility[0]}/{farmland.OriginalFertility[1]}/{farmland.OriginalFertility[2]}");
        }
    }

    private void DefaultBehavior(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        ItemStack heldItemstack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
        JsonObject attribute = heldItemstack?.Collectible?.Attributes?["bioCharFill"];
        if (attribute != null && attribute.AsFloat() > 0)
        {
            var requiredBiochar = (int)Math.Ceiling(ModConfig.configData.requiredBioChar / attribute.AsFloat());
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            if (IsValidFarmland(block.Code.GetName()))
            {
                if (heldItemstack.StackSize >= requiredBiochar)
                {
                    this.Api.Logger.Debug($"Biochar needed: {requiredBiochar}");
                    UpgradeFarmland(block, world, byPlayer, blockSel, requiredBiochar);
                    handling = EnumHandling.Handled;
                }
                else
                {
                    if (this.Api is ICoreClientAPI api)
                    {
                        api.TriggerIngameError((object)this, "low-biochar", String.Format(Lang.Get("farmlandnutrientmanagement:low-biochar"),
                            heldItemstack.GetName(), requiredBiochar));
                    }
                    handling = EnumHandling.PassThrough;
                }
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
        if (block is not null)
        {
            DefaultBehavior(world, byPlayer, blockSel, ref handling);
        }
        return true;
    }
}
