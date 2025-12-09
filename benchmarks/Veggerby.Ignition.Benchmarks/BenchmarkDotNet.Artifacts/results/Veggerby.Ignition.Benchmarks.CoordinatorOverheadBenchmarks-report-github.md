```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2


```
| Method       | Job      | Runtime   | IterationCount | LaunchCount | WarmupCount | SignalCount | Mean     | Error    | StdDev   | Allocated |
|------------- |--------- |---------- |--------------- |------------ |------------ |------------ |---------:|---------:|---------:|----------:|
| **WaitAllAsync** | **.NET 9.0** | **.NET 9.0**  | **Default**        | **Default**     | **Default**     | **1**           |       **NA** |       **NA** |       **NA** |        **NA** |
| WaitAllAsync | ShortRun | .NET 10.0 | 3              | 1           | 3           | 1           | 20.03 ns | 3.195 ns | 0.175 ns |         - |
| **WaitAllAsync** | **.NET 9.0** | **.NET 9.0**  | **Default**        | **Default**     | **Default**     | **10**          |       **NA** |       **NA** |       **NA** |        **NA** |
| WaitAllAsync | ShortRun | .NET 10.0 | 3              | 1           | 3           | 10          | 20.53 ns | 1.594 ns | 0.087 ns |         - |
| **WaitAllAsync** | **.NET 9.0** | **.NET 9.0**  | **Default**        | **Default**     | **Default**     | **100**         |       **NA** |       **NA** |       **NA** |        **NA** |
| WaitAllAsync | ShortRun | .NET 10.0 | 3              | 1           | 3           | 100         | 20.06 ns | 1.454 ns | 0.080 ns |         - |
| **WaitAllAsync** | **.NET 9.0** | **.NET 9.0**  | **Default**        | **Default**     | **Default**     | **1000**        |       **NA** |       **NA** |       **NA** |        **NA** |
| WaitAllAsync | ShortRun | .NET 10.0 | 3              | 1           | 3           | 1000        | 20.47 ns | 8.661 ns | 0.475 ns |         - |

Benchmarks with issues:
  CoordinatorOverheadBenchmarks.WaitAllAsync: .NET 9.0(Runtime=.NET 9.0) [SignalCount=1]
  CoordinatorOverheadBenchmarks.WaitAllAsync: .NET 9.0(Runtime=.NET 9.0) [SignalCount=10]
  CoordinatorOverheadBenchmarks.WaitAllAsync: .NET 9.0(Runtime=.NET 9.0) [SignalCount=100]
  CoordinatorOverheadBenchmarks.WaitAllAsync: .NET 9.0(Runtime=.NET 9.0) [SignalCount=1000]
