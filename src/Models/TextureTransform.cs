namespace LECG.Models
{
    public sealed record TextureTransform(
        double ScaleXMillimeters,
        double ScaleYMillimeters,
        double OffsetXMillimeters,
        double OffsetYMillimeters,
        double RotationDegrees,
        bool LinkTransforms)
    {
        public const double MillimetersPerFoot = 304.8;

        public static TextureTransform Uniform(double sizeMillimeters) =>
            new(sizeMillimeters, sizeMillimeters, 0, 0, 0, true);

        public double ScaleXFeet => ScaleXMillimeters / MillimetersPerFoot;
        public double ScaleYFeet => ScaleYMillimeters / MillimetersPerFoot;
        public double OffsetXFeet => OffsetXMillimeters / MillimetersPerFoot;
        public double OffsetYFeet => OffsetYMillimeters / MillimetersPerFoot;
    }
}
