﻿#define DEBUG_SESSION

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Phone.Info;
using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using Ocell.Library;
using Ocell.Library.Notifications;
using Ocell.Library.Tasks;
using Ocell.Library.Twitter;
using TweetSharp;



namespace Ocell.BackgroundAgent
{
    public class ScheduledAgent : ScheduledTaskAgent
    {
        private static volatile bool _classInitialized;

        /// <remarks>
        /// Constructor de ScheduledAgent que inicializa el controlador UnhandledException
        /// </remarks>
        public ScheduledAgent()
        {
            if (!_classInitialized)
            {
                _classInitialized = true;
                // Suscribirse al controlador de excepciones administradas
                Deployment.Current.Dispatcher.BeginInvoke(delegate
                {
                    Application.Current.UnhandledException += ScheduledAgent_UnhandledException;
                });
            }

            _threads = 0;
        }

        /// Código para ejecutar en excepciones no controladas
        private void ScheduledAgent_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // Se ha producido una excepción no controlada; interrumpir el depurador
                System.Diagnostics.Debugger.Break();
            }
        }

        EventWaitHandle _waitHandle = new AutoResetEvent(false);
        int _threads;

        private void SignalThreadStart()
        {
            Interlocked.Increment(ref _threads);
        }

        private void SignalThreadEnd()
        {
            Interlocked.Decrement(ref _threads);
            if (_threads <= 0)
                _waitHandle.Set();
        }

        bool IsMemoryUsageHigh()
        {
            double highPercentage = 0.95;
            double highMemory = DeviceStatus.ApplicationMemoryUsageLimit * highPercentage;
            return DeviceStatus.ApplicationCurrentMemoryUsage > highMemory;
        }

        void TerminateAll()
        {
            // Allow the scheduled agent to terminate without taking into account other threads.
            _threads = 0;
            _waitHandle.Set();
        }

        /// <summary>
        /// Agente que ejecuta una tarea programada
        /// </summary>
        /// <param name="task">
        /// Tarea invocada
        /// </param>
        /// <remarks>
        /// Se llama a este método cuando se invoca una tarea periódica o con uso intensivo de recursos
        /// </remarks>
        protected override void OnInvoke(ScheduledTask task)
        {
            DateTime start, end;
            start = DateTime.Now;
            SignalThreadStart();
            DebugWriter.Add("");
            try
            {
                DoWork();
            }
            catch (Exception)
            {
                TerminateAll();
            }

            SignalThreadEnd();

            end = DateTime.Now;
            int maxTimeout = 24000 - (end - start).Milliseconds;

            _waitHandle.WaitOne(maxTimeout);
            WriteMemUsage("Exit with " + _threads.ToString() + " running");
            DebugWriter.Save();
            NotifyComplete();
        }

        string contents = "";
        void WriteMemUsage(string message)
        {
#if DEBUG_SESSION
            long used = DeviceStatus.ApplicationCurrentMemoryUsage / 1024;
            long percentage = DeviceStatus.ApplicationCurrentMemoryUsage*100 / DeviceStatus.ApplicationMemoryUsageLimit;
            string toWrite = string.Format("{3}: {0} - {1} KB ({2}% of available memory)", 
                message, used, percentage, DateTime.Now.ToString("HH:mm:ss.ff"));
            Debug.WriteLine(toWrite);
            DebugWriter.Add(toWrite);
#endif
        }

        void DoWork()
        {
            WriteMemUsage("Scheduled agent started");

            SendScheduledTweets();

            WriteMemUsage("Scheduled tweets sent");
            if (IsMemoryUsageHigh())
            {
                Thread.Sleep(100);
                Config.Dispose();
                GC.Collect();
                WriteMemUsage("Disposed memory");
                if (IsMemoryUsageHigh())
                    return;
            }

            NotifyMentionsAndMessages();
            WriteMemUsage("Checked for mentions and messages");
            if (IsMemoryUsageHigh())
            {                
                Thread.Sleep(100);
                Config.Dispose();
                GC.Collect();
                WriteMemUsage("Disposed memory");
                if (IsMemoryUsageHigh())
                    return;
            }

            UpdateTiles();
            WriteMemUsage("Tiles updated");
        }

        void SendScheduledTweets()
        {
            var copyList = new List<TwitterStatusTask>(Config.TweetTasks);
            foreach (var task in copyList)
            {
                Config.TweetTasks.Remove(task);
                task.Completed += (sender, e) => SignalThreadEnd();
                task.Error += (sender, e) =>
                    {
                        Config.TweetTasks.Add(task);
                        Config.SaveTasks();
                        SignalThreadEnd();
                    };
                SignalThreadStart();
                task.Execute();
            }
            Config.SaveTasks();
        }

        int notifications;
        bool newMessages, newMentions;
        string from;
        List<string> usersWithNotifications;

        void CreateToast(string type, int count, string from, string to)
        {
            if (count > 1)
                type += "s";

            ShellToast toast = new ShellToast();
            toast.NavigationUri = new Uri("/MainPage.xaml", UriKind.Relative);
            toast.Title = to;

            if (count == 1)
                toast.Content = string.Format("New {0} from @{1}", type, from);
            else
                toast.Content = string.Format("{0} new {1}", count, type);

            toast.Show();
        }

        void BuildTile()
        {
            StandardTileData mainTile = new StandardTileData();
            if (notifications == 1)
            {
                if (newMentions)
                    mainTile.BackContent = string.Format((char)8203+"@{0} mentioned you (@{1})", from, usersWithNotifications[0]);
                else
                    mainTile.BackContent = string.Format((char)8203+"@{0} sent you (@{1}) a message", from, usersWithNotifications[0]);
            }
            else
            {
                string content = "new ";

                if (newMentions)
                {
                    content += "mentions";
                    if (newMessages)
                        content += " and messages";
                }
                else if (newMessages)
                    content += "messages";

                content += " for " + GetChainOfNames(usersWithNotifications);
                mainTile.BackContent = content;
            }
            mainTile.Count = notifications;
            ShellTile.ActiveTiles.First().Update(mainTile);
        }

        string GetChainOfNames(List<string> names)
        {
            string content = "";
            if (names == null || !names.Any())
                return content;

            int i = 0;
            content += names[i];
            i++;          

            for (; i < names.Count - 1; i++)
                content += ", " + names[i];

            if (i == names.Count - 1)
                content += " and " + names[i];

            return content;
        }

        void NotifyMentionsAndMessages()
        {
            usersWithNotifications = new List<string>();
            notifications = 0;
            from = "";
            newMessages = false;
            newMentions = false;

            foreach (var user in Config.Accounts)
            {
                if (user.Preferences.MentionsPreferences == NotificationType.TileAndToast
                    || user.Preferences.MentionsPreferences == NotificationType.Tile)
                {
                    SignalThreadStart();
                    ServiceDispatcher.GetService(user).ListTweetsMentioningMe(10, (statuses, response) => 
                        {
                            WriteMemUsage("Received mentions");
                            if (statuses == null || response.StatusCode != System.Net.HttpStatusCode.OK)
                            {
                                WriteMemUsage("Mentions: exit with error.");
                                SignalThreadEnd();
                                return;
                            }

                            var CheckDate = DateSync.GetLastCheckDate();
                            int newStatuses = statuses.Where(item => item.CreatedDate > CheckDate).Count();

                            if (newStatuses > 0)
                            {
                                newMentions = true;

                                if (newStatuses == 1)
                                    from = statuses.Where(item => item.CreatedDate > CheckDate).First().AuthorName;

                                if (!usersWithNotifications.Contains(user.ScreenName))
                                    usersWithNotifications.Add(user.ScreenName);

                                notifications += newStatuses;

                                if (user.Preferences.MentionsPreferences == NotificationType.TileAndToast)
                                    CreateToast("mention", newStatuses, from, user.ScreenName);

                                BuildTile();
                                     
                            }

                            WriteMemUsage("Mentions: Exit with " + newStatuses.ToString() + " new statuses.");
                            SignalThreadEnd();  
                        });
                }
                if (user.Preferences.MessagesPreferences == NotificationType.TileAndToast
                    || user.Preferences.MessagesPreferences == NotificationType.Tile)
                {
                    SignalThreadStart();
                    ServiceDispatcher.GetService(user).ListDirectMessagesReceived(10, (statuses, response) =>
                    {
                        WriteMemUsage("Received messages");
                        if (statuses == null || response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            WriteMemUsage("Messages: exit with error.");
                            SignalThreadEnd();
                            return;
                        }

                        var CheckDate = DateSync.GetLastCheckDate();
                        int newStatuses = statuses.Where(item => item.CreatedDate > CheckDate).Count();

                        if (newStatuses > 0)
                        {
                            newMessages = true;

                            if (newStatuses == 1)
                                from = statuses.Where(item => item.CreatedDate > CheckDate).First().AuthorName;

                            if (!usersWithNotifications.Contains(user.ScreenName))
                                usersWithNotifications.Add(user.ScreenName);

                            notifications += newStatuses;

                            if (user.Preferences.MessagesPreferences == NotificationType.TileAndToast)
                                CreateToast("message", newStatuses, from, user.ScreenName);

                            BuildTile();
                            
                        }

                        WriteMemUsage("Messages: Exit with " + newStatuses.ToString() + " new statuses.");
                        SignalThreadEnd();
                    });
                }
            }
        }

        void UpdateTiles()
        {
            if (Config.BackgroundLoadColumns == true)
            {
                foreach (var column in FindColumnsToUpdate())
                    UpdateColumn(column);
            }
        }

        protected void UpdateColumn(TwitterResource Resource)
        {
            TweetLoader Loader = new TweetLoader(Resource, false);
            Loader.Cached = false;
            Loader.TweetsToLoadPerRequest = 1;
            Loader.LoadFinished += (sender, e) =>
            {
                WriteMemUsage("Received tweet for column.");
                if (!Loader.Source.Any())
                {
                    SignalThreadEnd();
                    return;
                }

                string TileString = Uri.EscapeDataString(Loader.Resource.String);
                ITweetable Tweet = Loader.Source[0];
                ShellTile Tile = ShellTile.ActiveTiles.FirstOrDefault(item => item.NavigationUri.ToString().Contains(TileString));

                Loader.Dispose();

                if (Tile != null)
                {
                    StandardTileData TileData = new StandardTileData
                    {
                        BackContent = RemoveMention(Tweet.Text),
                        BackTitle = Tweet.Author.ScreenName
                    };
                    Tile.Update(TileData);
                }
                SignalThreadEnd();
            };
            Loader.Error += (e) => SignalThreadEnd();

            SignalThreadStart();
            Loader.Load();
        }

        private IEnumerable<TwitterResource> FindColumnsToUpdate()
        {
            string column;
            string url;
            int paramIndex;
            TwitterResource Resource;
            foreach (var tile in ShellTile.ActiveTiles)
            {
                url = tile.NavigationUri.ToString();
                paramIndex = url.IndexOf("column=");
                if (paramIndex != -1)
                {
                    column = Uri.UnescapeDataString(url.Substring(url.IndexOf("=") + 1));
                    Resource = Config.Columns.FirstOrDefault(item => item.String == column);
                    if (Resource != null && Resource.String == column)
                        yield return Resource;
                }
            }
        }

        private string RemoveMention(string Tweet)
        {
            if (Tweet[0] == '@')
                Tweet = (char)8203 + Tweet;
            return Tweet;
        }
    }
}