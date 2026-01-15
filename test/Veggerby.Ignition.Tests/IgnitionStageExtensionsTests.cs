using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Veggerby.Ignition.Stages;
using Xunit;

namespace Veggerby.Ignition.Tests;

public class IgnitionStageExtensionsTests
{
    [Fact]
    public void AddStagedIgnition_NullConfigureStages_ThrowsArgumentNullException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddStagedIgnition(null!));
    }

    [Fact]
    public void AddStagedIgnition_ConfiguresExecutionModeToStaged()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddStagedIgnition(stages =>
        {
            stages.AddParallelStage("Stage 0");
        });

        // assert
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Staged);
    }

    [Fact]
    public void AddStagedIgnition_RegistersStagesInDI()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddStagedIgnition(stages =>
        {
            stages.AddParallelStage("Stage 0");
            stages.AddSequentialStage("Stage 1");
        });

        // assert
        var sp = services.BuildServiceProvider();
        var registeredStages = sp.GetRequiredService<IReadOnlyList<IgnitionStage>>();
        registeredStages.Should().HaveCount(2);
        registeredStages[0].ExecutionMode.Should().Be(IgnitionExecutionMode.Parallel);
        registeredStages[1].ExecutionMode.Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void AddIgnitionStage_NullConfigureStage_ThrowsArgumentNullException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddIgnitionStage(0, null!));
    }

    [Fact]
    public void AddIgnitionStage_NegativeStageNumber_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddIgnitionStage(-1, stage => { }));
    }

    [Fact]
    public void AddIgnitionStage_ConfiguresExecutionModeToStaged()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnitionStage(0, stage =>
        {
            stage.AddTaskSignal("test", ct => Task.CompletedTask);
        });

        // assert
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Staged);
    }

    [Fact]
    public void AddIgnitionStage_RegistersStageConfiguration()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnitionStage(0, stage =>
        {
            stage.AddTaskSignal("test", ct => Task.CompletedTask);
        }, IgnitionExecutionMode.Sequential);

        // assert
        var sp = services.BuildServiceProvider();
        var stageConfig = sp.GetRequiredService<IOptions<IgnitionStageConfiguration>>().Value;
        stageConfig.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void AddIgnitionStage_DefaultExecutionMode_UsesParallel()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnitionStage(0, stage =>
        {
            stage.AddTaskSignal("test", ct => Task.CompletedTask);
        });

        // assert
        var sp = services.BuildServiceProvider();
        var stageConfig = sp.GetRequiredService<IOptions<IgnitionStageConfiguration>>().Value;
        stageConfig.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Parallel);
    }

    [Fact]
    public void AddSignalToStage_NullOrEmptyName_ThrowsArgumentException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => 
            services.AddSignalToStage(0, null!, _ => Substitute.For<IIgnitionSignal>()));
        
        Assert.Throws<ArgumentException>(() => 
            services.AddSignalToStage(0, string.Empty, _ => Substitute.For<IIgnitionSignal>()));
        
        Assert.Throws<ArgumentException>(() => 
            services.AddSignalToStage(0, "  ", _ => Substitute.For<IIgnitionSignal>()));
    }

    [Fact]
    public void AddSignalToStage_NullSignalFactory_ThrowsArgumentNullException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => 
            services.AddSignalToStage(0, "test", (Func<IServiceProvider, IIgnitionSignal>)null!));
    }

    [Fact]
    public void AddSignalToStage_RegistersStagedFactory()
    {
        // arrange
        var services = new ServiceCollection();
        var signal = Substitute.For<IIgnitionSignal>();
        signal.Name.Returns("test-signal");

        // act
        services.AddSignalToStage(0, "test-signal", _ => signal);

        // assert
        var sp = services.BuildServiceProvider();
        var factories = sp.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Count.Should().BeGreaterThan(0);
        
        var stagedFactory = factories.OfType<StagedIgnitionSignalFactory>().FirstOrDefault();
        stagedFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddSignalToStage_ConfiguresStageExecutionMode()
    {
        // arrange
        var services = new ServiceCollection();
        var signal = Substitute.For<IIgnitionSignal>();
        signal.Name.Returns("test-signal");

        // act
        services.AddSignalToStage(2, "test-signal", _ => signal, IgnitionExecutionMode.DependencyAware, TimeSpan.FromSeconds(5));

        // assert
        var sp = services.BuildServiceProvider();
        var stageConfig = sp.GetRequiredService<IOptions<IgnitionStageConfiguration>>().Value;
        stageConfig.GetExecutionMode(2).Should().Be(IgnitionExecutionMode.DependencyAware);
    }

    [Fact]
    public void AddTaskToStage_NullOrEmptyName_ThrowsArgumentException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => 
            services.AddTaskToStage(0, null!, (sp, ct) => Task.CompletedTask));
        
        Assert.Throws<ArgumentException>(() => 
            services.AddTaskToStage(0, string.Empty, (sp, ct) => Task.CompletedTask));
    }

    [Fact]
    public void AddTaskToStage_NullTaskFactory_ThrowsArgumentNullException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => 
            services.AddTaskToStage(0, "test", (Func<IServiceProvider, CancellationToken, Task>)null!));
    }

    [Fact]
    public void AddTaskToStage_RegistersStagedFactory()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddTaskToStage(1, "test-task", (sp, ct) => Task.CompletedTask, IgnitionExecutionMode.Sequential, TimeSpan.FromSeconds(10));

        // assert
        var sp = services.BuildServiceProvider();
        var factories = sp.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Count.Should().BeGreaterThan(0);
        
        var stagedFactory = factories.OfType<StagedIgnitionSignalFactory>().FirstOrDefault();
        stagedFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddTaskToStage_ConfiguresStageExecutionMode()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddTaskToStage(3, "test-task", (sp, ct) => Task.CompletedTask, IgnitionExecutionMode.Sequential);

        // assert
        var sp = services.BuildServiceProvider();
        var stageConfig = sp.GetRequiredService<IOptions<IgnitionStageConfiguration>>().Value;
        stageConfig.GetExecutionMode(3).Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void AddTaskToStage_WithTimeout_PassesThroughTimeout()
    {
        // arrange
        var services = new ServiceCollection();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        services.AddTaskToStage(0, "test-task", (sp, ct) => Task.CompletedTask, timeout: timeout);

        // assert - factory should be registered
        var sp = services.BuildServiceProvider();
        var factories = sp.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void IgnitionStageConfiguration_EnsureStage_NewStage_AddsStage()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        config.EnsureStage(0, IgnitionExecutionMode.Parallel);
        config.EnsureStage(1, IgnitionExecutionMode.Sequential);

        // assert
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Parallel);
        config.GetExecutionMode(1).Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void IgnitionStageConfiguration_EnsureStage_ExistingStage_PreservesOriginalMode()
    {
        // arrange
        var config = new IgnitionStageConfiguration();
        config.EnsureStage(0, IgnitionExecutionMode.Parallel);

        // act - try to change to Sequential
        config.EnsureStage(0, IgnitionExecutionMode.Sequential);

        // assert - original mode preserved
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Parallel);
    }

    [Fact]
    public void IgnitionStageConfiguration_GetExecutionMode_UndefinedStage_ReturnsNull()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        var mode = config.GetExecutionMode(999);

        // assert
        mode.Should().BeNull();
    }

    [Fact]
    public void AddIgnitionStage_MultipleStages_EachHasOwnExecutionMode()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnitionStage(0, stage => 
            stage.AddTaskSignal("infra", ct => Task.CompletedTask), 
            IgnitionExecutionMode.Sequential);

        services.AddIgnitionStage(1, stage => 
            stage.AddTaskSignal("services", ct => Task.CompletedTask), 
            IgnitionExecutionMode.Parallel);

        services.AddIgnitionStage(2, stage => 
            stage.AddTaskSignal("workers", ct => Task.CompletedTask), 
            IgnitionExecutionMode.DependencyAware);

        // assert
        var sp = services.BuildServiceProvider();
        var stageConfig = sp.GetRequiredService<IOptions<IgnitionStageConfiguration>>().Value;
        
        stageConfig.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Sequential);
        stageConfig.GetExecutionMode(1).Should().Be(IgnitionExecutionMode.Parallel);
        stageConfig.GetExecutionMode(2).Should().Be(IgnitionExecutionMode.DependencyAware);
    }

    [Fact]
    public void AddIgnitionStage_FluentBuilderPattern_AllowsChaining()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        var result = services
            .AddIgnitionStage(0, stage => stage.AddTaskSignal("s1", ct => Task.CompletedTask))
            .AddIgnitionStage(1, stage => stage.AddTaskSignal("s2", ct => Task.CompletedTask));

        // assert
        result.Should().BeSameAs(services);
    }
}
