using System;
using Autodesk.Revit.DB;

namespace LECG.ViewModels
{
    public sealed class ConversionTargetTypeOption
    {
        private ConversionTargetTypeOption(string name, ElementType? elementType, bool createFromSource)
        {
            Name = name;
            ElementType = elementType;
            CreateFromSource = createFromSource;
        }

        public string Name { get; }
        public ElementType? ElementType { get; }
        public bool CreateFromSource { get; }

        public static ConversionTargetTypeOption CreateType() =>
            new ConversionTargetTypeOption("Create Type", null, true);

        public static ConversionTargetTypeOption Existing(ElementType elementType)
        {
            ArgumentNullException.ThrowIfNull(elementType);
            return new ConversionTargetTypeOption(elementType.Name, elementType, false);
        }
    }

    public sealed class ConversionTargetLevelOption
    {
        private ConversionTargetLevelOption(string name, Level? level, bool preserveSourceLevel)
        {
            Name = name;
            Level = level;
            PreserveSourceLevel = preserveSourceLevel;
        }

        public string Name { get; }
        public Level? Level { get; }
        public bool PreserveSourceLevel { get; }

        public static ConversionTargetLevelOption PreserveLevel() =>
            new ConversionTargetLevelOption("Preserve Level", null, true);

        public static ConversionTargetLevelOption Existing(Level level)
        {
            ArgumentNullException.ThrowIfNull(level);
            return new ConversionTargetLevelOption(level.Name, level, false);
        }
    }
}
