# FND-04 H1 Execution Wrapper

Revision: `fnd04-h1-exec-time-v1`

このwrapperは、pre-runで固定済みのcanonical H1 prompt `fnd04-h1-v1` を変更しない。
FND-04のH1実行時だけ外側から付加し、処理時間取得方法を試行する。

## Rough execution time — experimental

H1では処理時間を**概算の分単位**で記録する。秒単位の精密比較は目的としない。

### Start

このpromptを受け取った後、repository確認、GitHub確認、todo作成、コード読取より先に、可能なら**最初のtool action**として次を実行する。

```bash
python -c "import time; print(int(time.time()))"
```

出力を `START_EPOCH` として保持する。

`python` が利用できない場合は、同等のローカル時刻取得コマンドを1回だけ使用してよい。時間取得のために本来の作業を止めたり、環境整備を行ったりしない。

### End

H1の実装・検証・exact Head CI確認がすべて終了した後、最終回答を書く直前の**最後のtool action**として同じ方法で `END_EPOCH` を取得する。

```text
DURATION_MINUTES = (END_EPOCH - START_EPOCH) / 60
```

最終報告では概算の整数分を記録する。1分未満なら `<1分` としてよい。

```text
START_EPOCH: <value or N/A>
END_EPOCH: <value or N/A>
DURATION_MINUTES: 約<n>分 | <1分 | N/A
```

### Rules

- 数十秒〜1分程度の誤差は許容する。
- GitHub PR / commit / ActionsのtimestampからAgent処理時間を逆算しない。
- CI durationとAgent processing durationを混同しない。
- 計測失敗時のみ `N/A` とする。
- FND-04ではH1 durationは試行データであり、H0/SRの欠損時間を補完しない。
- FND-04のSpeed Score / Quality-Time Index等には使用しない。

## Zero-finding H1

Formal Self-Review Findingが0件の場合もH1 phaseは明示的に実行する。ただし、Findingがないことを理由に新規改善を行ってはいけない。

- H1 code change: none
- H1 Head: H0 Headと同一
- empty commitを作成しない
- required verificationは再実行する
- exact-head CIは同一Headの既存成功CIを再利用してよい。再実行できる場合は再実行してもよいが、CIを発生させるためだけのコード変更やempty commitは禁止
- Final reportで `NO CHANGE / H1_HEAD_EQUALS_H0_HEAD` を明記する

## Finding disposition

Findingが1件以上ある場合、各Findingについて必ず一次証拠から独立に次を決める。

```text
SR-xx: accepted | rejected
Reason: <Issue/ADR/code/test/runtime evidence>
```

- Self-Reviewの推奨修正を自動採用しない。
- `accepted` のみ必要最小限で修正する。
- `rejected` はコード変更せず、棄却理由を一次証拠付きで残す。
- 全FindingがrejectedならZero-finding H1と同様にH1 HeadはH0 Headのままとする。
