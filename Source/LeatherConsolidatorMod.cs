using UnityEngine;
using Verse;

namespace PrivateLeatherConsolidator
{
    public sealed class LeatherConsolidatorMod : Mod
    {
        public LeatherConsolidatorMod(ModContentPack content) : base(content)
        {
            Log.Message("[革統合] DLLロード成功: LeatherConsolidator.dll");
        }

        public override string SettingsCategory()
        {
            return "革統合 - 1.6";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("革統合MODのDLLは正常に読み込まれています。");
            listing.GapLine();

            LeatherConsolidatorSettingsDef settings = LeatherConsolidatorBootstrap.Settings;
            if (settings == null)
            {
                listing.Label("設定Def: 未初期化または未読込");
            }
            else
            {
                listing.Label("設定Def: 読込済み");
                listing.Label($"Humanlike専用革を人皮へ統合: {settings.mergeHumanlikeLeathersIntoHuman}");
                listing.Label($"特殊高性能革を保護: {settings.protectExtremeLeathers}");
                listing.Label($"複数StuffCategory革を保護: {settings.protectMultiCategoryLeathers}");
                listing.Label($"ThingMakerフォールバック: {settings.enableThingMakerFallback}");
                listing.Label($"既存Bill移行: {settings.migrateExistingBills}");
                listing.Label($"既存生革移行: {settings.migrateExistingRawLeatherStacks}");
            }

            listing.GapLine();
            listing.Label($"現在の置換マップ: {LeatherConsolidatorBootstrap.ReplacementMap.Count} 件");
            listing.Label($"現在の保護革: {LeatherConsolidatorBootstrap.ProtectedLeathers.Count} 件");
            listing.Gap();
            listing.Label("設定値の変更は Defs/LeatherConsolidatorSettings.xml を編集後、RimWorldを再起動してください。");

            listing.End();
        }
    }
}
