# FND-06 live／ready health contract

FND-06は、Accepted ADR-0008のhealth check決定をAPIとDocker Compose runtimeへ実装する。

## 1. Endpoint semantics

| Endpoint | 意味 | PostgreSQL依存 |
| --- | --- | --- |
| `GET /health/live` | API processが生きているか | 依存しない |
| `GET /health/ready` | APIがtrafficを受けられるか | 依存する |

`/health/live`はdependency checkを一切実行しない。readiness用checkは`ready` tagで選択され、liveness endpointのpredicateはどのcheckも選択しない。したがってPostgreSQLの状態はlivenessへ影響しない。

`/health/ready`は次の両方を満たすときだけ成功する。

1. PostgreSQLへ接続できる
2. canonical EF Core migrationがすべて適用済みである

migration判定はFND-04の既存EF Core migration metadata（`public.__EFMigrationsHistory`と`IMigrationsAssembly`）だけを正本とする。FND-06は新しいmarker table、business schema marker、独自migration台帳を作らない。

readiness checkは読み取りのみで、schemaを作成・変更しない。API通常起動がmigrationを実行しないというFND-04契約は維持される。

## 2. Response contract

| 状態 | HTTP | body |
| --- | ---: | --- |
| healthy | 200 | `healthy` |
| not ready | 503 | `unhealthy` |

- `Content-Type: text/plain; charset=utf-8`
- `Cache-Control: no-store, no-cache`
- FND-02の`X-Correlation-ID`契約は維持される

health responseはconnection string、credential、host、exception type／detail、stack trace、check名、dependency識別子、descriptionを含まない。失敗理由（`database_unreachable`、`migrations_pending`、`dependency_failure`）は障害ログだけへ出力する。これはADR-0008 §14.3の「health check異常は技術記録」という区分に従う。

health failureはFND-02のbusiness error envelope（`code`／`message`）へ変換しない。`/health/*`以外のpath、たとえば`/health/does-not-exist`は従来どおり404 `endpoint_not_found` envelopeを返す。

## 3. Docker Compose health configuration

`api` serviceのhealthcheckは`/health/ready`へ実際のHTTP requestを送り、status lineが`200`であることを確認する。process存在、TCP port open、`pg_isready`をAPI readinessの代替としない。`postgres` serviceのhealthcheckは従来どおり`pg_isready`のままである。

```yaml
healthcheck:
  test:
    - CMD
    - bash
    - -c
    - 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3 && IFS= read -r status <&3 && [[ $$status == "HTTP/1.1 200 "* ]]'
  interval: 5s
  timeout: 15s
  retries: 3
  start_period: 20s
```

probe budget（15s）はreadiness endpointの内部budget（10s）より長い。readiness checkがbudgetを使い切った場合でも、probeは実際の503を観測する。

### 3.1 Probe tooling

shipped API runtime image（`mcr.microsoft.com/dotnet/aspnet:10.0-noble`、digest pinned）で実測したtool inventoryは次のとおりである。

```text
PRESENT bash
PRESENT openssl
ABSENT  curl
ABSENT  wget
ABSENT  nc
ABSENT  netcat
ABSENT  python3
ABSENT  ncat
ABSENT  socat
```

curl／wget等は存在しない。probeのためにimageへtoolを追加インストールせず、shipped imageに既に存在するbash builtin（`/dev/tcp`とredirection）だけでHTTP probeを構成する。同じ手法はFND-05のCompose verificationでも既に使用されている。

FND-06はrestart policy、自動復旧、monitoring／metrics基盤、alertingを追加しない。containerがunhealthyになっても再起動しない。

## 4. Verification

```bash
# API integration（Docker不要のhealth contract）
dotnet test MinimalBankSystem.slnx --no-build --filter "Category!=PostgreSqlIntegration"

# real PostgreSQL（readiness、migration未完了、実stop/start transition）
dotnet test tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj \
  --no-build --filter "Category=PostgreSqlIntegration"

# shipped Compose runtime E2E
export MBS_DATABASE_PASSWORD='local-test-value'
bash tests/fnd06/verify-health-compose.sh
```

`tests/fnd06/verify-health-compose.sh`は2つのrunを実行する。

- Run A: canonical shipped ordering。live／ready成功 → PostgreSQL停止でliveのみ成功・readyは失敗・containerはunhealthy・API processは稼働継続 → PostgreSQL復旧でAPI再起動なしにready復帰。
- Run B: migration未完了runtime。`--no-deps`でMigrator gateを迂回して未migration状態を作り、live成功・ready失敗・API側でschemaが作られていないことを確認したうえで、Migrator実行後に同一API containerがreadyへ復帰することを確認する。shipped compose orderingそのものは変更しない。
