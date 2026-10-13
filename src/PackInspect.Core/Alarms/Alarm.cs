namespace PackInspect.Core.Alarms;

/// <summary>A fault the operator must clear and acknowledge with Reset.</summary>
public sealed record Alarm(string Code, string Message, DateTimeOffset RaisedAt);
