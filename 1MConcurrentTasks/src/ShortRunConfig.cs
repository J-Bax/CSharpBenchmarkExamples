using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace Benchmarks.Config
{
    public class ShortRunConfig : ManualConfig
    {
        public ShortRunConfig()
        {
            AddJob(Job.ShortRun);
            //AddJob(Job.ShortRun.WithStrategy(RunStrategy.ColdStart));
        }
    }
}
