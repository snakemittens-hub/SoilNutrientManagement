using FarmlandNutrientManagement;
using System;
using Vintagestory.API.Common;

public class ModConfig
{
    public static ConfigData? configData;

    private static string configFile = "FarmlandNutrientManagementConfig.json";

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
            api.Logger.Error("Could not load config! Loading default settings instead.");
            api.Logger.Error(ex.Message);
            configData = new ConfigData();
        }
    }
}