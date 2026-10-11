using System;
using System.Collections;
using Casino.Core;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace Casino.Platform
{
    /// <summary>
    /// Local reminders on Android (Unity Mobile Notifications): "your free chips are ready", and on the evening a streak
    /// would end, a nudge to keep it. Never more than these two are scheduled, Android asks the player's permission
    /// first, and they can be switched off in Settings. They are scheduled when the game goes to the background and
    /// cleared when it comes back, so none ever arrives while playing.
    /// </summary>
    public static class Reminders
    {
        const string Channel = "lucky_ace_reminders";
        const int BonusId = 1001, StreakId = 1002;
        const int EveningHour = 19;

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool initialized;

        static bool Init()
        {
            if (initialized) return true;
            if (!AndroidNotificationCenter.Initialize()) return false;
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, "Reminders",
                "Free chips ready and streak reminders", Importance.Default));
            initialized = true;
            return true;
        }
#endif

        public static bool Supported =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Asks Android for permission to post (Android 13+ shows its own dialog); calls back with the answer.</summary>
        public static IEnumerator AskPermission(Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Init())
            {
                done?.Invoke(false);
                yield break;
            }
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) yield return null;
            done?.Invoke(request.Status == PermissionStatus.Allowed);
#else
            done?.Invoke(false);
            yield break;
#endif
        }

        /// <summary>The game is going to the background: schedule what's due.</summary>
        public static void Schedule(App app)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Cancel();
            var save = app.Save;
            if (!save.remindersEnabled || !Init() || AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed) return;
            var p = app.Progress;
            var now = DateTime.Now;

            var bonusAt = new DateTime(save.bonusReadyUtcTicks, DateTimeKind.Utc).ToLocalTime();
            if (bonusAt > now.AddMinutes(1))
                Send(BonusId, "Your free chips are ready", $"{ChipFormat.Full(p.BonusAmount)} chips are waiting for you at Lucky Ace.", bonusAt);

            int streak = p.Streak.Count;
            if (streak > 0)
            {
                // The evening of the first day that still needs playing: today, or tomorrow if today is done.
                var day = p.Streak.DoneToday(p.Day) ? now.Date.AddDays(1) : now.Date;
                var at = day.AddHours(EveningHour);
                if (at > now.AddMinutes(5))
                    Send(StreakId, $"Keep your {streak}-day streak", $"Play {p.Streak.DailyGoal} rounds today so it doesn't reset at midnight.", at);
            }
#endif
        }

        /// <summary>Back in the game: nothing should pop up while playing.</summary>
        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Init()) return;
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void Send(int id, string title, string text, DateTime at) =>
            AndroidNotificationCenter.SendNotificationWithExplicitID(new AndroidNotification(title, text, at), Channel, id);
#endif
    }
}
