using System.Collections.Generic;
using Verse;

namespace PrivateLeatherConsolidator
{
    public sealed class LeatherConsolidatorSettingsDef : Def
    {
        public bool removeLeatheryCategoryFromMergedLeathers = true;
        public bool protectExtremeLeathers = true;
        public bool protectMultiCategoryLeathers = true;
        public bool verboseLog = true;

        public List<string> alwaysKeep = new List<string>();
        public List<LeatherOverrideEntry> overrides = new List<LeatherOverrideEntry>();
    }

    public sealed class LeatherOverrideEntry
    {
        public string source;
        public string target;
    }
}
