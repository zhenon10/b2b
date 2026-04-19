using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;

namespace B2B.Mobile.Platforms.MacCatalyst;

public sealed class B2BShellRenderer : ShellRenderer
{
    protected override IShellTabBarAppearanceTracker CreateTabBarAppearanceTracker() =>
        new TabBarTopLineAppearanceTracker();
}

internal sealed class TabBarTopLineAppearanceTracker : ShellTabBarAppearanceTracker
{
    UIView? _topLine;

    public override void SetAppearance(UITabBarController controller, ShellAppearance appearance)
    {
        base.SetAppearance(controller, appearance);
        _topLine?.RemoveFromSuperview();
        _topLine = new UIView
        {
            BackgroundColor = UIColor.FromRGB(0xE8, 0xEA, 0xEF),
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        controller.TabBar.AddSubview(_topLine);
        var h = 1f / (float)UIScreen.MainScreen.Scale;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _topLine.HeightAnchor.ConstraintEqualTo(h),
            _topLine.LeadingAnchor.ConstraintEqualTo(controller.TabBar.LeadingAnchor),
            _topLine.TrailingAnchor.ConstraintEqualTo(controller.TabBar.TrailingAnchor),
            _topLine.TopAnchor.ConstraintEqualTo(controller.TabBar.TopAnchor)
        });
    }
}
