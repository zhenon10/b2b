using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using B2B.Mobile.Core.Push;
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.Core.Platforms.Android;
using Android.Util;

namespace B2B.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    actions: new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "b2b",
    DataHost = "product")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CrossFirebase.Initialize(this, () => Platform.CurrentActivity);
        CreateNotificationChannel();
        RequestPostNotificationsPermissionIfNeeded();
        // After Firebase init, try registering the token with the API.
        Log.Info("B2B.Push", "Starting push token sync...");
        App.Services.GetService<PushTokenSyncService>()?.Start();
        FirebaseCloudMessagingImplementation.OnNewIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is not null)
            FirebaseCloudMessagingImplementation.OnNewIntent(intent);
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channelId = Resources?.GetString(Resource.String.b2b_fcm_default_channel_id)
                        ?? $"{PackageName}.general";
        var channelName = Resources?.GetString(Resource.String.b2b_fcm_default_channel_name) ?? "Genel";

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        if (notificationManager is null) return;

        var channel = new NotificationChannel(channelId, channelName, NotificationImportance.Default);
        notificationManager.CreateNotificationChannel(channel);
        FirebaseCloudMessagingImplementation.ChannelId = channelId;
    }

    private void RequestPostNotificationsPermissionIfNeeded()
    {
        // Android 13+ requires runtime notification permission.
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return;

        const string perm = Android.Manifest.Permission.PostNotifications;
        if (ActivityCompat.CheckSelfPermission(this, perm) == Permission.Granted)
            return;

        ActivityCompat.RequestPermissions(this, new[] { perm }, 1001);
    }
}
