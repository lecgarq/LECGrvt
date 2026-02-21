using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace LECG.Models
{
    /// <summary>
    /// Helper class to capture the state of a FamilyInstance for in-place replacement.
    /// </summary>
    public class FamilyInstanceData
    {
        public XYZ LocationPoint { get; private set; }
        public Curve? LocationCurve { get; private set; }
        public double Rotation { get; private set; }
        public bool IsHandFlipped { get; private set; }
        public bool IsFacingFlipped { get; private set; }
        public ElementId LevelId { get; private set; }
        public ElementId? HostId { get; private set; }
        public Dictionary<string, object?> Parameters { get; } = new();

        public static FamilyInstanceData Capture(FamilyInstance instance)
        {
            ArgumentNullException.ThrowIfNull(instance);

            var data = new FamilyInstanceData
            {
                LevelId = instance.LevelId,
                HostId = instance.Host?.Id,
                IsHandFlipped = instance.CanHandFlip && instance.HandFlipped,
                IsFacingFlipped = instance.CanFacingFlip && instance.FacingFlipped
            };

            // Location
            if (instance.Location is LocationPoint lp)
            {
                data.LocationPoint = lp.Point;
                data.Rotation = lp.Rotation;
            }
            else if (instance.Location is LocationCurve lc)
            {
                data.LocationCurve = lc.Curve;
            }

            // Parameters (Instance only)
            foreach (Parameter p in instance.Parameters)
            {
                if (p.IsReadOnly || !p.HasValue) continue;

                // Built-in parameters are often read-only or handled by Revit. 
                // We focus on User/Shared parameters that might exist in both types.
                object? val = p.StorageType switch
                {
                    StorageType.Double => p.AsDouble(),
                    StorageType.Integer => p.AsInteger(),
                    StorageType.String => p.AsString(),
                    StorageType.ElementId => p.AsElementId(),
                    _ => null
                };

                if (val != null)
                {
                    data.Parameters[p.Definition.Name] = val;
                }
            }

            return data;
        }

        public void Apply(FamilyInstance target)
        {
            ArgumentNullException.ThrowIfNull(target);

            // Apply Flips (if possible)
            if (target.CanHandFlip && target.HandFlipped != IsHandFlipped) target.flipHand();
            if (target.CanFacingFlip && target.FacingFlipped != IsFacingFlipped) target.flipFacing();

            // Apply Rotation
            if (target.Location is LocationPoint lp && Math.Abs(Rotation) > 0.0001)
            {
                Line axis = Line.CreateBound(lp.Point, lp.Point + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(target.Document, target.Id, axis, Rotation);
            }

            // Apply Parameters
            foreach (var kvp in Parameters)
            {
                Parameter p = target.LookupParameter(kvp.Key);
                if (p != null && !p.IsReadOnly && kvp.Value != null)
                {
                    try
                    {
                        if (kvp.Value is double d) p.Set(d);
                        else if (kvp.Value is int i) p.Set(i);
                        else if (kvp.Value is string s) p.Set(s);
                        else if (kvp.Value is ElementId id) p.Set(id);
                    }
                    catch
                    {
                        // Best effort transfer
                    }
                }
            }
        }
    }
}
