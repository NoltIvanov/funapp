using Android.Graphics.Drawables;
using AndroidX.AppCompat.Widget;

namespace MauiApp1.Behaviors;

partial class EntryBorderBehavior : PlatformBehavior<Entry, AppCompatEditText>
{
    protected override void OnAttachedTo(Entry bindable, AppCompatEditText platformView)
    {
        platformView.Background = null;
        platformView.SetPadding(0, 0, 0, 0);

        var gradientDrawable = new Android.Graphics.Drawables.GradientDrawable();
        gradientDrawable.SetColor(Android.Graphics.Color.LightGray);  
        gradientDrawable.SetCornerRadius(16f); 
        gradientDrawable.SetStroke(2, Android.Graphics.Color.DarkGray);

        platformView.Background = gradientDrawable;
    }
}

