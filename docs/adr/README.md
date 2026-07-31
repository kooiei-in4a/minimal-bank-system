# Architecture Decision Records

変更コストが高く、複数機能へ影響する重要な設計判断を記録します。

## ADR化の対象

- データモデルとトランザクション境界
- 金額表現
- 残高更新の排他制御
- 複数口座のロック順序
- 冪等性
- 論理削除
- 取引履歴の不変性
- migrationとロールバック方式

## 状態

`Proposed → Accepted → Superseded / Rejected`

局所的で容易に変更可能な実装判断までADR化しません。
