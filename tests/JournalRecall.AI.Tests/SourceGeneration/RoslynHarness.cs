using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using JournalRecall.AI.SourceGeneration;

namespace JournalRecall.AI.Tests.SourceGeneration;

internal static class RoslynHarness
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
        .ToArray();

    private static CSharpCompilation Compile(string source) => CSharpCompilation.Create(
        "RoslynHarness.Tests",
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
        References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Runs the catalog generator and returns the generated source text.</summary>
    public static string RunGenerator(string source)
    {
        var driver = CSharpGeneratorDriver.Create(new AgentCatalogGenerator().AsSourceGenerator());
        driver.RunGenerators(Compile(source)).GetRunResult();
        var result = driver.RunGenerators(Compile(source)).GetRunResult();
        return string.Concat(result.GeneratedTrees.Select(t => t.ToString()));
    }

    /// <summary>Runs the analyzer and returns its diagnostics.</summary>
    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var withAnalyzers = Compile(source)
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new AgentDefinitionAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
