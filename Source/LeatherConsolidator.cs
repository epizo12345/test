using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace PrivateLeatherConsolidator
{
    [StaticConstructorOnStartup]
    public static class LeatherConsolidatorBootstrap
    {
        public static readonly Dictionary<ThingDef, ThingDef> ReplacementMap = new Dictionary<ThingDef, ThingDef>();
        public static readonly HashSet<ThingDef> ProtectedLeathers = new HashSet<ThingDef>();

        private static LeatherConsolidatorSettingsDef settings;
        private static HashSet<ThingDef> animalLeathers;

        static LeatherConsolidatorBootstrap()
        {
            try
            {
                LongEventHandler.ExecuteWhenFinished(Initialize);
            }
            catch (Exception ex)
            {
                Log.Error($"[革統合] 初期化予約に失敗: {ex}");
            }
        }

        private static void Initialize()
        {
            try
            {
                var allThings = DefDatabase<ThingDef>.AllDefsListForReading;
                var allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
                settings = DefDatabase<LeatherConsolidatorSettingsDef>.AllDefsListForReading.FirstOrDefault();

                ThingDef light = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Light");
                ThingDef plain = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Plain");
                ThingDef heavy = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Heavy");
                ThingDef bird = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Bird");
                ThingDef lizard = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Lizard");

                var targets = new List<ThingDef> { light, plain, heavy, bird, lizard }
                    .Where(x => x != null)
                    .Distinct()
                    .ToList();

                if (targets.Count == 0)
                {
                    Log.Error("[革統合] 統合先のバニラ革を1つも取得できませんでした。処理を中止します。");
                    return;
                }

                animalLeathers = new HashSet<ThingDef>(allThings
                    .Where(x => x?.race?.leatherDef != null)
                    .Select(x => x.race.leatherDef));

                BuildProtectedSet(targets);
                BuildReplacementMap(targets);
                ApplyOverrides();

                int animalCount = RemapAnimalLeatherDefs(allThings);
                int recipeCount = RemapRecipes(allRecipes);
                int categoryCount = HideMergedLeathersFromGeneralLeatherFilters();

                Log.Message($"[革統合] 完了: 動物革 {animalLeathers.Count} / 統合 {ReplacementMap.Count} / 保護 {ProtectedLeathers.Count} / 動物変更 {animalCount} / レシピ補正 {recipeCount} / 旧革Leathery除外 {categoryCount}");

                if (settings == null || settings.verboseLog)
                {
                    foreach (var pair in ReplacementMap.OrderBy(x => x.Key.defName))
                        Log.Message($"[革統合] {pair.Key.defName} ({pair.Key.label}) -> {pair.Value.defName} ({pair.Value.label})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[革統合] 初期化処理で例外: {ex}");
            }
        }

        private static void BuildProtectedSet(List<ThingDef> targets)
        {
            foreach (ThingDef t in targets)
                ProtectedLeathers.Add(t);

            string[] defaultProtected =
            {
                "Leather_Human",
                "Leather_Thrumbo",
                "Thrumbomane",
                "Leather_Thrumbomane"
            };

            foreach (string name in defaultProtected)
                ProtectByName(name);

            if (settings?.alwaysKeep != null)
            {
                foreach (string name in settings.alwaysKeep.Where(x => !string.IsNullOrWhiteSpace(x)))
                    ProtectByName(name.Trim());
            }

            foreach (ThingDef leather in animalLeathers)
            {
                if (leather == null || leather.stuffProps == null)
                    continue;

                if ((settings == null || settings.protectMultiCategoryLeathers) && HasNonLeatherStuffCategory(leather))
                    ProtectedLeathers.Add(leather);

                if ((settings == null || settings.protectExtremeLeathers) && IsExtremeLeather(leather))
                    ProtectedLeathers.Add(leather);
            }
        }

        private static void ProtectByName(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
                return;

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
                ProtectedLeathers.Add(def);
        }

        private static void BuildReplacementMap(List<ThingDef> targets)
        {
            foreach (ThingDef leather in animalLeathers)
            {
                if (!IsMergeCandidate(leather))
                    continue;
                if (ProtectedLeathers.Contains(leather))
                    continue;

                ThingDef closest = targets.OrderBy(t => LeatherDistance(leather, t)).FirstOrDefault();
                if (closest != null && closest != leather)
                    ReplacementMap[leather] = closest;
            }
        }

        private static void ApplyOverrides()
        {
            if (settings?.overrides == null)
                return;

            foreach (LeatherOverrideEntry entry in settings.overrides)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.source))
                    continue;

                ThingDef source = DefDatabase<ThingDef>.GetNamedSilentFail(entry.source.Trim());
                if (source == null)
                {
                    Log.Warning($"[革統合] override元Defが見つかりません: {entry.source}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.target))
                {
                    ProtectedLeathers.Add(source);
                    ReplacementMap.Remove(source);
                    continue;
                }

                ThingDef target = DefDatabase<ThingDef>.GetNamedSilentFail(entry.target.Trim());
                if (target == null)
                {
                    Log.Warning($"[革統合] override先Defが見つかりません: {entry.target}");
                    continue;
                }

                if (source == target)
                {
                    ProtectedLeathers.Add(source);
                    ReplacementMap.Remove(source);
                    continue;
                }

                ProtectedLeathers.Remove(source);
                ReplacementMap[source] = target;
            }
        }

        private static int RemapAnimalLeatherDefs(List<ThingDef> allThings)
        {
            int count = 0;
            foreach (ThingDef animal in allThings)
            {
                if (animal?.race?.leatherDef == null)
                    continue;

                ThingDef oldLeather = animal.race.leatherDef;
                if (ReplacementMap.TryGetValue(oldLeather, out ThingDef replacement))
                {
                    animal.race.leatherDef = replacement;
                    count++;
                }
            }
            return count;
        }

        private static int RemapRecipes(List<RecipeDef> recipes)
        {
            int changed = 0;
            foreach (RecipeDef recipe in recipes)
            {
                bool recipeChanged = false;

                if (recipe.ingredients != null)
                {
                    foreach (IngredientCount ingredient in recipe.ingredients)
                    {
                        ThingFilter filter = ingredient?.filter;
                        if (filter != null && RemapFilter(filter))
                            recipeChanged = true;
                    }
                }

                if (recipe.fixedIngredientFilter != null && RemapFilter(recipe.fixedIngredientFilter))
                    recipeChanged = true;
                if (recipe.defaultIngredientFilter != null && RemapFilter(recipe.defaultIngredientFilter))
                    recipeChanged = true;

                if (recipeChanged)
                    changed++;
            }
            return changed;
        }

        private static bool RemapFilter(ThingFilter filter)
        {
            bool changed = false;
            foreach (var pair in ReplacementMap)
            {
                if (!filter.Allows(pair.Key))
                    continue;

                filter.SetAllow(pair.Key, false);
                filter.SetAllow(pair.Value, true);
                changed = true;
            }
            return changed;
        }

        private static int HideMergedLeathersFromGeneralLeatherFilters()
        {
            if (settings != null && !settings.removeLeatheryCategoryFromMergedLeathers)
                return 0;

            StuffCategoryDef leathery = DefDatabase<StuffCategoryDef>.GetNamedSilentFail("Leathery");
            if (leathery == null)
            {
                Log.Warning("[革統合] StuffCategoryDef 'Leathery' が見つからないため旧革カテゴリ除外を省略します。");
                return 0;
            }

            int count = 0;
            foreach (ThingDef oldLeather in ReplacementMap.Keys)
            {
                if (oldLeather?.stuffProps?.categories == null)
                    continue;

                if (oldLeather.stuffProps.categories.Remove(leathery))
                    count++;
            }
            return count;
        }

        private static bool IsMergeCandidate(ThingDef def)
        {
            if (def == null || def.stuffProps == null)
                return false;

            return animalLeathers != null && animalLeathers.Contains(def);
        }

        private static bool HasNonLeatherStuffCategory(ThingDef def)
        {
            if (def?.stuffProps?.categories == null)
                return false;

            return def.stuffProps.categories.Any(c => c != null && c.defName != "Leathery");
        }

        private static bool IsExtremeLeather(ThingDef def)
        {
            if (def == null || def.stuffProps == null)
                return false;

            float market = def.BaseMarketValue;
            float sharp = GetStuffFactor(def, StatDefOf.ArmorRating_Sharp, 1f);
            float blunt = GetStuffFactor(def, StatDefOf.ArmorRating_Blunt, 1f);
            float heatArmor = GetStuffFactor(def, StatDefOf.ArmorRating_Heat, 1f);
            float hp = GetStuffFactor(def, StatDefOf.MaxHitPoints, 1f);

            return market >= 25f || sharp >= 2.4f || blunt >= 2.0f || heatArmor >= 2.0f || hp >= 2.5f;
        }

        private static double LeatherDistance(ThingDef a, ThingDef b)
        {
            double score = 0d;
            score += SqNorm(GetStuffFactor(a, StatDefOf.ArmorRating_Sharp, 1f), GetStuffFactor(b, StatDefOf.ArmorRating_Sharp, 1f), 1.00);
            score += SqNorm(GetStuffFactor(a, StatDefOf.ArmorRating_Blunt, 1f), GetStuffFactor(b, StatDefOf.ArmorRating_Blunt, 1f), 0.60);
            score += SqNorm(GetStuffFactor(a, StatDefOf.ArmorRating_Heat, 1f), GetStuffFactor(b, StatDefOf.ArmorRating_Heat, 1f), 0.40);
            score += SqNorm(GetStuffFactor(a, StatDefOf.MaxHitPoints, 1f), GetStuffFactor(b, StatDefOf.MaxHitPoints, 1f), 0.75);
            score += SqNorm(GetStuffFactor(a, StatDefOf.Insulation_Cold, 0f), GetStuffFactor(b, StatDefOf.Insulation_Cold, 0f), 0.004);
            score += SqNorm(GetStuffFactor(a, StatDefOf.Insulation_Heat, 0f), GetStuffFactor(b, StatDefOf.Insulation_Heat, 0f), 0.006);
            score += SqNorm(a.BaseMarketValue, b.BaseMarketValue, 0.08);
            return score;
        }

        private static double SqNorm(float a, float b, double weight)
        {
            double d = a - b;
            return d * d * weight;
        }

        private static float GetStuffFactor(ThingDef def, StatDef stat, float fallback)
        {
            if (def?.stuffProps?.statFactors == null || stat == null)
                return fallback;

            StatModifier modifier = def.stuffProps.statFactors.FirstOrDefault(x => x.stat == stat);
            return modifier != null ? modifier.value : fallback;
        }
    }
}
