namespace MauiApp1.Services;

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel);
}
