# spec-fix-001 Evidence Bundle

## Dataset metadata

- Benchmark ID: `spec-fix-001`
- Input dataset ID: `spec-fix-001-portable-v1`
- Repository label: `kooiei-in4a/minimal-bank-system`
- Fixed Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Fixed Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`
- Target blob SHA-1: `95097dbbe66eba6c32a18db8121db7e7f93d43d1`
- Document count: 10
- Generated: `2026-08-02`
- External access required: no

## Integrity and independence

This bundle is the complete factual input for the repair model. It contains the full target specification, the fixed target diff, authority material, approved decisions, final findings, approval items, and exclusions.

It intentionally excludes individual Round 1 / Round 2 model outputs, model names, scores, rankings, evaluation rubric, and Gold Fix criteria.

Each embedded document is delimited by `<document>` and `</document>`. SHA-256 is calculated from the exact UTF-8 text inside the delimiter, excluding the trailing newline before `</document>`.

## Document index

| Document ID | Title | SHA-256 |
|---|---|---|
| `DOC-GOV-001` | Governance and authority | `b808c28f561574c17edacdd9204f28500e35119f43fe7255bdba00611e3c5546` |
| `DOC-REQ-001` | Original requirements | `b22e5566cabbd4821b51dc22d6895865528fcee5147d5a976aef986b74ee0dc1` |
| `DOC-TRACE-001` | Requirements register digest | `e974f6475545ddb5717bd975690a018dcba9b7034708aec3b671aafe3f531168` |
| `DOC-B-001` | Approved B-01 to B-06 decisions | `3d156e63117afc7b9b089bb3a4bfcf83420aec211d9a441409b05c881464d20e` |
| `DOC-D-001` | Approved D-01 to D-17 decisions | `144b48e0553fba14c97d7a40bc265c9a4297d51d5e5da5a675c4adc1432aafed` |
| `DOC-DIFF-001` | Fixed target diff | `03343586513a7035f0bd964d5786c2ebf8fa0d731653a3b55382a96e6ac58d5a` |
| `DOC-SPEC-001` | Target specification full text | `c9d448105408dd75a6130ef42cc7ebf4529264f777945d370353d71f0710e368` |
| `FINAL-FINDINGS-001` | Final adjudicated findings | `506721dfa9440aa0bb50c0dac27125b1563c26e5528dc41be406e6b1122887ec` |
| `DOC-APPROVAL-001` | Approval items | `902e35d0763353a9db5d4744fe4d5774b5c2d97ae46c69103fa4905aef39930c` |
| `DOC-EXCLUSION-001` | Scope exclusions | `70d8447f206455e91e87d110f45c087daf0653d4abac42130edd2f0ffede5563` |

## Embedded documents

<document id="DOC-GOV-001" title="Governance and authority">
# Governance and authority

## Authority order

1. Kooが承認した製品方針・仕様判断
2. Accepted ADR
3. GitHub Issueで定義された作業範囲
4. コード・自動テスト
5. Pull Requestの説明・コメント

今回の固定対象時点では、B-01〜B-06およびD-01〜D-17が最上位の具体的製品判断である。対象仕様はDraftであり、承認前は実装正本ではない。

## Phase and gate

- Project phase: Phase 2（仕様化と受入条件の固定）
- Requirements Ready: PASS
- Specification Ready: NOT EVALUATED
- 対象仕様の独立レビュー、必要修正、Koo承認、別工程でのゲート再評価が終わるまで、ADR確定、実装Issue分割、アプリケーション実装へ進まない。

## Issue #7修正統制

仕様は次を定義する。

- 外部から観測可能な製品挙動
- 状態遷移
- 権限
- エラー契約
- 不変条件
- 境界値
- Given / When / Thenまたは同等に検証可能なAcceptance Criteria
- REQ、B、D、仕様節、ACの追跡
- ADRへ分離する技術方式

仕様へ混入させない。

- DBスキーマ、migration
- 認証ライブラリ
- transaction isolation
- lock対象・順序・timeoutの具体方式
- API URI・JSON命名
- UI
- 実金融サービス要件

## Issue #10レビュー統制

レビューは固定Base SHA / Head SHA / Target fileだけを対象とする。レビュー担当は対象仕様を変更せず、正本との欠落・改変・矛盾、AC、追跡、技術方式先取り、未承認判断を検査する。

修正ベンチマークでも、最終確定Findingだけを修正し、未承認判断をモデルが代行してはならない。
</document>
<document id="DOC-REQ-001" title="Original requirements">
# 最小銀行システム 要件定義書（MVP版）

## 1. ドメインモデルの前提構造
実装の簡略化のため、**「1顧客 ＝ 1口座」**（1対1の関係）と定義します。

| エンティティ | 主な属性 |
| :--- | :--- |
| **顧客 (Customer)** | 顧客ID、氏名、メールアドレス、ステータス（有効/解約済）、作成日時 |
| **口座 (Account)** | 口座番号、顧客ID（FK）、残高、作成日時 |
| **取引履歴 (Transaction)** | 取引ID、口座番号（FK）、取引種別、金額、取引後残高、対当口座番号、取引日時 |

---

## 2. 機能要件詳細

### 2.1 顧客・口座メンテナンス

*   **顧客登録（＋口座自動開設）**
    *   **入力:** 氏名、メールアドレス
    *   **処理:** 
        1. メールアドレスの一意性（重複がないか）を検証。
        2. 顧客レコードを作成。
        3. 口座番号を自動採番し、**初期残高 0円** の口座を作成。
    *   **エラー制約:** すでに登録済みのメールアドレスの場合は登録不可。

*   **顧客情報更新**
    *   **入力:** 顧客ID、氏名、メールアドレス
    *   **処理:** 氏名・メールアドレスを変更可能。
    *   **エラー制約:** 変更後のメールアドレスが**他の顧客**と重複する場合は更新不可。

*   **顧客削除（解約）**
    *   **入力:** 顧客ID
    *   **処理:** 該当顧客および口座のステータスを「解約済み」に変更（**論理削除**を推奨）。
    *   **エラー制約:** **残高が 0円 でない場合は削除不可**（1円以上残高がある、またはマイナスの場合はエラー）。

---

### 2.2 入金・出金機能

*   **入金**
    *   **入力:** 顧客ID（または口座番号）、入金金額
    *   **制約:** 1回あたり **1円以上 ～ 最大 10,000,000円（1,000万円）** まで。
    *   **処理:** 口座残高に加算し、取引履歴に「入金」として記録。

*   **出金（通常出金 / 全額出金）**
    *   **入力:** 顧客ID（または口座番号）、出金金額（または「全額フラグ」）
    *   **処理:** 
        *   **通常出金:** 指定された金額を差し引く。
        *   **全額出金:** 現在の「残高全額」を出金対象とし、残高を 0円 にする。
    *   **エラー制約:** 
        *   出金後の残高がマイナスになる場合は処理不可。
        *   出金金額が 0円 以下の場合は不可。

---

### 2.3 振込機能

*   **口座間振込**
    *   **入力:** 出金元顧客ID、振込先顧客ID、振込金額
    *   **処理:** 
        1. 出金元口座から金額を引き落とす。
        2. 振込先口座へ同額を加算する。
        3. 出金元・振込先双方の取引履歴を記録する。
    *   **エラー制約:** 
        *   出金元の残高不足。
        *   振込先顧客（口座）が存在しない、または解約済み。
        *   **自分自身への振込**（出金元 ＝ 振込先）は不可。
    *   **補足:** 振込処理は「引き落とし」と「加算」が**アトミック（不可分）**に行われる必要があります。途中でエラーが起きた場合は必ず全ロールバックするようトランザクション制御を行います。

---

### 2.4 取引履歴照会

*   **履歴閲覧**
    *   **入力:** 顧客ID
    *   **出力:** 対象口座の過去の取引一覧（時系列降順：最新が一番上）
    *   **表示項目:** 

| 項目名 | 内容例 |
| :--- | :--- |
| **日時** | 2026-08-01 10:00:00 |
| **取引種別** | 入金 / 出金 / 振込（送金） / 振込（受取） |
| **取引金額** | +5,000円 / -3,000円 |
| **取引後残高** | 12,000円 |
| **相手情報** | 振込時のみ相手の「顧客ID（または氏名）」を表示 |

---

## 3. 非機能要件・実装上の注意点（テスト用）

1.  **二重処理・排他制御（ロック）**
    *   同時に複数の出金・振込リクエストが来た際に残高整合性が崩れないよう、DBの行ロック（`SELECT FOR UPDATE` 等）を行うこと。
2.  **負の数の入力バリデーション**
    *   入金・出金・振込金額に負の数などが指定された場合に、不正に残高が増減しないよう共通でバリデーションをかけること。
</document>
<document id="DOC-TRACE-001" title="Requirements register digest">
# Requirements Register digest

| REQ ID | 要求の意味 |
|---|---|
| REQ-DOM-001 | 1顧客=1口座 |
| REQ-DOM-002 | Customer属性 |
| REQ-DOM-003 | Account属性 |
| REQ-DOM-004 | Transaction属性 |
| REQ-DOM-005 | 残高は常に0円以上 |
| REQ-CUS-001 | 顧客登録と口座自動開設 |
| REQ-CUS-002 | 登録メール重複拒否 |
| REQ-CUS-003 | 顧客情報更新 |
| REQ-CUS-004 | 更新メール重複拒否 |
| REQ-CUS-005 | 顧客・口座解約 |
| REQ-CUS-006 | 残高非0で解約拒否 |
| REQ-DEP-001 | 入金1〜10,000,000円、残高加算、履歴 |
| REQ-WDR-001 | 通常出金・全額出金 |
| REQ-WDR-002 | 通常出金 |
| REQ-WDR-003 | 全額出金 |
| REQ-WDR-004 | 出金後非負、0円以下拒否 |
| REQ-TRF-001 | 振込入力 |
| REQ-TRF-002 | 双方残高と双方履歴 |
| REQ-TRF-003 | 残高不足、不存在・解約済み振込先、自己振込拒否 |
| REQ-TRF-004 | 振込不可分・全rollback |
| REQ-HIS-001 | 履歴照会、時系列降順 |
| REQ-HIS-002 | 履歴表示項目 |
| REQ-CON-001 | 同時出金・振込時のDB行ロックと残高整合性 |
| REQ-VAL-001 | 入金・出金・振込の負数共通拒否 |

## Traceability rule

REQ IDが表に存在するだけでは追跡成立としない。要件の正常系、主要異常系、境界、権限、不可分性、同時実行を実際に検証するAcceptance Criteriaへ意味的に接続する。
</document>
<document id="DOC-B-001" title="Approved B-01 to B-06 decisions">
# Koo承認済み決定 B-01〜B-06

## B-01 解約後の操作

- 解約済み口座への入金: 拒否
- 解約済み口座からの出金: 拒否
- 解約済み顧客の氏名・メールアドレス変更: 拒否
- 解約後の取引履歴閲覧: 許可
- 解約済み口座を振込元とする操作: 拒否
- CustomerとAccountの両方に解約日時を持たせ、同一の解約日時を記録する。
- 有効状態では解約日時を未設定、解約済み状態では解約日時を必須とする。
- 解約後に許可する操作は、権限を持つ操作者による顧客情報参照および取引履歴閲覧のみとする。
- 解約後に残高または顧客情報を変更する操作を許可しない。
- 解約日時は取引との前後関係を確認できる日時として扱う。

## B-02 Customer / Account状態

- CustomerとAccountの両方に`有効` / `解約済み`を持たせる。
- 顧客解約処理でCustomerとAccountを同時に解約済みへ変更する。
- 許可遷移は`有効 → 解約済み`だけとする。
- 再開・取消を許可しない。
- 解約処理でAccount残高が正確に0円であることを確認する。
- 1円以上または負残高ではCustomer・Accountとも変更せず拒否する。
- 解約処理内で自動出金しない。
- Customer / Accountの状態、解約日時を一致させる。
- 解約済みAccount残高は0円とする。
- 状態変更と解約日時記録を不可分にする。

## B-03 金額境界

### 通常出金

- 1円以上
- 固定上限なし
- 処理時点の現在残高まで
- 出金後残高0円以上

### 振込

- 1円以上10,000,000円以下
- 処理時点の出金元残高以下
- 出金元と振込先は異なる
- 双方が存在し、有効

### 共通

- 残高更新時点でも金額、残高、状態を検証する。
- 同時実行でも残高をマイナスにしない。
- 条件違反時は残高と履歴を変更しない。

## B-04 操作者・認証・認可

- 全操作者に個別ログインを必須とする。
- 役割は`管理者`、`窓口担当者`、`閲覧者`。
- REST APIの各操作で認証・認可を確認する。

| 操作 | 管理者 | 窓口担当者 | 閲覧者 |
|---|:---:|:---:|:---:|
| 顧客情報参照 | 許可 | 許可 | 許可 |
| 顧客登録 | 許可 | 許可 | 拒否 |
| 顧客情報更新 | 許可 | 許可 | 拒否 |
| 顧客・口座解約 | 許可 | 許可 | 拒否 |
| 口座基本情報参照 | 許可 | 許可 | 拒否 |
| 現在残高の直接参照 | 許可 | 許可 | 拒否 |
| 入金 | 許可 | 許可 | 拒否 |
| 出金 | 許可 | 許可 | 拒否 |
| 振込 | 許可 | 許可 | 拒否 |
| 取引履歴閲覧 | 許可 | 許可 | 許可 |
| 履歴上の取引後残高閲覧 | 許可 | 許可 | 許可 |
| 利用者管理 | 許可 | 拒否 | 拒否 |
| 役割・権限管理 | 許可 | 拒否 | 拒否 |

- 閲覧者は現在残高を直接照会できないが、履歴上の取引後残高は閲覧できる。
- 権限不足時はデータを変更せず拒否する。
- 誰が何を行ったかは、Transactionとは別のAudit Logで追跡する。

## B-05 インターフェース・エラー

- 主たるインターフェースはREST API。
- 全機能でHTTP状態、固定エラーコード、人向け説明文を持つ共通形式を使用する。
- 固定コードを機械判定の正本とする。
- 必須分類:
  - 入力不正
  - 金額範囲外
  - 未認証
  - 権限不足
  - 顧客・口座不存在
  - 残高不足
  - 解約済み状態
  - 残高0円でないため解約不可
  - Customer / Account状態不整合
  - 同時実行競合
- 正式な固定コード一覧、HTTP状態対応、レスポンス項目を後続仕様で固定する。

## B-06 不可分性・整合性

- 顧客登録と口座開設を不可分にする。
- 通常出金・全額出金の成功時に出金履歴を作成する。
- 入金: 残高加算と履歴作成を不可分にする。
- 出金: 残高減算と履歴作成を不可分にする。
- 振込: 出金元減算、振込先加算、双方履歴作成を不可分にする。
- 一つでも失敗した場合、処理全体を失敗として変更を残さない。
- 残高と履歴の不一致、振込片側だけの更新、Customerだけの存在、状態・解約日時不一致、解約済み残高非0を許可しない。
- 同時実行時にも上記条件を破らない。
</document>
<document id="DOC-D-001" title="Approved D-01 to D-17 decisions">
# Koo承認済み決定 D-01〜D-17

| ID | 承認済み決定 |
|---|---|
| D-01 / DEC-007 | 振込の出金元・振込先は顧客IDまたは口座番号で指定できる |
| D-02 / DEC-008 | 履歴照会は顧客IDまたは口座番号で指定できる |
| D-03 / DEC-009 | 振込履歴に相手の顧客IDと氏名を表示する |
| D-04 / DEC-010・011 | 全額出金は履歴上の通常の`出金`に含め、取引種別は4種類を維持する |
| D-05 / DEC-033 | 残高0円時の全額出金は拒否する |
| D-06 / DEC-015 | 内部デモでは取引履歴をページングせず全件返却する |
| D-07 / DEC-016 | 通貨は日本円だけを扱う |
| D-08 / DEC-017 | メールアドレスは前後空白を除去し、小文字化した値で一意性を判定する |
| D-09 / DEC-019 | 取引日時降順に加え、取引ID等のタイブレーカーで同一時刻の順序を決定的にする |
| D-10 / DEC-020 | 口座番号は内部デモ向けの単純な識別子とし、固定桁・チェックディジットを要求しない |
| D-11 / DEC-021 | 利用者へ表示・入力する業務時刻はJSTとする。DB保存方式はADRで決める |
| D-12 / DEC-022 | health check、操作・障害ログ、バックアップ手順を内部デモの運用要件とする |
| D-13 / DEC-028 | 入金、通常出金、全額出金、振込に冪等性を要求する。同じ冪等キーの再送では残高・履歴を重複更新せず、最初と同じ業務結果を返す |
| D-14 / DEC-029 | 作成済み取引履歴の更新・削除を許可しない |
| D-15 / DEC-030 | 振込の送金側・受取側履歴に共通の振込識別子を記録する |
| D-16 / DEC-031 | 並行入金時も最終残高と各履歴の取引後残高が正確であることを要求する。実現方式はADRで決める |
| D-17 / DEC-032 | API契約として自動リトライを保証しない。競合時は固定エラーを返し、利用側は同じ冪等キーで安全に再実行できる。有限回の内部リトライ採否はADRで決める |

D-01〜D-17はすべてAgent A推奨案を追加条件なしでKooが承認した。製品として観測可能な契約は仕様へ固定し、技術的実現方式はADRへ分離する。
</document>
<document id="DOC-DIFF-001" title="Fixed target diff">
# Fixed target diff

- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Changed files: 1
- Additions: 977
- Deletions: 0
- Path: `docs/specs/bank-system-specification.md`
- Base state: file absent
- Head state: new file
- Git blob SHA-1 at Head: `95097dbbe66eba6c32a18db8121db7e7f93d43d1`

The complete added content is `DOC-TARGET-001` in this bundle. Because the file did not exist at Base, the semantic unified diff is exactly the full content of `DOC-TARGET-001` added with no deleted lines.
</document>
<document id="DOC-SPEC-001" title="Target specification full text">
# 最小銀行システム 製品仕様書

## 1. 文書管理

- Status: **Draft**
- 対象リリース: 内部デモ版 `v0.1.0`
- 対応Issue: #7
- Parent / Control Issue: #3
- 前提決定: Issue #5、Issue #8
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
- 残高・履歴・状態の整合性
- 冪等な金銭操作
- 操作・障害ログ、health check、バックアップ手順

### 2.2 対象外

- 実在する顧客、口座、送金データ
- 実金融機関との接続
- 公開インターネット上の本番金融サービス
- 法令準拠済み銀行勘定系としての提供
- 多通貨
- 解約の取消、再有効化
- 複数口座を持つ顧客
- 24時間監視、復旧訓練等の本格運用
- 取引履歴の更新・削除機能

---

## 3. 用語とID

| 用語 | 意味 |
| --- | --- |
| Customer | 顧客。1件のAccountと1対1で対応する |
| Account | 顧客の口座。残高、状態、解約日時を持つ |
| Transaction | 金銭取引の業務履歴 |
| Audit Log | 誰が何を行ったかを追跡する、Transactionとは別の記録 |
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

### 4.4 全体不変条件

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

| 操作 | 結果 |
| --- | --- |
| 顧客情報参照 | 権限があれば許可 |
| 取引履歴閲覧 | 権限があれば許可 |
| 顧客情報更新 | 拒否 |
| 入金 | 拒否 |
| 通常出金・全額出金 | 拒否 |
| 振込元として利用 | 拒否 |
| 振込先として利用 | 拒否 |

---

## 6. 操作者、認証前提、役割・権限

### 6.1 認証前提

- すべての操作者に個別ログインを必須とする。
- 未認証の要求は業務処理を開始せず拒否する。
- 認証方式、credential保存方式、セッション方式等はADRで決定する。
- 画面上で操作を隠すだけでは認可とせず、REST API側で必ず認可する。

### 6.2 役割

- 管理者
- 窓口担当者
- 閲覧者

### 6.3 権限マトリクス

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
| 利用者管理 | 許可 | 拒否 | 拒否 |
| 役割・権限管理 | 許可 | 拒否 | 拒否 |

閲覧者へ返す取引履歴には、履歴上の取引後残高を含める。これは現在残高の直接照会を許可するものではない。

---

## 7. 顧客登録と口座自動開設

### 7.1 入力

- 氏名
- メールアドレス

氏名およびメールアドレスは必須とする。メールアドレスは前後空白を除去し、小文字化した値を一意性判定に使用する。

### 7.2 正常結果

顧客登録は次を一つの業務処理として行う。

1. 正規化したメールアドレスの重複を確認する。
2. 有効状態のCustomerを作成する。
3. 一意な口座番号を採番する。
4. 有効状態、初期残高0円のAccountを作成する。
5. CustomerとAccountの対応を確立する。

すべて成功した場合だけ登録成功とする。

### 7.3 異常結果

- 正規化後のメールアドレスが既存Customerと重複する場合は拒否する。
- CustomerまたはAccountのどちらか一方の作成に失敗した場合、どちらも残さない。
- 権限がない場合、データを作成しない。

氏名の最大長、メールアドレスの最大長および詳細な形式検証方式は、API・データ設計で定義する。ただし、利用者へ追加制限を課す場合は本仕様の承認範囲と矛盾しないことを確認する。

---

## 8. 顧客情報参照・更新

### 8.1 参照

管理者、窓口担当者、閲覧者はCustomerの顧客ID、氏名、メールアドレス、状態、作成日時、解約日時を参照できる。

閲覧者はCustomer参照を通じてAccount基本情報または現在残高を取得できない。

### 8.2 更新

管理者および窓口担当者は、有効なCustomerの氏名およびメールアドレスを更新できる。

メールアドレス更新時は、前後空白を除去し、小文字化した値で他Customerとの重複を判定する。

次の場合は更新を拒否し、既存情報を変更しない。

- 対象Customerが存在しない
- CustomerまたはAccountが解約済み
- CustomerとAccountの状態が不整合
- 正規化後メールアドレスが他Customerと重複
- 操作者に権限がない

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

- 複数の入金が同時に行われても、最終残高と各Transactionの取引後残高は正確でなければならない。
- 同一の冪等キーによる同一要求の再送は、最初の業務結果を返し、残高・履歴を再度変更しない。
- 同一冪等キーを異なる要求内容へ使用した場合は拒否する。

### 10.5 拒否条件

- 未認証または権限不足
- 顧客・口座不存在
- 顧客IDと口座番号の不一致
- 解約済みまたは状態不整合
- 金額範囲外
- 冪等キー不正または競合
- 同時実行競合により安全な処理を継続できない

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

### 11.4 冪等性と失敗結果

同一冪等キーの同一要求再送では重複出金しない。同一キーを異なる要求へ使用した場合は拒否する。

次の場合は残高・履歴を変更せず拒否する。

- 未認証または権限不足
- 顧客・口座不存在
- 顧客IDと口座番号の不一致
- 解約済みまたは状態不整合
- 0円または負数
- 残高不足
- 残高0円での全額出金
- 冪等キー不正または競合
- 同時実行競合により安全な処理を継続できない

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

同一冪等キーの同一振込再送は、最初の業務結果を返し、残高・履歴を重複変更しない。同一キーを異なる振込内容へ使用した場合は拒否する。

同時振込や出金があっても出金元残高をマイナスにしない。複数Accountのロック順序等の方式はADRで決定する。

### 12.5 拒否条件

- 未認証または権限不足
- 出金元または振込先の不存在
- 顧客IDと口座番号の不一致
- 出金元または振込先が解約済み
- Customer／Account状態不整合
- 自分自身への振込
- 0円、負数、10,000,001円以上
- 残高不足
- 冪等キー不正または競合
- 同時実行競合により安全な処理を継続できない

---

## 13. 取引履歴照会

### 13.1 入力

- 顧客IDまたは口座番号

両方を指定する場合は、同一Accountを示さなければならない。

### 13.2 参照権限

管理者、窓口担当者、閲覧者が参照できる。解約済みCustomer／Accountの履歴も参照できる。

閲覧者は履歴に含まれる取引後残高を閲覧できるが、現在残高の直接照会はできない。

### 13.3 返却範囲と並び順

内部デモ版ではページングを行わず、対象Accountの全Transactionを返す。

並び順は次のとおりとする。

1. 取引日時の降順
2. 同一取引日時の場合は取引IDの降順

### 13.4 表示項目

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

## 14. 監査ログの製品要件

Transactionは金銭取引の業務記録とし、Audit Logは操作者と操作結果の追跡記録とする。両者を同一の記録として扱わない。

Audit Logは最低限、次を記録する。

- 操作者の識別子
- 操作者の役割
- 操作種別
- 対象顧客IDまたは口座番号
- 操作日時
- 成功または失敗
- 失敗時の固定エラーコード
- 要求を追跡できる識別子

Audit Logへcredential、secret、token、不要な個人情報を記録しない。保存方式、改ざん防止、保持期間、閲覧権限はADRまたは後続運用仕様で決定する。

---

## 15. 境界値

### 15.1 入金

| 条件 | 結果 |
| --- | --- |
| 負数 | 拒否 |
| 0円 | 拒否 |
| 1円 | 許可 |
| 10,000,000円 | 許可 |
| 10,000,001円 | 拒否 |

### 15.2 通常出金

| 条件 | 結果 |
| --- | --- |
| 負数 | 拒否 |
| 0円 | 拒否 |
| 1円かつ残高1円以上 | 許可 |
| 現在残高と同額 | 許可、残高0円 |
| 現在残高を1円超過 | 拒否 |
| 固定上限 | なし |

### 15.3 全額出金

| 条件 | 結果 |
| --- | --- |
| 残高が正数 | 許可、処理時点の全額を出金 |
| 残高0円 | 拒否 |
| 残高が負数 | データ不整合として拒否 |

### 15.4 振込

| 条件 | 結果 |
| --- | --- |
| 負数 | 拒否 |
| 0円 | 拒否 |
| 1円 | 許可 |
| 10,000,000円 | 許可 |
| 10,000,001円 | 拒否 |
| 出金元残高と同額 | 許可、出金元残高0円 |
| 残高超過 | 拒否 |
| 出金元と振込先が同一 | 拒否 |

### 15.5 状態と識別子

| 条件 | 結果 |
| --- | --- |
| 顧客IDだけ指定 | 対応Accountを解決できれば許可 |
| 口座番号だけ指定 | 対応Customerを解決できれば許可 |
| 両方指定し同一Account | 許可 |
| 両方指定し不一致 | 拒否 |
| 解約済みAccountへの金銭操作 | 拒否 |
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

### 16.2 HTTP状態

| HTTP状態 | 用途 |
| ---: | --- |
| 400 | 入力形式、識別子組合せ、金額範囲等が不正 |
| 401 | 未認証 |
| 403 | 認証済みだが権限不足 |
| 404 | 顧客または口座が存在しない |
| 409 | 現在状態、残高、冪等性、同時実行等との競合 |
| 500 | 内部整合性異常。内部詳細は返さない |

### 16.3 固定コード案

次のコード名は本Draftの提案であり、本仕様の承認時に製品契約として確定する。

| code | HTTP | 原因 |
| --- | ---: | --- |
| `validation_failed` | 400 | 必須入力または形式が不正 |
| `identifier_mismatch` | 400 | 顧客IDと口座番号が同一Accountを示さない |
| `amount_out_of_range` | 400 | 0円、負数、上限超過等 |
| `self_transfer_not_allowed` | 400 | 自分自身への振込 |
| `authentication_required` | 401 | 未認証 |
| `operation_not_permitted` | 403 | 権限不足 |
| `customer_not_found` | 404 | Customer不存在 |
| `account_not_found` | 404 | Account不存在 |
| `email_already_registered` | 409 | 正規化後メールアドレスが重複 |
| `account_closed` | 409 | 解約済みAccountへの禁止操作 |
| `customer_closed` | 409 | 解約済みCustomerへの禁止更新 |
| `account_balance_not_zero` | 409 | 残高0円でないため解約不可 |
| `insufficient_balance` | 409 | 出金または振込の残高不足 |
| `no_balance_to_withdraw` | 409 | 残高0円での全額出金 |
| `idempotency_key_conflict` | 409 | 同じ冪等キーへ異なる要求を送信 |
| `concurrent_operation_conflict` | 409 | 競合により安全に処理できない |
| `customer_account_state_inconsistent` | 500 | CustomerとAccountの状態・解約日時等が不整合 |
| `data_integrity_violation` | 500 | 残高・履歴等の内部不整合を検出 |

### 16.4 再試行

API契約としてシステムによる自動リトライを保証しない。

`concurrent_operation_conflict`等を受け取った利用側は、同一要求を同じ冪等キーで再実行できる。有限回の内部自動リトライを行うかはADRで決定する。無期限リトライは行わない。

---

## 17. 不可分性と同時実行時の期待結果

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

原始要件に基づき、出金・振込の残高更新ではDB行ロックを実施する。Issue #8のD-16に基づき、入金についても正確な取引後残高を保証する排他制御を要求する。行ロックの適用対象、分離レベル、ロック順序、待機、タイムアウト等の具体方式はADRで決定する。

---

## 18. Acceptance Criteria

### 18.1 顧客登録・更新

**AC-CUS-001 顧客登録成功**

- Given 管理者または窓口担当者が認証済みで、正規化後メールアドレスが未登録
- When 氏名とメールアドレスで顧客登録する
- Then 有効なCustomerと有効・残高0円のAccountが一つずつ作成される

**AC-CUS-002 メール重複**

- Given `User@Example.com`が登録済み
- When ` user@example.com `で登録または他Customerを更新する
- Then `email_already_registered`となり、データは変更されない

**AC-CUS-003 不可分な登録**

- Given Customer作成後にAccount作成を完了できない
- When 顧客登録する
- Then Customerだけを残さず処理全体が失敗する

**AC-CUS-004 解約済み更新拒否**

- Given CustomerとAccountが解約済み
- When 管理者または窓口担当者が氏名・メールを更新する
- Then 更新を拒否し、既存値を変更しない

### 18.2 解約

**AC-CLS-001 解約成功**

- Given 有効なCustomerとAccountがあり、残高が0円
- When 管理者または窓口担当者が解約する
- Then 両方が解約済みとなり、同じ解約日時が記録される

**AC-CLS-002 正残高で解約拒否**

- Given 残高が1円以上
- When 解約する
- Then `account_balance_not_zero`となり、状態と解約日時は変わらない

**AC-CLS-003 負残高・状態不整合**

- Given 負残高またはCustomer／Account状態不整合を検出する
- When 解約する
- Then 内部整合性エラーとなり、部分更新しない

**AC-CLS-004 再有効化禁止**

- Given 解約済み
- When 有効へ戻そうとする
- Then 操作を許可しない

### 18.3 入金

**AC-DEP-001 境界成功**

- Given 有効なAccount
- When 1円または10,000,000円を入金する
- Then 残高と入金履歴が一回分だけ増える

**AC-DEP-002 境界拒否**

- Given 有効なAccount
- When 0円、負数または10,000,001円を入金する
- Then `amount_out_of_range`となり、残高・履歴は変わらない

**AC-DEP-003 並行入金**

- Given 残高1,000円
- When 500円と300円の入金が並行して成功する
- Then 最終残高は1,800円で、二つの履歴の取引後残高は処理順に対応する正確な値となる

### 18.4 出金

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
- Then `no_balance_to_withdraw`となり、履歴は作成されない

**AC-WDR-004 残高不足**

- Given 残高5,000円
- When 5,001円を通常出金する
- Then `insufficient_balance`となり、残高・履歴は変わらない

### 18.5 振込

**AC-TRF-001 振込成功**

- Given 出金元残高10,000円、振込先残高2,000円
- When 3,000円を振り込む
- Then 出金元7,000円、振込先5,000円となり、双方に同一振込IDの履歴が作成される

**AC-TRF-002 不可分性**

- Given 振込の一工程を完了できない
- When 振込する
- Then どちらの残高も変化せず、片側履歴も残らない

**AC-TRF-003 振込境界**

- Given 残高が十分にある
- When 1円または10,000,000円を振り込む
- Then 成功する
- And 0円、負数または10,000,001円では失敗する

**AC-TRF-004 自分自身・解約済み拒否**

- Given 出金元と振込先が同一、またはいずれかが解約済み
- When 振込する
- Then 残高・履歴を変更せず拒否する

### 18.6 履歴・権限

**AC-HIS-001 履歴順序**

- Given 複数のTransactionがあり、同一時刻の履歴も存在する
- When 履歴を照会する
- Then 取引日時降順、同一時刻では取引ID降順で全件返る

**AC-HIS-002 振込相手情報**

- Given 振込履歴
- When 履歴を照会する
- Then 相手口座番号、相手顧客ID、相手氏名、共通振込IDが表示される

**AC-AUTH-001 閲覧者**

- Given 閲覧者が認証済み
- When 顧客情報または取引履歴を参照する
- Then 許可される
- And 現在残高の直接参照、登録、更新、解約、入金、出金、振込は403となる

**AC-AUTH-002 API認可**

- Given 権限のない操作者
- When REST APIを直接呼び出す
- Then 画面の状態に関係なく処理を拒否し、データを変更しない

### 18.7 冪等性・同時実行・エラー

**AC-IDEM-001 同一要求再送**

- Given 金銭操作が成功済み
- When 同じ冪等キー、同じ要求内容で再送する
- Then 最初と同じ業務結果を返し、残高・履歴を重複変更しない

**AC-IDEM-002 同一キー異内容**

- Given ある冪等キーが使用済み
- When 同じキーで異なる要求内容を送る
- Then `idempotency_key_conflict`となり、データを変更しない

**AC-CON-001 同時出金**

- Given 残高が一件分しかない
- When 残高を超過し得る複数出金を並行実行する
- Then 成功した処理だけが反映され、残高はマイナスにならない

**AC-ERR-001 エラー形式**

- Given 任意の業務エラー
- When APIが失敗を返す
- Then HTTP状態、固定`code`、人向け`message`を返し、内部詳細やcredentialを含めない

### 18.8 運用

**AC-OPS-001 health check**

- Given 内部デモ環境
- When health checkを実行する
- Then 稼働可否を判定できる

**AC-OPS-002 ログとバックアップ**

- Given 内部デモリリース候補
- When Release Readyを評価する
- Then 操作・障害ログおよびバックアップ手順が存在し、検証対象として追跡できる

---

## 19. トレーサビリティ

### 19.1 REQから仕様・受入条件

| 要件ID | 主な仕様節 | 主なAcceptance Criteria |
| --- | --- | --- |
| REQ-DOM-001 | §4.4、§7 | AC-CUS-001、AC-CUS-003 |
| REQ-DOM-002 | §4.1、§5 | AC-CLS-001 |
| REQ-DOM-003 | §4.2、§5 | AC-CLS-001、AC-CLS-003 |
| REQ-DOM-004 | §4.3、§13 | AC-HIS-001、AC-HIS-002 |
| REQ-DOM-005 | §4.4、§17 | AC-WDR-004、AC-CON-001 |
| REQ-CUS-001 | §7 | AC-CUS-001、AC-CUS-003 |
| REQ-CUS-002 | §7 | AC-CUS-002 |
| REQ-CUS-003 | §8 | AC-CUS-004 |
| REQ-CUS-004 | §8 | AC-CUS-002 |
| REQ-CUS-005 | §5、§9 | AC-CLS-001、AC-CLS-004 |
| REQ-CUS-006 | §5、§9 | AC-CLS-002、AC-CLS-003 |
| REQ-DEP-001 | §10、§15.1 | AC-DEP-001、AC-DEP-002、AC-DEP-003 |
| REQ-WDR-001 | §11 | AC-WDR-001、AC-WDR-002 |
| REQ-WDR-002 | §11.2、§15.2 | AC-WDR-001、AC-WDR-004 |
| REQ-WDR-003 | §11.3、§15.3 | AC-WDR-002、AC-WDR-003 |
| REQ-WDR-004 | §11、§15.2〜15.3 | AC-WDR-003、AC-WDR-004 |
| REQ-TRF-001 | §12.1〜12.2 | AC-TRF-001、AC-TRF-003 |
| REQ-TRF-002 | §12.3 | AC-TRF-001、AC-TRF-002 |
| REQ-TRF-003 | §12.5 | AC-TRF-004 |
| REQ-TRF-004 | §12.3、§17 | AC-TRF-002 |
| REQ-HIS-001 | §13.1〜13.3 | AC-HIS-001 |
| REQ-HIS-002 | §13.4 | AC-HIS-002 |
| REQ-CON-001 | §17 | AC-DEP-003、AC-CON-001 |
| REQ-VAL-001 | §15、§16 | AC-DEP-002、AC-TRF-003、AC-ERR-001 |

### 19.2 Blocking Decision

| 決定 | 主な仕様節 |
| --- | --- |
| B-01 | §5.4、§8、§10〜13 |
| B-02 | §4.1〜4.2、§5、§9 |
| B-03 | §11〜12、§15 |
| B-04 | §6、§13.2、§18.6 |
| B-05 | §16 |
| B-06 | §4.4、§7、§9〜12、§17 |

### 19.3 Phase 2 Decision

| 決定 | 主な仕様節 |
| --- | --- |
| D-01 | §12.1、§15.5 |
| D-02 | §13.1、§15.5 |
| D-03 | §4.3、§13.4 |
| D-04 | §4.3、§11.3、§13.4 |
| D-05 | §11.3、§15.3 |
| D-06 | §13.3 |
| D-07 | §4.4 |
| D-08 | §7、§8 |
| D-09 | §13.3 |
| D-10 | §4.2、§7 |
| D-11 | §3、§13.4、§14 |
| D-12 | §2.1、§18.8 |
| D-13 | §4.4、§10〜12、§18.7 |
| D-14 | §4.4、§13.4 |
| D-15 | §4.3、§12.3、§13.4 |
| D-16 | §10.4、§17、§18.3 |
| D-17 | §16.4、§17 |

---

## 20. Out of scope

- 物理DBスキーマとmigration
- 金額データ型
- 具体的な認証プロトコル・ライブラリ
- DBトランザクション分離レベル
- 行ロック対象、取得順、待機、タイムアウトの具体方式
- 冪等キーの保存方式
- 口座番号の採番アルゴリズム
- 日時の物理保存方式
- Audit Logの保持期間・保護技術
- APIの具体的なURI命名、JSONの命名規約、OpenAPI生成方式
- UI
- 実金融サービスに必要な法令・本人確認・不正検知・限度額管理

---

## 21. ADR候補と仕様から分離した技術事項

| ADR候補 | 本仕様で固定した要求 | ADRで決める方式 |
| --- | --- | --- |
| ADR-CANDIDATE-001 | 日本円、残高0円以上 | 金額の物理保存形式 |
| ADR-CANDIDATE-002 | 登録・残高・履歴・振込の不可分範囲 | DBトランザクション方式 |
| ADR-CANDIDATE-003 | DB行ロックを使用し、同時実行でも残高・履歴を正確にする | 行ロック対象、分離レベル、待機、タイムアウト |
| ADR-CANDIDATE-004 | 振込で部分成功・デッドロックを回避する | 複数Accountの決定的ロック順序 |
| ADR-CANDIDATE-005 | 金銭操作の冪等性 | 冪等キーの保存・照合方式 |
| ADR-CANDIDATE-006 | Customer／Accountに有効・解約済み状態と解約日時を持たせる | 状態・論理削除の物理表現 |
| ADR-CANDIDATE-007 | Transactionの更新・削除を禁止する | 追記専用等の実装方式 |
| ADR-CANDIDATE-008 | 同一時刻の履歴順序を決定的にする | 順序キーの生成・保存方式 |
| ADR-CANDIDATE-009 | 単純で一意な口座番号 | 採番方式 |
| ADR-CANDIDATE-010 | 業務時刻はJST | DB日時保存方式 |
| 追加ADR候補 | 個別ログイン、3役割、API認可 | 認証・認可方式 |
| 追加ADR候補 | Audit Logを別記録として保持 | 保存、保護、閲覧、保持方式 |
| 追加ADR候補 | APIは自動リトライを保証しない | 有限回の内部リトライ採否 |
| 追加ADR候補 | backup手順を提供する | バックアップ・復旧の具体方式 |

---

## 22. 未決事項・既知制約

### 22.1 本DraftでKoo承認が必要な事項

- §16.3の固定エラーコード名とHTTP状態の対応
- 氏名・メールアドレスの具体的な最大長と詳細な形式制約を製品契約として固定するか
- Audit Logの利用者向け閲覧機能をv0.1.0へ含めるか。現時点では記録のみを要求し、閲覧APIは対象外とする

これらは本Draftの独立レビューおよびKoo承認で確定する。未承認のまま実装へ進めない。

### 22.2 既知制約

- 1顧客1口座だけを扱う
- 取引履歴は全件返却するため、大量データには適さない
- 日本円、日本時間の内部デモに限定する
- 口座番号は実銀行形式ではない
- 自動リトライは外部契約として保証しない
- 実金融サービスとして使用できない

### 22.3 ゲート状態

本書作成時点でSpecification Readyは`NOT EVALUATED`である。

本書の独立レビュー、必要な修正、Koo承認および別工程でのゲート再評価が完了するまで、ADRの確定、実装Issue分割、アプリケーション実装へ進まない。
</document>
<document id="FINAL-FINDINGS-001" title="Final adjudicated findings">
# 最終確定Finding（実行モデル向け）

## Final verdict

- Verdict: `FAIL`
- Specification Ready: `NOT READY`
- Blocker: 0
- Major: 4
- Minor: 5
- Nit: 2

## F-001 Major — 解約後参照範囲

- 根拠: B-01は解約後に許可する操作を顧客情報参照と取引履歴閲覧「のみ」とする。B-04は口座基本情報参照と現在残高直接参照を別操作とする。
- 問題: §5.4に両操作の拒否がなく、§6.3の一般権限と競合する。
- 必須修正: 解約後は口座基本情報参照と現在残高直接参照を全役割で拒否し、状態別制約が一般権限より優先すると明示する。顧客情報、履歴、履歴上残高は権限があれば許可する。
- AC: 役割別に5種類の参照を独立検証し、拒否時非更新を確認する。
- Koo承認: 不要。

## F-002 Major — Acceptance Criteriaと実質トレーサビリティ不足

- 根拠: Issue #7は正常系、主要異常系、状態、境界、権限、エラーを検証可能にすることを要求する。
- 問題: REQ-CUS-003が更新成功へ、REQ-TRF-003が残高不足・不存在等へ、REQ-CON-001が同時振込へ実質追跡されない。複数原因が一つのACに混在する。
- 必須修正: 最低限、有効Customer更新、再解約、解約不存在、解約権限不足、解約済み入出金、解約後参照、通常出金0/負数、振込残高不足、振込元/先不存在、identifier mismatch、401/403、閲覧者参照範囲、同時振込または出金・振込競合、冪等同時到着・再送、履歴表示項目、Audit Log、障害ログ証拠を原因別ACにする。§19を意味的対応へ更新する。
- Koo承認: 直接修正部分は不要。F-003/F-004/F-005/F-008の未承認結果は創作しない。

## F-003 Major — 冪等性外部契約

- 根拠: D-13は同一key再送で最初と同じ業務結果、D-17は競合後に同じkeyで安全に再実行を要求する。
- 問題: key scope、request identity、結果確定区分、競合でkeyを消費するか、処理中再送、異内容再送、保証期間が未確定。
- 必須対応: `BLOCKED_BY_APPROVAL`。次の8軸をOpen approval itemにする: scope/namespace、request identity、顧客IDと口座番号指定の同一性、結果確定区分、競合時key消費、in-progress replay、same-key different-payload、guarantee period/expiry。
- 保全: D-13/D-17、自動リトライ非保証、技術方式のADR分離。
- 禁止: 一つの契約を選んで確定する、table/hash/lock方式を固定する。

## F-004 Major — 利用者管理・役割権限管理のv0.1.0契約

- 根拠: B-04は管理者に利用者管理と役割・権限管理を許可する。
- 問題: 製品API、初期構築運用、将来機能のどれか、操作集合、利用者状態、役割変更範囲、管理者喪失防止、監査、ACが未確定。
- 必須対応: `BLOCKED_BY_APPROVAL`。製品APIか運用手順か、最低操作集合、利用者状態、固定3役割の付与か役割定義編集も許すか、禁止条件、Audit Log、管理者成功/非管理者403をOpen approval itemにする。
- 保全: §6.3、3役割、個別ログイン、API側認可。
- 禁止: CRUD、seed-only、将来機能のいずれかを代行決定する。

## F-005 Minor — 固定error codeと原因対応

- 根拠: B-05は固定codeを機械判定の正本とし、§16.3はKoo承認待ち。
- 問題: AC-CLS-003、AC-TRF-004等が異なる原因をまとめ、期待HTTP/codeが一意でない。
- 必須修正: 原因別ACに分割し、期待HTTPとcodeの承認状態を一意にする。未承認codeは`PENDING_KOO_APPROVAL`等で扱う。
- 禁止: 新codeの確定、全API内部評価順の固定。
- 関連承認: §22.1固定code/HTTP対応。

## F-006 Minor — Audit Logと障害ログ

- 根拠: B-04は操作者監査、D-12は操作・障害ログを要求する。
- 問題: §14はAudit Log中心で、内部例外、異常終了、依存障害、health異常の診断記録と証拠が閉じていない。
- 必須修正: Transaction、Audit Log、障害ログの責務を分離し、操作成功/失敗、障害、secret非記録、Release Ready証拠をAC化する。
- 禁止: Audit Log閲覧APIの必須化、保存基盤の固定。

## F-007 Minor — 入金の技術方式先取り

- 根拠: 原始要件は出金・振込のDB行ロックを要求し、D-16は入金の正確性だけを固定して方式をADRへ送る。
- 問題: ADR-CANDIDATE-003が入金にもDB行ロックを固定したように読める。
- 必須修正: 出金・振込の行ロック要求を維持し、入金は正確性だけを固定して排他方式をADRへ残す。

## F-008 Minor — Transaction 0件の履歴レスポンス

- 根拠: 登録直後はAccountが存在しTransaction 0件となり得る。
- 問題: 正常な空結果とAccount不存在/identifier mismatchの外部結果が未定義。
- 必須対応: `BLOCKED_BY_APPROVAL`。HTTP状態、collection形状、不存在との区別、不一致との区別をOpen approval itemにする。
- 禁止: `200 []`、204、404等を代行決定する。

## F-009 Minor — 氏名・メール制約の未承認委譲

- 根拠: §22.1は最大長・詳細形式を製品契約にするか未決。
- 問題: §7.3はAPI・データ設計へ委譲済みと先取りする。
- 必須修正: 未決でありKoo承認後に本仕様または承認済み後続設計へ反映する中立表現にする。
- 保全: 必須、trim/lowercase、正規化後一意性。
- 禁止: 具体値、regex、RFCレベルの確定。

## N-001 Nit — REST API主宣言

- 必須修正: 主たる外部インターフェースがREST APIであると独立して明記する。
- 禁止: endpoint、method、URI、JSON命名の追加。

## N-002 Nit — ADR候補の安定ID

- 必須修正: 追加4候補へ既存001〜010と衝突しない一意な安定IDを付与する。
- 禁止: ADR作成・承認、既存ID変更、候補意味変更。
</document>
<document id="DOC-APPROVAL-001" title="Approval items">
# Approval items

## Existing approval items recorded in target specification §22.1

1. §16.3の固定エラーコード名とHTTP状態の対応
2. 氏名・メールアドレスの具体的最大長と詳細形式を製品契約として固定するか
3. Audit Log利用者向け閲覧機能をv0.1.0へ含めるか

## New approval items identified by final adjudication

### F-003 idempotency external contract

- key scope / namespace
- request identity
- 顧客ID指定と口座番号指定の同一性
- result finalization categories
- concurrent conflict consumes key or not
- in-progress replay result
- same-key different-payload result
- guarantee period / expiry

### F-004 user and role management contract

- product API or setup/operations only
- minimum user-management operations
- user lifecycle state
- assignment among fixed three roles or role-definition editing
- prohibitions such as losing the last administrator
- audit scope
- success / 403 acceptance criteria

### F-008 zero-transaction history response

- HTTP status for existing Account with zero Transactions
- response collection shape
- distinction from Account not found
- distinction from identifier mismatch

No execution model may choose these decisions.
</document>
<document id="DOC-EXCLUSION-001" title="Scope exclusions">
# Scope exclusions

- 対象仕様以外のファイル修正
- PR #9、Issue、Round 1・Round 2成果物の変更
- 新機能追加
- 実金融サービス要件
- UI
- API endpoint URI / method / JSON naming
- DB schema / migration
- authentication library / credential storage
- transaction isolation / lock order / timeout
- idempotency table / fingerprint implementation
- ADR creation or approval
- code, test implementation, Docker, CI
- release, tag, publish
- other model outputs, model evaluations, scores, ranks
- Gold Fix and evaluation rubric
</document>
