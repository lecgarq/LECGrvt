using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadCurveTessellationService : ICadCurveTessellationService
    {
        public IEnumerable<Curve>? Tessellate(Curve c)
        {
            IList<XYZ> points = c.Tessellate();
            if (points.Count < 2)
            {
                return null;
            }

            List<Curve> lines = new List<Curve>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                XYZ p1 = Flatten(points[i]);
                XYZ p2 = Flatten(points[i + 1]);
                if (!p1.IsAlmostEqualTo(p2))
                {
                    lines.Add(Line.CreateBound(p1, p2));
                }
            }

            return lines;
        }

        private XYZ Flatten(XYZ p)
        {
            return new XYZ(p.X, p.Y, 0);
        }
    }
}
