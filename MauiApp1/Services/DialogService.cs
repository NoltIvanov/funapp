namespace MauiApp1.Services;

public class DialogService : IDialogService
{
    public async Task ShowAlertAsync(string title, string message, string cancel)
    {
        await Shell.Current.DisplayAlertAsync(title, message, cancel);
    }
}
