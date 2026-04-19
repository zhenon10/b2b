namespace B2B.Mobile.Core.Shell;

public partial class MainFlyoutView : ContentView
{
    public MainFlyoutView()
    {
        InitializeComponent();
    }

    /// <summary>Kategori paneline dışarıdan <c>CategoriesFlyoutViewModel</c> atanır.</summary>
    public void SetCategoriesBindingContext(object bindingContext) =>
        CategoriesPanel.BindingContext = bindingContext;
}
