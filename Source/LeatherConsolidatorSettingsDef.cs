using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PrivateLeatherConsolidator
{
    // Internal runtime config populated from the user's ModSettings before consolidation starts.
    public sealed class LeatherConsolidatorSettingsDef : Def
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
    }

    public sealed class LeatherConsolidatorModSettings : ModSettings
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
                Sanitize();
        }

        public void ApplyTo(LeatherConsolidatorSettingsDef target)
        {
            if (target == null)
                return;

            Sanitize();
            target.removeLeatheryCategoryFromMergedLeathers = removeLeatheryCategoryFromMergedLeathers;
            target.mergeHumanlikeLeathersIntoHuman = mergeHumanlikeLeathersIntoHuman;
            target.protectExtremeLeathers = protectExtremeLeathers;
            target.protectMultiCategoryLeathers = protectMultiCategoryLeathers;
            target.enableThingMakerFallback = enableThingMakerFallback;
            target.migrateExistingBills = migrateExistingBills;
            target.migrateExistingRawLeatherStacks = migrateExistingRawLeatherStacks;
            target.migrateHeldRawLeatherStacks = migrateHeldRawLeatherStacks;
            target.auditRemainingReferences = auditRemainingReferences;
            target.verboseLog = verboseLog;
            target.alwaysKeep = new List<string>(alwaysKeep);
            target.overrides = overrides.Select(x => new LeatherOverrideEntry(x.source, x.target)).ToList();
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

        public void Sanitize()
        {
            if (alwaysKeep == null)
                alwaysKeep = new List<string>();
            if (overrides == null)
                overrides = new List<LeatherOverrideEntry>();

            alwaysKeep = alwaysKeep
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();

            overrides.RemoveAll(x => x == null);
            foreach (LeatherOverrideEntry entry in overrides)
            {
                entry.source = (entry.source ?? string.Empty).Trim();
                entry.target = (entry.target ?? string.Empty).Trim();
            }
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
