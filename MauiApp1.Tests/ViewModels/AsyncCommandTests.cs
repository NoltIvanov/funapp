using MauiApp1.ViewModels;

namespace MauiApp1.Tests.ViewModels;

public class AsyncCommandTests
{
    [Fact]
    public async Task ExecuteAsync_DisablesCommandWhileRunningAndRaisesCanExecuteChanged()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canExecuteChangedCount = 0;
        var command = new AsyncCommand(async () =>
        {
            started.SetResult();
            await release.Task;
        });
        command.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

        var execution = command.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(command.CanExecute(null));

        release.SetResult();
        await execution.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(command.CanExecute(null));
        Assert.Equal(2, canExecuteChangedCount);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRunWhenCanExecuteIsFalse()
    {
        var executeCount = 0;
        var command = new AsyncCommand(
            () =>
            {
                executeCount++;
                return Task.CompletedTask;
            },
            () => false);

        await command.ExecuteAsync();

        Assert.Equal(0, executeCount);
    }
}
