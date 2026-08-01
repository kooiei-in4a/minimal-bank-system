# Agent B 独立レビュー結果

## Review metadata

- Review ID: `spec-review-001`
- Prompt version: `spec-review-prompt-v1`
- Reviewer model: `DeepSeek V4 Pro`
- Review date: `2026-08-02`
- Repository: `kooiei-in4a/minimal-bank-system`
- PR: `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Changed files: `docs/specs/bank-system-specification.md` (+977 lines, 1 file only)
- Tool limitations: 実在ブラウザ・認証環境なし。ghでIssues/PR取得。git show/diffで仕様書と差分を参照。

## Verdict

`PASS WITH FINDINGS`

## Executive summary

仕様書 `docs/specs/bank-system-specification.md` は、原始要件、Koo決定B-01〜B-06（Issue #5）、D-01〜D-17（Issue #8）をすべて正確に反映している。BlockerおよびMajorに該当する問題は検出されなかった。

本仕様書は以下の点で十分な品質を持つ。

- CustomerとAccountの状態遷移、解約日時、残高0円条件が一貫している。
- 管理者・窓口担当者・閲覧者の3役割権限マトリクスがKoo決定を完全再現している。
- 入金・出金・全額出金・振込の境界値が網羅的であり、矛盾がない。
- 冪等性（同一キー同一要求の再送防止、同一キー異内容拒否）が全金銭操作で定義されている。
- 行ロックの実施要求は正本どおり保持し、具体方式（分離レベル、ロック順序、タイムアウト等）はADRへ適切に分離されている。
- 全24件のREQ、B-01〜B-06、D-01〜D-17が仕様節とAcceptance Criteriaへ追跡可能である。
- 取引履歴と監査ログの責務が明確に分離されている。
- 未承認事項3件が§22.1で明示的にKoo承認待ちとされている。

Minorレベルの指摘が3件あるが、いずれも仕様の主契約そのものを損なうものではなく、表現の明確化や受入条件の補足で解消できる範囲である。

## Findings

### Blocker

なし

### Major

なし

### Minor

#### m-1 AC-CLS-003「負残高・状態不整合」がエラーコードを特定していない

- 根拠: §16.3では負残高は`data_integrity_violation`(500)、状態不整合は`customer_account_state_inconsistent`(500)と区別しているが、AC-CLS-003は「内部整合性エラー」とのみ記述し、期待する固定コードを明示していない。
- 問題: 実装後テストで検証すべきエラーコードが曖昧になる。テスト自動化時に期待コードの特定に仕様書からだけでは不十分。
- 影響: テスト作成者の解釈に依存し、レビュー時の合否判断が不明瞭になる可能性がある。
- 修正方針: AC-CLS-003を2つに分割するか、単一AC内で「負残高の場合は`data_integrity_violation`、状態不整合の場合は`customer_account_state_inconsistent`」とそれぞれの期待コードを明記する。

#### m-2 解約済みAccountに対する口座基本情報参照の権限制限が仕様本文で自明でない

- 根拠: 権限マトリクス§6.3では「口座基本情報参照」は閲覧者に拒否とされている。また§8.1では「閲覧者はCustomer参照を通じてAccount基本情報または現在残高を取得できない」と明記されている。一方、§5.4の「解約後の操作」表には「口座基本情報参照」の行がない。解約済みAccountの口座基本情報を窓口担当者・管理者が参照できるか（解約後も許可されるか）が明示されていない。
- 問題: 「顧客情報参照」は許可されるが「口座基本情報参照」の解約後扱いが不明。B-01は「権限を持つ操作者による顧客情報参照および取引履歴閲覧」とだけ記述し、口座基本情報の解約後参照可否を直接定義していない。
- 影響: 実装者が解約済み口座の基本情報参照を許可するか拒否するか判断に迷う。管理者/窓口担当者が解約済み顧客の口座基本情報を参照できることは運用上自然だが、仕様の明示がないためADR/実装段階で手戻りの可能性がある。
- 修正方針: §5.4の解約後操作表に「口座基本情報参照」の行を追加し、権限のある操作者に許可する旨を明記する。または§8.1に「解約済みの場合も同様に参照可能」であることを追記する。

#### m-3 受入条件に「履歴0件の照会（空リスト）」のテストケースがない

- 根拠: §13.3は「対象Accountの全Transactionを返す」と定義している。原始要件の異常系分析では「履歴照会で該当取引が0件の場合の出力（空リストの形式）が未定義」（requirements-review-001.md §9）と指摘されていた。
- 問題: 履歴が1件も存在しない口座（新規開設直後など）の照会結果が、空リストとして返るのか、特定のHTTP状態で返るのかを受入条件で検証できない。
- 影響: 実装後テストで新規口座の履歴照会結果の確認方法が不明瞭。テスト自動化時に期待出力形式を仕様から一意に決定できない。
- 修正方針: AC-HISに「履歴0件照会」のケースを追加し、「対象口座にTransactionが存在しない場合、空リストを返す（404ではなく200または同等）」のような期待結果を定義する。

### Nit

なし

## Required fixes

Koo承認前に必須となる修正はない。Minor 3件はいずれも推奨であり、Kooの判断で本フェーズで修正するか、実装Issue分割時に補完するかを選択できる。

ただし、m-2（解約後口座基本情報参照）については、Koo決定B-01の解釈範囲にかかわるため、実装着手前に確定することが望ましい。

## Open approval items

### 1. §16.3の固定エラーコード名とHTTP状態の対応

- 判断内容: 仕様書Draftで提示した18種類の固定コード名とHTTP状態の組み合わせを製品契約として確定するか。
- 選択肢:
  - A: Draft案の全18コードをそのまま承認
  - B: 表記の一部変更・追加・削除を行った上で承認
- Reviewer recommendation: A（Draft案を承認）
- 推奨理由: 全18コードはB-05が要求する全エラー分類を網羅している。Koo決定に完全準拠している。コード体系に過不足・重複・矛盾は認められない。401/403の区別も適切。
- 不採用時の影響: コード名の変更はAPI契約全体に波及する。実装開始後に変更すると全テストの修正が必要になるため、本フェーズでの確定が推奨される。

### 2. 氏名・メールアドレスの最大長・詳細形式制約を製品契約として固定するか

- 判断内容: 氏名およびメールアドレスの最大文字数、許可文字種等の形式制約を仕様レベルで固定するか、API/DB設計へ委譲するか。
- 選択肢:
  - A: 製品契約として本仕様で固定する（例: 氏名100文字以内、メールアドレス254文字以内等）
  - B: API・データ設計で定義する（§7.3の現状どおり）
- Reviewer recommendation: A（製品契約として固定）
- 推奨理由: 入力検証は製品仕様の一部であり、受入条件も形式制約に依存する。特にメールアドレスはRFC 5321に基づく上限（254文字）が広く知られており、固定は容易。氏名も内部デモとしては実用的な上限（例: 100文字）で十分。API設計時に実装者が独自に決めると仕様との不整合が生じる。
- 不採用時の影響: Bを選択した場合、API設計段階で追加された制約が本仕様の「追加制限」に該当しないかの確認が必要になる。仕様書とAPI設計の間で制約の不一致が生じるリスクがある。

### 3. Audit Log閲覧機能をv0.1.0へ含めるか

- 判断内容: Audit Logの「記録」機能は既に製品要件として定義済み（§14）。記録されたAudit Logを利用者が閲覧するAPIをv0.1.0のスコープに含めるか。
- 選択肢:
  - A: v0.1.0のスコープ外とする（現行Draftの立場。記録のみ）
  - B: v0.1.0に含める（記録＋閲覧API）
- Reviewer recommendation: A（v0.1.0スコープ外）
- 推奨理由: v0.1.0は内部デモであり、取引履歴（Transaction）閲覧があれば最小限の監査証跡として機能する。Audit Log閲覧を追加すると管理者向けUI/APIの工数が増加し、本プロジェクトの主目的（開発方式検証）から逸脱するリスクがある。Transactionの不変性（D-14）と監査ログの別記録（§14）が守られていれば、v0.1.0の目的は達成できる。
- 不採用時の影響: Bを選択した場合、管理機能の実装範囲が拡大し、Issue分割が複雑化する。ADRでAudit Logの保存・保護方式に加え閲覧方式の設計も必要になる。

## Specification Ready recommendation

`READY FOR KOO APPROVAL`

Minor Finding 3件はSpecification Readyのゲート通過を妨げない。仕様書は正常系、主要異常系、状態遷移、境界値、受入条件、対象外をすべて定義済みであり、要件と仕様の対応関係も確認できる。憲章（simulation-charter.md §6）のSpecification Ready基準を満たしている。

ただし、Specification Ready判定自体はKooの判断であり、本レビューはゲート判定を代行しない。Minor Findingと残存承認事項3件についてKooが判断した後、別IssueでSpecification Readyゲートを再評価することを推奨する。

## Traceability review

- 全REQ (24件): §19.1で全24件のREQが仕様節とAcceptance Criteriaへマッピングされている。対応漏れなし。
- B-01〜B-06 (6件): §19.2で全6件のKoo決定が仕様節へマッピングされている。対応漏れ・誤対応なし。
- D-01〜D-17 (17件): §19.3で全17件のKoo決定が仕様節へマッピングされている。対応漏れ・誤対応なし。
- Acceptance Criteria (20件): AC-CUS-001〜004, AC-CLS-001〜004, AC-DEP-001〜003, AC-WDR-001〜004, AC-TRF-001〜004, AC-HIS-001〜002, AC-AUTH-001〜002, AC-IDEM-001〜002, AC-CON-001, AC-ERR-001, AC-OPS-001〜002。AC番号に連番の跳び・重複なし。節番号とAC番号の対応に矛盾なし。
- 判定: **追跡性は完全に成立している。**

## Residual risks

- 固定エラーコード名がKoo承認前に変更された場合、API契約全体の再確認が必要になる。
- 氏名・メールアドレスの最大長がAPI設計で定義される場合、仕様書とAPI設計の間で検証条件の不整合が生じるリスクがある（Minor m-2と関連）。
- 行ロックの実施要求は§17で明示されているが、ADR段階で楽観的ロック等の代替案が提案された場合、原始要件（REQ-CON-001「DBの行ロックを行うこと」）との整合性再確認が必要。
- Audit Logの閲覧機能がv0.1.0に追加された場合、管理機能の実装範囲拡大によりプロジェクト全体のスケジュールに影響する可能性がある。

## Review evidence

- 確認した正本:
  - `AGENTS.md`: 全125行確認
  - Issue #3: gh issue view 3 で本文取得、現在フェーズ=Phase 2、Specification Ready=NOT EVALUATEDを確認
  - Issue #5 Koo決定コメント: gh issue view 5 --comments でB-01〜B-06全決定を確認
  - Issue #8 Koo決定コメント: gh issue view 8 --comments でD-01〜D-17全決定を確認
  - `docs/project/simulation-charter.md`: 全91行確認
  - `docs/requirements/bank-system-requirements.md`: 全94行確認
  - `docs/reviews/requirements-review-001.md`: 全668+行確認
  - `docs/traceability/requirements-register.md`: 全235行確認
  - Issue #7: gh issue view 7 で本文取得
  - Issue #10: gh issue view 10 で本文取得

- 確認した差分:
  - `git diff dedbcaf 4944fb -- docs/specs/bank-system-specification.md`: +977 lines, 新規ファイル1件のみ

- 実施した照合:
  - B-01〜B-06全6件の仕様書への反映を全行照合（16箇所）
  - D-01〜D-17全17件の仕様書への反映を全行照合（17箇所）
  - 権限マトリクス§6.3の全13行をKoo決定B-04と逐語照合
  - 境界値§15の全条件をB-03、各REQと逐語照合
  - 全24REQの§19.1トレーサビリティマッピングの正確性を確認
  - AC-CLS-004と§2.2「対象外」の間の潜在的不整合を検出（Minor m-2）
  - §16.3固定コード18件とB-05要求10分類の網羅性を照合（全分類カバー確認）
  - Transaction情報項目とD-03/D-15の比較照合
  - 冪等性定義とD-13の比較照合
  - 不可分性定義§17とB-06/D-16の比較照合
  - 行ロック要求とREQ-CON-001の保持確認

- 実施できなかった検証:
  - 実在ブラウザでのE2Eテスト（実装前のため）
  - REST APIの実際の呼び出し検証（実装前のため）

- レビュー中に対象を変更していないこと:
  - Base SHA `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`: 変更なし
  - Head SHA `4944fb22806526f9e92dc47b516b57431c6c7f0a`: 変更なし
  - 仕様書 `docs/specs/bank-system-specification.md`: 変更なし
  - Specification Ready: `NOT EVALUATED` のまま
  - Issue/PR: 変更なし
  - 他LLMレビュー結果: 参照せず

## Final status

- Code / specification changes: none
- Issue / PR updates: none
- Merge: none
- Specification Ready update: none
