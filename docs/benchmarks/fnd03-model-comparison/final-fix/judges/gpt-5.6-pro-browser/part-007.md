
```

**GPT-5.6 Sol / Codexをbaseに、Claude Opus 5のfailure-proofを限定統合する。**

### Adopt

#### GPT-5.6 Sol / Codexから採用

- container作成前に生成する一意ownership label
- `IContainerResourceOwner`
- native Testcontainers disposeを最大1回に制限するflag
- cleanup gate
- labelによるDocker daemon列挙
- force remove＋再列挙0件によるowner release
- native failure＋independent failureのaggregation
- independent cleanup成功後もnative failureを可視化
- startup catchから同じcleanup state machineを呼ぶ構造

#### Claude Opus 5 / Claude Codeから採用

test-only範囲で以下を統合します。

- 実Docker endpointに対するtransport fault injection
- 実Testcontainersの最初のremove request failure
- same-instance second Dispose no-opの実証
- upstream daemon側container残存確認
- transport復旧後のindependent final cleanup
- startup primary failure＋cleanup transport failureの組合せ

### Reject

- Opus案のproduction用hand-written Docker HTTP client
- DeepSeek V4 Flashのfull fake Docker daemon
- CLI exit codeやstderr文字列だけでabsenceを判定する方式
- private reflectionだけをT-01の正本証拠とする方式
- native cleanup failureをfallback成功時に隠す方式
- double failure後に同じTestcontainers instanceを再Disposeする方式
- manual `ForceCleanupAsync`をfixture teardownから独立させる方式
- root `.editorconfig`等のunrelated変更

### Final implementation shape

1. fixture初期化前に一意ownership labelを生成する。
2. Docker resource ownerをTestcontainers instanceとは別objectとして先に作る。
3. containerへownership labelを付与して起動する。
4. cleanup gate内で、native Testcontainers disposeは一度だけ試す。
5. native呼出し開始前に`nativeDisposeAttempted = true`を立てる。
6. native成功・失敗に関係なく、independent ownerがlabel対象をdaemonから検索する。
7. 対象があればforce removeし、再検索が0件になるまでownerを解放しない。
8. native失敗＋independent成功の場合はresource ownerを解放するが、native failureはthrowする。
9. independent失敗の場合はlabel ownerを保持し、次回はnative instanceを呼ばずindependent cleanupだけをretryする。
10. startup failureでも同じcleanup methodを呼び、primary failureとcleanup failureをAggregateExceptionで保持する。
11. existing unreachable Docker endpoint testを残す。
12. 実transport fault testを追加し、actual daemon残存・最終不在を確認する。

---

## 10. Key Findings

1. **Testcontainers instance referenceはcleanup ownerではない。**
   resource identityはID、name、label等として別管理する必要があります。
2. **owner identityを取得する時期が重要。**
   `StartAsync`成功後だけIDを保存する方式では、container作成後・startup完了前のfailureに弱くなります。label ownerを作成前から保持する方式が最も堅牢です。
3. **Docker CLIの非0終了はcontainer不在を意味しない。**
   absenceは特定の`No such container`またはDocker API 404として識別し、authorization、daemon、transport errorを区別する必要があります。
4. **fallback成功は元のcleanup failureを消さない。**
   actual resourceを回収できても、最初のcleanup failureはtest failureとして可視化する必要があります。
5. **double failureが最重要境界である。**
   native failure＋independent failure後の次回cleanupが、同じpoisoned instanceへ戻らないことを確認する必要があります。
6. **success-pathの実Docker testだけでは不十分。**
   actual container不在を確認しても、failure pathが正しいとは限りません。
7. **pre-removeは今回のfailure injectionにならない。**
   containerを先に削除すると、Testcontainersは「既にない」と判断して正常終了する可能性があります。
8. **fake daemonは証明力と保守コストの交換条件になる。**
   実Testcontainers protocolを通せますが、Docker API surfaceの追随負担が大きくなります。
9. **quality/minはquality gate通過後にのみ意味を持つ。**
   Composerは最速ですが、failure visibility Majorにより採用不可です。
10. **6件はmerge-readyだが、最終統合では実装と証明を別candidateから選ぶ価値がある。**
    Solのproduction state machineとOpusのfailure proofの組合せが最も堅牢です。

---

## 11. Final Conclusion

```
Top candidate:
  GPT-5.6 Sol / Codex
  Score: 95 / 100

Recommended final approach:
  B
  GPT-5.6 Sol / Codexをimplementation baseとし、
  Claude Opus 5 / Claude Codeのreal transport failure proofを限定統合する。

Candidates merge-ready:
  - GPT-5.6 Sol / Codex
  - GPT-5.6 Luna / Codex
  - Claude Opus 5 / Claude Code
  - Grok 4.5 / Cursor
  - DeepSeek V4 Flash / Open Code
  - GPT-5.6 Luna / Open Code

Candidates not merge-ready:
  - GPT-5.6 Terra / Codex
  - Claude Sonnet 5 / Claude Code
  - Composer 2.5 / Cursor
  - DeepSeek V4 Pro / Open Code
  - Qwen3.7 Plus / Open Code
  - MiMo-V2.5-Pro / Open Code
  - MiMo-V2.5 / Open Code
  - MiniMax M3 / Open Code

Next action:
  Common BaseからFinal Synthesisを作成し、
  Solのlabel-based independent owner state machineを採用する。
  その上でOpusのreal transport failure testをtest-onlyで追加し、
  exact Head CIと独立レビューを通してからPR #104系統へ統合する。

```

**最終判定:** 単独採用候補としてはGPT-5.6 Sol / Codexが最上位です。ただし、最終成果物としてはSol実装＋Opus failure proofの限定統合が、正しさ、証拠、保守性の最良バランスです。