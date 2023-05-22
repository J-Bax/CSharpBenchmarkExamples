using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Benchmarks.Config;

namespace Benchmarks
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [Config(typeof(ShortRunConfig))]
    public class TaskRunOrNot
    {
        private const int delayMillisec = 10000;

        private readonly TimeSpan delayMillisecTS = TimeSpan.FromMilliseconds(delayMillisec);

        private const int N = 1000000;

        private Task[] Tasks = new Task[N];
        public TaskRunOrNot() {}

        [Benchmark]
        public async Task<long> AvoidingTaskRun_SharingSameDelay_SameTask()
        {
            long start = Stopwatch.GetTimestamp();

            Task delayTask = Task.Delay(TimeSpan.FromMilliseconds(delayMillisec));

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < N; i++)
            {
                tasks.Add(delayTask);
            }
            await Task.WhenAll(tasks);


            return Stopwatch.GetTimestamp() - start;
        }

        [Benchmark]
        public async Task<long> AvoidingTaskRun_SharingSameDelay_DifferentTasks()
        {
            long start = Stopwatch.GetTimestamp();

            Task delayTask = Task.Delay(TimeSpan.FromMilliseconds(delayMillisec));

            List <Task> tasks = new List<Task>();
            for (int i = 0; i < N; i++)
            {
                tasks.Add(delayTask.ContinueWith((Task _) => { }));
            }
            await Task.WhenAll(tasks);


            return Stopwatch.GetTimestamp() - start;
        }

        [Benchmark(Baseline = true)]
        public async Task<long> Baseline()
        {
            long start = Stopwatch.GetTimestamp();

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < N; i++)
            {
                Task task = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMillisec));
                });
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);

            return Stopwatch.GetTimestamp() - start;
        }

        [Benchmark]
        public async Task<long> AvoidingTaskRun()
        {
            long start = Stopwatch.GetTimestamp();

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < N; i++)
            {
                tasks.Add(Task.Delay(TimeSpan.FromMilliseconds(delayMillisec)));
            }
            await Task.WhenAll(tasks);


            return Stopwatch.GetTimestamp() - start;
        }

        [Benchmark]
        public async Task<long> SetInitialCapacity_List()
        {
            long start = Stopwatch.GetTimestamp();

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


            return Stopwatch.GetTimestamp() - start;
        }

        const string ListEntry = "Hello World";

        [Benchmark]
        public long SetInitialCapacity_List_String()
        {
            List<string> items = new List<string>(N);
            for (int i = 0; i < N; i++)
            {
                items.Add(ListEntry);
            }
            return items.Count;
        }

        [Benchmark(Baseline = true)]
        public long Baseline_String()
        {
            List<string> items = new List<string>();
            for (int i = 0; i < N; i++)
            {
                items.Add(ListEntry);
            }
            return items.Count;
        }
    }

    public class Program
    {
        public static void Main(string[] _)
        {
            BenchmarkRunner.Run<TaskRunOrNot>();
        }
    }
}