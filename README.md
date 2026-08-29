# 革統合 - RimWorld 1.6

自分用のRimWorld 1.6専用MODです。

## 目的

MODで大量に増える動物革・異種族の独自人皮を、既存の少数の革へ統合します。

- 実際にどこかの種族の `race.leatherDef` として使われている革だけを自動統合候補にします。
- Humanlikeな追加種族の通常レザーは `Leather_Human` へ統合します。
- 元の `ThingDef` 自体は削除しません。
- 種族が今後落とす革は統合先へ変更します。
- 特定革を要求する `RecipeDef` / `ThingFilter` は統合先へ補正します。
- 統合済みの旧革から `StuffCategoryDef: Leathery` を外し、通常の革材料候補から消します。
- Tick処理はありません。Def読み込み完了後に一度だけ処理します。

## 人型種族の革

`mergeHumanlikeLeathersIntoHuman` が `true` の場合、`race.Humanlike == true` の種族が持つ独自革は原則として `Leather_Human` に統合します。

ただし次のものは先に保護されるため、自動で人皮にはしません。

- `Leathery` 以外のStuffCategoryも持つ素材
- 極端に高性能な特殊素材
- `alwaysKeep` 指定
- overrideで保護した素材

そのため、Humanlikeなロボット種族が金属など特殊カテゴリ素材を落とすケースを人皮化しにくい設計です。

## 自動統合先

通常の非Humanlike革:

- `Leather_Light`
- `Leather_Plain`
- `Leather_Heavy`
- `Leather_Bird`
- `Leather_Lizard`

Humanlike種族の通常革:

- `Leather_Human`

分類は元革と統合先の素材性能を比較して最も近いものを選びます。

比較対象:

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
- 極端に高性能な特殊革
- `Leathery` 以外のStuffCategoryも持つ特殊革
- 設定ファイルの `alwaysKeep` に追加した革

## 設定

`Defs/LeatherConsolidatorSettings.xml` を直接編集します。

### Humanlike独自革を人皮へ統合

```xml
<mergeHumanlikeLeathersIntoHuman>true</mergeHumanlikeLeathersIntoHuman>
```

`false` にすると、人型種族の独自革も通常の性能分類へ回します。

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

## 既存セーブについて

旧革Defは削除しないため、すでにマップや倉庫に存在する革がDef欠落で壊れることは避ける設計です。

既存在庫そのものを強制的に別ThingDefへ変換する処理はまだ入れていません。既存スタックは残りますが、通常の `Leathery` 材料候補からは外れます。

## ビルド

GitHub ActionsでRimWorld 1.6向けDLLをビルドします。成功すると `LeatherConsolidator-RimWorld-1.6` artifact が生成されます。
