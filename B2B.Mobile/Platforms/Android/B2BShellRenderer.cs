using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using AColor = Android.Graphics.Color;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace B2B.Mobile.Platforms.Android;

public sealed class B2BShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
        new TabBarTopLineAppearanceTracker(this, shellItem);
}

internal sealed class TabBarTopLineAppearanceTracker : ShellBottomNavViewAppearanceTracker
{
    public TabBarTopLineAppearanceTracker(IShellContext shellContext, ShellItem shellItem)
        : base(shellContext, shellItem)
    {
    }

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.M)
            return;

        var existing = bottomView.Background;
        if (existing is null || bottomView.Context?.Resources?.DisplayMetrics is null)
            return;

        var stroke = Math.Max(1, (int)Math.Ceiling(bottomView.Context.Resources.DisplayMetrics.Density));
        var line = new ColorDrawable(AColor.ParseColor("#E8EAEF"));
        var layers = new LayerDrawable(new Drawable[] { existing, line });
        layers.SetLayerGravity(1, GravityFlags.Top | GravityFlags.FillHorizontal);
        layers.SetLayerHeight(1, stroke);
        bottomView.Background = layers;
    }
}
