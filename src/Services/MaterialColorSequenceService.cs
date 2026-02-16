using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialColorSequenceService : IMaterialColorSequenceService
    {
        private int _colorIndex = 0;
        private static readonly Color[] ColorPalette = new Color[]
        {
            new Color(76, 175, 80), new Color(33, 150, 243), new Color(255, 193, 7), new Color(244, 67, 54),
            new Color(156, 39, 176), new Color(0, 188, 212), new Color(255, 87, 34), new Color(139, 195, 74),
            new Color(63, 81, 181), new Color(121, 85, 72), new Color(96, 125, 139), new Color(233, 30, 99),
        };

        public Color GetNextColor()
        {
            Color color = ColorPalette[_colorIndex % ColorPalette.Length];
            _colorIndex++;
            return color;
        }
    }
}
