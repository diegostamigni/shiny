using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Shiny.Infrastructure;
using Shiny.Jobs;
using Shiny.Settings;
using TaskStackBuilder = Android.App.TaskStackBuilder;


namespace Shiny.Notifications
{
    public class NotificationManagerImpl : INotificationManager
    {
        readonly AndroidContext context;
        readonly IRepository repository;
        readonly ISettings settings;
        readonly IJobManager jobs;

        NotificationManager newManager;
        NotificationManagerCompat compatManager;


        public NotificationManagerImpl(AndroidContext context,
                                       IJobManager jobs,
                                       IRepository repository,
                                       ISettings settings)
        {
            this.context = context;
            this.jobs = jobs;
            this.repository = repository;
            this.settings = settings;

            if ((int) Build.VERSION.SdkInt >= 26)
            {
                this.newManager = NotificationManager.FromContext(context.AppContext);
            }
            else
            {
                this.compatManager = NotificationManagerCompat.From(context.AppContext);
            }
        }


        public Task Cancel(int id)
            => this.repository.Remove<Notification>(id.ToString());


        public async Task Clear()
        {
            this.newManager?.CancelAll();
            this.compatManager?.CancelAll();
            await this.repository.Clear<Notification>();
        }


        public async Task<IEnumerable<Notification>> GetPending()
            => await this.repository.GetAll<Notification>();


        public async Task<AccessState> RequestAccess()
        {
            var state = AccessState.Available;
            if (!this.compatManager?.AreNotificationsEnabled() ?? false)
                state = AccessState.Disabled;

            else if (!this.newManager?.AreNotificationsEnabled() ?? false)
                state = AccessState.Disabled;

            else
                state = await this.jobs.RequestAccess();

            return state;
        }


        //https://stackoverflow.com/questions/45462666/notificationcompat-builder-deprecated-in-android-o
        public async Task Send(Notification notification)
        {
            if (notification.Id == 0)
                notification.Id = this.settings.IncrementValue("NotificationId");

            if (notification.ScheduleDate != null)
            {
                await this.repository.Set(notification.Id.ToString(), notification);
                return;
            }

            var launchIntent = this
                .context
                .AppContext
                .PackageManager
                .GetLaunchIntentForPackage(this.context.Package.PackageName)
                .SetFlags(notification.Android.LaunchActivityFlags.ToNative());

            if (!notification.Payload.IsEmpty())
                launchIntent.PutExtra("Payload", notification.Payload);

            var pendingIntent = TaskStackBuilder
                .Create(this.context.AppContext)
                .AddNextIntent(launchIntent)
                .GetPendingIntent(notification.Id, PendingIntentFlags.OneShot);
                //.GetPendingIntent(notification.Id, PendingIntentFlags.OneShot | PendingIntentFlags.CancelCurrent);

            var smallIconResourceId = this.context.GetResourceIdByName(notification.Android.SmallIconResourceName);

            var builder = new NotificationCompat.Builder(this.context.AppContext)
                .SetContentTitle(notification.Title)
                .SetContentText(notification.Message)
                .SetSmallIcon(smallIconResourceId)
                .SetContentIntent(pendingIntent);

            // TODO
            //if ((int)Build.VERSION.SdkInt >= 21 && notification.Android.Color != null)
            //    builder.SetColor(notification.Android.Color.Value)

            builder.SetAutoCancel(notification.Android.AutoCancel);
            builder.SetOngoing(notification.Android.OnGoing);

            if (notification.Android.Priority != null)
                builder.SetPriority(notification.Android.Priority.Value);

            if (notification.Android.Vibrate)
                builder.SetVibrate(new long[] {500, 500});

            if (notification.Sound != null)
            {
                if (!notification.Sound.Contains("://"))
                    notification.Sound =
                        $"{ContentResolver.SchemeAndroidResource}://{this.context.Package.PackageName}/raw/{notification.Sound}";

                var uri = Android.Net.Uri.Parse(notification.Sound);
                builder.SetSound(uri);
            }

            if (this.newManager != null)
            {
                var channelId = notification.Android.ChannelId;

                if (this.newManager.GetNotificationChannel(channelId) == null)
                {
                    var channel = new NotificationChannel(
                        channelId,
                        notification.Android.Channel,
                        notification.Android.NotificationImportance.ToNative()
                    );
                    var d = notification.Android.ChannelDescription;
                    if (!d.IsEmpty())
                        channel.Description = d;

                    this.newManager.CreateNotificationChannel(channel);
                }

                builder.SetChannelId(channelId);
                this.newManager.Notify(notification.Id, builder.Build());
            }
            else
            {
                this.compatManager.Notify(notification.Id, builder.Build());
            }
        }
    }
}
