using Xunit;

// Several tests redirect the process-wide Console.Out to capture the real
// JSON console logger output. Parallel test collections would interleave
// writes on that shared stream, so this assembly runs sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
