using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FarmlandNutrientManagement;

public class FNMCore : ModSystem
{
    // Called on server and client
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockBehaviorClass("UpgradeFarmland", typeof(UpgradeFarmlandBehavior));
        api.Logger.Notification("Farmland Nutrient Management Mod: Started.");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        ModConfig.tryToLoadConfig(api);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ModConfig.tryToLoadConfig(api);
    }
}
