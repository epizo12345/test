using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace PrivateLeatherConsolidator
{
    public sealed class LeatherConsolidatorMod : Mod
    {
        private static bool harmonyPatched;
        public static LeatherConsolidatorModSettings PersistentSettings { get; private set; }

        private Vector2 scrollPosition;
        private bool showMappings = true;
        private bool showProtected = true;

        public LeatherConsolidatorMod(ModContentPack content) : base(content)
        {
            PersistentSettings = GetSettings<LeatherConsolidatorModSettings>() ?? new LeatherConsolidatorModSettings();

            if (!harmonyPatched)
            {
                new Harmony("epizo12345.LeatherConsolidator").PatchAll();
                harmonyPatched = true;
            }

            // Queue once as an additional guard; the Harmony prefix also applies settings immediately
            // before the consolidation initializer executes.
            LongEventHandler.ExecuteWhenFinished(ApplyPersistentSettingsToRuntimeDef);
            Log.Message("[革統合] DLLロード成功: LeatherConsolidator.dll / ゲーム内設定を有効化");
        }

        public static void ApplyPersistentSettingsToRuntimeDef()
        {
            try
            {
                LeatherConsolidatorSettingsDef runtimeDef = DefDatabase<LeatherConsolidatorSettingsDef>.AllDefsListForReading.FirstOrDefault();
                if (runtimeDef == null || PersistentSettings == null)
                    return;

                PersistentSettings.ApplyTo(runtimeDef);
            }
            catch (Exception ex)
            {
                Log.Error($"[革統合] ゲーム内設定の適用に失敗: {ex}");
            }
        }

        public override string SettingsCategory()
        {
            return "革統合 - 1.6";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            LeatherConsolidatorModSettings settings = PersistentSettings;
            if (settings == null)
            {
                Widgets.Label(inRect, "設定の読み込みに失敗しました。");
                return;
            }

            settings.alwaysKeep ??= new List<string>();
            settings.overrides ??= new List<LeatherOverrideEntry>();

            float viewHeight = 980f
                + settings.alwaysKeep.Count * 34f
                + settings.overrides.Count * 34f
                + (showMappings ? LeatherConsolidatorBootstrap.ReplacementMap.Count * 30f : 0f)
                + (showProtected ? LeatherConsolidatorBootstrap.ProtectedLeathers.Count * 28f : 0f);

            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, Math.Max(inRect.height, viewHeight));
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("革統合MODのDLLは正常に読み込まれています。");
            listing.Label("設定はRimWorldのModSettingsへ保存されます。統合先や保護対象の変更はRimWorld再起動後に反映されます。");
            listing.GapLine();

            listing.Label("基本設定");
            listing.CheckboxLabeled("Humanlike専用革を人皮へ統合", ref settings.mergeHumanlikeLeathersIntoHuman, "追加Humanlike種族だけが使う通常の革をバニラ人皮へまとめます。");
            listing.CheckboxLabeled("特殊高性能革を自動保護", ref settings.protectExtremeLeathers, "通常5革の性能範囲を大きく超える革を自動統合しません。");
            listing.CheckboxLabeled("特殊StuffCategory革を自動保護", ref settings.protectMultiCategoryLeathers, "Leathery/Fabric以外の特殊StuffCategoryを持つ素材を自動統合しません。");
            listing.CheckboxLabeled("統合済み旧革をLeatheryカテゴリから外す", ref settings.removeLeatheryCategoryFromMergedLeathers, "材料一覧や一般的な革選択から旧革を除外します。");
            listing.CheckboxLabeled("ThingMaker生成時フォールバック", ref settings.enableThingMakerFallback, "他MODが統合前の革を直接生成した場合も、生成時に統合先へ差し替えます。");

            listing.Gap();
            listing.Label("既存セーブ移行");
            listing.CheckboxLabeled("既存Billの材料設定を移行", ref settings.migrateExistingBills);
            listing.CheckboxLabeled("マップ上の既存生革を移行", ref settings.migrateExistingRawLeatherStacks);
            listing.CheckboxLabeled("所持品・コンテナ・キャラバンの既存生革を移行", ref settings.migrateHeldRawLeatherStacks);

            listing.Gap();
            listing.Label("診断");
            listing.CheckboxLabeled("未解決の旧革参照を監査", ref settings.auditRemainingReferences);
            listing.CheckboxLabeled("詳細ログを出力", ref settings.verboseLog);

            listing.GapLine();
            listing.Label($"検出した生産革候補: {LeatherConsolidatorBootstrap.CandidateLeatherCount} 件");
            listing.Label($"現在の置換マップ: {LeatherConsolidatorBootstrap.ReplacementMap.Count} 件");
            listing.Label($"現在の保護革: {LeatherConsolidatorBootstrap.ProtectedLeathers.Count} 件");

            listing.GapLine();
            listing.Label("常時保護する革");
            listing.Label("DefNameを指定します。下の現在の置換一覧から『保護に追加』を押す方法が簡単です。");
            DrawAlwaysKeepEditor(listing, settings);
            if (listing.ButtonText("＋ 保護Defを追加"))
                settings.alwaysKeep.Add(string.Empty);

            listing.GapLine();
            listing.Label("手動統合指定");
            listing.Label("source=元の革DefName / target=統合先DefName。targetを空欄にするとsourceを保護します。");
            DrawOverrideEditor(listing, settings);
            if (listing.ButtonText("＋ 手動統合を追加"))
                settings.overrides.Add(new LeatherOverrideEntry());

            listing.GapLine();
            if (listing.ButtonText(showMappings ? "▼ 現在の置換一覧を隠す" : "▶ 現在の置換一覧を表示"))
                showMappings = !showMappings;
            if (showMappings)
                DrawMappings(listing, settings);

            listing.Gap();
            if (listing.ButtonText(showProtected ? "▼ 現在の保護一覧を隠す" : "▶ 現在の保護一覧を表示"))
                showProtected = !showProtected;
            if (showProtected)
                DrawProtected(listing);

            listing.GapLine();
            listing.Label("設定を変更したらこの画面を閉じ、RimWorldを再起動してください。設定は自動保存されます。");
            if (listing.ButtonText("設定を初期値に戻す"))
                settings.ResetToDefaults();

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawAlwaysKeepEditor(Listing_Standard listing, LeatherConsolidatorModSettings settings)
        {
            int removeIndex = -1;
            for (int i = 0; i < settings.alwaysKeep.Count; i++)
            {
                Rect row = listing.GetRect(30f);
                float buttonWidth = 70f;
                Rect fieldRect = new Rect(row.x, row.y + 2f, row.width - buttonWidth - 8f, 26f);
                Rect removeRect = new Rect(row.xMax - buttonWidth, row.y + 2f, buttonWidth, 26f);

                settings.alwaysKeep[i] = Widgets.TextField(fieldRect, settings.alwaysKeep[i] ?? string.Empty);
                if (Widgets.ButtonText(removeRect, "削除"))
                    removeIndex = i;
            }

            if (removeIndex >= 0)
                settings.alwaysKeep.RemoveAt(removeIndex);
        }

        private static void DrawOverrideEditor(Listing_Standard listing, LeatherConsolidatorModSettings settings)
        {
            Rect header = listing.GetRect(24f);
            float removeWidth = 58f;
            float usable = header.width - removeWidth - 16f;
            float sourceWidth = usable * 0.48f;
            float targetWidth = usable - sourceWidth;
            Widgets.Label(new Rect(header.x, header.y, sourceWidth, header.height), "source");
            Widgets.Label(new Rect(header.x + sourceWidth + 8f, header.y, targetWidth, header.height), "target");

            int removeIndex = -1;
            for (int i = 0; i < settings.overrides.Count; i++)
            {
                LeatherOverrideEntry entry = settings.overrides[i] ?? new LeatherOverrideEntry();
                settings.overrides[i] = entry;

                Rect row = listing.GetRect(30f);
                Rect sourceRect = new Rect(row.x, row.y + 2f, sourceWidth, 26f);
                Rect targetRect = new Rect(row.x + sourceWidth + 8f, row.y + 2f, targetWidth, 26f);
                Rect removeRect = new Rect(row.xMax - removeWidth, row.y + 2f, removeWidth, 26f);

                entry.source = Widgets.TextField(sourceRect, entry.source ?? string.Empty);
                entry.target = Widgets.TextField(targetRect, entry.target ?? string.Empty);
                if (Widgets.ButtonText(removeRect, "削除"))
                    removeIndex = i;
            }

            if (removeIndex >= 0)
                settings.overrides.RemoveAt(removeIndex);
        }

        private static void DrawMappings(Listing_Standard listing, LeatherConsolidatorModSettings settings)
        {
            foreach (KeyValuePair<ThingDef, ThingDef> pair in LeatherConsolidatorBootstrap.ReplacementMap.OrderBy(x => x.Key.label ?? x.Key.defName))
            {
                Rect row = listing.GetRect(28f);
                float buttonWidth = 92f;
                Rect labelRect = new Rect(row.x, row.y, row.width - buttonWidth - 8f, row.height);
                Rect buttonRect = new Rect(row.xMax - buttonWidth, row.y + 1f, buttonWidth, row.height - 2f);

                string sourceLabel = string.IsNullOrEmpty(pair.Key.label) ? pair.Key.defName : pair.Key.label;
                string targetLabel = string.IsNullOrEmpty(pair.Value.label) ? pair.Value.defName : pair.Value.label;
                Widgets.Label(labelRect, $"{sourceLabel} ({pair.Key.defName}) → {targetLabel} ({pair.Value.defName})");

                bool alreadyKept = settings.alwaysKeep.Any(x => string.Equals((x ?? string.Empty).Trim(), pair.Key.defName, StringComparison.OrdinalIgnoreCase));
                if (alreadyKept)
                {
                    Widgets.Label(buttonRect, "保護予約済");
                }
                else if (Widgets.ButtonText(buttonRect, "保護に追加"))
                {
                    settings.alwaysKeep.Add(pair.Key.defName);
                }
            }
        }

        private static void DrawProtected(Listing_Standard listing)
        {
            foreach (ThingDef def in LeatherConsolidatorBootstrap.ProtectedLeathers.OrderBy(x => x.label ?? x.defName))
            {
                Rect row = listing.GetRect(26f);
                string label = string.IsNullOrEmpty(def.label) ? def.defName : def.label;
                string reason = LeatherConsolidatorBootstrap.ProtectionReasons.TryGetValue(def, out string value) ? value : "保護";
                Widgets.Label(row, $"{label} ({def.defName}) — {reason}");
            }
        }
    }
}
