/// <summary>
/// 负责全局游戏 Model 的生命周期。
/// </summary>
public sealed class GameDataModule : GameModuleBase
{
    public override string Name => "GameData";

    public override void Initialize()
    {
        GameModel.Singleton.Init();
    }

    public override void Dispose()
    {
        GameModel.Singleton.Dispose();
    }
}
