# 革統合 - RimWorld 1.6

自分用のRimWorld 1.6専用MODです。

## 方針

- MODで増えた一般的な革を、既存の少数の革へ統合する。
- 人皮・スランボ系・極端な性能を持つ特殊革は保護する。
- 動物の `race.leatherDef` を起動時に一度だけ統合先へ変更する。
- 特定の革を要求する `RecipeDef` / `ThingFilter` は統合先へ補正する。
- 元の `ThingDef` 自体は削除しない。既存セーブや他MODとの互換性を優先する。
- Tick処理は行わない。

## 現在の統合先

- `Leather_Light`
- `Leather_Plain`
- `Leather_Heavy`
- `Leather_Bird`
- `Leather_Lizard`

## 常時保護

- `Leather_Human`
- `Leather_Thrumbo`
- Odyssey等のスランボ系上位革（存在する場合）
- 市場価値・防御・耐久が極端に高い特殊革

## ビルド

GitHub ActionsでRimWorld 1.6向けDLLをビルドします。成功すると `LeatherConsolidator-RimWorld-1.6` artifact が生成されます。

## 注意

初期版です。RimWorld 1.6 APIとの整合性はGitHub Actionsのコンパイル結果を基準に修正します。
