using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public class CadData
    {
        public List<Curve> Curves { get; } = new List<Curve>();
        public List<HatchData> Hatches { get; } = new List<HatchData>();
    }

    public class HatchData
    {
        public Color Color { get; set; } = new Color(0, 0, 0);
        public List<CurveLoop> Loops { get; set; } = new List<CurveLoop>();
    }
}
