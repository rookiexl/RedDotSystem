using System.Collections.Generic;

namespace Game.RedDot
{
/// <summary>
/// 红点配置校验问题级别。
/// </summary>
public enum RedDotValidationSeverity
{
    Warning = 0,
    Error = 1,
}

/// <summary>
/// 单条红点配置校验问题。
/// </summary>
public readonly struct RedDotValidationIssue
{
    public string Code { get; }
    public string Message { get; }
    public RedDotValidationSeverity Severity { get; }

    public RedDotValidationIssue(
        RedDotValidationSeverity severity,
        string code,
        string message)
    {
        Severity = severity;
        Code = code;
        Message = message;
    }
}

/// <summary>
/// 一次红点配置校验的完整结果。
/// </summary>
public sealed class RedDotValidationReport
{
    private readonly List<RedDotValidationIssue> mIssues =
        new List<RedDotValidationIssue>();

    public IReadOnlyList<RedDotValidationIssue> Issues
    {
        get
        {
            return mIssues;
        }
    }

    public int ErrorCount
    {
        get
        {
            var count = 0;
            foreach(var issue in mIssues)
            {
                if(issue.Severity == RedDotValidationSeverity.Error)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public int WarningCount
    {
        get
        {
            var count = 0;
            foreach(var issue in mIssues)
            {
                if(issue.Severity == RedDotValidationSeverity.Warning)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public bool HasErrors
    {
        get
        {
            return ErrorCount > 0;
        }
    }

    internal void Add(
        RedDotValidationSeverity severity,
        string code,
        string message)
    {
        mIssues.Add(new RedDotValidationIssue(severity, code, message));
    }

    internal void AddRange(IEnumerable<RedDotValidationIssue> issues)
    {
        if(issues == null)
        {
            return;
        }
        mIssues.AddRange(issues);
    }
}
}
