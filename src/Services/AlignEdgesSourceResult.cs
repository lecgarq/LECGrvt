using Autodesk.Revit.DB;

namespace LECG.Services
{
    public enum AlignEdgesSourceStatus
    {
        Aligned,
        PartiallyAligned,
        NoValidTargets,
        Failed
    }

    public sealed record AlignEdgesSourceResult(
        ElementId SourceElementId,
        string SourceName,
        AlignEdgesSourceStatus Status,
        int InsertedPointCount,
        int MovedVertexCount,
        int SkippedVertexCount,
        int MissCount,
        string? FailureMessage = null)
    {
        public static AlignEdgesSourceResult FromCounts(
            ElementId sourceElementId,
            string sourceName,
            int boundaryHitCount,
            int insertedPointCount,
            int movedVertexCount,
            int skippedVertexCount,
            int missCount)
        {
            bool changed = insertedPointCount > 0 || movedVertexCount > 0;
            bool hadResolvedTargets = changed || skippedVertexCount > 0 || boundaryHitCount > 0;
            AlignEdgesSourceStatus status = hadResolvedTargets
                ? (missCount > 0 ? AlignEdgesSourceStatus.PartiallyAligned : AlignEdgesSourceStatus.Aligned)
                : AlignEdgesSourceStatus.NoValidTargets;

            return new AlignEdgesSourceResult(
                sourceElementId,
                sourceName,
                status,
                insertedPointCount,
                movedVertexCount,
                skippedVertexCount,
                missCount);
        }

        public static AlignEdgesSourceResult Failed(ElementId sourceElementId, string sourceName, string failureMessage)
        {
            return new AlignEdgesSourceResult(
                sourceElementId,
                sourceName,
                AlignEdgesSourceStatus.Failed,
                0,
                0,
                0,
                0,
                failureMessage);
        }
    }
}
