﻿using Acr.UserDialogs;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media.Session;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Net;
using LibVLCSharp.Forms.Shared;
using Plugin.LocalNotifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xamarin.Forms;
using static CloudStreamForms.App;
using static CloudStreamForms.CloudStreamCore;
using static CloudStreamForms.Droid.MainActivity;
using Application = Android.App.Application;
using static CloudStreamForms.Droid.LocalNot;
using static CloudStreamForms.Droid.MainHelper;
using System.Threading;
using Android.Webkit;
using static Android.App.ActivityManager;


namespace CloudStreamForms.Droid
{
    [Service]

    public class OnKilledService : Service
    { 

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }
        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            MainActivity.activity.Killed();
            StopSelf();
           // base.OnTaskRemoved(rootIntent);
        }
    }

    [Service]
    public class DemoIntentService : IntentService
    {
        public DemoIntentService() : base("DemoIntentService")
        {
        }

        protected override void OnHandleIntent(Android.Content.Intent intent)
        {
            print("perform some long running work");
            System.Console.WriteLine("work complete");

            print("HANDLE" + intent.Extras.GetString("data"));
        }
    }

    [Service]
    public class MainIntentService : IntentService
    {
        public MainIntentService() : base("MainIntentService")
        {

        }

        protected override void OnHandleIntent(Android.Content.Intent intent)
        {
            string data = intent.Extras.GetString("data");

            if (data.StartsWith("handleDownload")) {
                int id = int.Parse(FindHTML(data, $"{nameof(id)}=", "|||")); //intent.Extras.GetInt("downloadId");
                int dType = int.Parse(FindHTML(data, $"{nameof(dType)}=", "|||"));
                var manager = Application.Context.GetSystemService(Context.NotificationService) as NotificationManager;
                manager.Cancel(id);
                DownloadHandle.isPaused[id] = dType;
                DownloadHandle.changedPause?.Invoke(null, id);
            }
        }
    }

    [Service]
    public class NullIntent : IntentService
    {
        public override void OnCreate()
        {
            base.OnCreate();

        }

        protected override void OnHandleIntent(Intent intent)
        {

        }
    }

    [Service]
    public class ChromeCastIntentService : IntentService
    {
        public ChromeCastIntentService() : base("ChromeCastIntentService")
        {

        }

        protected override void OnHandleIntent(Android.Content.Intent intent)
        {
            string data = intent.Extras.GetString("data");
            //play" : "pause", "goforward", "stop
            print("HANDLE [" + data + "]");

            switch (data) {
                case "play":
                    MainChrome.PauseAndPlay(false);
                    break;
                case "pause":
                    MainChrome.PauseAndPlay(true);
                    break;
                case "goforward":
                    MainChrome.SeekMedia(30);
                    break;
                case "goback":
                    MainChrome.SeekMedia(-30);
                    break;
                case "stop":
                    MainChrome.StopCast();
                    break;
                default:
                    break;
            }

        }
    }

    [System.Serializable]
    public class LocalAction
    {
        public string action;
        public string name;
        public int sprite;
    }

    [System.Serializable]
    public class LocalNot
    {
        public string title;
        public string body;
        public bool autoCancel;
        public bool showWhen;
        public int smallIcon;
        public string bigIcon;
        public bool mediaStyle = true;
        public string data = "";
        public int id;
        public bool onGoing = false;
        public int progress = -1;
        public DateTime? when = null;
        public List<LocalAction> actions = new List<LocalAction>();

        public int notificationImportance = (int)NotificationImportance.Default;


        public static NotificationManager _manager => (NotificationManager)Application.Context.GetSystemService(Context.NotificationService);
        public static Dictionary<string, Bitmap> cachedBitmaps = new Dictionary<string, Bitmap>(); // TO ADD PREFORMACE WHEN ADDING NOTIFICATION W SAME IMAGE

        public static async Task<Bitmap> GetImageBitmapFromUrl(string url)
        {
            if (cachedBitmaps.ContainsKey(url)) {
                return cachedBitmaps[url];
            }

            try {
                Bitmap imageBitmap = null;

                using (var webClient = new WebClient()) {
                    var imageBytes = await webClient.DownloadDataTaskAsync(url);
                    if (imageBytes != null && imageBytes.Length > 0) {
                        imageBitmap = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length);
                    }
                }
                cachedBitmaps.Add(url, imageBitmap);
                return imageBitmap;
            }
            catch (Exception) {
                return null;
            }

        }

        public static long CurrentTimeMillis(DateTime time)
        {
            return (long)(time - Jan1st1970).TotalMilliseconds;
        }

        private static readonly DateTime Jan1st1970 = new DateTime
    (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static async void ShowLocalNot(LocalNot not, Context context = null)
        {
            var cc = context ?? Application.Context;
            var builder = new Notification.Builder(cc);
            builder.SetContentTitle(not.title);

            bool containsMultiLine = not.body.Contains("\n");

            if (Build.VERSION.SdkInt < BuildVersionCodes.O || !containsMultiLine) {
                builder.SetContentText(not.body);
            }
            builder.SetSmallIcon(not.smallIcon);
            builder.SetAutoCancel(not.autoCancel);
            builder.SetOngoing(not.onGoing);

            if (not.progress != -1) {
                builder.SetProgress(100, not.progress, false);
            }

            builder.SetVisibility(NotificationVisibility.Public);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) {
                var channelId = $"{cc.PackageName}.general";
                var channel = new NotificationChannel(channelId, "General", (NotificationImportance)not.notificationImportance);
                _manager.CreateNotificationChannel(channel);

                builder.SetChannelId(channelId);

                if (not.bigIcon != "") {
                    var bitmap = await GetImageBitmapFromUrl(not.bigIcon);
                    if (bitmap != null) {
                        builder.SetLargeIcon(bitmap);
                        if (not.mediaStyle) {
                            builder.SetStyle(new Notification.MediaStyle()); // NICER IMAGE
                        }
                    }
                }

                if (containsMultiLine) {
                    var b = new Notification.BigTextStyle();
                    b.BigText(not.body);
                    builder.SetStyle(b); // Text
                                         // builder.SetContentText(not.body);
                }

                if (not.actions.Count > 0) {
                    List<Notification.Action> actions = new List<Notification.Action>();

                    for (int i = 0; i < not.actions.Count; i++) {
                        var _resultIntent = new Intent(context, typeof(MainIntentService));
                        _resultIntent.PutExtra("data", not.actions[i].action);
                        var pending = PendingIntent.GetService(context, 3337 + i + not.id,
                         _resultIntent,
                        PendingIntentFlags.UpdateCurrent
                         );

                        actions.Add(new Notification.Action(not.actions[i].sprite, not.actions[i].name, pending));
                    }

                    builder.SetActions(actions.ToArray());
                }
            }

            builder.SetShowWhen(not.showWhen);
            if (not.when != null) {
                builder.SetWhen(CurrentTimeMillis((DateTime)not.when));
            }
            var stackBuilder = Android.Support.V4.App.TaskStackBuilder.Create(cc);

            var resultIntent = GetLauncherActivity(cc);
            if (not.data != "") {

                resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
                var _data = Android.Net.Uri.Parse(not.data);//"cloudstreamforms:tt0371746Name=Iron man=EndAll");
                resultIntent.SetData(_data);
                stackBuilder.AddNextIntent(resultIntent);
                var resultPendingIntent =
              stackBuilder.GetPendingIntent(not.id, (int)PendingIntentFlags.UpdateCurrent);
                builder.SetContentIntent(resultPendingIntent);
            }
            else {
                //Intent resultIntent = new Intent(context, typeof(MainActivity));
                stackBuilder.AddParentStack(activity.Class);
                // resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded );
                /* resultIntent.SetAction(Intent.ActionMain);
                 resultIntent.AddCategory(Intent.CategoryLauncher);*/
                stackBuilder.AddNextIntent(resultIntent);
                // resultIntent.SetFlags(ActivityFlags.task);
            }


            _manager.Notify(not.id, builder.Build());
        }
        public static Intent GetLauncherActivity(Context context = null)
        {
            var cc = context ?? Application.Context;
            var packageName = cc.PackageName;
            return cc.PackageManager.GetLaunchIntentForPackage(packageName).SetPackage(null);
        }
    }

    [Service]
    public class NotifyAtTime : IntentService
    {
        public NotifyAtTime() : base("NotifyAtTime")
        {
        }

        protected override void OnHandleIntent(Android.Content.Intent intent)
        {
            ToastLength toastLength = ToastLength.Short;

            Toast.MakeText(Android.App.Application.Context, "Hello world", toastLength).Show();

        }
    }


    [Service]
    public class DownloadUrlService : IntentService
    {
        public DownloadUrlService() : base("DownloadUrlService")
        {
            SetIntentRedelivery(true);
        }
        string poster;
        string title;
        int id;



        public override void OnDestroy()
        {
            ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = id, smallIcon = Resource.Drawable.bicon, body = "Download ddddd" });

            base.OnDestroy();
        }

        public override void OnCreate()
        {


            base.OnCreate();
        }
        protected override void OnHandleIntent(Intent intent)
        {
            //startIntent = intent;
            var context = Application.Context;
            id = intent.Extras.GetInt("id", -1);
            string url = intent.Extras.GetString("url", "");
            title = intent.Extras.GetString("title", "");
            string path = intent.Extras.GetString("path", "");
            bool showNotification = intent.Extras.GetBoolean("not", false);
            bool showNotificationWhenDone = intent.Extras.GetBoolean("notdone", false);
            bool openWhenDone = intent.Extras.GetBoolean("opendone", false);
            poster = intent.Extras.GetString("poster", "");
            string fileName = intent.Extras.GetString("file", "");

            int progress = 0;
            int _progress = 0;

            void UpdateDloadNot()
            {
                //poster != ""
                ShowLocalNot(new LocalNot() { mediaStyle = false, bigIcon = poster, title = title, autoCancel = false, onGoing = true, id = id, smallIcon = Resource.Drawable.bicon, progress = progress, body = progress + "%" }, context);
            }
            bool isDone = false;

            void ShowDone()
            {
                if (showNotificationWhenDone) {
                    ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = id, smallIcon = Resource.Drawable.bicon, body = "Download done!" }, context); // ((e.Cancelled || e.Error != null) ? "Download Failed!"
                }
                // Toast.MakeText(context, "PG DONE!!!", ToastLength.Long).Show(); 
            }
            /*
            try { 
                URL _url = new URL(url);
                URLConnection connection = _url.OpenConnection();
                connection.Connect();
                int fileLength = connection.ContentLength;
                String fileExtension = MimeTypeMap.GetFileExtensionFromUrl(url);
                fileName = CensorFilename(fileName);
                InputStream input = new BufferedInputStream(connection.InputStream);
                Java.IO.File _file = new Java.IO.File(path);
                _file.Mkdirs();
                path += "/" + CensorFilename(fileName);

                OutputStream output = new FileOutputStream(new Java.IO.File(path));
                byte[] data = new byte[1024];
                long total = 0;
                int count;
                int previousProgress = 0;
                while ((count = input.Read(data)) != -1) {
                    total += count;
                    int cprogress = (int)(total * 100 / fileLength);
                    output.Write(data, 0, count);
                    if (cprogress == 100 || cprogress > previousProgress + 4) {
                        // Only post progress event if we've made progress.
                        previousProgress = cprogress;
                        progress = cprogress;
                        UpdateDloadNot();

                    }
                }
                output.Flush();
                output.Close();
                input.Close();
            }
            catch (Exception) {
                throw;
            }*/

            //  fileName = System.CurrentTimeMillis() + "." + fileExtension;

            try {
                Java.IO.File _file = new Java.IO.File(path);
                _file.Mkdirs();
                path += "/" + CensorFilename(fileName);

                using (WebClient wc = new WebClient()) {
                    wc.DownloadProgressChanged += (o, e) => {
                        progress = e.ProgressPercentage;
                        // Toast.MakeText(context, "PROGRESS::" + e.ProgressPercentage, ToastLength.Short).Show();
                        if (progress < 100) {

                            if (_progress != progress) {
                                _progress = progress;
                                if (showNotification && id != -1) {
                                    UpdateDloadNot();
                                }
                            }
                        }
                        else {
                            isDone = true;
                            ShowDone();
                        }
                    };
                    wc.DownloadFileCompleted += (o, e) => {
                        if (isDone) {
                            ShowDone();
                        }
                        else {
                            ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = id, smallIcon = Resource.Drawable.bicon, body = "Download Failed!" }, context); // ((e.Cancelled || e.Error != null) ? "Download Failed!"
                        }
                    };

                    wc.DownloadFileAsync(
                         new System.Uri(url),
                         path
                    );
                }
            }
            catch (Exception) {
                if (showNotificationWhenDone) {
                    ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = id, smallIcon = Resource.Drawable.bicon, body = "Download Failed!" }, context);
                }
                //App.ShowToast("Download Failed");
            }
            StopSelf();
            //  return GetPath(mainPath, extraPath) + "/" + CensorFilename(fileName);
        }


    }


    [BroadcastReceiver]
    public class DownloadUrlReceiver : BroadcastReceiver
    {

        /*
        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            return StartCommandResult.Sticky;
           // return base.OnStartCommand(intent, flags, startId);
        }*/

        /*
    public override void OnTaskRemoved(Intent rootIntent)
    {
        ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = id, smallIcon = Resource.Drawable.bicon, body = "Download Failed, App killed Task!" });
        //  StartService(rootIntent);
       var triggerTime = CurrentTimeMillis(DateTime.UtcNow.AddSeconds(10)); // NotifyTimeInMilliseconds((DateTime)time);
        var alarmManager = GetAlarmManager();
        var context = Application.Context;

        var _resultIntent = new Intent(context, typeof(DownloadUrlService));
        //  _resultIntent.PutExtra("data", App.ConvertToString(localNot)); 
        var pending = PendingIntent.GetBroadcast(context, id,
             _resultIntent,
            PendingIntentFlags.CancelCurrent
             );


        alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pending);
        MainActivity.mainDroid.ShowNotIntentAsync("Hello", "yeet", id, "tt4869896", "IronYeet", System.DateTime.Now.AddSeconds(10));
        //  base.OnTaskRemoved(rootIntent);
    }*/



        string poster;
        string title;
        int id;
        //Intent startIntent;
        public override void OnReceive(Context context, Intent intent)
        {
            Intent downloadIntent = new Intent(context, typeof(DownloadUrlService));
            downloadIntent.PutExtras(intent.Extras);
            context.StartService(downloadIntent);
            /*
            var _resultIntent = new Intent(context, typeof(DownloadUrlReceiver));
            //  _resultIntent.PutExtra("data", App.ConvertToString(localNot)); 
            var pending = PendingIntent.GetBroadcast(context, id,
                 _resultIntent,
                PendingIntentFlags.CancelCurrent
                 );
            var alarmManager = MainHelper.GetAlarmManager();
            var triggerTime = CurrentTimeMillis(DateTime.UtcNow.AddSeconds(2));
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pending);*/
            //LocalNot.ShowLocalNot(new LocalNot() { title = "da", id = 12345 });
            //ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = 1234, smallIcon = Resource.Drawable.bicon, body = "Download Failed, App killed Task!" });

            MainActivity.mainDroid.ShowNotIntentAsync("Hello", "yeet", 12345, "tt4869896", "IronYeet", System.DateTime.Now);
            // return;


        }




    }


    [BroadcastReceiver]
    public class AlertReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            ToastLength toastLength = ToastLength.Short;
            LocalNot localNot = new LocalNot();
            foreach (var prop in typeof(LocalNot).GetFields()) {
                if (prop.FieldType == typeof(string)) {
                    prop.SetValue(localNot, intent.Extras.GetString(prop.Name));
                }
                if (prop.FieldType == typeof(int)) {
                    prop.SetValue(localNot, intent.Extras.GetInt(prop.Name));
                }
                if (prop.FieldType == typeof(float)) {
                    prop.SetValue(localNot, intent.Extras.GetFloat(prop.Name));
                }
                if (prop.FieldType == typeof(DateTime)) {
                    prop.SetValue(localNot, DateTime.Parse(intent.Extras.GetString(prop.Name)));
                }
                if (prop.FieldType == typeof(bool)) {
                    prop.SetValue(localNot, intent.Extras.GetBoolean(prop.Name));
                }
            }

            //  Toast.MakeText(Android.App.Application.Context, "da:" + localNot.title, toastLength).Show();
            ShowLocalNot(localNot);

            /*
            try {
              //  print("GOT DATATATA::::::::::::::::::::::::::::::::::::.!!");
                string data = intent.GetStringExtra("data");
                var not = App.ConvertToObject<LocalNot>(data, null);
                if (not != null) {
                    MainDroid.ShowLocalNot(not);
                }
            }
            catch (Exception) {

            }*/
        }
    }

    public static class DownloadHandle
    {

        static Dictionary<int, long> progressDownloads = new Dictionary<int, long>();
        const string DOWNLOAD_KEY = "DownloadProgress";
        const string DOWNLOAD_KEY_INTENT = "DownloadProgressIntent";

        public static void OnKilled()
        {
            try {
                /*
                foreach (var path in App.GetKeysPath(DOWNLOAD_KEY_INTENT)) {
                    //  App.GetKey<long>(DOWNLOAD_KEY, path, 0);
                    string data = App.GetKey<string>(path, null);
                    int id = int.Parse(FindHTML(data, $"{nameof(id)}=", "|||"));
                    App.CancelNotifaction(id);
                }*/

                foreach (var id in DownloadHandle.activeIds) {
                    var manager = Application.Context.GetSystemService(Context.NotificationService) as NotificationManager;
                    manager.Cancel(id);
                    //  App.CancelNotifaction(id);
                }
                foreach (var key in outputStreams.Keys) {
                    var outp = outputStreams[key];
                    var inpp = inputStreams[key];
                    outp.Flush();
                    outp.Close();
                    inpp.Close();
                }
                foreach (var key in progressDownloads.Keys) {
                    print("SAVED KEY:" + key);
                    App.SetKey(DOWNLOAD_KEY, key.ToString(), progressDownloads[key]);
                }
            }
            catch (Exception _ex) {
                print("EXEPTION WHEN DESTROYED: " + _ex);
            }
        }

        public static void ResumeIntents()
        {
            int downloadResumes = 0;
            foreach (var path in App.GetKeysPath(DOWNLOAD_KEY_INTENT)) {
                //  App.GetKey<long>(DOWNLOAD_KEY, path, 0);
                downloadResumes++;
                string data = App.GetKey<string>(path, null);
                HandleIntent(data, true);
            }

            if (downloadResumes == 1) {
                App.ShowToast("Resumed Download");
            }
            else if (downloadResumes != 0) {
                App.ShowToast($"Resumed {downloadResumes} downloads");
            }

        }
        public static void HandleIntent(string intent, bool resumeIntent = false)
        {
            if (intent == null) return;

            int id = int.Parse(FindHTML(intent, $"{nameof(id)}=", "|||"));
            string url = FindHTML(intent, $"{nameof(url)}=", "|||");
            string title = FindHTML(intent, $"{nameof(title)}=", "|||");
            string path = FindHTML(intent, $"{nameof(path)}=", "|||");
            bool showNotification = bool.Parse(FindHTML(intent, $"{nameof(showNotification)}=", "|||"));
            bool showNotificationWhenDone = bool.Parse(FindHTML(intent, $"{nameof(showNotificationWhenDone)}=", "|||"));
            bool openWhenDone = bool.Parse(FindHTML(intent, $"{nameof(openWhenDone)}=", "|||"));
            string poster = FindHTML(intent, $"{nameof(poster)}=", "|||");
            string fileName = FindHTML(intent, $"{nameof(fileName)}=", "|||");
            string beforeTxt = FindHTML(intent, $"{nameof(beforeTxt)}=", "|||");
            HandleIntent(id, url, title, path, showNotification, showNotificationWhenDone, openWhenDone, poster, fileName, beforeTxt, resumeIntent);
        }

        public static void HandleIntent(Intent intent)
        {
            if (intent == null) return;

            var context = Application.Context;
            int id = intent.Extras.GetInt("id", -1);
            string url = intent.Extras.GetString("url", "");
            string title = intent.Extras.GetString("title", "");
            string path = intent.Extras.GetString("path", "");
            bool showNotification = intent.Extras.GetBoolean("not", false);
            bool showNotificationWhenDone = intent.Extras.GetBoolean("notdone", false);
            bool openWhenDone = intent.Extras.GetBoolean("opendone", false);
            string poster = intent.Extras.GetString("poster", "");
            string fileName = intent.Extras.GetString("file", "");
            string beforeTxt = intent.Extras.GetString("beforeTxt", "");
            HandleIntent(id, url, title, path, showNotification, showNotificationWhenDone, openWhenDone, poster, fileName, beforeTxt);
        }


        static Dictionary<int, OutputStream> outputStreams = new Dictionary<int, OutputStream>();
        static Dictionary<int, InputStream> inputStreams = new Dictionary<int, InputStream>();
        public static List<int> activeIds = new List<int>();
        /// <summary>
        /// 0 = download, 1 = Pause, 2 = remove
        /// </summary>
        public static Dictionary<int, int> isPaused = new Dictionary<int, int>();

        public static EventHandler<int> changedPause;

        public static void HandleIntent(int id, string url, string title, string path, bool showNotification, bool showNotificationWhenDone, bool openWhenDone, string poster, string fileName, string beforeTxt, bool resumeIntent = false)
        {
            if (isPaused.ContainsKey(id)) return;

            var context = Application.Context;
            App.SetKey(DOWNLOAD_KEY_INTENT, id.ToString(), $"{nameof(id)}={id}|||{nameof(url)}={url}|||{nameof(title)}={title}|||{nameof(path)}={path}|||{nameof(showNotification)}={showNotification}|||{nameof(showNotificationWhenDone)}={showNotificationWhenDone}|||{nameof(openWhenDone)}={openWhenDone}|||{nameof(poster)}={poster}|||{nameof(fileName)}={fileName}|||{nameof(beforeTxt)}={beforeTxt}|||");

            int progress = 0;

            void UpdateDloadNot(string progressTxt)
            {
                //poster != ""
                int isPause = isPaused[id];
                bool canPause = isPause == 0;
                if (isPause != 2) {
                    ShowLocalNot(new LocalNot() { actions = new List<LocalAction>() { new LocalAction() { action = $"handleDownload|||id={id}|||dType={(canPause ? 1 : 0)}|||", name = canPause ? "Pause" : "Resume" }, new LocalAction() { action = $"handleDownload|||id={id}|||dType=2|||", name = "Stop" } }, mediaStyle = false, bigIcon = poster, title = title, autoCancel = false, onGoing = true, id = id, smallIcon = Resource.Drawable.bicon, progress = progress, body = progressTxt }, context);
                }
            }

            void ShowDone(bool succ, string? overrideText = null)
            {
                if (showNotificationWhenDone) {
                    ShowLocalNot(new LocalNot() { mediaStyle = poster != "", bigIcon = poster, title = title, autoCancel = true, onGoing = false, id = id, smallIcon = Resource.Drawable.bicon, body = overrideText ?? (succ ? "Download done!" : "Download Failed") }, context); // ((e.Cancelled || e.Error != null) ? "Download Failed!"
                }
                // Toast.MakeText(context, "PG DONE!!!", ToastLength.Long).Show(); 
            }
            /*
       var manager = DownloadManager.FromContext(Application.Context);
       var request = new DownloadManager.Request( Android.Net.Uri.Parse(url));
       request.SetNotificationVisibility(DownloadVisibility.Hidden);
       request.SetDestinationUri(Android.Net.Uri.Parse(path));

       long downloadId = manager.Enqueue(request);
       return;*/

            Thread t = new Thread(() => {

                if ((int)Android.OS.Build.VERSION.SdkInt > 9) {
                    StrictMode.ThreadPolicy policy = new
                    StrictMode.ThreadPolicy.Builder().PermitAll().Build();
                    StrictMode.SetThreadPolicy(policy);
                }
                long total = 0;
                int fileLength = 0;

                void UpdateProgress()
                {
                    UpdateDloadNot($"{beforeTxt}{progress} % ({ConvertBytesToAny(total, 1, 2)} MB/{ConvertBytesToAny(fileLength, 1, 2)} MB)");
                }

                void UpdateFromId(object sender, int _id)
                {
                    if (_id == id) {
                        UpdateProgress();
                    }
                }

                try {
                    try {
                        Java.IO.File __file = new Java.IO.File(path);
                        __file.Mkdirs();
                    }
                    catch (Exception _ex) {
                        print("FAILED:::" + _ex);
                    }


                    fileName = CensorFilename(fileName);
                    path += "/" + fileName;
                    print("PATH=====" + path + "|" + fileName);


                    URL _url = new URL(url);
                    URLConnection connection = _url.OpenConnection();

                    var rFile = new Java.IO.File(path);
                    if (!rFile.Exists()) {
                        print("FILE DOSENT EXITS");
                        rFile.CreateNewFile();
                    }
                    else {
                        if (resumeIntent) {
                            total = rFile.Length();
                            connection.SetRequestProperty("Range", "bytes=" + rFile.Length() + "-");
                        }
                        else {
                            rFile.Delete();
                            rFile.CreateNewFile();
                        }
                    }
                    connection.SetRequestProperty("Accept-Encoding", "identity");

                    connection.Connect();
                    fileLength = connection.ContentLength + (int)total;
                    String fileExtension = MimeTypeMap.GetFileExtensionFromUrl(url);
                    InputStream input = new BufferedInputStream(connection.InputStream);

                    //long skip = App.GetKey<long>(DOWNLOAD_KEY, id.ToString(), 0);

                    OutputStream output = new FileOutputStream(rFile, true);

                    outputStreams[id] = output;
                    inputStreams[id] = input;
                    isPaused[id] = 0;
                    activeIds.Add(id);

                    int cProgress()
                    {
                        return (int)(total * 100 / fileLength);
                    }
                    progress = cProgress();

                    byte[] data = new byte[1024];
                    // skip;
                    int count;
                    int previousProgress = 0;
                    UpdateDloadNot(total == 0 ? "Download starting" : "Download resuming");

                    System.DateTime lastUpdateTime = System.DateTime.Now;
                    long lastTotal = total;
                    const int UPDATE_TIME = 1;

                    changedPause += UpdateFromId;

                    while ((count = input.Read(data)) != -1) {
                        total += count;

                        output.Write(data, 0, count);
                        progressDownloads[id] = total;
                        progress = cProgress();


                        if (isPaused[id] == 1) {
                            print("PAUSEDOWNLOAD");
                            UpdateProgress();
                            while (isPaused[id] == 1) {
                                Thread.Sleep(100);
                            }
                            if (isPaused[id] != 2) {
                                UpdateProgress();
                            }
                        }
                        if (isPaused[id] == 2) { // DELETE FILE
                            print("DOWNLOAD STOPPED");
                            ShowDone(false, "Download Stopped");
                            //  Thread.Sleep(100);
                            output.Flush();
                            output.Close();
                            input.Close();
                            outputStreams.Remove(id);
                            inputStreams.Remove(id);
                            isPaused.Remove(id);
                            // Thread.Sleep(100);
                            rFile.Delete();
                            App.RemoveKey(DOWNLOAD_KEY, id.ToString());
                            App.RemoveKey(DOWNLOAD_KEY_INTENT, id.ToString());
                            changedPause -= UpdateFromId;
                            activeIds.Remove(id);
                            Thread.Sleep(100);
                            return;
                        }

                        if (DateTime.Now.Subtract(lastUpdateTime).TotalSeconds > UPDATE_TIME) {
                            lastUpdateTime = DateTime.Now;
                            long diff = total - lastTotal;
                            //  UpdateDloadNot($"{ConvertBytesToAny(diff/UPDATE_TIME, 2,2)}MB/s | {progress}%");
                            //{ConvertBytesToAny(diff / UPDATE_TIME, 2, 2)}MB/s | 
                            UpdateProgress();
                            lastTotal = total;
                        }

                        if (progress >= 100 || progress > previousProgress) {
                            UpdateProgress();
                            // Only post progress event if we've made progress.
                            previousProgress = progress;
                            if (progress >= 100) {
                                ShowDone(true);
                            }
                            else {
                                // UpdateDloadNot(progress + "%");
                            }
                        }
                    }

                    ShowDone(true);
                    output.Flush();
                    output.Close();
                    input.Close();
                    outputStreams.Remove(id);
                    inputStreams.Remove(id);
                    activeIds.Remove(id);
                }
                catch (Exception _ex) {
                    print("DOWNLOADURL: " + url);
                    print("DOWNLOAD FAILED BC: " + _ex);
                    ShowDone(false);
                }
                finally {
                    App.RemoveKey(DOWNLOAD_KEY, id.ToString());
                    App.RemoveKey(DOWNLOAD_KEY_INTENT, id.ToString());
                    changedPause -= UpdateFromId;
                    isPaused.Remove(id);
                }
                /*
                try {
                    // FILE STUFF
                    try {
                        Java.IO.File __file = new Java.IO.File(path);
                        __file.Mkdirs();
                    }
                    catch (Exception _ex) {
                        print("FAILED:::" + _ex);
                    }

                    fileName = CensorFilename(fileName);
                    path += "/" + fileName;
                    print("PATH=====" + path + "|" + fileName);

                    var rFile = new Java.IO.File(path);
                    bool exists = true;
                    if (!rFile.Exists()) {
                        exists = false;
                        print("FILE DOSENT EXITS");
                        rFile.CreateNewFile();
                    }


                    long skip = App.GetKey<long>(DOWNLOAD_KEY, id.ToString(), 0);

                    URL _url = new URL(url);
                    URLConnection connection = _url.OpenConnection();
                    if (exists) {
                        connection.SetRequestProperty("Range", "bytes=" + rFile.Length() + "-");
                    }
                    connection.Connect();
                    //   connection.InputStream.Seek (skip,SeekOrigin.Begin);
                    int fileLength = connection.ContentLength;
                    String fileExtension = MimeTypeMap.GetFileExtensionFromUrl(url);
                    InputStream input = new BufferedInputStream(connection.InputStream);
       
                   // App.ShowToast(connection.InputStream.Position + "<<<");



                    // input.Skip(skip);

                    OutputStream output = new FileOutputStream(rFile, true);

                    outputStreams[id] = output;
                    inputStreams[id] = input;

                    byte[] data = new byte[1024];
                    long total = skip;
                    int count;
                    int previousProgress = 0;
                    UpdateDloadNot();
                    while ((count = input.Read(data)) != -1) {
                        total += count;
                        int cprogress = (int)(total * 100 / fileLength);
                        output.Write(data, 0, count);
                        progressDownloads[id] = total;

                        if (cprogress == 100 || cprogress > previousProgress) {
                            // Only post progress event if we've made progress.
                            previousProgress = cprogress;
                            progress = cprogress;
                            UpdateDloadNot();
                        }
                    }
                    ShowDone(true);
                    output.Flush();
                    output.Close();
                    input.Close();
                    outputStreams.Remove(id);
                    inputStreams.Remove(id);
                }
                catch (Exception _ex) {
                    print("DOWNLOADURL: " + url);
                    print("DOWNLOAD FAILED BC: " + _ex);
                    ShowDone(false);
                }
                finally {
                    App.RemoveKey(DOWNLOAD_KEY, id.ToString());
                    App.RemoveKey(DOWNLOAD_KEY_INTENT, id.ToString());
                }
                */
            });
            t.Start();
        }
    }



    [Activity(Label = "CloudStream 2", Icon = "@drawable/bicon", Theme = "@style/MainTheme.Splash", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation), IntentFilter(new[] { Intent.ActionView }, DataScheme = "cloudstreamforms", Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable })]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public static MainDroid mainDroid;
        public static MainActivity activity;

        protected override void OnNewIntent(Intent intent)
        {
            //App.ShowToast("ON NEW INTENT");
            //print("DA:::.2132131");
            if (intent.DataString != null) {

                print("INTENTNADADA:::" + intent.DataString);
            }
            Bundle extras = intent.Extras;
            if (extras != null) {
                print("DADADA:D:A:D:AD:A:D:A:D:A222");
                if (extras.ContainsKey("data")) {
                    print("DADADA:D:A:D:AD:A:D:A:D:A2233332");
                    // extract the extra-data in the Notification
                    string msg = extras.GetString("data");
                    print("DADADA:D:A:D:AD:A:D:A:D:A" + msg);
                }
            }

            base.OnNewIntent(intent);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {

            print("ON CREATED:::::!!!!!!!!!");



            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);
            string data = Intent?.Data?.EncodedAuthority;

            try {
                MainPage.intentData = data;
            }
            catch (Exception) { }

            // int intHeight = (int)(Resources.DisplayMetrics.HeightPixels / Resources.DisplayMetrics.Density);
            //int intWidth = (int)(Resources.DisplayMetrics.WidthPixels / Resources.DisplayMetrics.Density);



            // ======================================= INIT =======================================

            FFImageLoading.Forms.Platform.CachedImageRenderer.Init(enableFastRenderer: true);
            Rg.Plugins.Popup.Popup.Init(this, savedInstanceState);
            UserDialogs.Init(this);
            LibVLCSharpFormsRenderer.Init();
            XamEffects.Droid.Effects.Init();

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);



            LocalNotificationsImplementation.NotificationIconId = Resource.Drawable.bicon;
            MainDroid.NotificationIconId = Resource.Drawable.bicon;


            LoadApplication(new App());
            activity = this;

            mainDroid = new MainDroid();
            mainDroid.Awake();

            if (Intent.DataString != null) {
                print("GOT NON NULL DATA");
                if (Intent.DataString != "") {
                    print("INTENTDATA::::" + Intent.DataString);
                    MainPage.PushPageFromUrlAndName(Intent.DataString);
                }
            }
            RequestPermission(this);

            //App.ShowToast("ON CREATE");

            //mainDroid.Test();
            /*
            MessagingCenter.Subscribe<VideoPage>(this, "allowLandScapePortrait", sender =>
            {
                RequestedOrientation = ScreenOrientation.Unspecified;
            });
            MessagingCenter.Subscribe<VideoPage>(this, "preventLandScape", sender =>
            {
                RequestedOrientation = ScreenOrientation.Portrait;
            });*/
            // Window.DecorView.SetBackgroundResource(Resource.Drawable.splash_background_remove);//Resources.GetDrawable(Resource.Drawable.splash_background_remove);


            /*
            var alarm = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
            var context = ApplicationContext;
            var _testIntent = new Intent(context, typeof(AlertReceiver));

            var pending = PendingIntent.GetBroadcast(context, 1337, _testIntent, 0);

            alarm.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, MainDroid.CurrentTimeMillis(DateTime.UtcNow.AddSeconds(5)), pending);*/

            MainChrome.OnDisconnected += (o, e) => {
                MainDroid.CancelChromecast();
            };

            MainChrome.OnNotificationChanged += (o, e) => {
                print("CHROMECAST CHANGED::: ");
                print("ID=====================" + e.isCasting + "|" + e.isPlaying + "|" + e.isPaused);
                if (!e.isCasting) {// || !e.isPlaying) {
                    MainDroid.CancelChromecast();
                }
                else {
                    MainDroid.UpdateChromecastNotification(e.title, e.body, e.isPaused, e.posterUrl);
                }
            };
            ResumeIntentData();
            StartService(new Intent(BaseContext, typeof(OnKilledService)));
           // DownloadHandle.ResumeIntents();
        }


        async void ResumeIntentData()
        {
           await Task.Delay(1000);
           print("STARTINTENT");
            DownloadHandle.ResumeIntents();

        }

        public void Killed()
        {
           // ShowNotification("finish", "Yeet");
            MainDroid.CancelChromecast(); // TO REMOVE IT, CANT INTERACT WITHOUT THE CORE
            DownloadHandle.OnKilled();
        }

        protected override void OnDestroy()
        {
            Killed();
            base.OnDestroy();
        }


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public static int REQUEST_START = 112;
        public static int REQUEST_INSTALL = 113;
        public static int REQUEST_INSTALL2 = 113;
        public static int REQUEST_INSTALL3 = 114;
        private static void RequestPermission(Activity context)
        {

            List<string> requests = new List<string>() {
                Manifest.Permission.WriteExternalStorage, Manifest.Permission.RequestInstallPackages,Manifest.Permission.InstallPackages,Manifest.Permission.WriteSettings
            };

            for (int i = 0; i < requests.Count; i++) {
                bool hasPermission = (ContextCompat.CheckSelfPermission(context, requests[i]) == Permission.Granted);
                if (!hasPermission) {
                    ActivityCompat.RequestPermissions(context,
                       new string[] { requests[i] },
                     REQUEST_START + i);
                }
            }

        }
    }


    public static class MainHelper
    {
        public static AlarmManager GetAlarmManager()
        {
            var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
            return alarmManager;
        }

        public static string GetPath(bool mainPath, string extraPath)
        {
            return (mainPath ? (Android.OS.Environment.ExternalStorageDirectory + "/" + Android.OS.Environment.DirectoryDownloads) : (Android.OS.Environment.ExternalStorageDirectory + "/" + Android.OS.Environment.DirectoryDownloads + "/Extra")) + extraPath;
        }
        public static string CensorFilename(string name, bool toLower = true)
        {
            name = Regex.Replace(name, @"[^A-Za-z0-9\.]+", String.Empty);
            name.Replace(" ", "");
            if (toLower) {
                name = name.ToLower();
            }
            return name;
        }


    }


    public class MainDroid : App.IPlatformDep
    {
        public void SetBrightness(double opacity)
        {
            Android.Provider.Settings.System.PutInt(MainActivity.activity.ContentResolver, Android.Provider.Settings.System.ScreenBrightness, (int)(opacity * 255));
        }

        public double GetBrightness()
        {
            return Android.Provider.Settings.System.GetInt(MainActivity.activity.ContentResolver, Android.Provider.Settings.System.ScreenBrightness) / 255.0;
        }


        // FROM https://github.com/edsnider/localnotificationsplugin/blob/master/src/Plugin.LocalNotifications.Android/LocalNotificationsImplementation.cs


        /// <summary>
        /// Get or Set Resource Icon to display
        /// </summary>
        public static int NotificationIconId { get; set; }
        static string _packageName => Application.Context.PackageName;



        int LocalNotificationIconId
        {
            get {
                if (NotificationIconId != 0) {
                    return NotificationIconId;
                }
                else {
                    return Resource.Drawable.plugin_lc_smallicon;
                }
            }
        }






        public static void CancelFutureNotification(int id)
        {
            var context = MainActivity.activity.ApplicationContext;

            var alarmManager = GetAlarmManager();
            var _resultIntent = new Intent(context, typeof(AlertReceiver));
            var pending = PendingIntent.GetBroadcast(context, id,
                    _resultIntent,
                   PendingIntentFlags.CancelCurrent
                    );
            alarmManager.Cancel(pending);
        }



        public async void ShowNotIntentAsync(string title, string body, int id, string titleId, string titleName, DateTime? time = null, string bigIconUrl = "")
        {

            var localNot = new LocalNot() { title = title, body = body, id = id, data = "cloudstreamforms:" + titleId + "Name=" + titleName + "=EndAll", bigIcon = bigIconUrl, autoCancel = true, mediaStyle = true, notificationImportance = (int)NotificationImportance.Default, showWhen = true, when = time, smallIcon = LocalNotificationIconId };


            if (time == null) {
                ShowLocalNot(localNot);
            }
            else {
                print("SHOWS NOTIFICATION== " + body + " in " + ((DateTime)time).Subtract(DateTime.UtcNow).TotalSeconds);
                var context = MainActivity.activity.ApplicationContext;

                var _resultIntent = new Intent(context, typeof(AlertReceiver));
                //  _resultIntent.PutExtra("data", App.ConvertToString(localNot));


                // IF NOT BITSERALIZER IS AVALIBLE
                foreach (var prop in typeof(LocalNot).GetFields()) {
                    if (prop.FieldType == typeof(int)) {
                        _resultIntent.PutExtra(prop.Name, (int)prop.GetValue(localNot));//(int)prop.GetValue(localNot));
                    }
                    if (prop.FieldType == typeof(float)) {
                        _resultIntent.PutExtra(prop.Name, (float)prop.GetValue(localNot));//(int)prop.GetValue(localNot));
                    }
                    if (prop.FieldType == typeof(bool)) {
                        _resultIntent.PutExtra(prop.Name, (bool)prop.GetValue(localNot));//(int)prop.GetValue(localNot));
                    }
                    if (prop.FieldType == typeof(string)) {
                        _resultIntent.PutExtra(prop.Name, (string)prop.GetValue(localNot));//(int)prop.GetValue(localNot));
                    }
                    if (prop.FieldType == typeof(DateTime)) {
                        _resultIntent.PutExtra(prop.Name, ((DateTime)prop.GetValue(localNot)).ToLongDateString());//(int)prop.GetValue(localNot));
                    }
                    if (prop.FieldType.IsEnum) {
                        _resultIntent.PutExtra(prop.Name, (int)prop.GetValue(localNot));//(int)prop.GetValue(localNot));
                    }
                }


                _resultIntent.PutExtra("title", localNot.title);

                var pending = PendingIntent.GetBroadcast(context, id,
                     _resultIntent,
                    PendingIntentFlags.CancelCurrent
                     );




                var triggerTime = CurrentTimeMillis(((DateTime)time).Add(DateTime.UtcNow.Subtract(DateTime.Now)));// NotifyTimeInMilliseconds((DateTime)time);
                var alarmManager = GetAlarmManager();

                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pending);

                /*
                var alarm = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
                var _testIntent = new Intent(context, typeof(AlertReceiver));

                var pending = PendingIntent.GetBroadcast(context, 1337, _testIntent, 0);

                alarm.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, MainDroid.CurrentTimeMillis(DateTime.UtcNow.AddSeconds(5)), pending);*/

            }

            /*var builder = new Notification.Builder(Application.Context);
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            builder.SetAutoCancel(true);
            builder.SetShowWhen(true);
            builder.SetSmallIcon(LocalNotificationIconId);


            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) {
                var channelId = $"{_packageName}.general";
                var channel = new NotificationChannel(channelId, "General", NotificationImportance.Default);

                _manager.CreateNotificationChannel(channel);

                builder.SetChannelId(channelId);

                var context = MainActivity.activity.ApplicationContext;
                if (bigIconUrl != "") {
                    print("BIGBIG::" + bigIconUrl);
                    var bitmap = await GetImageBitmapFromUrl(bigIconUrl);
                    if (bitmap != null) {
                        builder.SetLargeIcon(bitmap);
                        builder.SetStyle(new Notification.MediaStyle()); // NICER IMAGE
                    }
                }
            }


            var resultIntent = GetLauncherActivity();
            resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            //"cloudstreamforms:tt0371746Name=Iron man=EndAll"
            string data = $"cloudstreamforms:{titleId}Name={titleName}=EndAll";

            var _data = Android.Net.Uri.Parse(data);
            resultIntent.SetData(_data);

            var stackBuilder = Android.Support.V4.App.TaskStackBuilder.Create(Application.Context);
            stackBuilder.AddNextIntent(resultIntent);
            var resultPendingIntent =
                stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);


            builder.SetContentIntent(resultPendingIntent);




            if (time != null) {


                //var serializedNotification = SerializeNotification(localNotification);
                //intent.PutExtra(ScheduledAlarmHandler.LocalNotificationKey, serializedNotification);
                var context = MainActivity.activity.ApplicationContext;

                var _resultIntent = new Intent(context, typeof(NotifyAtTime));
                _resultIntent.PutExtra("title", builder.)

                var pending = PendingIntent.GetService(context, 0,
                 _resultIntent,
                //PendingIntentFlags.CancelCurrent
                PendingIntentFlags.UpdateCurrent
                 );




                // var pendingIntent = PendingIntent.GetService()//GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.CancelCurrent);
                var triggerTime = NotifyTimeInMilliseconds((DateTime)time);
                var alarmManager = GetAlarmManager();

                alarmManager.Set(AlarmType.RtcWakeup, triggerTime, pending);

                // builder.SetWhen(CurrentTimeMillis((DateTime)time));
            }
            else {

                _manager.Notify(id, builder.Build());
            }*/
        }


        public void ShowNotIntent(string title, string body, int id, string titleId, string titleName, DateTime? time = null, string bigIconUrl = "")
        {
            ShowNotIntentAsync(title, body, id, titleId, titleName, time, bigIconUrl);
        }

        public const int CHROME_CAST_NOTIFICATION_ID = 1337;

        public static void CancelChromecast()
        {
            _manager.Cancel(CHROME_CAST_NOTIFICATION_ID);
        }

        static MediaSession mediaSession = new MediaSession(Application.Context, "Chromecast");

        public static async void UpdateChromecastNotification(string title, string body, bool isPaused, string poster)
        {
            var builder = new Notification.Builder(Application.Context);
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            builder.SetAutoCancel(true);

            builder.SetSmallIcon(Resource.Drawable.biconWhite);//LocalNotificationIconId);
            builder.SetOngoing(true);


            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) {
                var channelId = $"{_packageName}.general";
                var channel = new NotificationChannel(channelId, "General", NotificationImportance.Default);

                _manager.CreateNotificationChannel(channel);

                builder.SetChannelId(channelId);
                //https://m.media-amazon.com/images/M/MV5BMTczNTI2ODUwOF5BMl5BanBnXkFtZTcwMTU0NTIzMw@@._V1_UX182_CR0,0,182,268_AL_.jpg
                var bitmap = await GetImageBitmapFromUrl(poster);//"https://m.media-amazon.com/images/M/MV5BMTczNTI2ODUwOF5BMl5BanBnXkFtZTcwMTU0NTIzMw@@._V1_UX182_CR0,0,182,268_AL_.jpg");
                if (bitmap != null) {
                    builder.SetLargeIcon(bitmap);
                }
                var context = MainActivity.activity.ApplicationContext;

                builder.SetStyle(new Notification.MediaStyle().SetMediaSession(mediaSession.SessionToken).SetShowActionsInCompactView(0, 1, 2)); // NICER IMAGE

                List<string> actionNames = new List<string>() { "-30s", isPaused ? "Play" : "Pause", "+30s", "Stop" };
                List<int> sprites = new List<int>() { Resource.Drawable.netflixGoBack128, isPaused ? Resource.Drawable.netflixPlay128v2 : Resource.Drawable.netflixPause128v2, Resource.Drawable.netflixGoForward128, Resource.Drawable.netflixStop128v2 };
                List<string> actionIntent = new List<string>() { "goback", isPaused ? "play" : "pause", "goforward", "stop" }; // next

                List<Notification.Action> actions = new List<Notification.Action>();

                for (int i = 0; i < sprites.Count; i++) {
                    var _resultIntent = new Intent(context, typeof(ChromeCastIntentService));
                    _resultIntent.PutExtra("data", actionIntent[i]);
                    var pending = PendingIntent.GetService(context, 2337 + i,
                     _resultIntent,
                    PendingIntentFlags.UpdateCurrent
                     );

                    actions.Add(new Notification.Action(sprites[i], actionNames[i], pending));
                }
                builder.SetActions(actions.ToArray());
            }
            /*
            var resultIntent = GetLauncherActivity();
           // resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

            //var _da = Android.Net.Uri.Parse("cloudstreamforms:tt0371746Name=Iron man=EndAll");
            // resultIntent.SetData(_da);

            var stackBuilder = Android.Support.V4.App.TaskStackBuilder.Create(Application.Context);
           stackBuilder.AddNextIntent(resultIntent);
            var resultPendingIntent =
                stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);


            builder.SetContentIntent(resultPendingIntent);
            */
            _manager.Notify(CHROME_CAST_NOTIFICATION_ID, builder.Build());
        }


        /// <summary>
        /// Show a local notification
        /// </summary>
        /// <param name="title">Title of the notification</param>
        /// <param name="body">Body or description of the notification</param>
        /// <param name="id">Id of the notification</param>
        public async void Show(string title, string body, int id = 0)
        {
            return;
            var builder = new Notification.Builder(Application.Context);
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            builder.SetAutoCancel(true);


            builder.SetSmallIcon(LocalNotificationIconId);



            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) {
                var channelId = $"{_packageName}.general";
                var channel = new NotificationChannel(channelId, "General", NotificationImportance.Default);

                _manager.CreateNotificationChannel(channel);

                builder.SetChannelId(channelId);
                //https://m.media-amazon.com/images/M/MV5BMTczNTI2ODUwOF5BMl5BanBnXkFtZTcwMTU0NTIzMw@@._V1_UX182_CR0,0,182,268_AL_.jpg
                var bitmap = await GetImageBitmapFromUrl("https://m.media-amazon.com/images/M/MV5BMTczNTI2ODUwOF5BMl5BanBnXkFtZTcwMTU0NTIzMw@@._V1_UX182_CR0,0,182,268_AL_.jpg");
                if (bitmap != null) {
                    builder.SetLargeIcon(bitmap);
                }
                var context = MainActivity.activity.ApplicationContext;


                MediaSession mediaSession = new MediaSession(context, "tag");

                builder.SetStyle(new Notification.MediaStyle().SetMediaSession(mediaSession.SessionToken).SetShowActionsInCompactView(0, 1, 2)); // NICER IMAGE


                // mediaSession.SetPlaybackState(PlaybackState.)

                bool isPaused = true;

                List<string> actionNames = new List<string>() { "-30s", isPaused ? "Play" : "Pause", "+30s", "Stop" };
                List<int> sprites = new List<int>() { Resource.Drawable.netflixGoBack128, isPaused ? Resource.Drawable.netflixPlay128v2 : Resource.Drawable.netflixPause128v2, Resource.Drawable.netflixGoForward128, Resource.Drawable.netflixStop128v2 };
                List<string> actionIntent = new List<string>() { "goback", isPaused ? "play" : "pause", "goforward", "stop" }; // next

                List<Notification.Action> actions = new List<Notification.Action>();

                for (int i = 0; i < sprites.Count; i++) {
                    var _resultIntent = new Intent(context, typeof(DemoIntentService));
                    // _resultIntent.SetAction("com.CloudStreamForms.CloudStreamForms.pause");
                    // _resultIntent.AddFlags(ActivityFlags.IncludeStoppedPackages);
                    _resultIntent.PutExtra("data", actionIntent[i]);
                    // _resultIntent.AddFlags(ActivityFlags.ReceiverForeground);

                    //PendingIntent.GetActivity
                    //GetBroadcast
                    //GetService
                    var pending = PendingIntent.GetService(context, 1337 + i,
                     _resultIntent,
                    //PendingIntentFlags.CancelCurrent
                    PendingIntentFlags.UpdateCurrent
                     );

                    actions.Add(new Notification.Action(sprites[i], actionNames[i], pending));
                }
                builder.SetActions(actions.ToArray());

                //builder.SetColorized(true);
                //  builder.SetColor(Resource.Color.colorPrimary);
                /*
                var context = MainActivity.activity.ApplicationContext;
                var _resultIntent = new Intent(context, typeof(MainActivity));
                //_resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

                var da = Android.Net.Uri.Parse("cloudstreamforms:tt0371746Name=Iron man=EndAll");
                _resultIntent.SetData(da);
                _resultIntent.PutExtra("data", da);
                _resultIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

                print("PDATA::::" + _resultIntent.DataString);
                var pending = PendingIntent.GetActivity(context, 0,
                    _resultIntent,
                   //PendingIntentFlags.CancelCurrent
                   PendingIntentFlags.UpdateCurrent
                    );

                // SET AFTER THO 
                //RemoteViews remoteViews = new RemoteViews(Application.Context.PackageName, Resource.Xml.PausePlay);
                // remoteViews.SetImageViewResource(R.id.notifAddDriverIcon, R.drawable.my_trips_new);
                // builder.SetCustomContentView(remoteViews);

                builder.SetShowWhen(false);
                builder.SetContentIntent(pending);
                builder.SetFullScreenIntent(pending, true);
                */

                /*
                Intent notificationIntent = new Intent(context, typeof(MainActivity));
                notificationIntent.PutExtra("NotificationMessage", "YEET");
                notificationIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
                PendingIntent pendingNotificationIntent = PendingIntent.GetActivity(context, 1337, notificationIntent,PendingIntentFlags.UpdateCurrent);

                notification.setLatestEventInfo(getApplicationContext(), notificationTitle, notificationMessage, pendingNotificationIntent);*/
                //  builder.SetProgress(100, 51, false); // PROGRESSBAR
                //  builder.SetLargeIcon(Android.Graphics.Drawables.Icon.CreateWithResource(context, Resource.Drawable.bicon)); // POSTER
                // builder.SetActions(new Notification.Action(Resource.Drawable.design_bottom_navigation_item_background, "Hello", pending)); // IDK TEXT PRESS
            }

            var resultIntent = GetLauncherActivity();
            resultIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

            var _da = Android.Net.Uri.Parse("cloudstreamforms:tt0371746Name=Iron man=EndAll");
            resultIntent.SetData(_da);

            var stackBuilder = Android.Support.V4.App.TaskStackBuilder.Create(Application.Context);
            stackBuilder.AddNextIntent(resultIntent);
            var resultPendingIntent =
                stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);


            builder.SetContentIntent(resultPendingIntent);

            _manager.Notify(id, builder.Build());
        }
        public static Intent GetLauncherActivity()
        {
            var packageName = Application.Context.PackageName;
            return Application.Context.PackageManager.GetLaunchIntentForPackage(packageName);
        }





        private long NotifyTimeInMilliseconds(DateTime notifyTime)
        {
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
            var epochDifference = (new DateTime(1970, 1, 1) - DateTime.MinValue).TotalSeconds;

            var utcAlarmTimeInMillis = utcTime.AddSeconds(-epochDifference).Ticks / 10000;
            return utcAlarmTimeInMillis;
        }





        static bool hidden = false;
        // static int baseShow = 0;

        public void UpdateBackground(int color)
        {
            Window window = MainActivity.activity.Window;
            window.SetNavigationBarColor(Android.Graphics.Color.Rgb(color, color, color));
            /*
            Window window = MainActivity.activity.Window;
            int color = Settings.BlackColor - 5;
            if(color > 255) { color = 255; }
            if(color < 0) { color = 0; }
            window.SetNavigationBarColor(Android.Graphics.Color.Rgb(color, color, color));*/
        }

        public void UpdateStatusBar()
        {
            // Window window = MainActivity.activity.Window;
            ToggleFullscreen(!Settings.HasStatusBar);
            if (Settings.HasStatusBar) {
                ShowStatusBar();
            }
            else {
                HideStatusBar();
            }
            /*
            if (!Settings.HasStatusBar) {
                print("REMOVE STATUS BAR::::");
                window.AddFlags(WindowManagerFlags.Fullscreen); // REMOVES STATUS BAR
            }
            else {
                window.ClearFlags(WindowManagerFlags.Fullscreen); // ADD STATUS BAR
            }*/
        }

        public void ToggleFullscreen(bool fullscreen)
        {
            Window window = MainActivity.activity.Window;

            if (fullscreen) {
                window.AddFlags(WindowManagerFlags.Fullscreen); // REMOVES STATUS BAR
            }
            else {
                window.ClearFlags(WindowManagerFlags.Fullscreen);
            }
        }

        public void ToggleRealFullScreen(bool fullscreen)
        {
            Window window = MainActivity.activity.Window;
            print("TOGGLE" + fullscreen);

            var uiOptions = (int)window.DecorView.SystemUiVisibility;
            // uiOptions |= (int)SystemUiFlags.LowProfile;
            // uiOptions |= (int)SystemUiFlags.Fullscreen;


            //var attrs = window.Attributes;

            if (fullscreen) {
                uiOptions |= (int)SystemUiFlags.HideNavigation;
                //uiOptions |= (int)SystemUiFlags.ImmersiveSticky;
                uiOptions |= (int)SystemUiFlags.Fullscreen;
                //uiOptions |= (int)SystemUiFlags.LayoutStable;
                //uiOptions |= (int)SystemUiFlags.LayoutHideNavigation;
                //uiOptions |= (int)SystemUiFlags.LayoutFullscreen;
                //    uiOptions |= (int)SystemUiFlags.LowProfile;

                window.AddFlags(WindowManagerFlags.TurnScreenOn);
                window.AddFlags(WindowManagerFlags.KeepScreenOn);
                window.AddFlags(WindowManagerFlags.Fullscreen); // REMOVES STATUS BAR

                //   attrs.Flags |= Android.Views.WindowManagerFlags.Fullscreen;

                //   window.AddFlags(WindowManagerFlags.Fullscreen);
                // window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);
            }
            else {
                uiOptions &= ~(int)SystemUiFlags.HideNavigation;
                //     uiOptions &= ~(int)SystemUiFlags.ImmersiveSticky;
                uiOptions &= ~(int)SystemUiFlags.Fullscreen;
                //   uiOptions &= ~(int)SystemUiFlags.LayoutStable;
                //   uiOptions &= ~(int)SystemUiFlags.LayoutHideNavigation;
                //  uiOptions &= ~(int)SystemUiFlags.LayoutFullscreen;
                //   uiOptions &= ~(int)SystemUiFlags.LowProfile;

                window.ClearFlags(WindowManagerFlags.TurnScreenOn);
                window.ClearFlags(WindowManagerFlags.KeepScreenOn);
                window.ClearFlags(WindowManagerFlags.Fullscreen);

                // window.AddFlags(WindowManagerFlags.ForceNotFullscreen);
                // window.ClearFlags(WindowManagerFlags.Fullscreen);

                //  attrs.Flags &= ~Android.Views.WindowManagerFlags.Fullscreen;
            }

            //   window.Attributes = attrs;

            window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;

            if (!fullscreen) {
                UpdateStatusBar();
            }

        }

        public void LandscapeOrientation()
        {
            MainActivity.activity.RequestedOrientation = ScreenOrientation.Landscape;
        }

        public void NormalOrientation()
        {
            MainActivity.activity.RequestedOrientation = ScreenOrientation.Unspecified;
        }

        public void ShowStatusBar()
        {
            //  if (!hidden) return;

            Window window = MainActivity.activity.Window;
            // window.ClearFlags(WindowManagerFlags.TurnScreenOn);
            //window.ClearFlags(WindowManagerFlags.KeepScreenOn);
            //ToggleFullscreen(!Settings.HasStatusBar);

            //if (Settings.HasStatusBar) {
            window.ClearFlags(WindowManagerFlags.Fullscreen);
            //}

            int uiOptions = (int)window.DecorView.SystemUiVisibility;
            //  baseShow = uiOptions;

            //  uiOptions &= ~(int)SystemUiFlags.LowProfile;
            uiOptions &= ~(int)SystemUiFlags.Fullscreen;
            //   uiOptions &= ~(int)SystemUiFlags.HideNavigation;
            uiOptions &= ~(int)SystemUiFlags.ImmersiveSticky;

            window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
        }

        public void HideStatusBar()
        {

            //if (hidden) return;
            hidden = true;

            Window window = MainActivity.activity.Window;
            //  window.AddFlags(WindowManagerFlags.TurnScreenOn);
            // window.AddFlags(WindowManagerFlags.KeepScreenOn);

            if (Settings.HasStatusBar) {
                window.AddFlags(WindowManagerFlags.Fullscreen);
            }

            int uiOptions = (int)window.DecorView.SystemUiVisibility;
            //  baseShow = uiOptions;

            // uiOptions |= (int)SystemUiFlags.LowProfile;
            uiOptions |= (int)SystemUiFlags.Fullscreen;
            // uiOptions |= (int)SystemUiFlags.HideNavigation;
            uiOptions |= (int)SystemUiFlags.ImmersiveSticky;

            window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
            /*
            var activity = (Activity)Forms.Context;
            var window = activity.Window;
            var attrs = window.Attributes;
            attrs.Flags |= Android.Views.WindowManagerFlags.Fullscreen;
            window.Attributes = attrs;

            window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);
            window.AddFlags(WindowManagerFlags.Fullscreen);

            var decorView = window.DecorView;

            var uiOptions =
                (int)Android.Views.SystemUiFlags.LayoutStable |
                (int)Android.Views.SystemUiFlags.LayoutHideNavigation |
                (int)Android.Views.SystemUiFlags.LayoutFullscreen |
                (int)Android.Views.SystemUiFlags.HideNavigation |
                (int)Android.Views.SystemUiFlags.Fullscreen |
                (int)Android.Views.SystemUiFlags.Immersive;

            decorView.SystemUiVisibility = (Android.Views.StatusBarVisibility)uiOptions;

            window.DecorView.SystemUiVisibility = StatusBarVisibility.Hidden;*/
        }
        public StorageInfo GetStorageInformation(string path = "")
        {
            StorageInfo storageInfo = new StorageInfo();

            long totalSpaceBytes = 0;
            long freeSpaceBytes = 0;
            long availableSpaceBytes = 0;

            /*
              We have to do the check for the Android version, because the OS calls being made have been deprecated for older versions. 
              The ‘old style’, pre Android level 18 didn’t use the Long suffixes, so if you try and call use those on 
              anything below Android 4.3, it’ll crash on you, telling you that that those methods are unavailable. 
              http://blog.wislon.io/posts/2014/09/28/xamarin-and-android-how-to-use-your-external-removable-sd-card/
             */
            if (path == "") {
                totalSpaceBytes = Android.OS.Environment.ExternalStorageDirectory.TotalSpace;
                freeSpaceBytes = Android.OS.Environment.ExternalStorageDirectory.FreeSpace;
                availableSpaceBytes = Android.OS.Environment.ExternalStorageDirectory.UsableSpace;
            }
            else {
                StatFs stat = new StatFs(path); //"/storage/sdcard1"

                if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr2) {


                    long blockSize = stat.BlockSizeLong;
                    totalSpaceBytes = stat.BlockCountLong * stat.BlockSizeLong;
                    availableSpaceBytes = stat.AvailableBlocksLong * stat.BlockSizeLong;
                    freeSpaceBytes = stat.FreeBlocksLong * stat.BlockSizeLong;

                }
                else {
                    totalSpaceBytes = (long)stat.BlockCount * (long)stat.BlockSize;
                    availableSpaceBytes = (long)stat.AvailableBlocks * (long)stat.BlockSize;
                    freeSpaceBytes = (long)stat.FreeBlocks * (long)stat.BlockSize;
                }
            }

            storageInfo.TotalSpace = totalSpaceBytes;
            storageInfo.AvailableSpace = availableSpaceBytes;
            storageInfo.FreeSpace = freeSpaceBytes;
            return storageInfo;

        }

        public static void OpenPathAsVideo(string path, string name, string subtitleLoc)
        {
            OpenPathsAsVideo(new List<string>() { path }, new List<string>() { name }, subtitleLoc);
        }
        public bool DeleteFile(string path)
        {
            //Context context = Android.App.Application.Context;
            try {
                Java.IO.File file = new Java.IO.File(path);
                if (file.Exists()) {

                    file.Delete();
                }
                return true;
            }


            catch (Exception) {
                return false;
            }
            /*

            string where = MediaStore.MediaColumns.Data + "=?";
            string[] selectionArgs = new string[] { file.AbsolutePath };
            ContentResolver contentResolver = context.ContentResolver;
            Android.Net.Uri filesUri = MediaStore.Files.GetContentUri("external");

            if (file.Exists()) {
                contentResolver.Delete(filesUri, where, selectionArgs);
            }*/
        }

        static readonly int NOTIFICATION_ID = 1000;
        static readonly string CHANNEL_ID = "location_notification";
        internal static readonly string COUNT_KEY = "count";
        public void Test()
        {

            Show("Test", "test");
            print("HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH");

        }

        static Java.Lang.Thread downloadThread;
        public static void DownloadFromLink(string url, string title, string toast = "", string ending = "", bool openFile = false, string descripts = "")
        {
            print("DOWNLOADING: " + url);

            DownloadManager.Request request = new DownloadManager.Request(Android.Net.Uri.Parse(url));
            request.SetTitle(title);
            request.SetDescription(descripts);
            string mainPath = Android.OS.Environment.DirectoryDownloads;
            string subPath = title + ending;
            string fullPath = mainPath + "/" + subPath;

            print("PATH: " + fullPath);

            request.SetDestinationInExternalPublicDir(mainPath, subPath);
            request.SetVisibleInDownloadsUi(true);
            request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);

            DownloadManager manager;
            manager = (DownloadManager)MainActivity.activity.GetSystemService(Context.DownloadService);

            long downloadId = manager.Enqueue(request);




            // AUTO OPENS FILE WHEN DONE DOWNLOADING
            if (openFile || toast != "") {
                downloadThread = new Java.Lang.Thread(() => {
                    try {
                        bool exists = false;
                        while (!exists) {
                            try {
                                string p = manager.GetUriForDownloadedFile(downloadId).Path;
                                exists = true;
                            }
                            catch (System.Exception) {
                                Java.Lang.Thread.Sleep(100);
                            }

                        }
                        Java.Lang.Thread.Sleep(1000);
                        if (toast != "") {
                            App.ShowToast(toast);
                        }
                        if (openFile) {

                            print("OPEN FILE");
                            //            
                            string truePath = ("file://" + Android.OS.Environment.ExternalStorageDirectory + "/" + fullPath);

                            OpenFile(truePath);
                        }
                    }
                    finally {
                        downloadThread.Join();
                    }
                });
                downloadThread.Start();
            }
        }
        public static void OpenFile(string link)
        {
            //  Android.Net.Uri uri = Android.Net.Uri.Parse(link);//link);
            Java.IO.File file = new Java.IO.File(Java.Net.URI.Create(link));
            print("Path:" + file.Path);

            Android.Net.Uri photoURI = FileProvider.GetUriForFile(MainActivity.activity.ApplicationContext, (MainActivity.activity.ApplicationContext.PackageName + ".provider.FileProvider"), file);
            Intent promptInstall = new Intent(Intent.ActionView).SetDataAndType(photoURI, "application/vnd.android.package-archive"); //vnd.android.package-archive
            promptInstall.AddFlags(ActivityFlags.NewTask);
            promptInstall.AddFlags(ActivityFlags.GrantReadUriPermission);
            promptInstall.AddFlags(ActivityFlags.NoHistory);
            promptInstall.AddFlags(ActivityFlags.ClearWhenTaskReset | ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(promptInstall);
            /*
            Intent promptInstall = new Intent(Intent.ActionView).SetData(uri);//.SetDataAndType(uri, "application/vnd.android.package-archive");
            //   promptInstall.AddFlags(ActivityFlags.NewTask);
            promptInstall.AddFlags(ActivityFlags.GrantReadUriPermission);
            promptInstall.AddFlags(ActivityFlags.GrantWriteUriPermission);
            promptInstall.AddFlags(ActivityFlags.GrantPrefixUriPermission);
            promptInstall.AddFlags(ActivityFlags.GrantPersistableUriPermission);

            promptInstall.AddFlags(ActivityFlags.NewTask);*/


            // Android.App.Application.Context.ApplicationContext.start
            //Android.App.Application.Context.StartService(intent);
            // Android.App.Application.Context.StartActivity(promptInstall);
        }

        public static Java.IO.File WriteFile(string name, string basePath, string write)
        {
            try {
                System.IO.File.Delete(basePath + "/" + name);
            }
            catch (System.Exception) {

            }
            //name = Regex.Replace(name, @"[^A-Za-z0-9\.]+", String.Empty);
            //name.Replace(" ", "");
            //  name = name.ToLower();

            Java.IO.File file = new Java.IO.File(basePath, name);
            Java.IO.File _file = new Java.IO.File(basePath);
            CloudStreamCore.print("PATH: " + basePath + "/" + name);
            _file.Mkdirs();
            file.CreateNewFile();
            Java.IO.FileWriter writer = new Java.IO.FileWriter(file);
            // Writes the content to the file
            writer.Write(write);
            writer.Flush();
            writer.Close();
            return file;
        }


        public static async Task OpenPathsAsVideo(List<string> path, List<string> name, string subtitleLoc)
        {
            string absolutePath = Android.OS.Environment.ExternalStorageDirectory + "/" + Android.OS.Environment.DirectoryDownloads;
            CloudStreamCore.print("AVS: " + absolutePath);

            bool subtitlesEnabled = subtitleLoc != "";
            string writeData = CloudStreamForms.App.ConvertPathAndNameToM3U8(path, name, subtitlesEnabled, "content://" + absolutePath + "/");
            Java.IO.File subFile = null;
            WriteFile(CloudStreamForms.App.baseM3u8Name, absolutePath, writeData);
            if (subtitlesEnabled) {
                subFile = WriteFile(CloudStreamForms.App.baseSubtitleName, absolutePath, subtitleLoc);
            }

            // await Task.Delay(5000);

            Device.BeginInvokeOnMainThread(() => {
                // OpenPathAsVideo(path.First(), name.First(), "");
                OpenVlcIntent(absolutePath + "/" + CloudStreamForms.App.baseM3u8Name, absolutePath + "/" + App.baseSubtitleName);
            });
        }




        public static void OpenVlcIntent(string path, string subfile = "") //Java.IO.File subFile)
        {
            Android.Net.Uri uri = Android.Net.Uri.Parse(path);

            Intent intent = new Intent(Intent.ActionView).SetDataAndType(uri, "video/*");
            //intent.SetPackage("org.videolan.vlc");
            // Main.print("Da_ " + Android.Net.Uri.Parse(subfile));

            if (subfile != "") {
                var sfile = Android.Net.Uri.FromFile(new Java.IO.File(subfile));  //"content://" + Android.Net.Uri.Parse(subfile);
                                                                                  //  print(sfile.Path);
                intent.PutExtra("subtitles_location", sfile);//Android.Net.Uri.FromFile(subFile));
                                                             // intent.PutExtra("subtitles_location", );//Android.Net.Uri.FromFile(subFile));
            }

            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.AddFlags(ActivityFlags.GrantWriteUriPermission);
            intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);
            intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);

            intent.AddFlags(ActivityFlags.NewTask);


            // Android.App.Application.Context.ApplicationContext.start
            //Android.App.Application.Context.StartService(intent);
            Android.App.Application.Context.StartActivity(intent);
        }

        public void PlayVlc(string url, string name, string subtitleLoc)
        {
            try {
                MainDroid.OpenPathAsVideo(url, name, subtitleLoc);
            }
            catch (Exception) {
                CloudStreamForms.App.OpenBrowser(url);
            }
        }
        public void PlayVlc(List<string> url, List<string> name, string subtitleLoc)
        {
            try {
                MainDroid.OpenPathsAsVideo(url, name, subtitleLoc);
            }
            catch (Exception) {
                CloudStreamForms.App.OpenBrowser(url.First());
            }

        }
        public void Awake()
        {
            App.platformDep = this;
        }

        public void ShowToast(string message, double duration)
        {
            Device.BeginInvokeOnMainThread(() => {
                ToastLength toastLength = ToastLength.Short;
                if (duration >= 3) {
                    toastLength = ToastLength.Long;
                }
                Toast.MakeText(Android.App.Application.Context, message, toastLength).Show();
            });

        }



        public string DownloadFile(string file, string fileName, bool mainPath, string extraPath)
        {
            return WriteFile(CensorFilename(fileName), GetPath(mainPath, extraPath), file).Path;
        }



        public string DownloadAdvanced(int id, string url, string fileName, string titleName, bool mainPath, string extraPath, bool showNotification = true, bool showNotificationWhenDone = true, bool openWhenDone = false, string poster = "", string beforeTxt = "")
        {
            var context = MainActivity.activity.ApplicationContext;
            Intent downloadIntent = new Intent(context, typeof(DownloadUrlService));

            //string title = fileName;
            string path = GetPath(mainPath, extraPath);

            downloadIntent.PutExtra("id", id);
            downloadIntent.PutExtra("url", url);
            downloadIntent.PutExtra("title", titleName);
            downloadIntent.PutExtra("file", fileName);
            downloadIntent.PutExtra("path", path);
            downloadIntent.PutExtra("not", showNotification);
            downloadIntent.PutExtra("notdone", showNotificationWhenDone);
            downloadIntent.PutExtra("opendone", openWhenDone);
            downloadIntent.PutExtra("poster", poster);
            downloadIntent.PutExtra("beforeTxt", beforeTxt);
            DownloadHandle.HandleIntent(downloadIntent);
            // MainActivity.activity.StartForegroundService(downloadIntent);
            //  activity.BindService(downloadIntent, f.Class , Bind.AutoCreate);
            // Application.Context.SendBroadcast(downloadIntent);
            /*
            var pending = PendingIntent.GetBroadcast(context, id,
                  downloadIntent,
                 PendingIntentFlags.UpdateCurrent
                  );




            var triggerTime = CurrentTimeMillis(DateTime.UtcNow);// NotifyTimeInMilliseconds((DateTime)time);
            var alarmManager = GetAlarmManager();

            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pending);*/

            // MainActivity.activity.SendBroadcast(downloadIntent);
            return path + "/" + CensorFilename(fileName);
        }

        public string DownloadUrl(string url, string fileName, bool mainPath, string extraPath, string toast = "", bool isNotification = false, string body = "")
        {
            try {

                string basePath = GetPath(mainPath, extraPath);
                CloudStreamCore.print(basePath);
                Java.IO.File _file = new Java.IO.File(basePath);

                _file.Mkdirs();
                basePath += "/" + CensorFilename(fileName);
                CloudStreamCore.print(basePath);
                //webClient.DownloadFile(url, basePath);
                using (WebClient wc = new WebClient()) {
                    wc.DownloadProgressChanged += (o, e) => {

                        App.OnDownloadProgressChanged(basePath, e);

                        /*
                        if (e.ProgressPercentage == 100) {
                            App.ShowToast("Download Successful");
                            //OpenFile(basePath);
                        }*/
                        // print(e.ProgressPercentage + "|" + basePath);
                    };
                    wc.DownloadFileCompleted += (o, e) => {
                        if (toast != "") {
                            if (isNotification) {
                                App.ShowNotification(toast, body);
                            }
                            else {
                                App.ShowToast(toast);
                            }
                        }
                    };
                    wc.DownloadFileAsync(
                        // Param1 = Link of file
                        new System.Uri(url),
                        // Param2 = Path to save
                        basePath
                    );
                }

            }
            catch (Exception) {
                App.ShowToast("Download Failed");
                return "";
            }

            return GetPath(mainPath, extraPath) + "/" + CensorFilename(fileName);
        }




        public string GetBuildNumber()
        {
            var context = Android.App.Application.Context;
            var VersionNumber = context.PackageManager.GetPackageInfo(context.PackageName, PackageInfoFlags.MetaData).VersionName;
            var BuildNumber = context.PackageManager.GetPackageInfo(context.PackageName, PackageInfoFlags.MetaData).VersionCode.ToString();
            return BuildNumber + " " + VersionNumber;
        }

        public void DownloadUpdate(string update)
        {
            string downloadLink = "https://github.com/LagradOst/CloudStream-2/releases/download/" + update + "/com.CloudStreamForms.CloudStreamForms.apk";
            App.ShowToast("Download started!");
            //  DownloadUrl(downloadLink, "com.CloudStreamForms.CloudStreamForms.apk", true, "", "Download complete!");
            DownloadFromLink(downloadLink, "com.CloudStreamForms.CloudStreamForms.apk", "Download complete!", "", false, "");

        }

        public string GetDownloadPath(string path, string extraFolder)
        {
            return GetPath(true, extraFolder + "/" + CensorFilename(path, false));
        }

        public string GetExternalStoragePath()
        {
            return Android.OS.Environment.ExternalStorageDirectory.Path;
        }

        public int ConvertDPtoPx(int dp)
        {
            return (int)(dp * MainActivity.activity.ApplicationContext.Resources.DisplayMetrics.Density);
        }

        public void CancelNot(int id)
        {
            CancelFutureNotification(id);
        }
    }
}
