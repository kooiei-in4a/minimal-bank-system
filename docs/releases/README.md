# Releases

Release Candidateの判定、デプロイ、migration、smoke test、ロールバック、既知制約、リリース結果を保存します。

## Release Readyの最低条件

- 承認済み仕様とAccepted ADRがある。
- 要件からテストまでの追跡が成立している。
- unit、integration、concurrency、E2Eの必須テストが成功している。
- migrationとデプロイ手順が検証済みである。
- ロールバック手順と制約が明示されている。
- 実データまたは実金融サービスを使用していない。
- KooによるGo / No-Go判断が記録されている。
