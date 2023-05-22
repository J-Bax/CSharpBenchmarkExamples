using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace Benchmarks.Config
{
    public class ShortRunConfig : ManualConfig
    {
        public ShortRunConfig()
        {
            AddJob(Job.ShortRun.WithRuntime(CoreRuntime.Core60));
            AddJob(Job.ShortRun.WithRuntime(CoreRuntime.Core70));
            AddJob(Job.ShortRun.WithRuntime(CoreRuntime.Core80));
            //AddJob(Job.ShortRun.WithStrategy(RunStrategy.ColdStart));
        }
    }
}
