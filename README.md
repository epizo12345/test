# 革統合 - RimWorld 1.6

自分用のRimWorld 1.6専用MODです。

## 目的

MODで大量に増える動物革・異種族の独自人皮を、既存の少数の革へ統合します。

- `race.leatherDef` だけでなく、解体生成物・毛刈り・一部のscaleDefも革生産元として検出します。
- Humanlike専用の通常レザーは `Leather_Human` へ統合します。
- Humanlikeと非Humanlikeが同じ革を共有している場合は、人皮へ強制しません。
- 元の `ThingDef` はDefDatabaseから削除しません。
- 統合済み旧革は新規生成・Stuffランダム生成を停止します。
- 特定革を要求・生成するRecipe、建築コスト、解体/破壊生成物、毛刈り等の既知参照を統合先へ補正します。
- `ThingMaker.MakeThing` に軽量なHarmonyフォールバックを置き、他MODが旧革を直接生成した場合も統合先へ差し替えます。
- 統合後にStuff/Apparel/Weapon生成キャッシュを再構築します。
- Tick処理はありません。

## 自動分類

通常の非Humanlike革は次のバニラ革へ統合します。

- `Leather_Light`
- `Leather_Plain`
- `Leather_Heavy`
- `Leather_Bird`
- `Leather_Lizard`

Humanlike専用の通常革は `Leather_Human` へ統合します。

性能比較はRimWorldの素材用Statを直接参照します。

- `StuffPower_Armor_Sharp`
- `StuffPower_Armor_Blunt`
- `StuffPower_Armor_Heat`
- `StuffPower_Insulation_Cold`
- `StuffPower_Insulation_Heat`
- 最大HP倍率
- 市場価値

各項目を正規化して比較するため、市場価値だけで統合先が決まりにくいようにしています。

## 保護対象

以下は自動統合しません。

- `Leather_Human`
- `Leather_Thrumbo`
- Odyssey等のスランボ系上位革（存在する場合）
- 極端に高性能な非Humanlike特殊革
- `Leathery` 以外のStuffCategoryも持つ特殊革
- `alwaysKeep` に指定した革

旧 Optimization: Leathers の `Leather_Chitin` / `Leather_DragonScale` はLegacy互換用に保持します。
`Leather_Legend` の生革は `Leather_Thrumbo` へ移行します。

## 直接参照の補正

現在、少なくとも以下を補正します。

- `RaceProperties.leatherDef`
- `RecipeDef.ingredients`
- `RecipeDef.fixedIngredientFilter`
- `RecipeDef.defaultIngredientFilter`
- `RecipeDef.products`
- `BuildableDef.costList`
- `costListForDifficulty.costList`
- `ThingDef.defaultStuff`
- `butcherProducts`
- `smeltProducts`
- `killedLeavings`
- `killedLeavingsPlayerHostile`
- `killedLeavingsRanges`
- `CompProperties_Shearable.woolDef`
- `scaleDef` を持つComp

処理後に既知型の旧革参照が残っていれば `[革統合][未解決参照]` としてログへ出します。

## 既存セーブ移行

セーブロード後に一度だけ次を補正します。

- 既存Billの材料フィルタ
- マップ上の生革スタック
- ポーン所持品やマップ上コンテナ内の生革
- キャラバン内の生革

既存の服・防具・家具など、旧革をStuffとして持つ完成品は変更しません。

マップ上の生革変換は `GenPlace` が一部だけ配置できた場合も、実際に変換できた数量だけ旧スタックから減らします。部分失敗で革が複製されないようにしています。

## 旧 Optimization: Leathers からの移行

旧MODのpackageId `Scorpio.OptimizationLeathers` とは同時使用不可にしています。

既存セーブを読み込むため、次の旧Def名をLegacy互換Defとして保持します。

- `Leather_Legend`
- `Leather_Chitin`
- `Leather_DragonScale`

旧MODを無効化して本MODへ差し替える場合でも、これらを使った既存完成品がDef欠落しないことを目的としています。

## 設定

`Defs/LeatherConsolidatorSettings.xml` を直接編集します。

主なスイッチ:

```xml
<mergeHumanlikeLeathersIntoHuman>true</mergeHumanlikeLeathersIntoHuman>
<enableThingMakerFallback>true</enableThingMakerFallback>
<migrateExistingBills>true</migrateExistingBills>
<migrateExistingRawLeatherStacks>true</migrateExistingRawLeatherStacks>
<migrateHeldRawLeatherStacks>true</migrateHeldRawLeatherStacks>
<auditRemainingReferences>true</auditRemainingReferences>
```

### 革を必ず残す

```xml
<alwaysKeep>
  <li>Leather_Human</li>
  <li>Leather_Thrumbo</li>
  <li>SomeMod_SpecialLeather</li>
</alwaysKeep>
```

### 統合先を手動指定

```xml
<overrides>
  <li>
    <source>SomeMod_WolfLeather</source>
    <target>Leather_Heavy</target>
  </li>
</overrides>
```

`target` を空にするか、sourceと同じDefを指定すると、その革を保護します。

overrideは最終統合先まで正規化します。循環（A→B、B→Aなど）を検出した場合は警告を出し、その置換を無効化します。

## 依存MOD

- Harmony (`brrainz.harmony`)

## ビルド

GitHub ActionsでRimWorld 1.6向けDLLをビルドします。

CIでは次を確認します。

- About/Defs XMLがwell-formedであること
- `Krafs.Rimworld.Ref 1.6.4871` を使ったReleaseビルド
- MODフォルダ形式へのステージング
- `LeatherConsolidator-RimWorld-1.6` artifact生成

RimWorld本体を起動したDef解決テストまではGitHub Actionsでは行わないため、最終確認は実ゲームの起動ログで行います。
