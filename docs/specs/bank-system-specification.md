# 最小銀行システム 製品仕様書

## 1. 文書管理

- Status: **Draft — Koo-approved product contracts applied**
- 対象リリース: 内部デモ版 `v0.1.0`
- 対応Issue: #7
- Parent / Control Issue: #3
- 前提決定: Issue #5、Issue #8、Issue #7のKoo承認記録、Issue #40 FND-02 D-01/D-02のKoo承認（2026-08-08）
- 前提ゲート: Requirements Ready = `PASS`
- 対象ゲート: Specification Ready = `NOT EVALUATED`

### 1.1 目的

本書は、最小銀行システムの外部から観測可能な製品挙動、状態遷移、権限、入力境界、エラー契約、データ整合性および受入条件を定義する。

本書は、DBスキーマ、認証ライブラリ、金額の物理保存形式、トランザクション分離レベル、ロック取得順序等の技術方式を決定しない。それらは後続のADRで決定する。

### 1.2 Authority

正本の優先順位は次のとおりとする。

1. Kooが承認した製品方針および本仕様
2. Accepted ADR
3. GitHub Issueで定義された作業範囲
4. コードおよび自動テスト
5. Pull Requestの説明とコメント

本書が承認されるまではDraftであり、実装の正本として使用しない。

---

## 2. システム目的と内部デモ境界

本システムは、銀行業務を事業化するためのものではなく、仕様駆動、ADR統制、AI-PR駆動、役割分離およびトレーサビリティを検証するための内部デモである。

### 2.1 対象

- 顧客登録、参照、更新、解約
- 口座自動開設
- 入金
- 通常出金、全額出金
- 口座間振込
- 取引履歴照会
- 個別ログインと役割別認可
- 管理者による利用者管理と固定役割割当
- 残高・履歴・状態の整合性
- 冪等な金銭操作
- Audit Log、障害ログ、health check、バックアップ手順

### 2.2 対象外

- 実在する顧客、口座、送金データ
- 実金融機関との接続
- 公開インターネット上の本番金融サービス
- 法令準拠済み銀行勘定系としての提供
- 多通貨
- 解約の取消、再有効化
- 複数口座を持つ顧客
- 任意の役割定義の作成・編集・削除
- Audit Logの利用者向け閲覧API・UI
- 24時間監視、復旧訓練等の本格運用
- 取引履歴の更新・削除機能

### 2.3 主インターフェース

内部デモ版の主たる外部インターフェースはREST APIとする。具体的なURI、HTTP method、JSON命名規約、OpenAPI生成方式は後続のAPI設計で決定する。

---

## 3. 用語とID

| 用語 | 意味 |
| --- | --- |
| Customer | 顧客。1件のAccountと1対1で対応する |
| Account | 顧客の口座。残高、状態、解約日時を持つ |
| Transaction | 金銭取引の業務履歴 |
| Operator | 個別ログインを行う利用者 |
| Audit Log | 認証済み操作者による業務操作と結果を追跡する記録 |
| 障害ログ | 内部例外、異常終了、依存先障害、health check異常等を診断する技術記録 |
| 顧客ID | Customerを一意に識別するID |
| 口座番号 | Accountを一意に識別する内部デモ向けの単純な識別子 |
| 振込ID | 同一振込の送金側履歴と受取側履歴を結ぶ共通識別子 |
| 冪等キー | 同じ金銭操作の再送を識別し、重複実行を防ぐ値 |
| 業務時刻 | 利用者に表示・入力されるJSTの日時 |

要件IDは `docs/traceability/requirements-register.md` の `REQ-*` を使用する。Issue #5の決定はB-01〜B-06、Issue #8の決定はD-01〜D-17として追跡する。

---

## 4. ドメイン概念と不変条件

### 4.1 Customer

Customerは最低限、次の製品上の情報を持つ。

- 顧客ID
- 氏名
- メールアドレス
- 状態: `有効` または `解約済み`
- 作成日時
- 解約日時

### 4.2 Account

Accountは最低限、次の製品上の情報を持つ。

- 口座番号
- 顧客ID
- 残高
- 状態: `有効` または `解約済み`
- 作成日時
- 解約日時

口座番号は一意でなければならない。固定桁、チェックディジット等の銀行実務形式は要求しない。採番方式はADRで決定する。

### 4.3 Transaction

Transactionは最低限、次の情報を持つ。

- 取引ID
- 口座番号
- 取引種別
- 金額
- 取引後残高
- 取引日時
- 振込時の対当口座番号
- 振込時の相手顧客ID
- 振込時の相手氏名
- 振込時の振込ID

取引種別は次の4種類とする。

1. 入金
2. 出金
3. 振込（送金）
4. 振込（受取）

全額出金は取引種別上、通常出金と同じ`出金`として記録する。

### 4.4 Operator

Operatorは最低限、次の製品上の情報を持つ。

- 利用者識別子
- ログインに使用する識別情報
- 状態: `有効` または `無効`
- 役割: `管理者`、`窓口担当者`、`閲覧者`のいずれか一つ
- 作成日時
- 更新日時

credentialの保存方式、初期管理者の作成方式、認証プロトコルはADRまたは導入手順で決定する。

### 4.5 全体不変条件

1. 1 Customerは必ず1 Accountと対応し、CustomerだけまたはAccountだけが存在する正常状態を許可しない。
2. Account残高は常に0円以上である。
3. CustomerとAccountの状態は常に一致する。
4. CustomerとAccountの解約日時は常に一致する。
5. `有効`では解約日時を設定しない。
6. `解約済み`では解約日時を必須とし、残高は0円である。
7. 残高変更と対応するTransaction作成は、両方成功または両方失敗する。
8. 振込は送金元残高、受取先残高、送金側履歴、受取側履歴のすべてが成功またはすべて失敗する。
9. Transactionは作成後に更新・削除できない。
10. 同一の冪等キーによる同一要求の再送は、残高・履歴を重複変更しない。
11. 同時実行があっても残高および各Transactionの取引後残高は正確である。
12. 通貨は日本円だけとし、金額の業務単位は円とする。物理保存形式はADRで決定する。
13. 有効な管理者が0人になる利用者管理操作を許可しない。
14. 管理者は自分自身を無効化できない。

---

## 5. Customer／Account状態遷移と解約日時

### 5.1 許可状態

| Customer状態 | Account状態 | 解約日時 | 残高 | 許可 |
| --- | --- | --- | ---: | --- |
| 有効 | 有効 | 未設定 | 0円以上 | 正常 |
| 解約済み | 解約済み | 両方同一の日時 | 0円 | 正常 |
| 有効 | 解約済み | 任意 | 任意 | 不整合 |
| 解約済み | 有効 | 任意 | 任意 | 不整合 |
| 解約済み | 解約済み | 不一致または未設定 | 任意 | 不整合 |
| 解約済み | 解約済み | 同一 | 0円以外 | 不整合 |

### 5.2 許可遷移

```text
有効 → 解約済み
```

`解約済み → 有効`は許可しない。

### 5.3 解約条件

解約は次をすべて満たす場合だけ成功する。

- 操作者が管理者または窓口担当者である
- CustomerとAccountが存在する
- CustomerとAccountの両方が有効である
- CustomerとAccountの状態が一致している
- Account残高が正確に0円である

成功時は、CustomerとAccountを同時に解約済みへ変更し、同じ解約日時を記録する。

残高が正数または負数の場合、既に解約済みの場合、状態が不整合の場合、対象が存在しない場合、権限がない場合は、CustomerとAccountのどちらも変更しない。

解約処理の中で自動出金は行わない。

### 5.4 解約後の操作

解約後は、状態別制約が第6章の一般的な役割権限より優先する。

| 操作 | 管理者 | 窓口担当者 | 閲覧者 |
| --- | :---: | :---: | :---: |
| 顧客情報参照 | 許可 | 許可 | 許可 |
| 取引履歴閲覧 | 許可 | 許可 | 許可 |
| 履歴上の取引後残高閲覧 | 許可 | 許可 | 許可 |
| 口座基本情報参照 | 拒否 | 拒否 | 拒否 |
| 現在残高の直接参照 | 拒否 | 拒否 | 拒否 |
| 顧客情報更新 | 拒否 | 拒否 | 拒否 |
| 入金 | 拒否 | 拒否 | 拒否 |
| 通常出金・全額出金 | 拒否 | 拒否 | 拒否 |
| 振込元として利用 | 拒否 | 拒否 | 拒否 |
| 振込先として利用 | 拒否 | 拒否 | 拒否 |

拒否された操作では、状態、残高、履歴、顧客情報を変更しない。

---

## 6. 操作者、認証前提、役割・権限

### 6.1 認証前提

- すべての操作者に個別ログインを必須とする。
- 無効状態のOperatorはログインおよび業務API利用を許可しない。
- 未認証の要求は業務処理を開始せず、HTTP 401 / `authentication_required`で拒否する。
- 認証方式、credential保存方式、セッション方式等はADRで決定する。
- 画面上で操作を隠すだけでは認可とせず、REST API側で必ず認可する。

### 6.2 役割

役割は次の固定3種類だけとし、役割定義自体の作成・編集・削除を許可しない。

- 管理者
- 窓口担当者
- 閲覧者

### 6.3 権限マトリクス

次の表はCustomerとAccountが有効である場合の一般権限を示す。解約後は§5.4の状態別制約を優先する。

| 操作 | 管理者 | 窓口担当者 | 閲覧者 |
| --- | :---: | :---: | :---: |
| 顧客情報参照 | 許可 | 許可 | 許可 |
| 顧客登録 | 許可 | 許可 | 拒否 |
| 顧客情報更新 | 許可 | 許可 | 拒否 |
| 顧客・口座解約 | 許可 | 許可 | 拒否 |
| 口座基本情報参照 | 許可 | 許可 | 拒否 |
| 現在残高の直接参照 | 許可 | 許可 | 拒否 |
| 入金 | 許可 | 許可 | 拒否 |
| 通常出金・全額出金 | 許可 | 許可 | 拒否 |
| 振込 | 許可 | 許可 | 拒否 |
| 取引履歴閲覧 | 許可 | 許可 | 許可 |
| 履歴上の取引後残高閲覧 | 許可 | 許可 | 許可 |
| 利用者一覧・詳細参照 | 許可 | 拒否 | 拒否 |
| 利用者作成 | 許可 | 拒否 | 拒否 |
| 利用者有効化・無効化 | 許可 | 拒否 | 拒否 |
| 固定役割の割当変更 | 許可 | 拒否 | 拒否 |

閲覧者へ返す取引履歴には、履歴上の取引後残高を含める。これは現在残高の直接照会を許可するものではない。

### 6.4 利用者管理・役割権限管理

v0.1.0では、管理者専用REST APIとして次を提供する。

- 利用者一覧・詳細参照
- 利用者作成
- 利用者の有効化・無効化
- 固定3役割の割当変更

次を禁止する。

- 最後の有効な管理者を無効化する
- 最後の有効な管理者を窓口担当者または閲覧者へ変更する
- 管理者が自分自身を無効化する
- 固定3役割以外の役割を作成・割当する

利用者作成、有効化・無効化、役割変更、および拒否された管理操作をAudit Logへ記録する。

初期管理者の作成方式、credential管理、認証実装はADRまたは導入手順で決定する。

---

## 7. 顧客登録と口座自動開設

### 7.1 入力

- 氏名
- メールアドレス

氏名およびメールアドレスは必須とする。

### 7.2 入力制約

#### 氏名

- 前後空白を除去した後、1文字以上100文字以下とする。
- 使用可能文字の詳細制限は設けない。

#### メールアドレス

- 前後空白を除去した後、254文字以下とする。
- 一般的なメールアドレス形式として妥当でなければならない。
- 小文字化した値を一意性判定に使用する。
- 具体的な正規表現、RFC細部、標準ライブラリの選択は後続設計で決定する。

### 7.3 正常結果

顧客登録は次を一つの業務処理として行う。

1. 正規化したメールアドレスの重複を確認する。
2. 有効状態のCustomerを作成する。
3. 一意な口座番号を採番する。
4. 有効状態、初期残高0円のAccountを作成する。
5. CustomerとAccountの対応を確立する。

すべて成功した場合だけ登録成功とする。

### 7.4 異常結果

- 入力制約違反はHTTP 400 / `validation_failed`で拒否する。
- 正規化後のメールアドレスが既存Customerと重複する場合はHTTP 409 / `email_already_registered`で拒否する。
- CustomerまたはAccountのどちらか一方の作成に失敗した場合、どちらも残さない。
- 権限がない場合はHTTP 403 / `operation_not_permitted`で拒否し、データを作成しない。

---

## 8. 顧客情報参照・更新

### 8.1 参照

管理者、窓口担当者、閲覧者はCustomerの顧客ID、氏名、メールアドレス、状態、作成日時、解約日時を参照できる。

閲覧者はCustomer参照を通じてAccount基本情報または現在残高を取得できない。解約後は§5.4に従う。

### 8.2 更新

管理者および窓口担当者は、有効なCustomerの氏名およびメールアドレスを更新できる。更新値は§7.2の入力制約に従う。

メールアドレス更新時は、前後空白を除去し、小文字化した値で他Customerとの重複を判定する。

次の場合は更新を拒否し、既存情報を変更しない。

- 対象Customer不存在: HTTP 404 / `customer_not_found`
- CustomerまたはAccountが解約済み: HTTP 409 / `customer_closed`
- CustomerとAccountの状態不整合: HTTP 500 / `customer_account_state_inconsistent`
- 入力制約違反: HTTP 400 / `validation_failed`
- 正規化後メールアドレス重複: HTTP 409 / `email_already_registered`
- 権限不足: HTTP 403 / `operation_not_permitted`

---

## 9. 顧客・口座解約

解約は第5章の状態遷移および条件に従う。

外部から観測される成功結果は次のとおりとする。

- CustomerとAccountの状態が解約済みになる
- CustomerとAccountへ同じ解約日時が記録される
- Account残高は0円のまま変化しない

失敗時は状態、解約日時、残高、履歴のいずれも変更しない。

---

## 10. 入金

### 10.1 入力

- 顧客IDまたは口座番号
- 入金金額
- 冪等キー

顧客IDと口座番号を両方指定する場合は、同一Accountを示さなければならない。

### 10.2 金額条件

- 最小: 1円
- 最大: 10,000,000円
- 0円、負数、10,000,001円以上は拒否する

### 10.3 正常結果

- 対象CustomerとAccountが存在し、有効であることを確認する
- Account残高へ入金額を加算する
- `入金`Transactionを作成する
- Transactionの取引後残高を、実際の加算後残高と一致させる

残高加算とTransaction作成は両方成功または両方失敗する。

### 10.4 並行処理・冪等性

第17章の冪等性契約を適用する。

複数の入金が同時に行われても、最終残高と各Transactionの取引後残高は正確でなければならない。具体的な排他方式はADRで決定する。

### 10.5 拒否条件

- 未認証: HTTP 401 / `authentication_required`
- 権限不足: HTTP 403 / `operation_not_permitted`
- Customer不存在: HTTP 404 / `customer_not_found`
- Account不存在: HTTP 404 / `account_not_found`
- 顧客IDと口座番号の不一致: HTTP 400 / `identifier_mismatch`
- 解約済み: HTTP 409 / `account_closed`
- Customer／Account状態不整合: HTTP 500 / `customer_account_state_inconsistent`
- 金額範囲外: HTTP 400 / `amount_out_of_range`
- 冪等性エラー: 第17章に従う
- 同時実行競合: HTTP 409 / `concurrent_operation_conflict`

失敗時は残高と履歴を変更しない。

---

## 11. 通常出金・全額出金

### 11.1 共通入力

- 顧客IDまたは口座番号
- 通常出金では出金金額
- 全額出金では全額指定
- 冪等キー

顧客IDと口座番号を両方指定する場合は、同一Accountを示さなければならない。

### 11.2 通常出金

- 最小金額は1円
- 固定上限は設けない
- 処理時点のAccount残高以下でなければならない
- 出金後残高は0円以上でなければならない

成功時は残高を減算し、`出金`Transactionを作成する。残高減算とTransaction作成は不可分とする。

### 11.3 全額出金

- 処理時点のAccount残高全額を出金する
- 成功後の残高は0円とする
- 残高0円の場合は、0円出金を成功させず拒否する
- 履歴上の取引種別は通常出金と同じ`出金`とする

全額として扱う残高は処理時点の整合した残高でなければならない。具体的なロック方式はADRで決定する。

### 11.4 冪等性

第17章の冪等性契約を適用する。

### 11.5 拒否条件

- 未認証: HTTP 401 / `authentication_required`
- 権限不足: HTTP 403 / `operation_not_permitted`
- Customer不存在: HTTP 404 / `customer_not_found`
- Account不存在: HTTP 404 / `account_not_found`
- 顧客IDと口座番号の不一致: HTTP 400 / `identifier_mismatch`
- 解約済み: HTTP 409 / `account_closed`
- Customer／Account状態不整合: HTTP 500 / `customer_account_state_inconsistent`
- 0円または負数の通常出金: HTTP 400 / `amount_out_of_range`
- 残高不足: HTTP 409 / `insufficient_balance`
- 残高0円での全額出金: HTTP 409 / `no_balance_to_withdraw`
- 冪等性エラー: 第17章に従う
- 同時実行競合: HTTP 409 / `concurrent_operation_conflict`

失敗時は残高・履歴を変更しない。

---

## 12. 口座間振込

### 12.1 入力

- 出金元: 顧客IDまたは口座番号
- 振込先: 顧客IDまたは口座番号
- 振込金額
- 冪等キー

同一側で顧客IDと口座番号を両方指定する場合は、同一Accountを示さなければならない。

### 12.2 条件

- 金額は1円以上10,000,000円以下
- 振込金額は処理時点の出金元残高以下
- 出金元と振込先は異なるAccount
- 出金元・振込先のCustomerとAccountが存在し、有効
- 両側のCustomer／Account状態が整合

### 12.3 正常結果

一つの振込で次をすべて行う。

1. 出金元残高を減算する
2. 振込先残高を加算する
3. 出金元に`振込（送金）`Transactionを作成する
4. 振込先に`振込（受取）`Transactionを作成する
5. 双方のTransactionへ同一の振込IDを記録する
6. 双方のTransactionへ相手口座番号、相手顧客ID、相手氏名を記録する

すべて成功した場合だけ振込成功とする。一つでも失敗した場合は、どの残高・履歴も変更しない。

### 12.4 冪等性・競合

第17章の冪等性契約を適用する。

同時振込や出金があっても出金元残高をマイナスにしない。複数Accountのロック順序等の方式はADRで決定する。

### 12.5 拒否条件

- 未認証: HTTP 401 / `authentication_required`
- 権限不足: HTTP 403 / `operation_not_permitted`
- 振込元Customer不存在: HTTP 404 / `customer_not_found`
- 振込元Account不存在: HTTP 404 / `account_not_found`
- 振込先Customer不存在: HTTP 404 / `customer_not_found`
- 振込先Account不存在: HTTP 404 / `account_not_found`
- 顧客IDと口座番号の不一致: HTTP 400 / `identifier_mismatch`
- 出金元または振込先が解約済み: HTTP 409 / `account_closed`
- Customer／Account状態不整合: HTTP 500 / `customer_account_state_inconsistent`
- 自分自身への振込: HTTP 400 / `self_transfer_not_allowed`
- 0円、負数、10,000,001円以上: HTTP 400 / `amount_out_of_range`
- 残高不足: HTTP 409 / `insufficient_balance`
- 冪等性エラー: 第17章に従う
- 同時実行競合: HTTP 409 / `concurrent_operation_conflict`

---

## 13. 取引履歴照会

### 13.1 入力

- 顧客IDまたは口座番号

両方を指定する場合は、同一Accountを示さなければならない。

### 13.2 参照権限

管理者、窓口担当者、閲覧者が参照できる。解約済みCustomer／Accountの履歴も参照できる。

閲覧者は履歴に含まれる取引後残高を閲覧できるが、現在残高の直接照会はできない。

### 13.3 返却範囲と空結果

内部デモ版ではページングを行わず、対象Accountの全Transactionを返す。

Accountが存在しTransactionが0件の場合は、HTTP 200で空配列を返す。

```json
[]
```

次を区別する。

- 既存Account・Transaction 0件: HTTP 200 / `[]`
- Customer不存在: HTTP 404 / `customer_not_found`
- Account不存在: HTTP 404 / `account_not_found`
- 顧客ID・口座番号不一致: HTTP 400 / `identifier_mismatch`

### 13.4 並び順

1. 取引日時の降順
2. 同一取引日時の場合は取引IDの降順

### 13.5 表示項目

| 項目 | 内容 |
| --- | --- |
| 取引ID | Transactionの一意識別子 |
| 日時 | JSTで表示する業務時刻 |
| 取引種別 | 入金 / 出金 / 振込（送金） / 振込（受取） |
| 取引金額 | 入金・受取は正、出金・送金は負として表示 |
| 取引後残高 | 取引直後の正確な残高 |
| 相手口座番号 | 振込時だけ表示 |
| 相手顧客ID | 振込時だけ表示 |
| 相手氏名 | 振込時だけ表示 |
| 振込ID | 振込時だけ表示し、送金側・受取側で共通 |

Transactionは更新・削除できない。

---

## 14. Audit Logと障害ログ

### 14.1 Transactionとの分離

Transactionは金銭取引の業務記録である。Audit Logと障害ログをTransactionと同一記録として扱わない。

### 14.2 Audit Log

Audit Logは認証済み操作者による業務操作を追跡し、最低限、次を記録する。

- 操作者の識別子
- 操作者の役割
- 操作種別
- 対象顧客ID、口座番号または利用者識別子
- 操作日時
- 成功または失敗
- 失敗時の業務エラーコード
- 要求を追跡できる識別子

利用者作成、有効化・無効化、役割変更、および拒否された管理操作も記録対象とする。

### 14.3 障害ログ

障害ログは、内部例外、異常終了、依存先障害、health check異常等を診断する技術記録とする。同一の失敗イベントがAudit Logと障害ログの双方へ記録される場合があるが、両者の責務は同一ではない。

### 14.4 共通禁止事項

Audit Logおよび障害ログへcredential、secret、token、不要な個人情報を記録しない。保存方式、フォーマット、改ざん防止、保持期間はADRまたは後続運用仕様で決定する。

v0.1.0ではAudit Logの記録と検証証拠取得を必須とし、利用者向け閲覧API・UIは提供しない。

---

## 15. 境界値

### 15.1 入金

| 条件 | 結果 |
| --- | --- |
| 負数 | HTTP 400 / `amount_out_of_range` |
| 0円 | HTTP 400 / `amount_out_of_range` |
| 1円 | 許可 |
| 10,000,000円 | 許可 |
| 10,000,001円 | HTTP 400 / `amount_out_of_range` |

### 15.2 通常出金

| 条件 | 結果 |
| --- | --- |
| 負数 | HTTP 400 / `amount_out_of_range` |
| 0円 | HTTP 400 / `amount_out_of_range` |
| 1円かつ残高1円以上 | 許可 |
| 現在残高と同額 | 許可、残高0円 |
| 現在残高を1円超過 | HTTP 409 / `insufficient_balance` |
| 固定上限 | なし |

### 15.3 全額出金

| 条件 | 結果 |
| --- | --- |
| 残高が正数 | 許可、処理時点の全額を出金 |
| 残高0円 | HTTP 409 / `no_balance_to_withdraw` |
| 残高が負数 | HTTP 500 / `data_integrity_violation` |

### 15.4 振込

| 条件 | 結果 |
| --- | --- |
| 負数 | HTTP 400 / `amount_out_of_range` |
| 0円 | HTTP 400 / `amount_out_of_range` |
| 1円 | 許可 |
| 10,000,000円 | 許可 |
| 10,000,001円 | HTTP 400 / `amount_out_of_range` |
| 出金元残高と同額 | 許可、出金元残高0円 |
| 残高超過 | HTTP 409 / `insufficient_balance` |
| 出金元と振込先が同一 | HTTP 400 / `self_transfer_not_allowed` |

### 15.5 状態と識別子

| 条件 | 結果 |
| --- | --- |
| 顧客IDだけ指定 | 対応Accountを解決できれば許可 |
| 口座番号だけ指定 | 対応Customerを解決できれば許可 |
| 両方指定し同一Account | 許可 |
| 両方指定し不一致 | HTTP 400 / `identifier_mismatch` |
| 解約済みAccountへの金銭操作 | HTTP 409 / `account_closed` |
| 解約済みAccountの履歴照会 | 許可 |

---

## 16. 共通REST APIエラー契約

### 16.1 共通形式

すべてのエラーは最低限、次の項目を持つ。

```json
{
  "code": "fixed_error_code",
  "message": "人が理解できる説明"
}
```

- `code`は機械判定の正本とする。
- `message`は人向け説明であり、利用側は文字列一致に依存しない。
- 同じ原因には機能が異なっても同じ固定コードを使用する。
- 内部例外、SQL、credential、secret、token、不要な個人情報を返さない。
- ASP.NET Coreのapplication pipelineへ到達し、APIがHTTP error responseを返す場合は、原則としてこの共通形式を使用する。
- Kestrel等がapplication pipeline到達前に拒否するtransport／protocol-level errorは、FND-02の共通error envelope保証対象外とする。

### 16.2 HTTP状態の役割

| HTTP状態 | 用途 |
| ---: | --- |
| 400 | 入力形式、識別子組合せ、金額範囲等が不正 |
| 401 | 未認証 |
| 403 | 認証済みだが権限不足 |
| 404 | Customer、Accountまたは対象利用者が存在しない。routingで一致するAPI endpointが存在しない場合も含む |
| 405 | endpointに対して指定HTTP methodが許可されない |
| 409 | 現在状態、残高、重複、冪等性、同時実行等との競合 |
| 415 | request media typeがendpointで受理されない |
| 500 | 保存済みデータ・内部状態の不整合、または未分類のapplication／infrastructure内部障害。内部詳細は返さない |

### 16.3 固定コード

| code | HTTP | 原因 |
| --- | ---: | --- |
| `validation_failed` | 400 | 必須入力または形式が不正 |
| `identifier_mismatch` | 400 | 顧客IDと口座番号が同一Accountを示さない |
| `amount_out_of_range` | 400 | 0円、負数、上限超過等 |
| `self_transfer_not_allowed` | 400 | 自分自身への振込 |
| `authentication_required` | 401 | 未認証または有効な認証状態がない |
| `operation_not_permitted` | 403 | 認証済みだが権限不足 |
| `customer_not_found` | 404 | Customer不存在 |
| `account_not_found` | 404 | Account不存在 |
| `operator_not_found` | 404 | Operator不存在 |
| `endpoint_not_found` | 404 | routingで一致するAPI endpointが存在しない |
| `method_not_allowed` | 405 | endpointに対して指定HTTP methodが許可されない |
| `unsupported_media_type` | 415 | request media typeがendpointで受理されない |
| `email_already_registered` | 409 | 正規化後メールアドレスが重複 |
| `operator_login_identifier_already_registered` | 409 | 正規化後Operator login identifierが重複 |
| `account_closed` | 409 | 解約済みAccountへの禁止操作 |
| `customer_closed` | 409 | 解約済みCustomerへの禁止更新 |
| `account_balance_not_zero` | 409 | 正残高のため解約不可 |
| `insufficient_balance` | 409 | 出金または振込の残高不足 |
| `no_balance_to_withdraw` | 409 | 残高0円での全額出金 |
| `state_transition_not_allowed` | 409 | 再解約、再有効化、最後の管理者喪失、自己無効化等の禁止状態遷移 |
| `idempotency_in_progress` | 409 | 同一キー・同一要求が処理中 |
| `idempotency_key_conflict` | 409 | 同一キーへ異なる要求を送信 |
| `concurrent_operation_conflict` | 409 | 競合により安全に処理できない |
| `customer_account_state_inconsistent` | 500 | CustomerとAccountの状態・解約日時等が不整合 |
| `data_integrity_violation` | 500 | 負残高、残高・履歴等の内部不整合を検出 |
| `internal_error` | 500 | 特定のbusiness semanticsを持たないapplication／infrastructure内部障害のgeneric fallback |

### 16.4 原因別の一意性

- 正残高による解約拒否は`account_balance_not_zero`、負残高検出は`data_integrity_violation`とする。
- Customer／Account状態不整合は`customer_account_state_inconsistent`、既解約・再有効化は`state_transition_not_allowed`とする。
- 自己振込、解約済み、残高不足、振込元不存在、振込先不存在をそれぞれ異なる原因として扱う。
- 認証失敗と認可失敗を401と403で区別する。
- business resource不存在は`customer_not_found`、`account_not_found`、`operator_not_found`等を使用し、endpoint不存在の`endpoint_not_found`へ統合しない。
- application pipeline内のrouting／method／media type errorは、それぞれ`endpoint_not_found`、`method_not_allowed`、`unsupported_media_type`を使用する。
- `internal_error`はgeneric fallback専用とし、`data_integrity_violation`や`customer_account_state_inconsistent`等の意味を持つcodeを流用しない。

### 16.5 再試行

API契約としてシステムによる自動リトライを保証しない。

競合時はHTTP 409 / `concurrent_operation_conflict`を返し、利用側は同一要求を同じ冪等キーで安全に再実行できなければならない。有限回の内部自動リトライ採否はADRで決定する。無期限リトライは行わない。

---

## 17. 冪等性の外部契約

### 17.1 対象

入金、通常出金、全額出金、振込に冪等キーを必須とする。

### 17.2 scope / namespace

冪等キーのscopeは、認証済み操作者と操作種別の組合せとする。同じ文字列のキーでも、操作者または操作種別が異なる場合は別キーとして扱う。

### 17.3 同一要求の判定

同一要求は、業務結果に影響する入力項目を正規化した値で判定する。

顧客ID指定と口座番号指定が最終的に同じAccountへ解決され、その他の業務入力も一致する場合は同一要求として扱う。

具体的な正規化、fingerprint、hash、保存方式はADRまたは実装設計で決定する。

### 17.4 結果を固定する区分

次の結果は冪等キーに対する確定結果とし、同一要求再送時に最初と同じ業務結果を返す。

- 成功
- 確定的な業務エラー

確定的な業務エラーには、残高不足、解約済み、自己振込、対象不存在等、同じ要求に対する業務判定として確定した結果を含む。

### 17.5 キーを消費しない区分

次は確定結果とせず、冪等キーを消費しない。同じキーで安全に再実行できる。

- HTTP 409 / `concurrent_operation_conflict`
- 内部障害
- timeoutまたは結果不明
- 業務処理開始前に検出した入力形式エラー
- 認証エラー
- 認可エラー

### 17.6 処理中再送

同一キー・同一要求が処理中に再送された場合は、HTTP 409 / `idempotency_in_progress`を返し、残高・履歴を重複変更しない。処理中応答自体は確定結果として保存しない。

### 17.7 同一キー・異なるpayload

同一scope内で同じ冪等キーへ異なる要求内容を送信した場合は、HTTP 409 / `idempotency_key_conflict`で拒否し、データを変更しない。

### 17.8 保証期間

v0.1.0では冪等性保証の失効期限を設けない。対象となる冪等性記録と関連業務データが存在する限り、同一要求再送に対する結果再現を保証する。

保持・削除方式はADRまたは後続運用仕様で決定する。

---

## 18. 不可分性と同時実行時の期待結果

| 業務処理 | すべて成功すべき結果 |
| --- | --- |
| 顧客登録 | Customer作成、Account作成、1対1対応、初期残高0円 |
| 解約 | Customer状態変更、Account状態変更、同一解約日時、残高0円維持 |
| 入金 | 残高加算、入金履歴作成、正確な取引後残高 |
| 出金 | 残高減算、出金履歴作成、正確な取引後残高 |
| 振込 | 送金元減算、受取先加算、送金履歴、受取履歴、共通振込ID |

一部だけ成功した状態を外部から観測可能にしてはならない。

同時実行時は次を満たす。

- 残高をマイナスにしない。
- 同時入金でも各履歴の取引後残高を正確にする。
- 振込の片側だけを更新しない。
- 解約と金銭操作が競合した場合、どちらか一方だけが整合した状態で成功し、他方は状態または競合エラーとなる。
- 同じ冪等キーの同一要求を複数回受信しても一回分だけ反映する。

原始要件に基づき、出金・振込の残高更新ではDB行ロックを実施する。入金は、D-16に基づき最終残高と各Transactionの取引後残高の正確性を要求するが、具体的な排他方式をDB行ロックへ固定しない。行ロックの適用対象、分離レベル、ロック順序、待機、タイムアウトおよび入金の排他方式はADRで決定する。

---

## 19. Acceptance Criteria

各異常系ACは、期待HTTP状態、固定code、非更新結果を明示する。

### 19.1 顧客登録・更新

**AC-CUS-001 顧客登録成功**

- Given 管理者または窓口担当者が認証済みで、正規化後メールアドレスが未登録
- When 有効な氏名とメールアドレスで顧客登録する
- Then 有効なCustomerと有効・残高0円のAccountが一つずつ作成される

**AC-CUS-002 メール重複**

- Given `User@Example.com`が登録済み
- When ` user@example.com `で登録または他Customerを更新する
- Then HTTP 409 / `email_already_registered`となり、データは変更されない

**AC-CUS-003 不可分な登録**

- Given Customer作成後にAccount作成を完了できない
- When 顧客登録する
- Then Customerだけを残さず処理全体が失敗する

**AC-CUS-004 有効Customer更新成功**

- Given 有効なCustomerとAccountが存在し、変更後メールアドレスが他Customerと重複しない
- When 管理者または窓口担当者が有効な氏名またはメールアドレスへ更新する
- Then 指定した情報が更新され、CustomerとAccountの状態は変わらない

**AC-CUS-005 解約済み更新拒否**

- Given CustomerとAccountが解約済み
- When 管理者または窓口担当者が氏名・メールを更新する
- Then HTTP 409 / `customer_closed`となり、既存値を変更しない

**AC-CUS-006 氏名境界**

- Given 認証済みの登録権限者
- When trim後1文字または100文字の氏名を指定する
- Then 入力を受理する
- And trim後0文字または101文字以上ではHTTP 400 / `validation_failed`となり、データを変更しない

**AC-CUS-007 メールアドレス境界**

- Given 認証済みの登録権限者
- When trim後254文字以下で一般的なメール形式として妥当な値を指定する
- Then 入力を受理する
- And 255文字以上または形式不正ではHTTP 400 / `validation_failed`となり、データを変更しない

### 19.2 解約

**AC-CLS-001 解約成功**

- Given 有効なCustomerとAccountがあり、残高が0円
- When 管理者または窓口担当者が解約する
- Then 両方が解約済みとなり、同じ解約日時が記録される

**AC-CLS-002 正残高で解約拒否**

- Given 残高が1円以上
- When 解約する
- Then HTTP 409 / `account_balance_not_zero`となり、状態と解約日時は変わらない

**AC-CLS-003 負残高検出**

- Given Account残高が負数である内部不整合を検出する
- When 解約する
- Then HTTP 500 / `data_integrity_violation`となり、部分更新しない

**AC-CLS-004 状態不整合**

- Given CustomerとAccountの状態または解約日時が不整合
- When 解約する
- Then HTTP 500 / `customer_account_state_inconsistent`となり、部分更新しない

**AC-CLS-005 再解約拒否**

- Given CustomerとAccountが既に解約済み
- When 再度解約する
- Then HTTP 409 / `state_transition_not_allowed`となり、既存の解約日時を変更しない

**AC-CLS-006 解約対象不存在**

- Given 対象CustomerまたはAccountが存在しない
- When 解約する
- Then Customer不存在はHTTP 404 / `customer_not_found`、Account不存在はHTTP 404 / `account_not_found`となり、他データを変更しない

**AC-CLS-007 解約権限不足**

- Given 閲覧者が認証済み
- When 解約する
- Then HTTP 403 / `operation_not_permitted`となり、データを変更しない

**AC-CLS-008 再有効化禁止**

- Given CustomerとAccountが解約済み
- When 有効へ戻そうとする
- Then HTTP 409 / `state_transition_not_allowed`となり、状態と解約日時を変更しない

### 19.3 解約後の参照・操作

**AC-CLOSED-001 顧客情報参照**

- Given CustomerとAccountが解約済み
- When 管理者、窓口担当者、閲覧者がそれぞれ顧客情報を参照する
- Then 各役割で参照できる

**AC-CLOSED-002 履歴と履歴上残高の参照**

- Given CustomerとAccountが解約済みでTransactionが存在する
- When 管理者、窓口担当者、閲覧者がそれぞれ履歴を参照する
- Then 履歴および履歴上の取引後残高を参照できる

**AC-CLOSED-003 口座基本情報参照拒否**

- Given CustomerとAccountが解約済み
- When 管理者、窓口担当者、閲覧者がそれぞれ口座基本情報を参照する
- Then HTTP 409 / `account_closed`となり、データは変更されない

**AC-CLOSED-004 現在残高直接参照拒否**

- Given CustomerとAccountが解約済み
- When 管理者、窓口担当者、閲覧者がそれぞれ現在残高を直接参照する
- Then HTTP 409 / `account_closed`となり、履歴上の取引後残高閲覧には影響しない

**AC-CLOSED-005 解約後金銭操作拒否**

- Given CustomerとAccountが解約済み
- When 入金、通常出金、全額出金または振込元・振込先として利用する
- Then HTTP 409 / `account_closed`となり、残高・履歴・状態を変更しない

### 19.4 入金

**AC-DEP-001 境界成功**

- Given 有効なAccount
- When 1円または10,000,000円を入金する
- Then 残高と入金履歴が一回分だけ増える

**AC-DEP-002 0円拒否**

- Given 有効なAccount
- When 0円を入金する
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-DEP-003 負数拒否**

- Given 有効なAccount
- When 負数を入金する
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-DEP-004 上限超過拒否**

- Given 有効なAccount
- When 10,000,001円を入金する
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-DEP-005 解約済み入金拒否**

- Given 解約済みAccount
- When 入金する
- Then HTTP 409 / `account_closed`となり、残高・履歴は変わらない

**AC-DEP-006 入金対象不存在**

- Given 指定したCustomerまたはAccountが存在しない
- When 入金する
- Then Customer不存在はHTTP 404 / `customer_not_found`、Account不存在はHTTP 404 / `account_not_found`となり、他Accountを変更しない

**AC-DEP-007 入金識別子不一致**

- Given 顧客IDと口座番号が異なるAccountを示す
- When 入金する
- Then HTTP 400 / `identifier_mismatch`となり、残高・履歴は変わらない

**AC-DEP-008 並行入金**

- Given 残高1,000円
- When 500円と300円の入金が並行して成功する
- Then 最終残高は1,800円で、二つの履歴の取引後残高は処理順に対応する正確な値となる

### 19.5 出金

**AC-WDR-001 通常出金成功**

- Given 残高5,000円
- When 3,000円を出金する
- Then 残高2,000円となり、3,000円の出金履歴が作成される

**AC-WDR-002 全額出金成功**

- Given 残高5,000円
- When 全額出金する
- Then 残高0円となり、取引種別`出金`、金額5,000円の履歴が作成される

**AC-WDR-003 残高0円の全額出金拒否**

- Given 残高0円
- When 全額出金する
- Then HTTP 409 / `no_balance_to_withdraw`となり、履歴は作成されない

**AC-WDR-004 残高不足**

- Given 残高5,000円
- When 5,001円を通常出金する
- Then HTTP 409 / `insufficient_balance`となり、残高・履歴は変わらない

**AC-WDR-005 通常出金0円拒否**

- Given 有効なAccount
- When 0円を通常出金する
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-WDR-006 通常出金負数拒否**

- Given 有効なAccount
- When 負数を通常出金する
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-WDR-007 解約済み出金拒否**

- Given 解約済みAccount
- When 通常出金または全額出金する
- Then HTTP 409 / `account_closed`となり、残高・履歴は変わらない

**AC-WDR-008 出金対象不存在**

- Given 指定したCustomerまたはAccountが存在しない
- When 出金する
- Then Customer不存在はHTTP 404 / `customer_not_found`、Account不存在はHTTP 404 / `account_not_found`となり、他Accountを変更しない

**AC-WDR-009 出金識別子不一致**

- Given 顧客IDと口座番号が異なるAccountを示す
- When 出金する
- Then HTTP 400 / `identifier_mismatch`となり、残高・履歴は変わらない

### 19.6 振込

**AC-TRF-001 振込成功**

- Given 出金元残高10,000円、振込先残高2,000円
- When 3,000円を振り込む
- Then 出金元7,000円、振込先5,000円となり、双方に同一振込IDの履歴が作成される

**AC-TRF-002 不可分性**

- Given 振込の一工程を完了できない
- When 振込する
- Then どちらの残高も変化せず、片側履歴も残らない

**AC-TRF-003 振込境界成功**

- Given 出金元残高が十分にある
- When 1円または10,000,000円を振り込む
- Then 振込が成功する

**AC-TRF-004 振込0円拒否**

- Given 有効な出金元・振込先
- When 0円を振り込む
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-TRF-005 振込負数拒否**

- Given 有効な出金元・振込先
- When 負数を振り込む
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-TRF-006 振込上限超過拒否**

- Given 有効な出金元・振込先
- When 10,000,001円を振り込む
- Then HTTP 400 / `amount_out_of_range`となり、残高・履歴は変わらない

**AC-TRF-007 振込残高不足**

- Given 出金元残高5,000円
- When 5,001円を振り込む
- Then HTTP 409 / `insufficient_balance`となり、双方の残高・履歴は変わらない

**AC-TRF-008 振込元不存在**

- Given 振込元CustomerまたはAccountが存在しない
- When 振込する
- Then Customer不存在はHTTP 404 / `customer_not_found`、Account不存在はHTTP 404 / `account_not_found`となり、振込先を変更しない

**AC-TRF-009 振込先不存在**

- Given 振込先CustomerまたはAccountが存在しない
- When 振込する
- Then Customer不存在はHTTP 404 / `customer_not_found`、Account不存在はHTTP 404 / `account_not_found`となり、振込元を変更しない

**AC-TRF-010 振込元解約済み**

- Given 振込元が解約済み
- When 振込する
- Then HTTP 409 / `account_closed`となり、双方の残高・履歴は変わらない

**AC-TRF-011 振込先解約済み**

- Given 振込先が解約済み
- When 振込する
- Then HTTP 409 / `account_closed`となり、双方の残高・履歴は変わらない

**AC-TRF-012 自己振込拒否**

- Given 振込元と振込先が同一Account
- When 振込する
- Then HTTP 400 / `self_transfer_not_allowed`となり、残高・履歴は変わらない

**AC-TRF-013 振込識別子不一致**

- Given 振込元または振込先で、顧客IDと口座番号が異なるAccountを示す
- When 振込する
- Then HTTP 400 / `identifier_mismatch`となり、双方の残高・履歴は変わらない

### 19.7 履歴・権限

**AC-HIS-001 履歴順序と全件返却**

- Given 複数のTransactionがあり、同一時刻の履歴も存在する
- When 履歴を照会する
- Then ページングせず全件を、取引日時降順、同一時刻では取引ID降順で返す

**AC-HIS-002 履歴必須項目**

- Given 入金、出金、振込送金、振込受取のTransactionが存在する
- When 履歴を照会する
- Then 取引ID、日時、取引種別、符号付き金額、取引後残高を返し、振込では相手口座番号、相手顧客ID、相手氏名、共通振込IDを返す

**AC-HIS-003 閲覧者の履歴上残高**

- Given 閲覧者が認証済み
- When 取引履歴を参照する
- Then 履歴上の取引後残高を閲覧できる
- And 現在残高の直接参照はHTTP 403 / `operation_not_permitted`で拒否される

**AC-HIS-004 解約後履歴参照**

- Given CustomerとAccountが解約済み
- When 権限を持つ操作者が履歴を照会する
- Then 履歴と履歴上の取引後残高を参照できる

**AC-HIS-005 履歴対象不存在**

- Given 指定したCustomerまたはAccountが存在しない
- When 履歴を照会する
- Then Customer不存在はHTTP 404 / `customer_not_found`、Account不存在はHTTP 404 / `account_not_found`となる

**AC-HIS-006 履歴識別子不一致**

- Given 顧客IDと口座番号が異なるAccountを示す
- When 履歴を照会する
- Then HTTP 400 / `identifier_mismatch`となる

**AC-HIS-007 Transaction 0件**

- Given Accountが存在しTransactionが0件
- When 履歴を照会する
- Then HTTP 200で空配列`[]`を返す

### 19.8 認証・認可

**AC-AUTH-001 未認証401**

- Given 認証されていない要求
- When 任意の業務REST APIを呼び出す
- Then HTTP 401 / `authentication_required`で拒否され、業務処理を開始せずデータを変更しない

**AC-AUTH-002 認証済み権限不足403**

- Given 閲覧者が認証済み
- When 登録、更新、解約、入金、出金または振込を行う
- Then HTTP 403 / `operation_not_permitted`で拒否され、データを変更しない

**AC-AUTH-003 API側認可**

- Given 権限のない操作者
- When 画面を経由せずREST APIを直接呼び出す
- Then HTTP 403 / `operation_not_permitted`で拒否する

**AC-AUTH-004 閲覧者の口座基本情報拒否**

- Given 有効なAccountと認証済み閲覧者
- When 口座基本情報を参照する
- Then HTTP 403 / `operation_not_permitted`で拒否される
- And 顧客情報および取引履歴の参照権限は維持される

### 19.9 利用者管理

**AC-USER-001 利用者作成**

- Given 管理者が認証済み
- When 有効な識別情報と固定3役割の一つを指定して利用者を作成する
- Then 有効状態のOperatorが作成され、Audit Logへ成功操作が記録される

**AC-USER-002 利用者一覧・詳細**

- Given 管理者が認証済み
- When 利用者一覧または詳細を参照する
- Then 利用者識別子、状態、固定役割を参照できる

**AC-USER-003 利用者の無効化・有効化**

- Given 対象Operatorが存在し、禁止条件に該当しない
- When 管理者が無効化または有効化する
- Then 状態が更新され、Audit Logへ記録される

**AC-USER-004 固定役割変更**

- Given 対象Operatorが存在し、最後の有効管理者喪失に該当しない
- When 管理者が固定3役割の別役割へ変更する
- Then 役割が更新され、Audit Logへ記録される

**AC-USER-005 非管理者403**

- Given 窓口担当者または閲覧者が認証済み
- When 利用者管理APIを呼び出す
- Then HTTP 403 / `operation_not_permitted`となり、利用者情報を変更しない

**AC-USER-006 対象利用者不存在**

- Given 指定したOperatorが存在しない
- When 管理者が詳細参照、状態変更または役割変更する
- Then HTTP 404 / `operator_not_found`となり、他Operatorを変更しない

**AC-USER-007 最後の管理者保護**

- Given 対象が最後の有効な管理者
- When 無効化または非管理者役割へ変更する
- Then HTTP 409 / `state_transition_not_allowed`となり、状態と役割を変更せず、拒否操作をAudit Logへ記録する

**AC-USER-008 自己無効化禁止**

- Given 管理者が認証済み
- When 自分自身を無効化する
- Then HTTP 409 / `state_transition_not_allowed`となり、状態を変更せず、拒否操作をAudit Logへ記録する

**AC-USER-009 任意役割禁止**

- Given 管理者が認証済み
- When 固定3役割以外を作成または割当しようとする
- Then HTTP 400 / `validation_failed`となり、役割情報を変更しない

### 19.10 冪等性・同時実行・エラー

**AC-IDEM-001 成功済み同一要求再送**

- Given 金銭操作が成功済み
- When 同じscope、同じ冪等キー、同じ要求内容で再送する
- Then 最初と同じ業務結果を返し、残高・履歴を重複変更しない

**AC-IDEM-002 同一要求の同時到着**

- Given 同じscope、同じ冪等キー、同じ要求内容の金銭操作が同時に到着する
- When 両要求を処理する
- Then 業務反映は一回分だけで、処理中側にはHTTP 409 / `idempotency_in_progress`を返す

**AC-IDEM-003 同一キー異内容**

- Given あるscopeで冪等キーが使用済みまたは処理中
- When 同じキーで異なる要求内容を送る
- Then HTTP 409 / `idempotency_key_conflict`となり、データを変更しない

**AC-IDEM-004 競合後再送**

- Given 最初の要求がHTTP 409 / `concurrent_operation_conflict`で失敗した
- When 同じscope、同じ冪等キー、同じ要求内容で再送する
- Then キーが消費されておらず、業務処理を安全に再実行できる

**AC-IDEM-005 確定的業務エラー再送**

- Given 最初の要求が残高不足等の確定的業務エラーとなった
- When 同じscope、同じ冪等キー、同じ要求内容で再送する
- Then 最初と同じHTTP/codeを返し、残高・履歴を変更しない

**AC-IDEM-006 代替識別子の同一性**

- Given 顧客IDと口座番号が同じAccountへ解決され、その他の業務入力が一致する
- When 同じscope、同じ冪等キーで識別子形式だけを変えて再送する
- Then 同一要求として扱う

**AC-IDEM-007 非消費エラー**

- Given 入力形式、認証、認可、内部障害、timeoutまたは結果不明で確定結果にならなかった
- When 同じscope、同じ冪等キーで有効な同一要求を再送する
- Then キーが消費されておらず、業務処理を実行できる

**AC-IDEM-008 保証期間**

- Given 冪等性記録と関連業務データが存在する
- When 時間経過後に同一要求を再送する
- Then v0.1.0では期限切れとして再実行せず、確定済み結果を返す

**AC-CON-001 同時出金**

- Given 残高が一件分しかない
- When 残高を超過し得る複数出金を並行実行する
- Then 成功した処理だけが反映され、残高はマイナスにならない

**AC-CON-002 同時振込または出金・振込競合**

- Given 出金元残高がすべての要求を成功させるには不足している
- When 複数振込、または出金と振込を並行実行する
- Then 成功した処理だけが不可分に反映され、残高はマイナスにならない
- And 敗北した処理はHTTP 409 / `concurrent_operation_conflict`またはHTTP 409 / `insufficient_balance`として失敗する

**AC-CON-003 解約と金銭操作の競合**

- Given 有効で残高0円のAccount
- When 解約と金銭操作が競合する
- Then 整合条件を満たす一方だけが成功し、CustomerとAccountの状態、残高、履歴が矛盾しない
- And 敗北した処理はHTTP 409 / `account_closed`またはHTTP 409 / `concurrent_operation_conflict`となる

**AC-ERR-001 エラー形式**

- Given 任意の業務エラーまたはapplication pipeline内のHTTP error
- When APIが失敗を返す
- Then HTTP状態、固定`code`、人向け`message`を返し、内部詳細やcredentialを含めない
- And application pipeline到達前のtransport／protocol-level errorは、この共通形式の保証対象外とする

### 19.11 運用・ログ

**AC-OPS-001 health check**

- Given 内部デモ環境
- When health checkを実行する
- Then 稼働可否を判定できる

**AC-OPS-002 Audit Log成功操作**

- Given 認証済み操作者の業務操作が成功する
- When Audit Logを検証する
- Then 操作者、役割、操作、対象、時刻、成功、追跡識別子を確認できる

**AC-OPS-003 Audit Log失敗操作**

- Given 認証済み操作者の業務操作が失敗する
- When Audit Logを検証する
- Then 操作者、役割、操作、対象、時刻、失敗、業務code、追跡識別子を確認できる

**AC-OPS-004 障害ログ**

- Given 内部例外またはhealth check異常が発生する
- When 障害ログを検証する
- Then 障害診断に必要な証拠を確認でき、Audit Logだけで代替されていない

**AC-OPS-005 secret非記録**

- Given Audit Logおよび障害ログが記録される
- When 記録内容を検証する
- Then credential、secret、token、不要な個人情報を含まない

**AC-OPS-006 バックアップ手順**

- Given 内部デモリリース候補
- When Release Readyを評価する
- Then バックアップ手順が存在し、検証対象として追跡できる

**AC-OPS-007 Audit Log閲覧機能対象外**

- Given v0.1.0の機能範囲
- When 利用者向けAudit Log閲覧API・UIの有無を確認する
- Then 記録と検証証拠取得は可能だが、利用者向け閲覧機能は提供しない

---

## 20. トレーサビリティ

### 20.1 REQから仕様・受入条件

| 要件ID | 主な仕様節 | 主なAcceptance Criteria |
| --- | --- | --- |
| REQ-DOM-001 | §4.5、§7 | AC-CUS-001、AC-CUS-003 |
| REQ-DOM-002 | §4.1、§5 | AC-CLS-001、AC-CLOSED-001 |
| REQ-DOM-003 | §4.2、§5 | AC-CLS-001、AC-CLS-003、AC-CLS-004 |
| REQ-DOM-004 | §4.3、§13 | AC-HIS-001、AC-HIS-002 |
| REQ-DOM-005 | §4.5、§18 | AC-WDR-004、AC-CON-001、AC-CON-002 |
| REQ-CUS-001 | §7 | AC-CUS-001、AC-CUS-003 |
| REQ-CUS-002 | §7 | AC-CUS-002 |
| REQ-CUS-003 | §8 | AC-CUS-004、AC-CUS-005 |
| REQ-CUS-004 | §8 | AC-CUS-002、AC-CUS-004 |
| REQ-CUS-005 | §5、§9 | AC-CLS-001、AC-CLS-005〜008 |
| REQ-CUS-006 | §5、§9 | AC-CLS-002〜004 |
| REQ-DEP-001 | §10、§15.1 | AC-DEP-001〜008 |
| REQ-WDR-001 | §11 | AC-WDR-001、AC-WDR-002、AC-WDR-007〜009 |
| REQ-WDR-002 | §11.2、§15.2 | AC-WDR-001、AC-WDR-004〜006 |
| REQ-WDR-003 | §11.3、§15.3 | AC-WDR-002、AC-WDR-003 |
| REQ-WDR-004 | §11、§15.2〜15.3 | AC-WDR-003〜006 |
| REQ-TRF-001 | §12.1〜12.2 | AC-TRF-001、AC-TRF-003〜006 |
| REQ-TRF-002 | §12.3 | AC-TRF-001、AC-TRF-002 |
| REQ-TRF-003 | §12.5 | AC-TRF-007〜012 |
| REQ-TRF-004 | §12.3、§18 | AC-TRF-002、AC-CON-002 |
| REQ-HIS-001 | §13.1〜13.4 | AC-HIS-001、AC-HIS-004〜007 |
| REQ-HIS-002 | §13.5 | AC-HIS-002、AC-HIS-003 |
| REQ-CON-001 | §18 | AC-DEP-008、AC-CON-001〜003 |
| REQ-VAL-001 | §15、§16 | AC-DEP-002〜004、AC-WDR-005〜006、AC-TRF-004〜006 |

NIT-R001対応として、REQ-VAL-001から識別子不一致のAC-TRF-013を除外した。

### 20.2 Blocking Decision

| 決定 | 主な仕様節 | 主なAcceptance Criteria |
| --- | --- | --- |
| B-01 | §5.4、§8、§10〜13 | AC-CLOSED-001〜005、AC-CUS-005、AC-DEP-005、AC-WDR-007、AC-TRF-010〜011、AC-HIS-004 |
| B-02 | §4.1〜4.2、§5、§9 | AC-CLS-001〜008 |
| B-03 | §11〜12、§15 | AC-WDR-001〜006、AC-TRF-003〜007 |
| B-04 | §6、§14、§19.8〜19.9 | AC-AUTH-001〜004、AC-USER-001〜009、AC-OPS-002〜003 |
| B-05 | §2.3、§16 | AC-AUTH-001〜002、AC-ERR-001および原因別拒否AC |
| B-06 | §4.5、§7、§9〜12、§18 | AC-CUS-003、AC-TRF-002、AC-CON-001〜003 |

### 20.3 Phase 2 Decision

| 決定 | 主な仕様節 | 主なAcceptance Criteria |
| --- | --- | --- |
| D-01 | §12.1、§15.5 | AC-TRF-013 |
| D-02 | §13.1、§15.5 | AC-HIS-006 |
| D-03 | §4.3、§13.5 | AC-HIS-002 |
| D-04 | §4.3、§11.3、§13.5 | AC-WDR-002、AC-HIS-002 |
| D-05 | §11.3、§15.3 | AC-WDR-003 |
| D-06 | §13.3 | AC-HIS-001、AC-HIS-007 |
| D-07 | §4.5 | 金額系全AC |
| D-08 | §7、§8 | AC-CUS-002、AC-CUS-004、AC-CUS-007 |
| D-09 | §13.4 | AC-HIS-001 |
| D-10 | §4.2、§7 | AC-CUS-001 |
| D-11 | §3、§13.5、§14 | AC-HIS-002、AC-OPS-002〜004 |
| D-12 | §2.1、§14、§19.11 | AC-OPS-001〜007 |
| D-13 | §4.5、§10〜12、§17、§19.10 | AC-IDEM-001〜008 |
| D-14 | §4.5、§13.5 | AC-HIS-001〜002 |
| D-15 | §4.3、§12.3、§13.5 | AC-TRF-001、AC-HIS-002 |
| D-16 | §10.4、§18、§19.4 | AC-DEP-008 |
| D-17 | §16.5、§17〜18、§19.10 | AC-IDEM-004、AC-IDEM-007、AC-CON-001〜003 |

---

## 21. Out of scope

- 物理DBスキーマとmigration
- 金額データ型
- 具体的な認証プロトコル・ライブラリ
- credential保存方式
- 初期管理者の具体的な作成手段
- DBトランザクション分離レベル
- 行ロック対象、取得順、待機、タイムアウトの具体方式
- 入金の具体的な排他方式
- 冪等キーの保存、fingerprint、hash、削除方式
- 口座番号の採番アルゴリズム
- 日時の物理保存方式
- Audit Logおよび障害ログの保持期間・保護技術
- Audit Logの利用者向け閲覧API・UI
- APIの具体的なURI命名、JSONの命名規約、OpenAPI生成方式
- UI
- 実金融サービスに必要な法令・本人確認・不正検知・限度額管理

---

## 22. ADR候補と仕様から分離した技術事項

| ADR候補 | 本仕様で固定した要求 | ADRで決める方式 |
| --- | --- | --- |
| ADR-CANDIDATE-001 | 日本円、残高0円以上 | 金額の物理保存形式 |
| ADR-CANDIDATE-002 | 登録・残高・履歴・振込の不可分範囲 | DBトランザクション方式 |
| ADR-CANDIDATE-003 | 出金・振込ではDB行ロックを使用する。入金は並行時も残高・履歴を正確にする | 出金・振込のロック対象、分離レベル、待機、タイムアウト、および入金の排他方式 |
| ADR-CANDIDATE-004 | 振込で部分成功・デッドロックを回避する | 複数Accountの決定的ロック順序 |
| ADR-CANDIDATE-005 | 金銭操作の冪等性外部契約 | 冪等キーの保存、正規化、fingerprint、hash、照合、保持・削除方式 |
| ADR-CANDIDATE-006 | Customer／Accountに有効・解約済み状態と解約日時を持たせる | 状態・論理削除の物理表現 |
| ADR-CANDIDATE-007 | Transactionの更新・削除を禁止する | 追記専用等の実装方式 |
| ADR-CANDIDATE-008 | 同一時刻の履歴順序を決定的にする | 順序キーの生成・保存方式 |
| ADR-CANDIDATE-009 | 単純で一意な口座番号 | 採番方式 |
| ADR-CANDIDATE-010 | 業務時刻はJST | DB日時保存方式 |
| ADR-CANDIDATE-011 | 個別ログイン、固定3役割、API認可、利用者状態 | 認証、credential、初期管理者、セッション、利用者管理の実装方式 |
| ADR-CANDIDATE-012 | Audit LogをTransactionと別記録として保持する | Audit Logの保存、保護、保持方式 |
| ADR-CANDIDATE-013 | APIは自動リトライを保証しない | 有限回の内部リトライ採否 |
| ADR-CANDIDATE-014 | バックアップ手順を提供する | バックアップ・復旧の具体方式 |

候補IDは後続Issueから参照するための安定識別子であり、ADRの作成・採用・承認を意味しない。

---

## 23. Koo承認の反映状態

### 23.1 承認済み事項

2026-08-02にKooが次の6トピックを承認した。

1. 固定エラーコードとHTTP状態
   - §16の契約を採用する。
   - `state_transition_not_allowed`、`idempotency_in_progress`、`idempotency_key_conflict`を含む。
2. 氏名・メールアドレス制約
   - 氏名はtrim後1〜100文字、詳細文字制限なし。
   - メールはtrim後254文字以下、一般的な形式、lowercase後一意性。
   - 詳細検証方式は後続設計へ委譲する。
3. Audit Log閲覧機能
   - 記録と検証証拠取得は必須。
   - 利用者向け閲覧API・UIはv0.1.0対象外。
4. F-003 冪等性外部契約
   - §17のscope、要求同一性、確定区分、非消費区分、処理中、異payload、保証期間を採用する。
5. F-004 利用者管理・役割権限管理
   - 管理者専用REST API、最低操作集合、状態、固定3役割、管理者保護、Audit Logを採用する。
6. F-008 Transaction 0件時の履歴レスポンス
   - HTTP 200と空配列`[]`を採用する。
7. FND-02 共通API実行契約追補（Issue #40、2026-08-08）
   - generic internal failureはHTTP 500 / `internal_error` / `An internal error occurred.`とする。
   - application pipeline内の404、405、415は、それぞれ`endpoint_not_found`、`method_not_allowed`、`unsupported_media_type`で共通envelope化する。
   - application pipeline到達前のtransport／protocol-level errorは、この保証対象外とする。

### 23.2 独立レビューNit対応

- NIT-R001: REQ-VAL-001追跡行からAC-TRF-013を除外した。
- NIT-R002: 原因別異常ACへ期待HTTP状態、固定code、非更新結果を反映した。

### 23.3 Finding対応状態

| Finding | 状態 |
| --- | --- |
| F-001 | 修正済み、最終独立確認待ち |
| F-002 | 修正済み、最終独立確認待ち |
| F-003 | Koo承認反映済み、最終独立確認待ち |
| F-004 | Koo承認反映済み、最終独立確認待ち |
| F-005 | Koo承認反映済み、最終独立確認待ち |
| F-006 | 修正済み、最終独立確認待ち |
| F-007 | 修正済み、最終独立確認待ち |
| F-008 | Koo承認反映済み、最終独立確認待ち |
| F-009 | Koo承認反映済み、最終独立確認待ち |
| N-001 | 修正済み、最終独立確認待ち |
| N-002 | 修正済み、最終独立確認待ち |
| NIT-R001 | 修正済み、最終独立確認待ち |
| NIT-R002 | 修正済み、最終独立確認待ち |

### 23.4 ゲート状態

本書作成時点でSpecification Readyは`NOT EVALUATED`である。

本書の最終独立確認および別工程でのゲート再評価が完了するまで、ADRの確定、実装Issue分割、アプリケーション実装へ進まない。
