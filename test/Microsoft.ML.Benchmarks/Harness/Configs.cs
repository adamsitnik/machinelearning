using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Microsoft.ML.Benchmarks.Harness;

namespace Microsoft.ML.Benchmarks
{
    public class RecommendedConfig : ManualConfig
    {
        public RecommendedConfig()
        {
            Add(DefaultConfig.Instance); // this config contains all of the basic settings (exporters, columns etc)

            Add(GetJobDefinition()
                .WithCustomBuildConfiguration("Release")
                .With(CreateToolchain("netcoreapp2.1", null))
                .WithId("Core 2.1")
                .AsBaseline());

            Add(GetJobDefinition()
                .WithCustomBuildConfiguration("Release-Intrinsics")
                .With(new EnvironmentVariable[] { new EnvironmentVariable("COMPlus_TieredCompilation", "0") })
                .With(CreateToolchain("netcoreapp3.0", @"C:\Projects\machinelearning\Tools\dotnetcli\dotnet.exe"))
                .WithId("Core 3.0 NonTiered"));


            //Add(GetJobDefinition()
            //    .WithCustomBuildConfiguration("Release-Intrinsics")
            //    .With(new EnvironmentVariable[] { new EnvironmentVariable("COMPlus_TieredCompilation", "1") })
            //    .With(CreateToolchain("netcoreapp3.0", @"C:\Projects\machinelearning\Tools\dotnetcli\dotnet.exe"))
            //    .WithId("Core 3.0 Tiered"));

            Add(new ExtraMetricColumn()); // an extra colum that can display additional metric reported by the benchmarks

            UnionRule = ConfigUnionRule.AlwaysUseLocal; // global config can be overwritten with local (the one set via [ConfigAttribute])

            Add(new BenchmarkDotNet.Diagnostics.Windows.EtwProfiler(new BenchmarkDotNet.Diagnostics.Windows.EtwProfilerConfig(false, cpuSampleIntervalInMiliseconds: 1.0f)));
        }

        protected virtual Job GetJobDefinition()
            => Job.Default
                .WithWarmupCount(1) // ML.NET benchmarks are typically CPU-heavy benchmarks, 1 warmup is usually enough
                .WithMaxIterationCount(20);

        /// <summary>
        /// we need our own toolchain because MSBuild by default does not copy recursive native dependencies to the output
        /// </summary>
        private IToolchain CreateToolchain(string tfm, string cliPath)
        {
            var csProj = CsProjCoreToolchain.From(new NetCoreAppSettings(targetFrameworkMoniker: tfm, runtimeFrameworkVersion: null, name: tfm, customDotNetCliPath: cliPath));

            return new Toolchain(
                tfm,
                new ProjectGenerator(tfm), // custom generator that copies native dependencies
                csProj.Builder,
                csProj.Executor);
        }

        private static string GetTargetFrameworkMoniker()
        {
#if NETCOREAPP3_0 // todo: remove the #IF DEFINES when BDN 0.11.2 gets released (BDN gains the 3.0 support)
            return "netcoreapp3.0";
#else
            return NetCoreAppSettings.Current.Value.TargetFrameworkMoniker;
#endif
        }

        private static string GetBuildConfigurationName()
        {
#if NETCOREAPP3_0
            return "Release-Intrinsics";
#else
            return "Release";
#endif
        }
    }

    public class TrainConfig : RecommendedConfig
    {
        protected override Job GetJobDefinition()
            => Job.Dry // the "Dry" job runs the benchmark exactly once, without any warmup to mimic real-world scenario
                  .WithLaunchCount(3); // BDN will run 3 dedicated processes, sequentially
    }
}
