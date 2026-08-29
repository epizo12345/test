using System.Collections.Generic;
using Verse;

namespace PrivateLeatherConsolidator
{
    public sealed class LeatherConsolidatorSettings : ModSettings
    {
        public bool removeLeatheryCategoryFromMergedLeathers = true;
        public bool mergeHumanlikeLeathersIntoHuman = true;
        public bool protectExtremeLeathers = true;
        public bool protectMultiCategoryLeathers = true;
        public bool enableThingMakerFallback = true;
        public bool migrateExistingBills = true;
        public bool migrateExistingRawLeatherStacks = true;
        public bool migrateHeldRawLeatherStacks = true;
        public bool auditRemainingReferences = true;
        public bool verboseLog = true;

        public List<string> alwaysKeep = new List<string>();
        public List<LeatherOverrideEntry> overrides = new List<LeatherOverrideEntry>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref removeLeatheryCategoryFromMergedLeathers, "removeLeatheryCategoryFromMergedLeathers", true);
            Scribe_Values.Look(ref mergeHumanlikeLeathersIntoHuman, "mergeHumanlikeLeathersIntoHuman", true);
            Scribe_Values.Look(ref protectExtremeLeathers, "protectExtremeLeathers", true);
            Scribe_Values.Look(ref protectMultiCategoryLeathers, "protectMultiCategoryLeathers", true);
            Scribe_Values.Look(ref enableThingMakerFallback, "enableThingMakerFallback", true);
            Scribe_Values.Look(ref migrateExistingBills, "migrateExistingBills", true);
            Scribe_Values.Look(ref migrateExistingRawLeatherStacks, "migrateExistingRawLeatherStacks", true);
            Scribe_Values.Look(ref migrateHeldRawLeatherStacks, "migrateHeldRawLeatherStacks", true);
            Scribe_Values.Look(ref auditRemainingReferences, "auditRemainingReferences", true);
            Scribe_Values.Look(ref verboseLog, "verboseLog", true);
            Scribe_Collections.Look(ref alwaysKeep, "alwaysKeep", LookMode.Value);
            Scribe_Collections.Look(ref overrides, "overrides", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (alwaysKeep == null)
                    alwaysKeep = new List<string>();
                if (overrides == null)
                    overrides = new List<LeatherOverrideEntry>();

                alwaysKeep.RemoveAll(x => string.IsNullOrWhiteSpace(x));
                overrides.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.source));
            }
        }

        public void ResetToDefaults()
        {
            removeLeatheryCategoryFromMergedLeathers = true;
            mergeHumanlikeLeathersIntoHuman = true;
            protectExtremeLeathers = true;
            protectMultiCategoryLeathers = true;
            enableThingMakerFallback = true;
            migrateExistingBills = true;
            migrateExistingRawLeatherStacks = true;
            migrateHeldRawLeatherStacks = true;
            auditRemainingReferences = true;
            verboseLog = true;
            alwaysKeep.Clear();
            overrides.Clear();
        }
    }

    public sealed class LeatherOverrideEntry : IExposable
    {
        public string source = string.Empty;
        public string target = string.Empty;

        public LeatherOverrideEntry()
        {
        }

        public LeatherOverrideEntry(string source, string target)
        {
            this.source = source ?? string.Empty;
            this.target = target ?? string.Empty;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref source, "source", string.Empty);
            Scribe_Values.Look(ref target, "target", string.Empty);
        }
    }
}
