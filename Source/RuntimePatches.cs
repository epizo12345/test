using HarmonyLib;
using Verse;

namespace PrivateLeatherConsolidator
{
    [StaticConstructorOnStartup]
    public static class RuntimePatches
    {
        static RuntimePatches()
        {
            new Harmony("epizo12345.LeatherConsolidator").PatchAll();
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
