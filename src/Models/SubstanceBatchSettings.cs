namespace LECG.Models
{
    public sealed class SubstanceBatchSettings
    {
        public const string FileName = "substance-batch.json";

        public string LibraryRoot { get; set; } = @"C:\LECG\SubstanceBakes";
        public string OutputRoot { get; set; } = string.Empty;
        public int TargetSize { get; set; } = 2048;
        public double SizeMillimeters { get; set; } = 2500;
        public bool OverwriteExisting { get; set; }
        public bool ForceRebake { get; set; }
    }
}
