# FND-05 Pre-Run Completion Checklist

Status: **PREPARATION IN PROGRESS / IMPLEMENTATION PROHIBITED**

## 1. Policy decisions — fixed

- [x] implementation candidate = 3
- [x] C1 GPT-5.6 Luna / Codex
- [x] C2 Claude Sonnet 5 / Claude Code
- [x] C3 Grok 4.5 / Cursor
- [x] OpenCode = 0
- [x] independent Formal Self-Review phase = 0
- [x] implementation promptへCompletion Checksを埋め込む
- [x] Light L1 = Composer 2.5 / Cursor
- [x] Light L2 = GPT-5.6 Luna / Codex
- [x] Heavy H1 = GPT-5.6 Sol / Codex
- [x] Heavy H2 = Claude Opus 5 / Claude Code
- [x] Heavy promptへexplicit non-goalsを記載
- [x] Heavy full review budget = 原則各1回
- [x] Judge = conditional only
- [x] re-review = finding owner / blast radius based

## 2. Draft artifacts — created

- [x] `README.md`
- [x] `run.json`
- [x] `scoring.md`
- [x] `reference/assumption-ledger.md`
- [x] `reference/implementation-and-test-design-contract.md`
- [x] `reference/project-rule-catalog.md`
- [x] `reference/review-perspective-matrix.md`
- [x] `reference/mandatory-mutations.md`
- [x] `prompts/implementation.md`
- [x] `prompts/implementation-evaluation.md`
- [x] `prompts/selection-adjudication.md`
- [x] `prompts/final-synthesis.md`
- [x] `prompts/light-review-project-quality.md`
- [x] `prompts/light-review-contract-conformance.md`
- [x] `prompts/light-findings-fix.md`
- [x] `prompts/heavy-review-sol.md`
- [x] `prompts/heavy-review-opus.md`
- [x] `prompts/conditional-judge.md`
- [x] `prompts/issue-ready-review.md`
- [x] `prompts/targeted-fix.md`
- [x] `prompts/targeted-re-review.md`

## 3. Decisions still to lock

### D-01 Compose version floor

- [ ] local `docker compose version`取得
- [ ] GitHub Actions runner version取得
- [ ] required feature support確認
- [ ] minimum version固定

Required features:

- `service_healthy`
- `service_completed_successfully`
- secrets source
- `ps --all --format json`
- `config --quiet`

### D-02 Image identities

- [ ] PostgreSQL 18.4 full digest再確認
- [ ] .NET 10 SDK full digest
- [ ] ASP.NET 10 runtime full digest
- [ ] image source / platform確認
- [ ] run registryへ固定

### D-03 Secret design

- [ ] host secret source固定
- [ ] Compose secret name固定
- [ ] API / Migrator secret reader配置固定
- [ ] Postgres `_FILE` contract固定
- [ ] missing secret negative確認方法
- [ ] rendered config / argv / logs sentinel方式固定

### D-04 Lifecycle commands

- [ ] validate command
- [ ] clean start command
- [ ] stop command
- [ ] start-after-stop command
- [ ] canonical restart command
- [ ] down-retain-data command
- [ ] clean-reset command
- [ ] cleanup absence command

### D-05 External state capture

- [ ] Migrator exit code取得方法
- [ ] Migrator finished timestamp取得方法
- [ ] API state取得方法
- [ ] API started timestamp取得方法
- [ ] migration history query方法

### D-06 Failure injection

- [ ] invalid credential override
- [ ] migration failure override
- [ ] test-only asset placement
- [ ] no production backdoor確認

### D-07 Cross-platform contract

- [ ] GitHub Actions Linux required
- [ ] primary local environment required
- [ ] shell-specific scriptの代替方針
- [ ] path / line ending確認

### D-08 Final Synthesis identity

- [ ] author Model / Harness / Effort固定
- [ ] final branch / Draft PR事前作成

## 4. Repository / Issue preparation

- [ ] PR #144 final retrospective review
- [ ] FND-05 preparation PR review
- [ ] Issue #43 bodyとcurrent dependency statusを同期
- [ ] Issue #43へdesign contract revision記録
- [ ] Issue #43 Gate status再評価
- [ ] Parent #3 / WP-1 #33 current status同期

Issue本文の更新はpre-run docs review後に行う。draft contractを確定事項として先に書かない。

## 5. Common base / branches

- [ ] preparation PR merge
- [ ] current main full SHA取得
- [ ] common base fixed
- [ ] C1 branch created
- [ ] C2 branch created
- [ ] C3 branch created
- [ ] 3 / 3 branch Head = common base
- [ ] 3 Draft PR事前作成
- [ ] exact identity / effort固定
- [ ] candidate output 0件確認

## 6. Gate review

- [ ] `prompts/issue-ready-review.md`をfresh contextで実行
- [ ] Gate result = PASS
- [ ] implementation permitted = YES
- [ ] Koo start authorization

## 7. Stop rule

次のいずれかが未完了ならcandidate executionを開始しない。

- D-01〜D-08
- Issue Ready PASS
- common base / branches / PR identity
- exact model / harness / effort
- Koo start authorization

## 8. Current next action

1. final retrospective PR #144をreviewする。
2. FND-05 preparation PRをreviewする。
3. D-01〜D-08を一次証拠で確定する。
4. Issue #43をcurrent contractへ同期する。
5. Issue Ready Gate Reviewを実施する。

現時点ではDocker Compose実装を開始しない。
