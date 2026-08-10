# FND-05 Implementation Scoring

Revision: `fnd05-scoring-v2`

このrubricは3 candidateの実装開始前に固定する。Modelの評判、価格、過去順位で点数を調整しない。

## 1. Score

| Category | Points | Evaluation |
| --- | ---: | --- |
| A. Issue #43達成度 | 20 | ScopeとAcceptance Criteriaを満たすか |
| B. Runtime ordering / fail-closed | 20 | PostgreSQL usable → Migrator → API、failure時API非起動が実証されるか |
| C. Test oracle / mutation sensitivity | 20 | positive / negative evidence、reachability、mutationで欠陥を検出できるか |
| D. ADR / responsibility boundary | 15 | ADR-0001 / 0008 / 0009、FND-04 / 05 / 06境界へ適合するか |
| E. Secret / image / volume safety | 10 | credential非保存、argv非露出、D-02 digest、named volumeが正しいか |
| F. Project rule / code quality | 5 | MUST rule遵守、不要変更の少なさ、SHOULD違反の実害 |
| G. Reproducibility / operations | 5 | D-04 lifecycle contractが再現可能か |
| H. Evidence / CI identity | 5 | exact Head、D-05 evidence、CI、未検証事項が正確か |
| **Total** | **100** | |

## 2. Element-selection eligibility

Candidateは比較用でありcandidate PRを直接mergeしないため、`merge-ready`という表現を使わない。

`ELEMENT_SELECTION_ELIGIBLE: YES`は次を満たす場合だけ付与する。

- Blocker 0
- Major 0
- required verification成功
- no secret / credential leak
- no API startup migration
- no FND-06 / business / backup / production scope drift
- migration failure時API never-start
- test oracleがintended path / failure reasonを証明
- candidate Head / CI identity一致

## 3. Severity

### Blocker

- correct target / Headを評価不能
- secret / credentialがrepositoryへ保存された
- candidate executionが未許可状態で開始された
- 必須verificationが実行不能でClose判断不能

### Major

- Acceptance Criteria実質未達
- orderingが保証されない / failure path fail-open
- API startup migration
- named volume / D-02 image / D-03 secret contractの重大違反
- negative testがdefect classを検出できないfalse assurance
- FND-06等の責任を先取りしないと成立しない

### Minor

- Closeは可能だが限定的な保守性・証拠・運用問題が残る
- MUST ruleの非重大な局所gap
- test coverageの局所不足

### Nit

- behaviorやClose判断へ影響しない軽微な表現・整理

`SHOULD / SHOULD NOT`違反だけでMajorにしない。具体的な実害がある場合はroot causeでseverityを決める。

## 4. Evaluation evidence order

1. Koo-approved policy / approved specification / Accepted ADR / Issue #43
2. common base → candidate Head diff
3. production execution path
4. actual state / exit / ordering / migration history
5. automated tests / validators / mutation evidence
6. direct-head CI
7. project-rule result
8. PR説明 /自己申告

## 5. Selection principles

Final Synthesisは最高得点candidateのmergeではない。

要素単位で選ぶ:

- observable runtime design
- test oracle
- D-04 operational semantics
- D-06 failure injection
- evidence design
- Project Rule conformity

Candidate branch merge / cherry-pickは禁止。Selection後current mainからcurated Final Synthesisを作る。

Exact service name / file placement / Compose mechanismを、上位正本やpre-run lockなしに採用必須条件へ昇格しない。

## 6. Tie-break

1. fail-closed evidenceの強さ
2. mutation sensitivity
3. production pathを通した外部観測
4. Scopeの小ささ
5. MUST ruleへの自然な適合
6.不要差分の少なさ

処理時間はCoding Scoreへ混ぜない。
