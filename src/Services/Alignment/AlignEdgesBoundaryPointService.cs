using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesBoundaryPointService : IAlignEdgesBoundaryPointService
    {
        private const double RefinementZThreshold = 0.1;
        private const double ProfileDeviationTolerance = 0.03;
        private const double PointMergeTolerance = 0.01;
        private const double ParameterEpsilon = 1e-6;
        private const int MaxRefinementDepth = 8;
        private static readonly double[] HiddenTransitionProbeFractions = { 0.25, 0.5, 0.75 };

        private readonly IAlignEdgesHitPointProjectionService _alignEdgesHitPointProjectionService;
        private readonly IAlignEdgesCurveDivisionService _alignEdgesCurveDivisionService;

        public AlignEdgesBoundaryPointService(IAlignEdgesHitPointProjectionService alignEdgesHitPointProjectionService, IAlignEdgesCurveDivisionService alignEdgesCurveDivisionService)
        {
            _alignEdgesHitPointProjectionService = alignEdgesHitPointProjectionService;
            _alignEdgesCurveDivisionService = alignEdgesCurveDivisionService;
        }

        public List<XYZ> CollectBoundaryHitPoints(
            IEnumerable<CurveLoop> loops,
            ReferenceIntersector intersector,
            double minSpacing,
            double maxSpacing,
            Action<string>? debugLog = null)
        {
            ArgumentNullException.ThrowIfNull(loops);
            ArgumentNullException.ThrowIfNull(intersector);

            List<XYZ> newPoints = new List<XYZ>();

            foreach (CurveLoop loop in loops)
            {
                XYZ loopGuidePoint = ComputeLoopGuidePoint(loop);

                foreach (Curve curve in loop)
                {
                    IReadOnlyList<BoundarySample> samples = CollectAdaptiveSamples(curve, loopGuidePoint, intersector, minSpacing, maxSpacing);
                    if (samples.Count == 0)
                    {
                        continue;
                    }

                    IReadOnlyList<BoundarySample> necessarySamples = SelectNecessarySamples(samples, curve.Length, minSpacing);
                    debugLog?.Invoke($"Curve len={curve.Length:F1}ft kept {necessarySamples.Count} necessary samples.\n");

                    foreach (BoundarySample sample in necessarySamples)
                    {
                        if (sample.Hit != null && IsDistinctPoint(newPoints, sample.Hit.Value.Point))
                        {
                            newPoints.Add(sample.Hit.Value.Point);
                        }
                    }
                }
            }

            return newPoints;
        }

        private IReadOnlyList<BoundarySample> CollectAdaptiveSamples(Curve curve, XYZ loopGuidePoint, ReferenceIntersector intersector, double minSpacing, double maxSpacing)
        {
            double length = curve.Length;
            XYZ curveMid = curve.Evaluate(0.5, true);

            var samples = new SortedDictionary<double, BoundarySample>();
            foreach (double parameter in BuildInitialParameters(length, minSpacing, maxSpacing))
            {
                samples[parameter] = EvaluateSample(curve, parameter, curveMid, loopGuidePoint, intersector);
            }

            double[] parameters = samples.Keys.ToArray();
            for (int i = 0; i < parameters.Length - 1; i++)
            {
                RefineSegment(curve, curveMid, loopGuidePoint, intersector, minSpacing, parameters[i], parameters[i + 1], samples, 0);
            }

            return samples.Values.ToList();
        }

        private void RefineSegment(
            Curve curve,
            XYZ curveMid,
            XYZ loopGuidePoint,
            ReferenceIntersector intersector,
            double minSpacing,
            double startParameter,
            double endParameter,
            SortedDictionary<double, BoundarySample> samples,
            int depth)
        {
            if (depth >= MaxRefinementDepth)
            {
                return;
            }

            BoundarySample startSample = samples[startParameter];
            BoundarySample endSample = samples[endParameter];
            double chordLength = startSample.CurvePoint.DistanceTo(endSample.CurvePoint);

            if (chordLength <= minSpacing)
            {
                return;
            }

            if (!ShouldRefineSpan(curve, curveMid, loopGuidePoint, intersector, startParameter, endParameter, startSample, endSample, samples))
            {
                return;
            }

            double midParameter = (startParameter + endParameter) * 0.5;
            if (midParameter - startParameter <= ParameterEpsilon || endParameter - midParameter <= ParameterEpsilon)
            {
                return;
            }

            if (!samples.ContainsKey(midParameter))
            {
                samples[midParameter] = EvaluateSample(curve, midParameter, curveMid, loopGuidePoint, intersector);
            }

            RefineSegment(curve, curveMid, loopGuidePoint, intersector, minSpacing, startParameter, midParameter, samples, depth + 1);
            RefineSegment(curve, curveMid, loopGuidePoint, intersector, minSpacing, midParameter, endParameter, samples, depth + 1);
        }

        private IReadOnlyList<double> BuildInitialParameters(double length, double minSpacing, double maxSpacing)
        {
            var parameters = new SortedSet<double> { 0.0, 0.5, 1.0 };

            if (length > maxSpacing)
            {
                foreach (double parameter in _alignEdgesCurveDivisionService.GetInteriorParameters(length, minSpacing, maxSpacing))
                {
                    parameters.Add(parameter);
                }
            }

            return parameters.ToList();
        }

        private IReadOnlyList<BoundarySample> SelectNecessarySamples(IReadOnlyList<BoundarySample> samples, double length, double minSpacing)
        {
            if (samples.Count <= 2)
            {
                return Array.Empty<BoundarySample>();
            }

            var selectedIndices = new SortedSet<int>();
            CompressSolvedBoundary(samples, 0, samples.Count - 1, selectedIndices);
            return selectedIndices.Select(index => samples[index]).ToList();
        }

        private BoundarySample EvaluateSample(Curve curve, double parameter, XYZ curveMid, XYZ loopGuidePoint, ReferenceIntersector intersector)
        {
            XYZ curvePoint = curve.Evaluate(parameter, true);
            XYZ guidePoint = BlendGuidePoint(curvePoint, curveMid, loopGuidePoint);
            AlignEdgesHitInfo? hit = _alignEdgesHitPointProjectionService.ResolveHit(intersector, curvePoint, guidePoint);
            return new BoundarySample(parameter, curvePoint, hit);
        }

        private static bool HasLocalTransition(IReadOnlyList<BoundarySample> samples, int index)
        {
            return HasSampleTransition(samples[index - 1], samples[index])
                || HasSampleTransition(samples[index], samples[index + 1]);
        }

        private static bool HasProfileDeviation(IReadOnlyList<BoundarySample> samples, int index)
        {
            BoundarySample previous = samples[index - 1];
            BoundarySample current = samples[index];
            BoundarySample next = samples[index + 1];

            if (previous.Hit == null || current.Hit == null || next.Hit == null)
            {
                return false;
            }

            double parameterSpan = next.Parameter - previous.Parameter;
            if (parameterSpan <= ParameterEpsilon)
            {
                return false;
            }

            double ratio = (current.Parameter - previous.Parameter) / parameterSpan;
            double expectedZ = previous.Hit.Value.Point.Z + (next.Hit.Value.Point.Z - previous.Hit.Value.Point.Z) * ratio;
            return Math.Abs(current.Hit.Value.Point.Z - expectedZ) > ProfileDeviationTolerance;
        }

        private static bool HasSampleTransition(BoundarySample left, BoundarySample right)
        {
            if ((left.Hit == null) != (right.Hit == null))
            {
                return true;
            }

            if (left.Hit == null || right.Hit == null)
            {
                return false;
            }

            if (left.Hit.Value.ElementId != right.Hit.Value.ElementId)
            {
                return true;
            }

            return Math.Abs(left.Hit.Value.Point.Z - right.Hit.Value.Point.Z) > RefinementZThreshold;
        }

        private void CompressSolvedBoundary(
            IReadOnlyList<BoundarySample> samples,
            int startIndex,
            int endIndex,
            SortedSet<int> selectedIndices)
        {
            if (endIndex - startIndex <= 1)
            {
                return;
            }

            int splitIndex = FindSplitIndex(samples, startIndex, endIndex);
            if (splitIndex < 0)
            {
                return;
            }

            selectedIndices.Add(splitIndex);
            CompressSolvedBoundary(samples, startIndex, splitIndex, selectedIndices);
            CompressSolvedBoundary(samples, splitIndex, endIndex, selectedIndices);
        }

        private static int FindSplitIndex(IReadOnlyList<BoundarySample> samples, int startIndex, int endIndex)
        {
            int transitionIndex = FindTargetTransitionSplitIndex(samples, startIndex, endIndex);
            if (transitionIndex >= 0)
            {
                return transitionIndex;
            }

            return FindResidualSplitIndex(samples, startIndex, endIndex);
        }

        private static int FindTargetTransitionSplitIndex(IReadOnlyList<BoundarySample> samples, int startIndex, int endIndex)
        {
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                if (HasTargetIdentityTransition(samples[i - 1], samples[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindResidualSplitIndex(IReadOnlyList<BoundarySample> samples, int startIndex, int endIndex)
        {
            BoundarySample start = samples[startIndex];
            BoundarySample end = samples[endIndex];

            if (start.Hit == null || end.Hit == null)
            {
                return -1;
            }

            double parameterSpan = end.Parameter - start.Parameter;
            if (parameterSpan <= ParameterEpsilon)
            {
                return -1;
            }

            double maxResidual = ProfileDeviationTolerance;
            int splitIndex = -1;

            for (int i = startIndex + 1; i < endIndex; i++)
            {
                BoundarySample sample = samples[i];
                if (sample.Hit == null)
                {
                    continue;
                }

                double ratio = (sample.Parameter - start.Parameter) / parameterSpan;
                double expectedZ = start.Hit.Value.Point.Z + (end.Hit.Value.Point.Z - start.Hit.Value.Point.Z) * ratio;
                double residual = Math.Abs(sample.Hit.Value.Point.Z - expectedZ);

                if (residual > maxResidual)
                {
                    maxResidual = residual;
                    splitIndex = i;
                }
            }

            return splitIndex;
        }

        private static bool HasTargetIdentityTransition(BoundarySample left, BoundarySample right)
        {
            if ((left.Hit == null) != (right.Hit == null))
            {
                return true;
            }

            if (left.Hit == null || right.Hit == null)
            {
                return false;
            }

            return left.Hit.Value.ElementId != right.Hit.Value.ElementId;
        }

        private bool ShouldRefineSpan(
            Curve curve,
            XYZ curveMid,
            XYZ loopGuidePoint,
            ReferenceIntersector intersector,
            double startParameter,
            double endParameter,
            BoundarySample startSample,
            BoundarySample endSample,
            SortedDictionary<double, BoundarySample> samples)
        {
            if (HasSampleTransition(startSample, endSample))
            {
                return true;
            }

            double span = endParameter - startParameter;
            if (span <= ParameterEpsilon * 10)
            {
                return false;
            }

            foreach (double fraction in HiddenTransitionProbeFractions)
            {
                double parameter = startParameter + span * fraction;
                if (parameter - startParameter <= ParameterEpsilon || endParameter - parameter <= ParameterEpsilon)
                {
                    continue;
                }

                BoundarySample probe = GetOrCreateSample(curve, curveMid, loopGuidePoint, intersector, parameter, samples);
                if (HasHiddenTransition(startSample, endSample, probe))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDistinctPoint(IEnumerable<XYZ> existingPoints, XYZ candidate)
        {
            foreach (XYZ existingPoint in existingPoints)
            {
                if (existingPoint.DistanceTo(candidate) <= PointMergeTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private BoundarySample GetOrCreateSample(
            Curve curve,
            XYZ curveMid,
            XYZ loopGuidePoint,
            ReferenceIntersector intersector,
            double parameter,
            SortedDictionary<double, BoundarySample> samples)
        {
            if (!samples.TryGetValue(parameter, out BoundarySample sample))
            {
                sample = EvaluateSample(curve, parameter, curveMid, loopGuidePoint, intersector);
                samples[parameter] = sample;
            }

            return sample;
        }

        private static XYZ ComputeLoopGuidePoint(CurveLoop loop)
        {
            double sumX = 0.0;
            double sumY = 0.0;
            double sumZ = 0.0;
            int count = 0;

            foreach (Curve curve in loop)
            {
                IList<XYZ> tessellated = curve.Tessellate();
                foreach (XYZ point in tessellated)
                {
                    sumX += point.X;
                    sumY += point.Y;
                    sumZ += point.Z;
                    count++;
                }
            }

            if (count == 0)
            {
                Curve firstCurve = loop.First();
                return firstCurve.Evaluate(0.5, true);
            }

            return new XYZ(sumX / count, sumY / count, sumZ / count);
        }

        private static XYZ BlendGuidePoint(XYZ curvePoint, XYZ curveMid, XYZ loopGuidePoint)
        {
            double curveMidDistance = curvePoint.DistanceTo(curveMid);
            double loopGuideDistance = curvePoint.DistanceTo(loopGuidePoint);
            double totalDistance = curveMidDistance + loopGuideDistance;

            if (totalDistance <= ParameterEpsilon)
            {
                return curveMid;
            }

            double loopWeight = curveMidDistance / totalDistance;
            double curveWeight = 1.0 - loopWeight;

            return new XYZ(
                curveMid.X * curveWeight + loopGuidePoint.X * loopWeight,
                curveMid.Y * curveWeight + loopGuidePoint.Y * loopWeight,
                curveMid.Z * curveWeight + loopGuidePoint.Z * loopWeight);
        }

        private static bool HasHiddenTransition(BoundarySample start, BoundarySample end, BoundarySample probe)
        {
            if ((probe.Hit == null) != (start.Hit == null) || (probe.Hit == null) != (end.Hit == null))
            {
                return true;
            }

            if (probe.Hit == null || start.Hit == null || end.Hit == null)
            {
                return false;
            }

            ElementId startElementId = start.Hit.Value.ElementId;
            ElementId endElementId = end.Hit.Value.ElementId;
            ElementId probeElementId = probe.Hit.Value.ElementId;

            if (probeElementId != startElementId || probeElementId != endElementId)
            {
                return true;
            }

            double parameterSpan = end.Parameter - start.Parameter;
            if (parameterSpan <= ParameterEpsilon)
            {
                return false;
            }

            double ratio = (probe.Parameter - start.Parameter) / parameterSpan;
            double expectedZ = start.Hit.Value.Point.Z + (end.Hit.Value.Point.Z - start.Hit.Value.Point.Z) * ratio;
            return Math.Abs(probe.Hit.Value.Point.Z - expectedZ) > ProfileDeviationTolerance;
        }

        private readonly record struct BoundarySample(double Parameter, XYZ CurvePoint, AlignEdgesHitInfo? Hit);
    }
}
