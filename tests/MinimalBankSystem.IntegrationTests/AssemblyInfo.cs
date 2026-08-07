// The API runtime contract tests capture the process wide console stream to prove that technical
// logs are emitted as JSON and exclude prohibited fields. Console redirection is global state, so
// the assembly runs its tests sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
