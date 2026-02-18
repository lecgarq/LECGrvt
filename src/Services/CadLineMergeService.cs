using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadLineMergeService : ICadLineMergeService
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
                            merged.Add(CreateLineFromProjection(dir, normal, interceptGroup.Key, currentStart, currentEnd));
                            currentStart = intervals[i].Start;
                            currentEnd = intervals[i].End;
                        }
                    }

                    merged.Add(CreateLineFromProjection(dir, normal, interceptGroup.Key, currentStart, currentEnd));
                }
            }

            return merged;
        }

        private Line CreateLineFromProjection(XYZ dir, XYZ normal, double intercept, double startProj, double endProj)
        {
            XYZ p1 = (normal * intercept) + (dir * startProj);
            XYZ p2 = (normal * intercept) + (dir * endProj);
            return Line.CreateBound(p1, p2);
        }

        private XYZ RoundVector(XYZ v)
        {
            return new XYZ(Math.Round(v.X, 4), Math.Round(v.Y, 4), Math.Round(v.Z, 4)).Normalize();
        }
    }
}
