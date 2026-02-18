using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesBoundaryPointService : IAlignEdgesBoundaryPointService
    {
        private readonly IAlignEdgesCurveHitService _alignEdgesCurveHitService;
        private readonly IAlignEdgesHitPointProjectionService _alignEdgesHitPointProjectionService;

        public AlignEdgesBoundaryPointService() : this(new AlignEdgesCurveHitService(new ReferenceRaycastService()), new AlignEdgesHitPointProjectionService(new ReferenceRaycastService()))
        {
        }

        public AlignEdgesBoundaryPointService(IAlignEdgesCurveHitService alignEdgesCurveHitService, IAlignEdgesHitPointProjectionService alignEdgesHitPointProjectionService)
        {
            _alignEdgesCurveHitService = alignEdgesCurveHitService;
            _alignEdgesHitPointProjectionService = alignEdgesHitPointProjectionService;
        }

        public List<XYZ> CollectBoundaryHitPoints(
            Sketch sketch,
            ReferenceIntersector intersector,
            double minSpacing,
            double maxSpacing,
            Action<string>? debugLog = null)
        {
            List<XYZ> newPoints = new List<XYZ>();

            foreach (CurveArray curveArr in sketch.Profile)
            {
                foreach (Curve curve in curveArr)
                {
                    double length = curve.Length;

                    bool curveHitsReference = _alignEdgesCurveHitService.CurveHitsReference(intersector, curve);

                    if (curveHitsReference)
                    {
                        debugLog?.Invoke($"Curve len={length:F1}ft (arc/line)\n");

                        if (length > minSpacing)
                        {
                            int divisions = (int)Math.Ceiling(length / maxSpacing);
                            divisions = Math.Max(divisions, 2);

                            XYZ curveMid = curve.Evaluate(0.5, true);

                            for (int j = 1; j < divisions; j++)
                            {
                                double param = (double)j / divisions;
                                XYZ sketchPt = curve.Evaluate(param, true);

                                XYZ? hitPt = _alignEdgesHitPointProjectionService.ResolveHitPoint(intersector, sketchPt, curveMid);

                                if (hitPt != null)
                                {
                                    newPoints.Add(hitPt);
                                }
                            }
                        }
                    }
                }
            }

            return newPoints;
        }
    }
}
