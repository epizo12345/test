using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace PrivateLeatherConsolidator
{
    [StaticConstructorOnStartup]
    public static class LeatherConsolidatorBootstrap
    {
        public static readonly Dictionary<ThingDef, ThingDef> ReplacementMap = new Dictionary<ThingDef, ThingDef>();
        public static readonly HashSet<ThingDef> ProtectedLeathers = new HashSet<ThingDef>();
        public static readonly Dictionary<ThingDef, string> ProtectionReasons = new Dictionary<ThingDef, string>();

        private static readonly Dictionary<ThingDef, HashSet<ThingDef>> ProducerRaces = new Dictionary<ThingDef, HashSet<ThingDef>>();
        private static LeatherConsolidatorSettingsDef settings;
        private static HashSet<ThingDef> animalLeathers;
        private static HashSet<ThingDef> humanlikeOnlyLeathers;

        public static LeatherConsolidatorSettingsDef Settings => settings;
        public static int CandidateLeatherCount => animalLeathers?.Count ?? 0;

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
                ReplacementMap.Clear();
                ProtectedLeathers.Clear();
                ProtectionReasons.Clear();
                ProducerRaces.Clear();

                List<ThingDef> allThings = DefDatabase<ThingDef>.AllDefsListForReading;
                List<RecipeDef> allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
                settings = DefDatabase<LeatherConsolidatorSettingsDef>.AllDefsListForReading.FirstOrDefault();

                ThingDef light = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Light");
                ThingDef plain = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Plain");
                ThingDef heavy = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Heavy");
                ThingDef bird = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Bird");
                ThingDef lizard = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Lizard");
                ThingDef human = DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Human");

                List<ThingDef> targets = new List<ThingDef> { light, plain, heavy, bird, lizard }
                    .Where(x => x != null)
                    .Distinct()
                    .ToList();

                if (targets.Count == 0)
                {
                    Log.Error("[革統合] 統合先のバニラ革を1つも取得できませんでした。処理を中止します。");
                    return;
                }

                CollectLeatherProducers(allThings);
                animalLeathers = new HashSet<ThingDef>(ProducerRaces.Keys);
                humanlikeOnlyLeathers = new HashSet<ThingDef>(ProducerRaces
                    .Where(pair => pair.Value.Count > 0 && pair.Value.All(raceDef => raceDef?.race?.Humanlike == true))
                    .Select(pair => pair.Key));

                BuildProtectedSet(targets, human);
                BuildReplacementMap(targets, human);
                ApplyOverrides();
                NormalizeReplacementMap();

                int animalCount = RemapAnimalLeatherDefs(allThings);
                int recipeCount = RemapRecipes(allRecipes);
                int directCount = RemapKnownDirectReferences(allThings);
                int categoryCount = MakeMergedLeathersInert();

                ResetStuffCaches();

                int remainingRefs = 0;
                if (settings == null || settings.auditRemainingReferences)
                    remainingRefs = AuditRemainingKnownReferences(allThings, allRecipes);

                int humanlikeCount = human != null
                    ? ReplacementMap.Count(x => x.Value == human && humanlikeOnlyLeathers.Contains(x.Key))
                    : 0;

                Log.Message($"[革統合] 完了: 生産革 {animalLeathers.Count} / Humanlike専用革 {humanlikeOnlyLeathers.Count} / 人皮統合 {humanlikeCount} / 統合 {ReplacementMap.Count} / 保護 {ProtectedLeathers.Count} / 種族変更 {animalCount} / レシピ補正 {recipeCount} / 直接参照補正 {directCount} / 旧革Leathery除外 {categoryCount} / 未解決既知参照 {remainingRefs}");

                if (settings == null || settings.verboseLog)
                {
                    foreach (KeyValuePair<ThingDef, ThingDef> pair in ReplacementMap.OrderBy(x => x.Key.defName))
                    {
                        string reason = human != null && pair.Value == human && humanlikeOnlyLeathers.Contains(pair.Key)
                            ? " [Humanlike専用革→人皮]"
                            : string.Empty;
                        Log.Message($"[革統合] {pair.Key.defName} ({pair.Key.label}) -> {pair.Value.defName} ({pair.Value.label}){reason}");
                    }

                    foreach (KeyValuePair<ThingDef, string> pair in ProtectionReasons.OrderBy(x => x.Key.defName))
                        Log.Message($"[革統合][保護] {pair.Key.defName} ({pair.Key.label}): {pair.Value}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[革統合] 初期化処理で例外: {ex}");
            }
        }

        private static void CollectLeatherProducers(List<ThingDef> allThings)
        {
            foreach (ThingDef raceDef in allThings)
            {
                if (raceDef?.race == null)
                    continue;

                AddProducer(raceDef.race.leatherDef, raceDef);

                if (raceDef.butcherProducts != null)
                {
                    foreach (ThingDefCountClass product in raceDef.butcherProducts)
                    {
                        if (IsLeatheryStuff(product?.thingDef))
                            AddProducer(product.thingDef, raceDef);
                    }
                }

                if (raceDef.comps == null)
                    continue;

                foreach (CompProperties comp in raceDef.comps)
                {
                    if (comp is CompProperties_Shearable shearable && IsLeatheryStuff(shearable.woolDef))
                        AddProducer(shearable.woolDef, raceDef);

                    ThingDef scaleDef = GetThingDefMember(comp, "scaleDef");
                    if (IsLeatheryStuff(scaleDef))
                        AddProducer(scaleDef, raceDef);
                }
            }
        }

        private static void AddProducer(ThingDef leather, ThingDef raceDef)
        {
            if (leather == null || raceDef == null)
                return;

            if (!ProducerRaces.TryGetValue(leather, out HashSet<ThingDef> races))
            {
                races = new HashSet<ThingDef>();
                ProducerRaces.Add(leather, races);
            }
            races.Add(raceDef);
        }

        private static void BuildProtectedSet(List<ThingDef> targets, ThingDef humanLeather)
        {
            foreach (ThingDef target in targets)
                Protect(target, "統合先の基準革");

            string[] defaultProtected =
            {
                "Leather_Human",
                "Leather_Thrumbo",
                "Leather_AlphaThrumbo",
                "Thrumbomane",
                "Leather_Thrumbomane"
            };

            foreach (string name in defaultProtected)
                ProtectByName(name, "固定保護");

            if (settings?.alwaysKeep != null)
            {
                foreach (string name in settings.alwaysKeep.Where(x => !string.IsNullOrWhiteSpace(x)))
                    ProtectByName(name.Trim(), "alwaysKeep指定");
            }

            foreach (ThingDef leather in animalLeathers)
            {
                if (leather?.stuffProps == null)
                    continue;

                if (leather.defName.IndexOf("Thrumbo", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Protect(leather, "Thrumbo系名称");
                    continue;
                }

                bool isHumanlikeOrdinaryLeather = (settings == null || settings.mergeHumanlikeLeathersIntoHuman)
                    && humanLeather != null
                    && leather != humanLeather
                    && humanlikeOnlyLeathers.Contains(leather)
                    && IsOrdinaryLeatheryStuff(leather);

                if ((settings == null || settings.protectMultiCategoryLeathers) && HasIncompatibleStuffCategory(leather))
                {
                    Protect(leather, "Leathery/Fabric以外のStuffCategoryを持つ特殊素材");
                    continue;
                }

                if (!isHumanlikeOrdinaryLeather
                    && (settings == null || settings.protectExtremeLeathers)
                    && IsExtremeLeather(leather, targets))
                {
                    Protect(leather, "通常5革の性能範囲を大きく超える特殊革");
                }
            }
        }

        private static void Protect(ThingDef def, string reason)
        {
            if (def == null)
                return;

            ProtectedLeathers.Add(def);
            if (!ProtectionReasons.ContainsKey(def))
                ProtectionReasons[def] = reason;
        }

        private static void ProtectByName(string defName, string reason)
        {
            if (string.IsNullOrWhiteSpace(defName))
                return;

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
                Protect(def, reason);
        }

        private static void BuildReplacementMap(List<ThingDef> targets, ThingDef humanLeather)
        {
            foreach (ThingDef leather in animalLeathers)
            {
                if (!IsMergeCandidate(leather) || ProtectedLeathers.Contains(leather))
                    continue;

                if ((settings == null || settings.mergeHumanlikeLeathersIntoHuman)
                    && humanLeather != null
                    && humanlikeOnlyLeathers.Contains(leather)
                    && IsOrdinaryLeatheryStuff(leather)
                    && leather != humanLeather)
                {
                    ReplacementMap[leather] = humanLeather;
                    continue;
                }

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
                    Protect(source, "overrideで保護");
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
                    Protect(source, "overrideで自己指定＝保護");
                    ReplacementMap.Remove(source);
                    continue;
                }

                ProtectedLeathers.Remove(source);
                ProtectionReasons.Remove(source);
                ReplacementMap[source] = target;
            }
        }

        private static void NormalizeReplacementMap()
        {
            Dictionary<ThingDef, ThingDef> normalized = new Dictionary<ThingDef, ThingDef>();

            foreach (ThingDef source in ReplacementMap.Keys.ToList())
            {
                HashSet<ThingDef> visited = new HashSet<ThingDef>();
                ThingDef current = source;
                bool cycle = false;

                while (ReplacementMap.TryGetValue(current, out ThingDef next))
                {
                    if (!visited.Add(current) || visited.Contains(next))
                    {
                        cycle = true;
                        break;
                    }
                    current = next;
                }

                if (cycle)
                {
                    Log.Warning($"[革統合] override/置換に循環を検出したため無効化します: {source.defName}");
                    Protect(source, "置換循環を検出");
                    continue;
                }

                if (current != null && current != source)
                    normalized[source] = current;
            }

            ReplacementMap.Clear();
            foreach (KeyValuePair<ThingDef, ThingDef> pair in normalized)
                ReplacementMap[pair.Key] = pair.Value;
        }

        private static int RemapAnimalLeatherDefs(List<ThingDef> allThings)
        {
            int count = 0;
            foreach (ThingDef animal in allThings)
            {
                if (animal?.race?.leatherDef == null)
                    continue;

                if (TryGetReplacement(animal.race.leatherDef, out ThingDef replacement))
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
                        if (ingredient?.filter != null && RemapFilter(ingredient.filter))
                            recipeChanged = true;
                    }
                }

                if (recipe.fixedIngredientFilter != null && RemapFilter(recipe.fixedIngredientFilter))
                    recipeChanged = true;
                if (recipe.defaultIngredientFilter != null && RemapFilter(recipe.defaultIngredientFilter))
                    recipeChanged = true;
                if (RemapThingDefCountList(recipe.products))
                    recipeChanged = true;

                if (recipeChanged)
                    changed++;
            }
            return changed;
        }

        private static int RemapKnownDirectReferences(List<ThingDef> allThings)
        {
            int changed = 0;

            foreach (ThingDef def in allThings)
            {
                if (def == null)
                    continue;

                if (TryGetReplacement(def.defaultStuff, out ThingDef defaultStuff))
                {
                    def.defaultStuff = defaultStuff;
                    changed++;
                }

                if (RemapThingDefCountList(def.costList)) changed++;
                if (def.costListForDifficulty != null && RemapThingDefCountList(def.costListForDifficulty.costList)) changed++;
                if (RemapThingDefCountList(def.butcherProducts)) changed++;
                if (RemapThingDefCountList(def.smeltProducts)) changed++;
                if (RemapThingDefCountList(def.killedLeavings)) changed++;
                if (RemapThingDefCountList(def.killedLeavingsPlayerHostile)) changed++;
                if (RemapThingDefCountRangeList(def.killedLeavingsRanges)) changed++;

                if (def.comps == null)
                    continue;

                foreach (CompProperties comp in def.comps)
                {
                    if (comp is CompProperties_Shearable shearable
                        && TryGetReplacement(shearable.woolDef, out ThingDef replacementWool))
                    {
                        shearable.woolDef = replacementWool;
                        changed++;
                    }

                    if (RemapThingDefMember(comp, "scaleDef"))
                        changed++;
                }
            }

            return changed;
        }

        private static bool RemapThingDefCountList(List<ThingDefCountClass> list)
        {
            if (list == null)
                return false;

            bool changed = false;
            foreach (ThingDefCountClass entry in list)
            {
                if (entry == null)
                    continue;

                if (TryGetReplacement(entry.thingDef, out ThingDef replacementDef))
                {
                    entry.thingDef = replacementDef;
                    changed = true;
                }

                if (TryGetReplacement(entry.stuff, out ThingDef replacementStuff))
                {
                    entry.stuff = replacementStuff;
                    changed = true;
                }
            }
            return changed;
        }

        private static bool RemapThingDefCountRangeList(List<ThingDefCountRangeClass> list)
        {
            if (list == null)
                return false;

            bool changed = false;
            foreach (ThingDefCountRangeClass entry in list)
            {
                if (entry != null && TryGetReplacement(entry.thingDef, out ThingDef replacement))
                {
                    entry.thingDef = replacement;
                    changed = true;
                }
            }
            return changed;
        }

        public static bool RemapFilter(ThingFilter filter)
        {
            if (filter == null)
                return false;

            bool changed = false;
            foreach (KeyValuePair<ThingDef, ThingDef> pair in ReplacementMap)
            {
                if (!filter.Allows(pair.Key))
                    continue;

                filter.SetAllow(pair.Key, false);
                filter.SetAllow(pair.Value, true);
                changed = true;
            }
            return changed;
        }

        public static bool TryGetReplacement(ThingDef source, out ThingDef replacement)
        {
            if (source != null && ReplacementMap.TryGetValue(source, out replacement) && replacement != null && replacement != source)
                return true;

            replacement = null;
            return false;
        }

        private static int MakeMergedLeathersInert()
        {
            StuffCategoryDef leathery = DefDatabase<StuffCategoryDef>.GetNamedSilentFail("Leathery");
            int removedCategories = 0;

            foreach (ThingDef oldLeather in ReplacementMap.Keys)
            {
                if (oldLeather?.stuffProps == null)
                    continue;

                oldLeather.stuffProps.allowedInStuffGeneration = false;
                oldLeather.generateAllowChance = 0f;

                if ((settings == null || settings.removeLeatheryCategoryFromMergedLeathers)
                    && leathery != null
                    && oldLeather.stuffProps.categories != null
                    && oldLeather.stuffProps.categories.Remove(leathery))
                {
                    removedCategories++;
                }
            }

            if ((settings == null || settings.removeLeatheryCategoryFromMergedLeathers) && leathery == null)
                Log.Warning("[革統合] StuffCategoryDef 'Leathery' が見つからないため旧革カテゴリ除外を省略します。");

            return removedCategories;
        }

        private static void ResetStuffCaches()
        {
            try
            {
                GenStuff.ResetStaticData();
                PawnApparelGenerator.Reset();
                PawnWeaponGenerator.Reset();
            }
            catch (Exception ex)
            {
                Log.Warning($"[革統合] 素材生成キャッシュの再構築に失敗: {ex.Message}");
            }
        }

        private static bool IsMergeCandidate(ThingDef def)
        {
            return def != null
                && def.stuffProps != null
                && animalLeathers != null
                && animalLeathers.Contains(def)
                && IsLeatheryStuff(def);
        }

        private static bool IsLeatheryStuff(ThingDef def)
        {
            return def?.stuffProps?.categories != null
                && def.stuffProps.categories.Any(c => c != null && c.defName == "Leathery");
        }

        private static bool IsOrdinaryLeatheryStuff(ThingDef def)
        {
            if (def?.stuffProps?.categories == null)
                return false;

            bool hasLeathery = def.stuffProps.categories.Any(c => c != null && c.defName == "Leathery");
            bool onlyOrdinaryCategories = def.stuffProps.categories.All(c => c == null || c.defName == "Leathery" || c.defName == "Fabric");
            return hasLeathery && onlyOrdinaryCategories;
        }

        private static bool HasIncompatibleStuffCategory(ThingDef def)
        {
            return def?.stuffProps?.categories != null
                && def.stuffProps.categories.Any(c => c != null && c.defName != "Leathery" && c.defName != "Fabric");
        }

        private static bool IsExtremeLeather(ThingDef def, List<ThingDef> normalTargets)
        {
            if (def?.stuffProps == null || normalTargets == null || normalTargets.Count == 0)
                return false;

            float market = def.BaseMarketValue;
            float sharp = GetStuffPower(def, StatDefOf.StuffPower_Armor_Sharp, 0f);
            float blunt = GetStuffPower(def, StatDefOf.StuffPower_Armor_Blunt, 0f);
            float heatArmor = GetStuffPower(def, StatDefOf.StuffPower_Armor_Heat, 0f);
            float cold = GetStuffPower(def, StatDefOf.StuffPower_Insulation_Cold, 0f);
            float heat = GetStuffPower(def, StatDefOf.StuffPower_Insulation_Heat, 0f);
            float hp = GetStuffFactor(def, StatDefOf.MaxHitPoints, 1f);

            float maxMarket = normalTargets.Max(x => x.BaseMarketValue);
            float maxSharp = normalTargets.Max(x => GetStuffPower(x, StatDefOf.StuffPower_Armor_Sharp, 0f));
            float maxBlunt = normalTargets.Max(x => GetStuffPower(x, StatDefOf.StuffPower_Armor_Blunt, 0f));
            float maxHeatArmor = normalTargets.Max(x => GetStuffPower(x, StatDefOf.StuffPower_Armor_Heat, 0f));
            float maxCold = normalTargets.Max(x => GetStuffPower(x, StatDefOf.StuffPower_Insulation_Cold, 0f));
            float maxHeat = normalTargets.Max(x => GetStuffPower(x, StatDefOf.StuffPower_Insulation_Heat, 0f));
            float maxHp = normalTargets.Max(x => GetStuffFactor(x, StatDefOf.MaxHitPoints, 1f));

            return AboveNormalEnvelope(market, maxMarket, 2.5f, 3f)
                || AboveNormalEnvelope(sharp, maxSharp, 1.55f, 0.15f)
                || AboveNormalEnvelope(blunt, maxBlunt, 1.75f, 0.10f)
                || AboveNormalEnvelope(heatArmor, maxHeatArmor, 1.55f, 0.25f)
                || AboveNormalEnvelope(cold, maxCold, 1.60f, 5f)
                || AboveNormalEnvelope(heat, maxHeat, 1.60f, 5f)
                || AboveNormalEnvelope(hp, maxHp, 1.60f, 0.15f);
        }

        private static bool AboveNormalEnvelope(float value, float normalMax, float multiplier, float margin)
        {
            if (normalMax <= 0f)
                return value > margin;
            return value > normalMax * multiplier + margin;
        }

        private static double LeatherDistance(ThingDef a, ThingDef b)
        {
            double score = 0d;
            score += NormalizedSq(GetStuffPower(a, StatDefOf.StuffPower_Armor_Sharp, 0f), GetStuffPower(b, StatDefOf.StuffPower_Armor_Sharp, 0f), 0.50, 1.30);
            score += NormalizedSq(GetStuffPower(a, StatDefOf.StuffPower_Armor_Blunt, 0f), GetStuffPower(b, StatDefOf.StuffPower_Armor_Blunt, 0f), 0.25, 0.70);
            score += NormalizedSq(GetStuffPower(a, StatDefOf.StuffPower_Armor_Heat, 0f), GetStuffPower(b, StatDefOf.StuffPower_Armor_Heat, 0f), 0.50, 0.50);
            score += NormalizedSq(GetStuffFactor(a, StatDefOf.MaxHitPoints, 1f), GetStuffFactor(b, StatDefOf.MaxHitPoints, 1f), 0.50, 1.00);
            score += NormalizedSq(GetStuffPower(a, StatDefOf.StuffPower_Insulation_Cold, 0f), GetStuffPower(b, StatDefOf.StuffPower_Insulation_Cold, 0f), 15.0, 0.80);
            score += NormalizedSq(GetStuffPower(a, StatDefOf.StuffPower_Insulation_Heat, 0f), GetStuffPower(b, StatDefOf.StuffPower_Insulation_Heat, 0f), 15.0, 0.80);
            score += NormalizedSq(a.BaseMarketValue, b.BaseMarketValue, 5.0, 0.50);
            return score;
        }

        private static double NormalizedSq(float a, float b, double scale, double weight)
        {
            double d = (a - b) / Math.Max(scale, 0.0001);
            return d * d * weight;
        }

        private static float GetStuffPower(ThingDef def, StatDef stat, float fallback)
        {
            if (def?.statBases == null || stat == null)
                return fallback;

            StatModifier modifier = def.statBases.FirstOrDefault(x => x.stat == stat);
            return modifier != null ? modifier.value : fallback;
        }

        private static float GetStuffFactor(ThingDef def, StatDef stat, float fallback)
        {
            if (def?.stuffProps?.statFactors == null || stat == null)
                return fallback;

            StatModifier modifier = def.stuffProps.statFactors.FirstOrDefault(x => x.stat == stat);
            return modifier != null ? modifier.value : fallback;
        }

        private static ThingDef GetThingDefMember(object obj, string memberName)
        {
            if (obj == null)
                return null;

            Type type = obj.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && typeof(ThingDef).IsAssignableFrom(field.FieldType))
                return field.GetValue(obj) as ThingDef;

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead && typeof(ThingDef).IsAssignableFrom(property.PropertyType))
                return property.GetValue(obj, null) as ThingDef;

            return null;
        }

        private static bool RemapThingDefMember(object obj, string memberName)
        {
            if (obj == null)
                return false;

            Type type = obj.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && typeof(ThingDef).IsAssignableFrom(field.FieldType))
            {
                ThingDef oldValue = field.GetValue(obj) as ThingDef;
                if (TryGetReplacement(oldValue, out ThingDef replacement))
                {
                    field.SetValue(obj, replacement);
                    return true;
                }
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead && property.CanWrite && typeof(ThingDef).IsAssignableFrom(property.PropertyType))
            {
                ThingDef oldValue = property.GetValue(obj, null) as ThingDef;
                if (TryGetReplacement(oldValue, out ThingDef replacement))
                {
                    property.SetValue(obj, replacement, null);
                    return true;
                }
            }

            return false;
        }

        private static int AuditRemainingKnownReferences(List<ThingDef> allThings, List<RecipeDef> recipes)
        {
            int total = 0;
            int logged = 0;
            const int logLimit = 50;

            Action<string, ThingDef> report = (where, value) =>
            {
                if (value == null || !ReplacementMap.ContainsKey(value))
                    return;

                total++;
                if (logged < logLimit)
                {
                    Log.Warning($"[革統合][未解決参照] {where} -> {value.defName}");
                    logged++;
                }
            };

            foreach (RecipeDef recipe in recipes)
            {
                if (recipe?.products != null)
                {
                    foreach (ThingDefCountClass product in recipe.products)
                    {
                        report($"RecipeDef {recipe.defName}.products", product?.thingDef);
                        report($"RecipeDef {recipe.defName}.products.stuff", product?.stuff);
                    }
                }
            }

            foreach (ThingDef def in allThings)
            {
                if (def == null)
                    continue;

                report($"ThingDef {def.defName}.race.leatherDef", def.race?.leatherDef);
                report($"ThingDef {def.defName}.defaultStuff", def.defaultStuff);
                AuditCountList(def.costList, $"ThingDef {def.defName}.costList", report);
                if (def.costListForDifficulty != null)
                    AuditCountList(def.costListForDifficulty.costList, $"ThingDef {def.defName}.costListForDifficulty", report);
                AuditCountList(def.butcherProducts, $"ThingDef {def.defName}.butcherProducts", report);
                AuditCountList(def.smeltProducts, $"ThingDef {def.defName}.smeltProducts", report);
                AuditCountList(def.killedLeavings, $"ThingDef {def.defName}.killedLeavings", report);
                AuditCountList(def.killedLeavingsPlayerHostile, $"ThingDef {def.defName}.killedLeavingsPlayerHostile", report);

                if (def.killedLeavingsRanges != null)
                {
                    foreach (ThingDefCountRangeClass entry in def.killedLeavingsRanges)
                        report($"ThingDef {def.defName}.killedLeavingsRanges", entry?.thingDef);
                }

                if (def.comps == null)
                    continue;

                foreach (CompProperties comp in def.comps)
                {
                    if (comp is CompProperties_Shearable shearable)
                        report($"ThingDef {def.defName}.CompShearable.woolDef", shearable.woolDef);
                    report($"ThingDef {def.defName}.{comp?.GetType().Name}.scaleDef", GetThingDefMember(comp, "scaleDef"));
                }
            }

            if (total > logged)
                Log.Warning($"[革統合][未解決参照] ほか {total - logged} 件（ログ上限 {logLimit} 件）");

            return total;
        }

        private static void AuditCountList(List<ThingDefCountClass> list, string where, Action<string, ThingDef> report)
        {
            if (list == null)
                return;

            foreach (ThingDefCountClass entry in list)
            {
                report(where, entry?.thingDef);
                report(where + ".stuff", entry?.stuff);
            }
        }
    }
}
