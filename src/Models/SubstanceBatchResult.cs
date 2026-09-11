using System.Collections.Generic;

namespace LECG.Models
{
    public sealed record SubstanceBatchOptions(BakeOptions Bake, TextureTransform Transform, bool OverwriteExisting);

    public enum SubstanceMaterialOutcome { Created, Updated, Skipped, Failed }

    public sealed record SubstanceMaterialReport(string DisplayName, SubstanceMaterialOutcome Outcome, string? Error);

    public sealed class SubstanceBatchResult
    {
        public int Created { get; private set; }
        public int Updated { get; private set; }
        public int Skipped { get; private set; }
        public int Failed { get; private set; }
        public List<SubstanceMaterialReport> Reports { get; } = new();

        public void Add(SubstanceMaterialReport report)
        {
            System.ArgumentNullException.ThrowIfNull(report);
            Reports.Add(report);
            switch (report.Outcome)
            {
                case SubstanceMaterialOutcome.Created: Created++; break;
                case SubstanceMaterialOutcome.Updated: Updated++; break;
                case SubstanceMaterialOutcome.Skipped: Skipped++; break;
                case SubstanceMaterialOutcome.Failed: Failed++; break;
            }
        }

        public string Summary => $"{Created} created, {Updated} updated, {Skipped} skipped, {Failed} failed";
    }

    public sealed record SubstanceTextureRepathResult(
        int Selected,
        int Repathed,
        int MissingInDocument,
        int Failed,
        int UpdatedBitmapPaths)
    {
        public string Summary =>
            $"{Repathed} repathed, {MissingInDocument} not in document, {Failed} failed, {UpdatedBitmapPaths} bitmap paths updated";
    }
}
