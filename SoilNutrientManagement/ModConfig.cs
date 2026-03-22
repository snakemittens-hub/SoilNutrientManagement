using SoilNutrientManagement;
using System;
using Vintagestory.API.Common;

public class ModConfig
{
    public static ConfigData configData;

    private static string configFile = "SoilNutrientManagementConfig.json";

    public static void tryToLoadConfig(ICoreAPI api)
    {
        try
        {
            configData = api.LoadModConfig<ConfigData>(configFile);
            if (configData == null)
            {
                configData = new ConfigData();
            }
            api.StoreModConfig<ConfigData>(configData, configFile);
        }
        catch (Exception ex)
        {
            SNMCore.CoreAPI.Logger.Error("Could not load config! Loading default settings instead.");
            SNMCore.CoreAPI.Logger.Error(ex.Message);
            configData = new ConfigData();
        }
    }
}