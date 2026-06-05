using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Tests.Fakes;

/// <summary>Small <see cref="AIFunction"/> fakes for runner tool-dispatch tests.</summary>
internal static class ToolFakes
{
    public static AIFunction Echo() =>
        AIFunctionFactory.Create((string text) => $"echo:{text}", "echo");

    public static AIFunction AlwaysThrows(string name = "boom") =>
        AIFunctionFactory.Create(
            (Func<string>)(() => throw new InvalidOperationException("tool exploded")), name);

    public static Dictionary<string, object?> TextArg(string text) => new() { ["text"] = text };
}
