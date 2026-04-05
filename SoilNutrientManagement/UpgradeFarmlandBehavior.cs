using Newtonsoft.Json.Linq;
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
    public const string FertilizerHighPropsCacheKey = "UpgradeFarmlandBehavior.fertilizerHighPropsDict";
    private static Dictionary<string, string> _fertilizerHighPropsDict = new Dictionary<string, string>();
    public override void OnLoaded(ICoreAPI api){ 
        this.Api = api;

        _permaboostFertilizerStacks = ObjectCacheUtil.GetOrCreate(api, PermaboostFertilizersCacheKey, 
            (CreateCachableObjectDelegate<PermaFertilityBoost[]>)(() => GetPermaboostFertilizers(api))).ToArray();
        _fertilizerHighPropsDict = ObjectCacheUtil.GetOrCreate(api, FertilizerHighPropsCacheKey,
            (CreateCachableObjectDelegate<Dictionary<string, string>>)(() => GetFertilizerProps(api)));
    }
    public UpgradeFarmlandBehavior(Block block) : base(block){}

    public static PermaFertilityBoost[] GetPermaboostFertilizers(ICoreAPI api)
    {
        List<PermaFertilityBoost> permaboostFertilizers = [];

        foreach(var collObj in api.World.Collectibles.Where(c => c.Code != null))
        {
            JsonObject attribute = collObj?.Attributes?["fertilizerProps"];
            if (attribute == null || !attribute.Exists) continue;
            FertilizerProps fertilizerProps = attribute.AsObject<FertilizerProps>((FertilizerProps) null);
            if (fertilizerProps == null) continue;
            if (fertilizerProps.PermaBoost != null)
            {
                api.Logger.Debug("Found permaboost: " + fertilizerProps.PermaBoost.Code);
                permaboostFertilizers.Add(fertilizerProps.PermaBoost);
            }
        }
        api.Logger.Debug($"Found {permaboostFertilizers.ToArray().Length} fertility permaboosts");
        return permaboostFertilizers.ToArray();
    }

    public static Dictionary<string, string> GetFertilizerProps(ICoreAPI api)
    {
        //var fertPropsDict = new Dictionary<string, FertilizerProps>();
        var fertHighPropsDict = new Dictionary<string, string>();

        foreach (var collObj in api.World.Collectibles.Where(c => c.Code != null))
        {
            JsonObject attribute = collObj?.Attributes?["fertilizerProps"];
            if (attribute == null || !attribute.Exists) continue;
            FertilizerProps fertilizerProps = attribute.AsObject<FertilizerProps>((FertilizerProps)null);
            if (fertilizerProps == null) continue;
            else
            {
                api.Logger.Debug("Found fertilizer: " + collObj.Code);
                //fertPropsDict.Add(collObj.Code, fertilizerProps);
                if (fertilizerProps.N >= fertilizerProps.P && fertilizerProps.N >= fertilizerProps.K)
                {
                    fertHighPropsDict.Add(collObj.Code, "N");
                    api.Logger.Debug("Highest nutrient: N");
                }
                else if(fertilizerProps.P >= fertilizerProps.N && fertilizerProps.P >= fertilizerProps.K)
                {
                    fertHighPropsDict.Add(collObj.Code, "P");
                    api.Logger.Debug("Highest nutrient: P");
                }
                else if (fertilizerProps.K >= fertilizerProps.P && fertilizerProps.K >= fertilizerProps.N)
                {
                    fertHighPropsDict.Add(collObj.Code, "K");
                    api.Logger.Debug("Highest nutrient: K");
                }

            }
        }
        api.Logger.Debug($"Found {fertHighPropsDict.Count} fertilizers");
        return fertHighPropsDict;
    }
    static public bool IsValidFarmland(string name)
    {
        if (name.StartsWith("farmland-moist-"))
        {
            return true;
        }
        return false;
    }

    public bool HasEnoughFertilizer(TreeAttribute farmlandAttributes)
    {
        if (farmlandAttributes.HasAttribute("slowN") && farmlandAttributes.HasAttribute("slowP") && farmlandAttributes.HasAttribute("slowK"))
        {
            if (farmlandAttributes.GetFloat("slowN") >= ModConfig.configData.requiredN &&
                farmlandAttributes.GetFloat("slowP") >= ModConfig.configData.requiredP &&
                farmlandAttributes.GetFloat("slowK") >= ModConfig.configData.requiredK)
            {
                if (ModConfig.configData.limitP && farmlandAttributes.GetFloat("slowP") > ModConfig.configData.excessP)
                {
                    if (this.Api is ICoreClientAPI api)
                    {       
                        api.TriggerIngameError((object)this, "high-p", Lang.Get("farmlandnutrientmanagement:high-p", Array.Empty<object>()));
                    }
                    return false;
                }
                return true;
            }
            else
            {
                //user feedback for which fertilizers to add
                if (this.Api is ICoreClientAPI api)
                {
                    if (farmlandAttributes.GetFloat("slowN") < ModConfig.configData.requiredN)
                    {
                        api.TriggerIngameError((object)this, "low-n", Lang.Get("farmlandnutrientmanagement:low-n", Array.Empty<object>()));
                    }
                    else if (farmlandAttributes.GetFloat("slowP") < ModConfig.configData.requiredP)
                    {
                        api.TriggerIngameError((object)this, "low-p", Lang.Get("farmlandnutrientmanagement:low-p", Array.Empty<object>()));
                    }
                    else if (farmlandAttributes.GetFloat("slowK") < ModConfig.configData.requiredK)
                    {
                        api.TriggerIngameError((object)this, "low-k", Lang.Get("farmlandnutrientmanagement:low-k", Array.Empty<object>()));
                    }
                }
            }
        }
        return false;
    }

    public TreeAttribute RemovePermaboosts(TreeAttribute farmlandAttributes)
    {
        var currentPermaboosts = farmlandAttributes.GetStringArray("permaBoosts");
        if (currentPermaboosts.Length > 0) {
            foreach (PermaFertilityBoost permaboost in _permaboostFertilizerStacks)
            {
                if (currentPermaboosts.Contains(permaboost.Code)){
                    //this.Api.Logger.Debug($"Permaboost code: {permaboost.Code} - {permaboost.N} {permaboost.P} {permaboost.K}");
                    farmlandAttributes.SetInt("originalFertilityN", farmlandAttributes.GetInt("originalFertilityN") - permaboost.N);
                    farmlandAttributes.SetInt("originalFertilityP", farmlandAttributes.GetInt("originalFertilityP") - permaboost.P);
                    farmlandAttributes.SetInt("originalFertilityK", farmlandAttributes.GetInt("originalFertilityK") - permaboost.K);
                    //this.Api.Logger.Debug($"{farmlandAttributes.GetInt("originalFertilityN")} {farmlandAttributes.GetInt("originalFertilityP")} {farmlandAttributes.GetInt("originalFertilityK")}");
                }
            }
        }
        return farmlandAttributes;
    } 

    public TreeAttribute RestorePermaboosts(TreeAttribute farmlandAttributes)
    {
        var currentPermaboosts = farmlandAttributes.GetStringArray("permaBoosts");
        if (currentPermaboosts.Length > 0)
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
                return (int) fertility.Value;
        }
        return originalFertility;
    }

    public TreeAttribute DowngradeFertilizerOverlay(TreeAttribute farmlandAttributes)
    {    
        //get fertilizer overlay strengths
        ITreeAttribute fertilizerOverlay = (ITreeAttribute)farmlandAttributes["fertilizerOverlayStrength"];
        if (fertilizerOverlay != null)
        {
            var fertilizerOverlayStrength = new Dictionary<string, float>();
            foreach (KeyValuePair<string, IAttribute> keyValuePair in (IEnumerable<KeyValuePair<string, IAttribute>>)fertilizerOverlay)
            {
                //this.Api.Logger.Debug($"FertOverlay {keyValuePair.Key}: {keyValuePair.Value}");
                foreach (KeyValuePair<string, string> storedFertilizer in _fertilizerHighPropsDict)
                {
                    if (storedFertilizer.Key.Contains(keyValuePair.Key))
                    {
                        float oldFert = farmlandAttributes.GetFloat("slow" + storedFertilizer.Value);
                        float newFert = 0;
                        switch (storedFertilizer.Value)
                        {
                            case "N":
                                newFert = oldFert - ModConfig.configData.requiredN;
                                break;
                            case "P":
                                newFert = oldFert - ModConfig.configData.requiredP;
                                break;
                            case "K":
                                newFert = oldFert - ModConfig.configData.requiredK;
                                break;
                        }
                        float fertRatio = newFert / oldFert;
                        fertilizerOverlayStrength[keyValuePair.Key] = ((ScalarAttribute<float>)(keyValuePair.Value as FloatAttribute)).value * fertRatio;
                    }
                }
            }

            //apply new fertilizer overlay strengths to farmAttributes
            TreeAttribute overlayAttribute = new TreeAttribute();
            farmlandAttributes["fertilizerOverlayStrength"] = (IAttribute)overlayAttribute;
            foreach (KeyValuePair<string, float> keyValuePair in fertilizerOverlayStrength)
                overlayAttribute.SetFloat(keyValuePair.Key, keyValuePair.Value);
        }
        return farmlandAttributes;
    }

    private void UpgradeFarmland(Block block, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, int requiredBioChar)
    {
        var pos = blockSel.Position;
        var farmland = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFarmland;

        var farmlandAttributes = new TreeAttribute();
        farmland.ToTreeAttributes(farmlandAttributes);

        if (HasEnoughFertilizer(farmlandAttributes))
        {
            //Remove any permaboost original fertility modifiers
            farmlandAttributes = RemovePermaboosts(farmlandAttributes);

            //Cancel action if farmland is already base Terra Preta
            if (farmlandAttributes.GetInt("originalFertilityN") >= 80 && farmlandAttributes.GetInt("originalFertilityP") >= 80 && farmlandAttributes.GetInt("originalFertilityK") >= 80)
            {
                if (this.Api is ICoreClientAPI api2)
                {
                    api2.TriggerIngameError((object)this, "max-fert", Lang.Get("farmlandnutrientmanagement:max-fert", Array.Empty<object>()));
                }
                return;
            }

            //spend biochar
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(requiredBioChar);

            //upgrade original fertility values
            farmlandAttributes.SetInt("originalFertilityN", GetUpgradedFertility(farmlandAttributes.GetInt("originalFertilityN")));
            farmlandAttributes.SetInt("originalFertilityP", GetUpgradedFertility(farmlandAttributes.GetInt("originalFertilityP")));
            farmlandAttributes.SetInt("originalFertilityK", GetUpgradedFertility(farmlandAttributes.GetInt("originalFertilityK")));

            //Restore any permaboost original fertility modifiers
            farmlandAttributes = RestorePermaboosts(farmlandAttributes);

            //Reduce strength of all fertilizer visual overlays
            farmlandAttributes = DowngradeFertilizerOverlay(farmlandAttributes);

            //Subtract configured slowNPK values
            farmlandAttributes.SetFloat("slowN", farmlandAttributes.GetFloat("slowN") - ModConfig.configData.requiredN);
            farmlandAttributes.SetFloat("slowP", farmlandAttributes.GetFloat("slowP") - ModConfig.configData.requiredP);
            farmlandAttributes.SetFloat("slowK", farmlandAttributes.GetFloat("slowK") - ModConfig.configData.requiredK);

            //apply attribute changes
            farmland.FromTreeAttributes(farmlandAttributes, world);
            farmland.MarkDirty();

            try
            {
                world.PlaySoundAt(world.BlockAccessor.GetBlock(pos).Sounds.Hit, (double)pos.X + 0.5, (double)pos.Y + 0.75, (double)pos.Z + 0.5, byPlayer, true, 12f, 1f);
                //long randomsound = world.Rand.Next(1, 4);
                //world.PlaySoundAt(new AssetLocation("sounds/block/dirt" + randomsound), (double)pos.X + 0.5, (double)pos.Y + 0.75, (double)pos.Z + 0.5, byPlayer, true, 12f, 1f);
            }
            catch (Exception e) 
                { this.Api.Logger.Debug(e.ToString()); }

            if (this.Api is ICoreClientAPI api)
            {
                api.TriggerChatMessage(Lang.Get("farmlandnutrientmanagement:farmland-upgraded") + $"{ farmland.OriginalFertility[0]}/{ farmland.OriginalFertility[1]}/{ farmland.OriginalFertility[2]}");
            }
            this.Api.Logger.Debug(Lang.Get("farmlandnutrientmanagement:farmland-upgraded") + $"{farmland.OriginalFertility[0]}/{farmland.OriginalFertility[1]}/{farmland.OriginalFertility[2]}");


        }
    }

    private void DefaultBehavior(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        ItemStack heldItemstack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
        JsonObject attribute = heldItemstack?.Collectible?.Attributes?["bioCharFill"];
        if (attribute != null && attribute.AsFloat() > 0)// || attribute.Exists)
        {
            var requiredBiochar = (int) Math.Ceiling(ModConfig.configData.requiredBioChar / attribute.AsFloat());
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
        if(block is not null)
        {
            DefaultBehavior(world, byPlayer, blockSel, ref handling);
        }
        return true;
    }
}
