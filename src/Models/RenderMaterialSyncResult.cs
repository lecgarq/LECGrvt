namespace LECG.Models
{
    public enum NormalMapSyncStatus
    {
        NotRequested = 0,
        Updated,
        AlreadyNormal,
        NoAppearanceAsset,
        NoBumpSlot,
        NoConnectedBumpAsset,
        NoWritableBumpTypeProperty,
        WriteFailed,
    }

    public sealed record RenderMaterialSyncResult(
        bool Changed,
        bool GraphicsChanged,
        NormalMapSyncStatus NormalMapStatus)
    {
        public bool NormalMapChanged => NormalMapStatus == NormalMapSyncStatus.Updated;

        public bool NormalMapNotApplicable => NormalMapStatus is
            NormalMapSyncStatus.NoAppearanceAsset or
            NormalMapSyncStatus.NoBumpSlot or
            NormalMapSyncStatus.NoConnectedBumpAsset;

        public bool NormalMapFailed => NormalMapStatus is
            NormalMapSyncStatus.NoWritableBumpTypeProperty or
            NormalMapSyncStatus.WriteFailed;
    }
}
