using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesBoundaryPointService : IAlignEdgesBoundaryPointService
    {
        private readonly IReferenceRaycastService _referenceRaycastService;

        public AlignEdgesBoundaryPointService() : this(new ReferenceRaycastService())
        {
        }

        public AlignEdgesBoundaryPointService(IReferenceRaycastService referenceRaycastService)
        {
            _referenceRaycastService = referenceRaycastService;
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

                    bool curveHitsReference = false;
                    for (int sample = 0; sample <= 8; sample++)
                    {
                        double sampleT = sample / 8.0;
                        XYZ samplePt = curve.Evaluate(sampleT, true);
                        if (_referenceRaycastService.CheckHitsReference(intersector, samplePt))
                        {
                            curveHitsReference = true;
                            break;
                        }
                    }

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

                                XYZ? hitPt = _referenceRaycastService.GetHitPoint(intersector, sketchPt);

                                if (hitPt == null)
                                {
                                    XYZ dir = (curveMid - sketchPt).Normalize();
                                    for (double offset = 0.5; offset <= 1.5 && hitPt == null; offset += 0.5)
                                    {
                                        XYZ testPt = sketchPt.Add(dir.Multiply(offset));
                                        hitPt = _referenceRaycastService.GetHitPoint(intersector, testPt);
                                        if (hitPt != null)
                                        {
                                            hitPt = new XYZ(sketchPt.X, sketchPt.Y, hitPt.Z);
                                        }
                                    }
                                }

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
