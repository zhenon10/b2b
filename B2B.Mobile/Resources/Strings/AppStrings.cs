using System.Globalization;
using System.Resources;

namespace B2B.Mobile.Resources.Strings;

/// <summary><c>AppStrings.resx</c> yerelleştirilmiş metinler (varsayılan Türkçe).</summary>
public static class AppStrings
{
    private const string ResourceBaseName = "B2B.Mobile.Resources.Strings.AppStrings";

    private static readonly Lazy<ResourceManager> Manager = new(() =>
        new ResourceManager(ResourceBaseName, typeof(AppStrings).Assembly));

    public static string Get(string key) =>
        Manager.Value.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Common_CopySupportCode => Get(nameof(Common_CopySupportCode));
    public static string Common_CopySupportCodeHint => Get(nameof(Common_CopySupportCodeHint));
    public static string Connectivity_OfflineMessage => Get(nameof(Connectivity_OfflineMessage));
    public static string Connectivity_ConstrainedMessage => Get(nameof(Connectivity_ConstrainedMessage));
    public static string Login_PageTitle => Get(nameof(Login_PageTitle));
    public static string Login_WelcomeTitle => Get(nameof(Login_WelcomeTitle));
    public static string Login_WelcomeSubtitle => Get(nameof(Login_WelcomeSubtitle));
    public static string Login_EmailPlaceholder => Get(nameof(Login_EmailPlaceholder));
    public static string Login_EmailHint => Get(nameof(Login_EmailHint));
    public static string Login_PasswordPlaceholder => Get(nameof(Login_PasswordPlaceholder));
    public static string Login_PasswordHint => Get(nameof(Login_PasswordHint));
    public static string Login_RememberMeLabel => Get(nameof(Login_RememberMeLabel));
    public static string Login_RememberMeHint => Get(nameof(Login_RememberMeHint));
    public static string Login_Submit => Get(nameof(Login_Submit));
    public static string Login_SubmitHint => Get(nameof(Login_SubmitHint));
    public static string Login_Register => Get(nameof(Login_Register));
    public static string Login_RegisterHint => Get(nameof(Login_RegisterHint));
    public static string Register_PageTitle => Get(nameof(Register_PageTitle));
    public static string Register_Headline => Get(nameof(Register_Headline));
    public static string Register_Subtitle => Get(nameof(Register_Subtitle));
    public static string Register_DisplayNamePlaceholder => Get(nameof(Register_DisplayNamePlaceholder));
    public static string Register_EmailPlaceholder => Get(nameof(Register_EmailPlaceholder));
    public static string Register_PasswordPlaceholder => Get(nameof(Register_PasswordPlaceholder));
    public static string Register_PasswordRules => Get(nameof(Register_PasswordRules));
    public static string Register_Submit => Get(nameof(Register_Submit));
    public static string Register_SubmitHint => Get(nameof(Register_SubmitHint));
    public static string Register_GoLogin => Get(nameof(Register_GoLogin));
    public static string Register_GoLoginHint => Get(nameof(Register_GoLoginHint));
    public static string Cart_PageTitle => Get(nameof(Cart_PageTitle));
    public static string Cart_EmptyTitle => Get(nameof(Cart_EmptyTitle));
    public static string Cart_EmptySubtitle => Get(nameof(Cart_EmptySubtitle));
    public static string Cart_RemoveLine => Get(nameof(Cart_RemoveLine));
    public static string Cart_RemoveLineHint => Get(nameof(Cart_RemoveLineHint));
    public static string Cart_TotalCaption => Get(nameof(Cart_TotalCaption));
    public static string Cart_Clear => Get(nameof(Cart_Clear));
    public static string Cart_ClearHint => Get(nameof(Cart_ClearHint));
    public static string Cart_Checkout => Get(nameof(Cart_Checkout));
    public static string Cart_CheckoutHint => Get(nameof(Cart_CheckoutHint));
    public static string Product_SearchPlaceholder => Get(nameof(Product_SearchPlaceholder));
    public static string Product_SearchHint => Get(nameof(Product_SearchHint));
    public static string Products_ScanHint => Get(nameof(Products_ScanHint));
    public static string Profile_PageTitle => Get(nameof(Profile_PageTitle));
    public static string Profile_AccountSection => Get(nameof(Profile_AccountSection));
    public static string Profile_EmailCaption => Get(nameof(Profile_EmailCaption));
    public static string Profile_DisplayNameCaption => Get(nameof(Profile_DisplayNameCaption));
    public static string Profile_RolesCaption => Get(nameof(Profile_RolesCaption));
    public static string Profile_StatusCaption => Get(nameof(Profile_StatusCaption));
    public static string Profile_ResumeLockSection => Get(nameof(Profile_ResumeLockSection));
    public static string Profile_ResumeLockHint => Get(nameof(Profile_ResumeLockHint));
    public static string Profile_ResumeLockSwitch => Get(nameof(Profile_ResumeLockSwitch));
    public static string Profile_PinPlaceholder => Get(nameof(Profile_PinPlaceholder));
    public static string Profile_PinHint => Get(nameof(Profile_PinHint));
    public static string Profile_SaveLockSettings => Get(nameof(Profile_SaveLockSettings));
    public static string Profile_SaveLockSettingsHint => Get(nameof(Profile_SaveLockSettingsHint));
    public static string Profile_ChangePasswordSection => Get(nameof(Profile_ChangePasswordSection));
    public static string Profile_CurrentPasswordPlaceholder => Get(nameof(Profile_CurrentPasswordPlaceholder));
    public static string Profile_NewPasswordPlaceholder => Get(nameof(Profile_NewPasswordPlaceholder));
    public static string Profile_ConfirmPasswordPlaceholder => Get(nameof(Profile_ConfirmPasswordPlaceholder));
    public static string Profile_UpdatePassword => Get(nameof(Profile_UpdatePassword));
    public static string Profile_UpdatePasswordHint => Get(nameof(Profile_UpdatePasswordHint));
    public static string Profile_OpenSettings => Get(nameof(Profile_OpenSettings));
    public static string Profile_OpenSettingsHint => Get(nameof(Profile_OpenSettingsHint));
    public static string Profile_Logout => Get(nameof(Profile_Logout));
    public static string Profile_LogoutHint => Get(nameof(Profile_LogoutHint));
    public static string ProductDetail_PageTitle => Get(nameof(ProductDetail_PageTitle));
    public static string ProductDetail_QuantityLabel => Get(nameof(ProductDetail_QuantityLabel));
    public static string ProductDetail_DecreaseQtyHint => Get(nameof(ProductDetail_DecreaseQtyHint));
    public static string ProductDetail_IncreaseQtyHint => Get(nameof(ProductDetail_IncreaseQtyHint));
    public static string ProductDetail_QuantityEntryHint => Get(nameof(ProductDetail_QuantityEntryHint));
    public static string ProductDetail_AddToCart => Get(nameof(ProductDetail_AddToCart));
    public static string ProductDetail_AddToCartHint => Get(nameof(ProductDetail_AddToCartHint));
    public static string ProductDetail_ClosePreviewHint => Get(nameof(ProductDetail_ClosePreviewHint));
    public static string ProductDetail_ImageCarouselHint => Get(nameof(ProductDetail_ImageCarouselHint));
}
