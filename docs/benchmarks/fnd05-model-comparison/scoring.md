# FND-05 Implementation Scoring

Revision: `fnd05-scoring-v1`

このrubricは3 candidateの実装開始前に固定する。Modelの評判、価格、過去順位で点数を調整しない。

## 1. Score

| Category | Points | Evaluation |
| --- | ---: | --- |
| A. Issue #43達成度 | 20 | ScopeとAcceptance Criteriaを満たすか |
| B. Runtime ordering / fail-closed | 20 | PostgreSQL → Migrator → API、failure時API非起動が実証されるか |
| C. Test oracle / mutation sensitivity | 20 | positive / negative evidence、reachability、mutationで欠陥を検出できるか |
| D. ADR / responsibility boundary | 15 | ADR-0001 / 0008 / 0009、FND-04 / 05 / 06境界へ適合するか |
| E. Secret / image / volume safety | 10 | credential非保存、argv非露出、digest、named volumeが正しいか |
| F. Project rule / code quality | 5 | 配置、依存、単純性、一貫性、不要変更の少なさ |
| G. Reproducibility / operations | 5 | start / stop / restart / clean resetが再現可能か |
| H. Evidence / CI identity | 5 | exact Head、commands、CI、未検証事項が正確か |
| **Total** | **100** | |

## 2. Merge-ready classification

Candidateは比較用であり、candidate PRを直接mergeしない。

比較上の`merge-ready`は次をすべて満たす場合だけ付与する。

- Blocker 0
- Major 0
- required verification成功
- no secret / credential leak
- no API startup migration
- no FND-06 health先取り
- no business schema / backup / production orchestrator先取り
- migration failure時にAPI非起動
- test oracleが意図したpathへ到達する証拠あり
- candidate Head / CI identityが一致

## 3. Severity

### Blocker

- 正しいtarget / Headを評価できない
- secret / credentialがrepositoryへ保存された
- APIがmigration failure後も開始する
- API startupでschema migrationを実行する
- 必須verificationを実行不能で、Close判断ができない

### Major

- Acceptance Criteriaの実質未達
- startup orderingが実際には保証されない
- failure pathがfail-open
- named volume / digest / secret contractの重大違反
- negative testが守るべきdefect classを検出できないfalse assurance
- FND-06等の責任を先取りしないと成立しない設計

### Minor

- Closeは可能だが、限定的な保守性・証拠・運用上の問題が残る
- project ruleの非重大な逸脱
- test coverageの局所的不足

### Nit

- behaviorやClose判断へ影響しない軽微な表現・整理

## 4. Evaluation evidence order

1. Issue #43 / Accepted ADR / AGENTS.md
2. common baseからcandidate Headまでのdiff
3. production Compose / Dockerfile / scripts
4. actual container state / exit code / timestamps / migration history
5. automated tests / validators / mutation evidence
6. direct-head CI
7. PR説明と自己申告

PR説明だけを実装証拠にしない。

## 5. Selection principles

Final Synthesisは単純な最高得点candidateのmergeではない。

- production design
- test oracle
- operational commands
- failure injection
- project rule conformity

を要素単位で選ぶ。

candidate branchのmerge / cherry-pickは禁止する。Selection / Adjudication後、current mainからcurated Final Synthesisを作る。

## 6. Tie-break

得点が近い場合は次を優先する。

1. fail-closed evidenceの強さ
2. mutation sensitivity
3. production pathを通した外部観測
4. scopeの小ささ
5. project ruleへの自然な適合
6. code量の少なさではなく不要差分の少なさ

処理時間はCoding Scoreへ混ぜない。
