# FND-03 Independent Review Benchmark — Protocol-compatible Gold Review

Status: **CANONICAL POST-HOC GOLD / HISTORICAL METHODOLOGY CAVEAT**

重要: `post_hoc_adjudication: true`

このGoldは、完全blindな事前locked Goldではない。最初のReference lock後、raw reviewerを収集した後の追加一次source突合により、Testcontainers .NET 4.13.0のfailure-path Majorを明確化した。したがって、raw reviewerの内容を事前に見ずに固定された純粋なblind Goldとして扱ってはいけない。

## Target

- Repository: `kooiei-in4a/minimal-bank-system`
- Issue: #41
- PR: #104
- Base SHA: `7946cc55e49c0c6e21ad7b86c20a8435b4976269`
- Head SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Primary CI: `31277771209`

## Reference verdict

```text
REQUEST CHANGES / NOT MERGE READY
Blocker: 0
Major:   1
Minor:   1
Nit:     0
```

## Gold root causes

### G-01 — Major / blocking

Testcontainers 4.13.0の`Resource.Disposed`は、Docker resourceの削除完了前にdisposed stateをlatchする。`DockerContainer.DisposeAsyncCore()`のremoveが失敗した後、同じfailed instanceへの2回目の`DisposeAsync()`はno-op成功になり得る。Final Synthesis側がそのno-opを成功として扱ってhandleを解放すると、actual containerが残ったままdeterministic ownerを失う。

これは通常のgreen CIや正常系cleanupでは反証されない。Issue #41のcleanup failure後のretry、final cleanup、ownership保持の契約に影響するためMajorかつblockingである。

一次根拠: [`implementation-evaluation.md`](./implementation-evaluation.md)、Testcontainers 4.13.0の[`Resource.cs`](https://github.com/testcontainers/testcontainers-dotnet/blob/4.13.0/src/Testcontainers/Resource.cs)、[`DockerContainer.cs`](https://github.com/testcontainers/testcontainers-dotnet/blob/4.13.0/src/Testcontainers/Containers/DockerContainer.cs)。

### G-02 — Minor / non-blocking

Final Synthesisのdigest assertionは、running containerをDocker daemon側でinspectした証拠ではなく、Testcontainers builderへ渡したdigest-qualified image referenceのparse結果をconstantと比較している。digest pin自体とPostgreSQL 18.4 runtime checkは成立するが、Issue #41で求める「image digest確認」のverification artifactとしては証拠強度が不足する。

一次根拠: [`implementation-evaluation.md`](./implementation-evaluation.md) §3.3、[`implementation-evaluation-claude-opus-5.md`](./implementation-evaluation-claude-opus-5.md) §3.3。

## English methodology note

`post_hoc_adjudication: true`. This Gold was clarified by an additional primary-source comparison after the initial Reference lock and raw review capture. It must not be described as a fully blind, pre-locked Gold benchmark.
