using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrivateLeatherConsolidator
{
    public sealed class LeatherConsolidatorGameComponent : GameComponent
    {
        private static readonly List<Thing> tmpHeldThings = new List<Thing>();

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
                int mapStackChanges = 0;
                int heldStackChanges = 0;

                foreach (Map map in Current.Game.Maps)
                {
                    if (settings == null || settings.migrateExistingBills)
                        billChanges += MigrateBills(map);

                    if (settings == null || settings.migrateExistingRawLeatherStacks)
                        mapStackChanges += MigrateSpawnedRawLeatherStacks(map);

                    if (settings == null || settings.migrateHeldRawLeatherStacks)
                        heldStackChanges += MigrateHeldThingsOnMap(map);
                }

                if (settings == null || settings.migrateHeldRawLeatherStacks)
                    heldStackChanges += MigrateCaravanHeldThings();

                Log.Message($"[革統合] 既存セーブ移行: Bill補正 {billChanges} / マップ生革 {mapStackChanges} / 所持・コンテナ生革 {heldStackChanges}");
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
                    if (bill?.ingredientFilter != null && LeatherConsolidatorBootstrap.RemapFilter(bill.ingredientFilter))
                        changed++;
                }
            }

            return changed;
        }

        private static int MigrateSpawnedRawLeatherStacks(Map map)
        {
            int changed = 0;

            foreach (Thing oldThing in map.listerThings.AllThings.ToList())
            {
                if (oldThing == null || oldThing.Destroyed || !oldThing.Spawned || oldThing.Stuff != null)
                    continue;

                if (!LeatherConsolidatorBootstrap.TryGetReplacement(oldThing.def, out ThingDef replacementDef))
                    continue;

                int originalCount = oldThing.stackCount;
                Thing replacement = ThingMaker.MakeThing(replacementDef);
                replacement.stackCount = originalCount;
                int placedCount = 0;

                GenPlace.TryPlaceThing(
                    replacement,
                    oldThing.Position,
                    map,
                    ThingPlaceMode.Near,
                    (placed, count) => placedCount += count);

                if (!replacement.Destroyed && !replacement.Spawned)
                    replacement.Destroy(DestroyMode.Vanish);

                if (placedCount <= 0)
                {
                    Log.Warning($"[革統合] 既存在庫の変換先を配置できませんでした: {oldThing.def.defName} x{originalCount}");
                    continue;
                }

                int remaining = Math.Max(0, originalCount - placedCount);
                if (remaining == 0)
                    oldThing.Destroy(DestroyMode.Vanish);
                else
                    oldThing.stackCount = remaining;

                changed++;

                if (remaining > 0)
                    Log.Warning($"[革統合] 既存在庫を一部だけ変換しました: {oldThing.def.defName} {originalCount - remaining}/{originalCount}");
            }

            return changed;
        }

        private static int MigrateHeldThingsOnMap(Map map)
        {
            tmpHeldThings.Clear();
            ThingOwnerUtility.GetAllThingsRecursively(map, tmpHeldThings, allowUnreal: true);
            return MigrateHeldThingList(tmpHeldThings);
        }

        private static int MigrateCaravanHeldThings()
        {
            int changed = 0;
            if (Find.WorldObjects?.AllWorldObjects == null)
                return 0;

            foreach (Caravan caravan in Find.WorldObjects.AllWorldObjects.OfType<Caravan>())
            {
                tmpHeldThings.Clear();
                ThingOwnerUtility.GetAllThingsRecursively(caravan, tmpHeldThings, allowUnreal: true);
                changed += MigrateHeldThingList(tmpHeldThings);
            }

            return changed;
        }

        private static int MigrateHeldThingList(List<Thing> things)
        {
            int changed = 0;

            foreach (Thing oldThing in things.ToList())
            {
                if (oldThing == null || oldThing.Destroyed || oldThing.Spawned || oldThing.Stuff != null)
                    continue;

                if (!LeatherConsolidatorBootstrap.TryGetReplacement(oldThing.def, out ThingDef replacementDef))
                    continue;

                ThingOwner owner = oldThing.holdingOwner;
                if (owner == null)
                    continue;

                Thing replacement = ThingMaker.MakeThing(replacementDef);
                replacement.stackCount = oldThing.stackCount;

                if (owner.GetCountCanAccept(replacement, canMergeWithExistingStacks: true) < replacement.stackCount)
                {
                    replacement.Destroy(DestroyMode.Vanish);
                    continue;
                }

                int oldCount = oldThing.stackCount;
                if (!owner.Remove(oldThing))
                {
                    replacement.Destroy(DestroyMode.Vanish);
                    continue;
                }

                bool added = owner.TryAdd(replacement, canMergeWithExistingStacks: true);
                if (!added && !replacement.Destroyed)
                {
                    owner.TryAdd(oldThing, canMergeWithExistingStacks: false);
                    replacement.Destroy(DestroyMode.Vanish);
                    continue;
                }

                if (!oldThing.Destroyed)
                    oldThing.Destroy(DestroyMode.Vanish);

                changed++;
                Log.Message($"[革統合] 所持・コンテナ生革変換: {oldThing.def.defName} x{oldCount} -> {replacementDef.defName}");
            }

            return changed;
        }
    }
}
