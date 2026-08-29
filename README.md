# 革統合 - RimWorld 1.6

自分用のRimWorld 1.6専用MODです。

## 目的

MODで大量に増える動物革・異種族の独自人皮を、既存の少数の革へ統合します。

- 実際にどこかの種族の `race.leatherDef` として使われている革だけを自動統合候補にします。
- Humanlike専用の通常レザーは `Leather_Human` へ統合します。
- Humanlikeと非Humanlikeが同じ革を共有している場合は、人皮へは強制しません。
- 元の `ThingDef` 自体は削除しません。
- 種族が今後落とす革は統合先へ変更します。
- 特定革を要求する `RecipeDef` / `ThingFilter` は統合先へ補正します。
- 統合済みの旧革から `StuffCategoryDef: Leathery` を外し、通常の革材料候補から消します。
- `ThingMaker.MakeThing` に軽量なHarmonyフォールバックを置き、他MODが旧革を直接生成した場合も統合先へ差し替えます。
- Tick処理はありません。

## 人型種族の革

`mergeHumanlikeLeathersIntoHuman` が `true` の場合、その革を使っている全Raceが `Humanlike` で、かつ通常の `Leathery` 素材なら `Leather_Human` に統合します。

Humanlike専用の通常人皮については、性能が高くても `protectExtremeLeathers` より人皮統合を優先します。

ただし次は保護します。

- `Leathery` 以外のStuffCategoryも持つ素材
- `alwaysKeep` 指定
- overrideで保護した素材
- Humanlikeと非Humanlikeで共有される革

## 自動統合先

通常の非Humanlike革:

- `Leather_Light`
- `Leather_Plain`
- `Leather_Heavy`
- `Leather_Bird`
- `Leather_Lizard`

Humanlike専用の通常革:

- `Leather_Human`

通常革の分類では次を比較します。

- 刺突防御
- 打撃防御
- 熱防御
- 最大HP倍率
- 防寒
- 防暑
- 市場価値

## 常時保護

以下は自動統合しません。

- `Leather_Human`
- `Leather_Thrumbo`
- Odyssey等のスランボ系上位革（存在する場合）
- 極端に高性能な非Humanlike特殊革
- `Leathery` 以外のStuffCategoryも持つ特殊革
- 設定ファイルの `alwaysKeep` に追加した革

## 既存セーブ移行

セーブロード後に一度だけ次を補正します。

- 作業台などの既存Billが持つ材料フィルタを `旧革 -> 統合先` へ補正
- マップ上に既に存在する生革スタックを統合先の生革へ変換

既存の服・防具・家具など、旧革をStuffとして持つ完成品は変更しません。

生革変換は、新しいスタックの配置に成功した後で旧スタックを削除します。配置できなかった場合は旧スタックを残します。

現在の自動移行対象はマップ上にSpawnされている生革です。キャラバン・ポーン所持品・特殊コンテナ内の在庫は安全のため自動変換していません。

## 設定

`Defs/LeatherConsolidatorSettings.xml` を直接編集します。

主なスイッチ:

```xml
<mergeHumanlikeLeathersIntoHuman>true</mergeHumanlikeLeathersIntoHuman>
<enableThingMakerFallback>true</enableThingMakerFallback>
<migrateExistingBills>true</migrateExistingBills>
<migrateExistingRawLeatherStacks>true</migrateExistingRawLeatherStacks>
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

### overrideで保護する

`target` を空にするとその革を統合対象外にします。

```xml
<overrides>
  <li>
    <source>SomeMod_DragonLeather</source>
    <target></target>
  </li>
</overrides>
```

## 依存MOD

- Harmony (`brrainz.harmony`)

## ビルド

GitHub ActionsでRimWorld 1.6向けDLLをビルドします。成功すると `LeatherConsolidator-RimWorld-1.6` artifact が生成されます。
