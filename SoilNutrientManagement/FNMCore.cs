using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FarmlandNutrientManagement;

public class FNMCore : ModSystem
{
    public static ConfigData Config;

    // Called on server and client
    // Useful for registering block/entity classes on both sides
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

    //private void Event_PlayerJoin(IServerPlayer byPlayer, ICoreServerAPI api)
    //{
    //    api.Network.GetChannel("vsroofing").SendPacket<ConfigData>(FNMCore.Config, new IServerPlayer[1]
    //    {
    //  byPlayer
    //    });
    //}
}