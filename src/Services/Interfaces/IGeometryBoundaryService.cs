using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IGeometryBoundaryService
    {
        /// <summary>
        /// Extracts boundary CurveLoops from a Floor or Toposolid via its Sketch.Profile.
        /// Returns validated loops ordered outer-first with correct winding order.
        /// </summary>
        IList<CurveLoop> ExtractLoops(Element element);

        /// <summary>
        /// Ensures outer loops are CCW and void loops are CW relative to XYZ.BasisZ.
        /// Loop roles are determined by geometric containment depth, not raw sketch order.
        /// </summary>
        IList<CurveLoop> NormalizeWindingOrder(IList<CurveLoop> loops);

        /// <summary>
        /// Translates loops vertically so they all lie on one shared horizontal plane.
        /// This preserves XY geometry while removing per-loop Z drift from sketch extraction.
        /// </summary>
        IList<CurveLoop> AlignLoopsToCommonPlane(IList<CurveLoop> loops);
    }
}
