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
        private readonly IAlignEdgesCurveDivisionService _alignEdgesCurveDivisionService;

        public AlignEdgesBoundaryPointService() : this(new AlignEdgesCurveHitService(new ReferenceRaycastService()), new AlignEdgesHitPointProjectionService(new ReferenceRaycastService()), new AlignEdgesCurveDivisionService())
        {
        }

        public AlignEdgesBoundaryPointService(IAlignEdgesCurveHitService alignEdgesCurveHitService, IAlignEdgesHitPointProjectionService alignEdgesHitPointProjectionService, IAlignEdgesCurveDivisionService alignEdgesCurveDivisionService)
        {
            _alignEdgesCurveHitService = alignEdgesCurveHitService;
            _alignEdgesHitPointProjectionService = alignEdgesHitPointProjectionService;
            _alignEdgesCurveDivisionService = alignEdgesCurveDivisionService;
        }

        public List<XYZ> CollectBoundaryHitPoints(
            Sketch sketch,
            ReferenceIntersector intersector,
            double minSpacing,
            double maxSpacing,
            Action<string>? debugLog = null)
        {
            ArgumentNullException.ThrowIfNull(sketch);
            ArgumentNullException.ThrowIfNull(intersector);

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

                        IReadOnlyList<double> parameters = _alignEdgesCurveDivisionService.GetInteriorParameters(length, minSpacing, maxSpacing);
                        if (parameters.Count > 0)
                        {
                            XYZ curveMid = curve.Evaluate(0.5, true);

                            foreach (double param in parameters)
                            {
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
