using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class CadLineMergeService
    {
        public List<Line> MergeCollinearLines(List<Line> sourceLines)
        {
            ArgumentNullException.ThrowIfNull(sourceLines);

            if (sourceLines.Count == 0) return new List<Line>();

            var merged = new List<Line>();
            var groupedByDir = sourceLines.GroupBy(l => RoundVector(l.Direction));

            foreach (var dirGroup in groupedByDir)
            {
                XYZ dir = dirGroup.Key;
                XYZ normal = new XYZ(-dir.Y, dir.X, 0);

                var groupedByIntercept = dirGroup.GroupBy(l => Math.Round(l.GetEndPoint(0).DotProduct(normal), 4));

                foreach (var interceptGroup in groupedByIntercept)
                {
                    var intervals = interceptGroup.Select(l =>
                    {
                        double s = l.GetEndPoint(0).DotProduct(dir);
                        double e = l.GetEndPoint(1).DotProduct(dir);
                        return s < e ? (Start: s, End: e) : (Start: e, End: s);
                    }).OrderBy(i => i.Start).ToList();

                    if (intervals.Count == 0) continue;

                    double currentStart = intervals[0].Start;
                    double currentEnd = intervals[0].End;

                    for (int i = 1; i < intervals.Count; i++)
                    {
                        if (intervals[i].Start <= currentEnd + 0.001)
                        {
                            currentEnd = Math.Max(currentEnd, intervals[i].End);
                        }
                        else
                        {
                            var line = CreateLineFromProjection(dir, normal, interceptGroup.Key, currentStart, currentEnd);
                            if (line != null) merged.Add(line);
                            currentStart = intervals[i].Start;
                            currentEnd = intervals[i].End;
                        }
                    }

                    var lastLine = CreateLineFromProjection(dir, normal, interceptGroup.Key, currentStart, currentEnd);
                    if (lastLine != null) merged.Add(lastLine);
                }
            }

            return merged;
        }

        private Line? CreateLineFromProjection(XYZ dir, XYZ normal, double intercept, double startProj, double endProj)
        {
            if (Math.Abs(endProj - startProj) < 0.005) return null;

            XYZ p1 = (normal * intercept) + (dir * startProj);
            XYZ p2 = (normal * intercept) + (dir * endProj);
            try
            {
                return Line.CreateBound(p1, p2);
            }
            catch (Exception ex) when (IsExpectedCadLineMergeException(ex))
            {
                return null;
            }
        }

        private static bool IsExpectedCadLineMergeException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }

        private XYZ RoundVector(XYZ v)
        {
            return new XYZ(Math.Round(v.X, 4), Math.Round(v.Y, 4), Math.Round(v.Z, 4)).Normalize();
        }
    }
}
