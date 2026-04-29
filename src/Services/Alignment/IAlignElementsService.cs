using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public enum AlignMode
    {
        Left,
        Center,
        Right,
        Top,
        Middle,
        Bottom,
        DistributeHorizontally,
        DistributeVertically
    }

    public interface IAlignElementsService
    {
        void Align(Document doc, Element reference, List<Element> targets, AlignMode mode);
        void Distribute(Document doc, List<Element> elements, AlignMode mode);
    }
}
