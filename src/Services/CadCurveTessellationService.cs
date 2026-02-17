using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadCurveTessellationService : ICadCurveTessellationService
    {
        private readonly ICadPointFlattenService _cadPointFlattenService;

        public CadCurveTessellationService() : this(new CadPointFlattenService())
        {
        }

        public CadCurveTessellationService(ICadPointFlattenService cadPointFlattenService)
        {
            _cadPointFlattenService = cadPointFlattenService;
        }

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
                XYZ p1 = _cadPointFlattenService.Flatten(points[i]);
                XYZ p2 = _cadPointFlattenService.Flatten(points[i + 1]);
                if (!p1.IsAlmostEqualTo(p2))
                {
                    lines.Add(Line.CreateBound(p1, p2));
                }
            }

            return lines;
        }
    }
}
