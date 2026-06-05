using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace JournalRecall.AI.SourceGeneration;

/// <summary>
/// Compile-time diagnostics for discoverable agents, surfacing misconfiguration as build errors
/// instead of startup exceptions (ADR-0004): duplicate agent names (PW0001) and a missing model
/// selection (PW0002).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentDefinitionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DuplicateName = new(
        "PW0001", "Duplicate agent name",
        "Agent name '{0}' is declared by more than one IAgent — names must be unique",
        "JournalRecall", DiagnosticSeverity.Error, isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor MissingModel = new(
        "PW0002", "Agent does not select a model",
        "Agent '{0}' does not call UsingModel(...) in Configure; the runner cannot resolve a model",
        "JournalRecall", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DuplicateName, MissingModel);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var namesToLocations = new ConcurrentDictionary<string, ConcurrentBag<Location>>();

            start.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                if (!IsConcreteAgent(type))
                    return;

                var configure = type.GetMembers(AgentContract.ConfigureMethod)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsStatic);

                if (configure is not null && !ConfigureSelectsModel(configure))
                    symbolContext.ReportDiagnostic(Diagnostic.Create(MissingModel, type.Locations[0], type.Name));

                var name = TryGetNameLiteral(type);
                if (name is not null)
                    namesToLocations.GetOrAdd(name, _ => []).Add(type.Locations[0]);
            }, SymbolKind.NamedType);

            start.RegisterCompilationEndAction(end =>
            {
                foreach (var pair in namesToLocations.Where(p => p.Value.Count > 1))
                    foreach (var location in pair.Value)
                        end.ReportDiagnostic(Diagnostic.Create(DuplicateName, location, pair.Key));
            });
        });
    }

    private static bool IsConcreteAgent(INamedTypeSymbol type) =>
        type is { IsAbstract: false, TypeKind: TypeKind.Class }
        && type.AllInterfaces.Any(i => i.ToDisplayString() == AgentContract.AgentInterface);

    private static bool ConfigureSelectsModel(IMethodSymbol configure)
    {
        foreach (var reference in configure.DeclaringSyntaxReferences)
        {
            var invocations = reference.GetSyntax().DescendantNodes().OfType<InvocationExpressionSyntax>();
            if (invocations.Any(inv => inv.Expression is MemberAccessExpressionSyntax member
                    && member.Name.Identifier.Text == AgentContract.UsingModelMethod))
                return true;
        }
        return false;
    }

    private static string? TryGetNameLiteral(INamedTypeSymbol type)
    {
        foreach (var property in type.GetMembers(AgentContract.NameProperty).OfType<IPropertySymbol>().Where(p => p.IsStatic))
        {
            foreach (var reference in property.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not PropertyDeclarationSyntax declaration)
                    continue;

                if (LiteralValue(declaration.ExpressionBody?.Expression) is { } direct)
                    return direct;

                var getter = declaration.AccessorList?.Accessors
                    .FirstOrDefault(a => a.Keyword.IsKind(SyntaxKind.GetKeyword));
                if (LiteralValue(getter?.ExpressionBody?.Expression) is { } arrow)
                    return arrow;

                var returned = getter?.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault()?.Expression;
                if (LiteralValue(returned) is { } body)
                    return body;
            }
        }
        return null;
    }

    private static string? LiteralValue(ExpressionSyntax? expression) =>
        expression is LiteralExpressionSyntax literal && literal.Token.Value is string value ? value : null;
}
