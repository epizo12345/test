using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrivateLeatherConsolidator
{
    public sealed class LeatherConsolidatorGameComponent : GameComponent
    {
        public LeatherConsolidatorGameComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            LongEventHandler.ExecuteWhenFinished(MigrateLoadedGame);
        }

        private static void MigrateLoadedGame()
        {
            try
            {
                LeatherConsolidatorSettingsDef settings = LeatherConsolidatorBootstrap.Settings;
                int billChanges = 0;
                int stackChanges = 0;

                foreach (Map map in Current.Game.Maps)
                {
                    if (settings == null || settings.migrateExistingBills)
                        billChanges += MigrateBills(map);

                    if (settings == null || settings.migrateExistingRawLeatherStacks)
                        stackChanges += MigrateRawLeatherStacks(map);
                }

                Log.Message($"[革統合] 既存セーブ移行: Bill補正 {billChanges} / 生革スタック変換 {stackChanges}");
            }
            catch (Exception ex)
            {
                Log.Error($"[革統合] 既存セーブ移行で例外: {ex}");
            }
        }

        private static int MigrateBills(Map map)
        {
            int changed = 0;

            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                if (!(thing is IBillGiver billGiver) || billGiver.BillStack == null)
                    continue;

                foreach (Bill bill in billGiver.BillStack.Bills)
                {
                    ThingFilter filter = GetBillIngredientFilter(bill);
                    if (filter != null && LeatherConsolidatorBootstrap.RemapFilter(filter))
                        changed++;
                }
            }

            return changed;
        }

        private static ThingFilter GetBillIngredientFilter(Bill bill)
        {
            if (bill == null)
                return null;

            var property = AccessTools.Property(bill.GetType(), "ingredientFilter");
            if (property != null && typeof(ThingFilter).IsAssignableFrom(property.PropertyType))
                return property.GetValue(bill, null) as ThingFilter;

            var field = AccessTools.Field(bill.GetType(), "ingredientFilter");
            if (field != null && typeof(ThingFilter).IsAssignableFrom(field.FieldType))
                return field.GetValue(bill) as ThingFilter;

            return null;
        }

        private static int MigrateRawLeatherStacks(Map map)
        {
            int changed = 0;

            foreach (Thing oldThing in map.listerThings.AllThings.ToList())
            {
                if (oldThing == null || oldThing.Destroyed || !oldThing.Spawned)
                    continue;

                if (!LeatherConsolidatorBootstrap.ReplacementMap.TryGetValue(oldThing.def, out ThingDef replacementDef))
                    continue;

                if (oldThing.Stuff != null)
                    continue;

                int stackCount = oldThing.stackCount;
                Thing replacement = ThingMaker.MakeThing(replacementDef);
                replacement.stackCount = stackCount;

                if (!GenPlace.TryPlaceThing(replacement, oldThing.Position, map, ThingPlaceMode.Near))
                {
                    replacement.Destroy(DestroyMode.Vanish);
                    Log.Warning($"[革統合] 既存在庫の変換先を配置できませんでした: {oldThing.def.defName} x{stackCount}");
                    continue;
                }

                oldThing.Destroy(DestroyMode.Vanish);
                changed++;
            }

            return changed;
        }
    }
}
