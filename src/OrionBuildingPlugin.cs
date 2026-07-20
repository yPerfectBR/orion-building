using Orion.Gameplay;
using Orion.PluginContracts;

namespace OrionBuilding;

public sealed class OrionBuildingPlugin : IOrionPlugin
{
    public string Id => "orion:building";

    public Version Version { get; } = new(1, 0, 0);

    public void Load(IPluginLoadContext context) => _ = context;

    public void OnEnable(IPluginContext context)
    {
        BuildingGameplayServices services = new();
        context.Services.Register<IBuildingApi>(services, this);
        context.Services.Register<IPlayerBlockUseHandler>(services, this);
    }

    public void OnWorldInitialize(IWorldInitContext context) => _ = context;

    public void OnDisable(IPluginContext context) => _ = context;
}
