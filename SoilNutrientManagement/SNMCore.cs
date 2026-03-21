using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SoilNutrientManagement;

public class SNMCore : ModSystem
{
    public static ICoreAPI CoreAPI;
    public static ICoreClientAPI ClientAPI;
    public static ICoreServerAPI ServerAPI;
    // Called on server and client
    // Useful for registering block/entity classes on both sides
    public override void Start(ICoreAPI api)
    {
        CoreAPI = api;
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
}