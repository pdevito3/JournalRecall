using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace JournalRecall.Api.Databases;

/// <summary>
/// Attaches a stable operation name and the C# call site to an EF query. EF writes both as SQL
/// comments, and <c>EfCommandEnrichment</c> promotes them into the OpenTelemetry span. Without this,
/// every database span in a trace reads as a bare table name and you cannot tell which feature slice
/// issued it.
/// </summary>
public static class QueryTagging
{
    /// <summary>
    /// Tags a query with <paramref name="operationName"/> plus the calling member, file, and line.
    /// </summary>
    /// <param name="source">The query to tag.</param>
    /// <param name="operationName">
    /// A stable dotted name in <c>area.operation</c> form, for example <c>sessions.get_by_id</c>.
    /// Keep it constant across refactors so dashboards and alerts stay valid.
    /// </param>
    public static IQueryable<T> TagWithOperationCallSite<T>(
        this IQueryable<T> source,
        string operationName,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        source
            .TagWith($"JournalRecall.Query: {operationName}; Member: {memberName}")
            .TagWithCallSite(filePath, lineNumber);
}
