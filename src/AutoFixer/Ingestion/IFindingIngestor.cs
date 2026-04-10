using AutoFixer.Models;

namespace AutoFixer.Ingestion;

public interface IFindingIngestor
{
    Task<IEnumerable<Finding>> IngestFindingsAsync(string repoName, CancellationToken cancellationToken = default);
}
