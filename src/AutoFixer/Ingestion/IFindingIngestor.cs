using AutoFixer.Models;

namespace AutoFixer.Ingestion;

public interface IFindingIngestor
{
    /// <summary>
    /// Ingest findings for the given repository. If <paramref name="inputFilePath"/> is non-null,
    /// the ingestor should prefer reading from that file (file-based ingestion) rather than calling
    /// a remote API.
    /// </summary>
    Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, string? inputFilePath = null, CancellationToken cancellationToken = default);
}
