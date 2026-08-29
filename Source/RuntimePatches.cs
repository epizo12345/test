using HarmonyLib;
using Verse;

namespace PrivateLeatherConsolidator
{
    [HarmonyPatch(typeof(LeatherConsolidatorBootstrap), "Initialize")]
    public static class BootstrapInitializeSettingsPatch
    {
        public static void Prefix()
        {
            LeatherConsolidatorMod.ApplyPersistentSettingsToRuntimeDef();
        }
    }

    [HarmonyPatch(typeof(ThingMaker), nameof(ThingMaker.MakeThing))]
    public static class ThingMakerMakeThingPatch
    {
        public static void Prefix(ref ThingDef def, ref ThingDef stuff)
        {
            LeatherConsolidatorSettingsDef settings = LeatherConsolidatorBootstrap.Settings;
            if (settings != null && !settings.enableThingMakerFallback)
                return;

            if (def != null && LeatherConsolidatorBootstrap.ReplacementMap.TryGetValue(def, out ThingDef replacementDef))
                def = replacementDef;

            if (stuff != null && LeatherConsolidatorBootstrap.ReplacementMap.TryGetValue(stuff, out ThingDef replacementStuff))
                stuff = replacementStuff;
        }
    }
}
