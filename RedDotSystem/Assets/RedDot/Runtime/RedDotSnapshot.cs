namespace Game.RedDot
{
/// <summary>
/// 传给视图的不可变红点状态。
/// </summary>
public readonly struct RedDotSnapshot
{
    public static readonly RedDotSnapshot Empty = new RedDotSnapshot(string.Empty, 0, RedDotType.NONE);

    public string RedDotName { get; }
    public int Result { get; }
    public RedDotType DisplayType { get; }
    public bool IsVisible => Result > 0;
    public bool IsValid => !string.IsNullOrEmpty(RedDotName);

    public RedDotSnapshot(string redDotName, int result, RedDotType displayType)
    {
        RedDotName = redDotName;
        Result = result;
        DisplayType = displayType;
    }
}
}
