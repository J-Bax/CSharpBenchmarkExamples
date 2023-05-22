# C# Benchmarks
A collection of random benchmarks in C#.

## 1M Concurrent Tasks
Inspired by this hackernews post: https://news.ycombinator.com/item?id=36024209
The blog post: https://pkolaczk.github.io/memory-consumption-of-async/

The authors code had some inefficiencies in the C# version. I was curious whether there are big improvements possible without changing the intent of the benchmark.

The problem statement was thus:
`Let’s launch N concurrent tasks, where each task waits for 10 seconds and then the program exists after all tasks finish. The number of tasks is controlled by the command line argument.`

Here is the original code:
```csharp
List<Task> tasks = new List<Task>();
for (int i = 0; i < numTasks; i++)
{
    Task task = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
    });
    tasks.Add(task);
}
await Task.WhenAll(tasks);

```


### Observation 1: Author was not passing an initial size to List.
- This causes the List to be re-sized and copied each time the internal capacity is exhausted.
- Causes a large amount of intermediate allocations and GC pressure.
- Other languages tested have the same inefficiency but this is particularly unfair for managed/garbage collected languages.
- Depending on how frequently GC can run, those intermediate buffers may stick around for awhile leading to higher peak memory utilization.
- In an unmanaged language, those intermediate buffers are likely freed as soon as the new buffer is created and copied to
    - The peak memory utilization is likely lower compared to the managed languages.

Let's see what the impact on memory is by passing an initial capacity:

```csharp
List<Task> tasks = new List<Task>(N);
for (int i = 0; i < N; i++)
{
    Task task = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromMilliseconds(delayMillisec));
    });
    tasks.Add(task);
}
await Task.WhenAll(tasks);
```

|                  Method |    Mean |   Error |  StdDev | Lock Contentions |       Gen0 |       Gen1 |      Gen2 | Allocated | Alloc Ratio |
|------------------------ |--------:|--------:|--------:|-----------------:|-----------:|-----------:|----------:|----------:|------------:|
| SetInitialCapacity_List | 12.15 s | 1.295 s | 0.071 s |       14070.0000 | 55000.0000 | 29000.0000 | 4000.0000 | 419.96 MB |        0.98 |
|                Baseline | 12.30 s | 3.038 s | 0.167 s |       14416.0000 | 55000.0000 | 29000.0000 | 4000.0000 | 428.44 MB |        1.00 |

Surprisingly the result is fairly negligible. The total allocated memory surprised me as well and was a lot higher than I expected. This means that the size of the array itself is negligle compared to the contents of the array. That means that the memory footprint of <Task> is pretty high.
- An array of size 1M is going to use about ~8MB of memory (64-bit/8-byte references * 1M)
- If the List re-size logic follows exponential growth (1 -> 2 -> 4 -> ... -> ~500K -> ~1M), then the combined size of all intermediate buffers is approximately 1M.
- This roughly matches the difference we see ~9MB.

To confirm the theory, let's benchmark resizing a list without all of the task stuff.
- This benchmark adds the same string to a list 1M times, first with an initial capacity on the list of 1M, and second without a capacity.

|                         Method |     Mean |     Error |    StdDev | Ratio |     Gen0 |     Gen1 |     Gen2 | Allocated | Alloc Ratio |
|------------------------------- |---------:|----------:|----------:|------:|---------:|---------:|---------:|----------:|------------:|
| SetInitialCapacity_List_String | 5.135 ms |  1.744 ms | 0.0956 ms |  0.61 | 265.6250 | 265.6250 | 265.6250 |   7.63 MB |        0.48 |
|                Baseline_String | 8.401 ms | 10.838 ms | 0.5940 ms |  1.00 | 453.1250 | 437.5000 | 437.5000 |     16 MB |        1.00 |

#### Conclusion: Minimal impact on memory usage
 - The lack of initial List capacity doesn't contribute much to the overall peak memory usage.
 - The Task objects themselves contribute significantly more memory.

### Observation 2: Delay task is unnecessarily wrapped in Task.Run(...)
- By default .NET will eagerly execute a Task when it is created.
    - When the first real async operation occurs, usually there is a yield operation that will yield back to the caller.
- To immediately yield a new Task without running it, you can call Task.Run(...) which creates the Task and schedules it to the Threadpool
    - Typically this is needed for CPU-bound work to avoid blocking the caller.
    - For IO-bound async code, this is typically not necessary.

Task.Delay(...) will immediately yield to the caller, so the Task.Run(...) isn't giving us any benefit here.

Let's try the same benchmark without the Task.Run(...) call:

```csharp
List<Task> tasks = new List<Task>();
for (int i = 0; i < N; i++)
{
    tasks.Add(Task.Delay(TimeSpan.FromMilliseconds(delayMillisec)));
}
await Task.WhenAll(tasks);
```

|          Method |    Mean |   Error |  StdDev | Ratio |       Gen0 | Completed Work Items | Lock Contentions |       Gen1 |      Gen2 | Allocated | Alloc Ratio |
|---------------- |--------:|--------:|--------:|------:|-----------:|---------------------:|-----------------:|-----------:|----------:|----------:|------------:|
| AvoidingTaskRun | 10.93 s | 1.255 s | 0.069 s |  0.91 | 23000.0000 |         1000001.0000 |        7336.0000 | 13000.0000 | 3000.0000 | 183.86 MB |        0.43 |
|        Baseline | 12.05 s | 0.978 s | 0.054 s |  1.00 | 55000.0000 |         2000046.0000 |       13132.0000 | 29000.0000 | 4000.0000 | 428.46 MB |        1.00 |

Clearly the result is significant.
- Memory is roughly halved if we get rid of the Task.
- Less queueing delay in executing all the tasks with the benchmark completing in ~11s rather than ~12s as before.
    - A perfect result would be ~10s.

#### Conclusion: Significant impact on memory usage

### Observation 3 (from kevingadd): What if a single Task.Delay was used instead of N?

There are two ways I thought of to test this. The first is to create one delay, but continue to use N different tasks. I put N continuations on the delay task to achieve this.
```csharp
Task delayTask = Task.Delay(TimeSpan.FromMilliseconds(delayMillisec));
List <Task> tasks = new List<Task>();
for (int i = 0; i < N; i++)
{
    tasks.Add(delayTask.ContinueWith((Task _) => { }));
}
await Task.WhenAll(tasks);
```

The second is to create one delay, with one task and then await on it N times (IMO this doesn't quite match the original intent of "...N concurrent tasks, where each task waits for 10 seconds...") e.g:
```csharp
Task delayTask = Task.Delay(TimeSpan.FromMilliseconds(delayMillisec));
List <Task> tasks = new List<Task>();
for (int i = 0; i < N; i++)
{
    tasks.Add(delayTask);
}
await Task.WhenAll(tasks);
```

Lets see what the impact is of both approaches:
|                                          Method |    Mean |   Error |  StdDev | Ratio | Completed Work Items | Lock Contentions |       Gen0 |       Gen1 |      Gen2 | Allocated | Alloc Ratio |
|------------------------------------------------ |--------:|--------:|--------:|------:|---------------------:|-----------------:|-----------:|-----------:|----------:|----------:|------------:|
|       AvoidingTaskRun_SharingSameDelay_SameTask | 10.03 s | 0.074 s | 0.004 s |  0.83 |               1.0000 |                - |  1000.0000 |  1000.0000 | 1000.0000 |  39.63 MB |        0.09 |
| AvoidingTaskRun_SharingSameDelay_DifferentTasks | 10.73 s | 1.583 s | 0.087 s |  0.89 |         1000001.0000 |                - | 15000.0000 |  8000.0000 | 2000.0000 | 146.45 MB |        0.34 |
|                                        Baseline | 12.12 s | 0.284 s | 0.016 s |  1.00 |         2000034.0000 |       14365.0000 | 55000.0000 | 29000.0000 | 4000.0000 | 428.38 MB |        1.00 |