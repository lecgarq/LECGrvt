using System.Collections.Generic;
using System.Linq;

namespace LECG.Core.Naming
{
    public static class FamilyNamePolicy
    {
        public static string ResolveName(IEnumerable<string> existingNames, string sourceFamilyName, string customName)
        {
            string baseName = string.IsNullOrWhiteSpace(customName) ? $"{sourceFamilyName}_Converted" : customName;
            string finalName = baseName;
            int counter = 1;

            var nameSet = new HashSet<string>(existingNames);

            while (nameSet.Contains(finalName))
            {
                finalName = $"{baseName}_{counter}";
                counter++;
            }

            return finalName;
        }
    }
}
