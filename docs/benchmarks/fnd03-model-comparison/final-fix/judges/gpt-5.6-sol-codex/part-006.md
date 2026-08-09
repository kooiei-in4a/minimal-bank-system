- `StartAsync`成功後にだけIDを保存する設計。
- native Disposeの正常returnだけでownerを解放する設計。
- 同一poisoned instanceの再Dispose。
- `docker inspect`の任意のnon-zeroを不存在扱いする設計。
- fallback成功時に元のfailureを正常終了へ変える設計。
- manual `ForceCleanupAsync`をproduction lifecycleへ統合しない設計。
- full fake Docker daemon、raw HTTP client、custom proxyをproduction cleanupの必須部品にする設計。
- container事前削除やpre-cancelをT-01として扱うtest。

### Final implementation shape

実装者はPR #108のarchitectureを維持する。追加統合は必須ではない。

将来proofをさらに強化する場合も、production codeは変えず、Terraの`UnsafeDeleteAsync` override patternをtestへ限定して追加するのが適切である。Opusのtransport proxy全体を取り込む必要はない。

---

## 10. Key Findings

1. Testcontainers instanceの参照保持はresource ownershipではない。retry能力を持つのはdaemon identityである。
2. container IDだけではstartup partial createを完全に所有できない。create requestへ事前設定できるunique label/nameが必要になる。
3. stable nameを持っていても、native Dispose成功時にindependent verificationを省略するとpartial-create orphanを防げない。
4. 独立cleanupは「removeを呼んだ」だけでなく、daemonが不存在を返すまで完了ではない。
5. `docker inspect`失敗とcontainer不存在は同義ではない。daemon unavailable、permission、TLS、endpoint mismatchを区別する必要がある。
6. fallback成功後もnative cleanup failureはtest failureとして残す必要がある。resource leak防止とfailure visibilityは別責任である。
7. `UnsafeDeleteAsync` overrideは、実Testcontainers latchを使いつつ大規模proxyを避けられる有力なtest-only injectionである。
8. fake Docker daemonはAPI sequence proofには有用だが、actual Docker Engineのresource-state proofを完全には代替しない。
9. green CIは全14件に共通であり、failure-path品質の識別力を持たなかった。
10. Quality/minは補助指標に過ぎない。最速候補はconfirmed Majorを残した。

---

## 11. Final Conclusion

```
Top candidate:
GPT-5.6 Sol / Codex — PR #108 — 94/100

Recommended final approach:
PR #108をそのまま採用する。unique label owner + one-shot native dispose +
unconditional independent daemon verificationを最終形とする。

Candidates merge-ready:
1 / 14
- GPT-5.6 Sol / Codex

Candidates not merge-ready:
13 / 14
- GPT-5.6 Luna / Codex
- GPT-5.6 Terra / Codex
- Claude Opus 5 / Claude Code
- Claude Sonnet 5 / Claude Code
- Grok 4.5 / Cursor
- Composer 2.5 / Cursor
- DeepSeek V4 Pro / Open Code
- DeepSeek V4 Flash / Open Code
- Qwen3.7 Plus / Open Code
- GPT-5.6 Luna / Open Code
- MiMo-V2.5-Pro / Open Code
- MiMo-V2.5 / Open Code
- MiniMax M3 / Open Code

Next action:
別セッションでPR #108 HeadをFinal Synthesisへ採用し、exact Head CIを再確認してAgent B独立レビューへ進む。
この評価セッションではコード、branch、PR、Issue、artifactを変更していない。
```