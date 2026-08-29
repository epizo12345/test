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
- 本MOD独自の革ThingDefは追加しません。
- Tick処理はありません。

## 自動分類

通常の非Humanlike革は次のバニラ革へ統合します。

- `Leather_Light`
- `Leather_Plain`
- `Leather_Heavy`
- `Leather_Bird`
- `Leather_Lizard`

Humanlike専用の通常革は `Leather_Human` へ統合します。

性能比較には素材用Stat、防寒・防暑、最大HP倍率、市場価値を使い、各項目を正規化して比較します。

## 保護対象

以下は自動統合しません。

- `Leather_Human`
- `Leather_Thrumbo`
- Odyssey等のスランボ系上位革
- 通常5革の性能範囲を大きく超える特殊革
- `Leathery` / `Fabric` 以外の特殊StuffCategoryを持つ素材
- ゲーム内設定で「常時保護」に指定した革

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

## ゲーム内設定

RimWorldの **オプション → MOD設定 → 革統合 - 1.6** から設定します。

主な設定:

- Humanlike専用革を人皮へ統合
- 特殊高性能革を自動保護
- 特殊StuffCategory革を自動保護
- 統合済み旧革をLeatheryカテゴリから外す
- ThingMaker生成時フォールバック
- 既存Bill / 生革 / 所持品等の移行
- 未解決参照監査
- 詳細ログ
- 常時保護する革の追加・削除
- 手動統合 `source → target` の追加・削除
- 現在の置換一覧
- 現在の保護一覧と保護理由

現在の置換一覧では、統合されている革を **「保護に追加」** ボタンで次回起動から保護できます。

設定はRimWorld標準のModSettingsへ保存されます。Defの統合処理は起動時に一度だけ行うため、設定変更後はRimWorldを再起動してください。

`Defs/LeatherConsolidatorSettings.xml` は内部のランタイム用Defであり、ユーザーが編集する必要はありません。

## 依存MOD

- Harmony (`brrainz.harmony`)

旧 Optimization: Leathers (`Scorpio.OptimizationLeathers`) とは同時使用しないでください。

## ビルド

GitHub ActionsでRimWorld 1.6向けDLLをビルドします。

CIでは次を確認します。

- About/Defs XMLがwell-formedであること
- `Krafs.Rimworld.Ref 1.6.4871` を使ったReleaseビルド
- MODフォルダ形式へのステージング
- `LeatherConsolidator-RimWorld-1.6` artifact生成

RimWorld本体を起動したDef解決・UI操作テストまではGitHub Actionsでは行わないため、最終確認は実ゲームで行います。
