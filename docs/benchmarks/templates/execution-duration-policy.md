# Benchmark Execution Duration Policy / Prompt Block

Status: Proposed for future benchmark runs

## Purpose

処理時間を比較に使用するrunでは、candidate開始前に収集方法を固定し、全candidateへ同じ方法を適用する。

処理時間を一貫して収集できないrunでは、後からGitHub timestampやAgentの自己申告から推測せず、run全体で速度比較を無効化する。

## Pre-run decision

各benchmark runはcandidate開始前に次を固定する。

```yaml
DURATION_COLLECTION_MODE: "enabled" # enabled | disabled
DURATION_UNIT: "seconds"
DURATION_SOURCE: "<collector-defined source>" # enabled時のみ
```

### enabled

- H0 / Formal Self-Review / H1を別々に計測する。
- 同じphaseでは全candidateへ同じ計測方法を使用する。
- start / endの境界をpromptで明示する。
- Agent自身に経過時間を推測させない。
- GitHub PR作成時刻、commit時刻、CI開始/終了時刻をAgent実行時間の代替にしない。
- 比較可能なdurationがprimary pool全件で揃わない場合、Speed Score / Quality-Time Index / Practical Scoreの速度成分をrun全体で公開しない。

推奨境界:

```text
H0 start:
  candidate prompt投入直後
H0 end:
  H0 exact Head CI確認 + H0 snapshot lock

SR start:
  fresh-context Formal Self-Review prompt投入直後
SR end:
  SR findings / structured JSON固定

H1 start:
  H1 fix prompt投入直後
H1 end:
  H1 exact Head CI確認 + H1 snapshot lock
```

### disabled

- 全candidateのdurationを `N/A` とする。
- Agentへ時間推測を要求しない。
- Speed Scoreを算出しない。
- Quality / Time Indexを算出しない。
- Practical Scoreの速度成分を算出しない。
- GitHub timestampや一部candidateの自己申告値から欠損値を補完しない。

## Required prompt block

今後のH0 / SR / H1 promptには、次のblockを必ず含める。

```text
## Duration collection

DURATION_COLLECTION_MODE: <enabled | disabled>
DURATION_UNIT: seconds
DURATION_SOURCE: <pre-locked source | N/A>

If enabled:
- Do not estimate elapsed time yourself.
- Use only the pre-locked collector/source.
- Report the measured duration for this phase exactly as supplied by that source.

If disabled:
- Report `Duration: N/A`.
- Do not infer duration from GitHub timestamps, CI runtime, commit timestamps, or subjective estimates.
```

## Required result fields

各phaseのfinal reportには次を含める。

```text
Duration collection mode:
Duration source:
Duration:
```

`disabled`の場合:

```text
Duration collection mode: disabled
Duration source: N/A
Duration: N/A
```

## Run registry

`run.json`等のrun registryには最低限次を記録する。

```json
{
  "duration_policy": {
    "mode": "enabled_or_disabled",
    "unit": "seconds",
    "source": "collector_or_N/A",
    "speed_score": "enabled_or_not_calculated",
    "quality_time_index": "enabled_or_not_calculated",
    "practical_score_speed_component": "enabled_or_not_calculated"
  }
}
```

## FND-04 disposition

FND-04ではH0 durationが8 candidateで一貫して収集されなかったため、処理時間比較をrun全体で無効化する。

- H0 duration: `N/A`
- SR duration: `N/A`
- H1 duration: `N/A`
- Speed Score: not calculated
- Quality / Time Index: not calculated
- Practical Score speed component: not calculated

一部candidateに残っている時間情報は参考メタデータとして保持してよいが、比較採点には使用しない。
