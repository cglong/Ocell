﻿using BufferAPI;
using DanielVaughan.Windows;
using Hammock;
using Microsoft.Phone.Tasks;
using Ocell.Library;
using Ocell.Library.Tasks;
using Ocell.Library.Twitter;
using Ocell.Localization;
using Sharplonger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Net;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using TweetSharp;

namespace Ocell.Pages
{
    public class NewTweetModel : ExtendedViewModelBase
    {
        #region Fields
        IEnumerable<UserToken> accountList;
        public IEnumerable<UserToken> AccountList
        {
            get { return accountList; }
            set { Assign("AccountList", ref accountList, value); }
        }

        bool isDM;
        public bool IsDM
        {
            get { return isDM; }
            set { Assign("IsDM", ref isDM, value); }
        }

        string tweetText;
        public string TweetText
        {
            get { return tweetText; }
            set { Assign("TweetText", ref tweetText, value); }
        }

        int remainingChars;
        public int RemainingChars
        {
            get { return remainingChars; }
            set { Assign("RemainingChars", ref remainingChars, value); }
        }

        string remainingCharsStr;
        public string RemainingCharsStr
        {
            get { return remainingCharsStr; }
            set { Assign("RemainingCharsStr", ref remainingCharsStr, value); }
        }

        Brush remainingCharsColor;
        public Brush RemainingCharsColor
        {
            get { return remainingCharsColor; }
            set { Assign("RemainingCharsColor", ref remainingCharsColor, value); }
        }

        bool usesTwitlonger;
        public bool UsesTwitlonger
        {
            get { return usesTwitlonger; }
            set { Assign("UsesTwitlonger", ref usesTwitlonger, value); }
        }

        bool isScheduled;
        public bool IsScheduled
        {
            get { return isScheduled; }
            set { Assign("IsScheduled", ref isScheduled, value); }
        }

        DateTime scheduledDate;
        public DateTime ScheduledDate
        {
            get { return scheduledDate; }
            set { Assign("ScheduledDate", ref scheduledDate, value); }
        }

        DateTime scheduledTime;
        public DateTime ScheduledTime
        {
            get { return scheduledTime; }
            set { Assign("ScheduledTime", ref scheduledTime, value); }
        }

        bool sendingDM;
        public bool SendingDM
        {
            get { return sendingDM; }
            set { Assign("SendingDM", ref sendingDM, value); }
        }

        IList selectedAccounts;
        public IList SelectedAccounts
        {
            get { return selectedAccounts; }
            set { Assign("SelectedAccounts", ref selectedAccounts, value); }
        }

        bool isGeotagged;
        public bool IsGeotagged
        {
            get { return isGeotagged; }
            set { Assign("IsGeotagged", ref isGeotagged, value); }
        }

        bool geotagEnabled;
        public bool GeotagEnabled
        {
            get { return geotagEnabled; }
            set { Assign("GeotagEnabled", ref geotagEnabled, value); }
        }

        bool isSuggestingUsers;
        public bool IsSuggestingUsers
        {
            get { return isSuggestingUsers; }
            set { Assign("IsSuggestingUsers", ref isSuggestingUsers, value); }
        }

        public SafeObservable<string> Suggestions
        {
            get
            {
                if (Completer != null)
                    return Completer.Suggestions;
                else
                    return new SafeObservable<string>();
            }
        }

        Autocompleter completer;
        public Autocompleter Completer
        {
            get { return completer; }
            set { Assign("Completer", ref completer, value); }
        }
        #endregion

        #region Commands
        DelegateCommand sendTweet;
        public ICommand SendTweet
        {
            get { return sendTweet; }
        }

        DelegateCommand scheduleTweet;
        public ICommand ScheduleTweet
        {
            get { return scheduleTweet; }
        }

        DelegateCommand saveDraft;
        public ICommand SaveDraft
        {
            get { return saveDraft; }
        }

        DelegateCommand selectImage;
        public ICommand SelectImage
        {
            get { return selectImage; }
        }

        DelegateCommand sendWithBuffer;
        public ICommand SendWithBuffer
        {
            get { return sendWithBuffer; }
        }
        #endregion

        GeoCoordinateWatcher geoWatcher = new GeoCoordinateWatcher();
        int requestsLeft;

        public NewTweetModel()
            : base("NewTweet")
        {
            SelectedAccounts = new List<object>();
            AccountList = Config.Accounts.ToList();
            IsGeotagged = Config.EnabledGeolocation == true &&
                (Config.TweetGeotagging == true || Config.TweetGeotagging == null);
            GeotagEnabled = Config.EnabledGeolocation == true;

            SetupCommands();

            this.PropertyChanged += (sender, e) =>
            {
                switch (e.PropertyName)
                {
                    case "IsLoading":
                        RaiseExecuteChanged();
                        break;
                    case "SelectedAccounts":
                        RaiseExecuteChanged();
                        break;
                    case "TweetText":
                        SetRemainingChars();
                        break;
                    case "UsesTwitlonger":
                        RaiseExecuteChanged();
                        break;
                    case "IsGeotagged":
                        Config.TweetGeotagging = IsGeotagged;
                        break;
                    case "Completer":
                        UpdateAutocompleter();
                        break;
                }
            };

            IsDM = DataTransfer.ReplyingDM;

            // Avoid that ugly 01/01/0001 by default.
            var date = DateTime.Now.AddHours(1);
            ScheduledDate = date;
            ScheduledTime = date;

            TryLoadDraft();

            if (Config.EnabledGeolocation == true)
                geoWatcher.Start();
        }



        const int ShortUrlLength = 20;
        Brush redBrush = new SolidColorBrush(Colors.Red);
        void SetRemainingChars()
        {
            var txtLen = TweetText == null ? 0 : TweetText.Length;

            foreach (var url in GetUrls(TweetText))
                if (url.Length > ShortUrlLength)
                    txtLen -= url.Length - ShortUrlLength;

            RemainingChars = 140 - txtLen;

            if (RemainingChars >= 0)
            {
                RemainingCharsStr = RemainingChars.ToString();
                RemainingCharsColor = App.Current.Resources["PhoneSubtleBrush"] as Brush;
                UsesTwitlonger = false;
            }
            else if (RemainingChars >= -10)
            {
                RemainingCharsStr = RemainingChars.ToString();
                UsesTwitlonger = true;
                RemainingCharsColor = redBrush;
            }
            else
            {
                RemainingCharsStr = "Twitlonger";
                UsesTwitlonger = true;
                RemainingCharsColor = App.Current.Resources["PhoneSubtleBrush"] as Brush;
            }
        }

        public void TryLoadDraft()
        {
            TwitterDraft draft = DataTransfer.Draft;
            if (draft != null)
            {
                TweetText = draft.Text;

                if (draft.Scheduled != null)
                {
                    IsScheduled = true;
                    ScheduledTime = draft.Scheduled.GetValueOrDefault();
                    ScheduledDate = draft.Scheduled.GetValueOrDefault();
                }
            }
            else
            {
                TweetText = DataTransfer.Text == null ? "" : DataTransfer.Text;
            }
        }


        bool commandsSet = false;
        void SetupCommands()
        {
            sendTweet = new DelegateCommand(Send, (param) => (RemainingChars >= 0 || UsesTwitlonger) && SelectedAccounts.Count > 0 && !IsLoading);
            scheduleTweet = new DelegateCommand(Schedule, (param) => (RemainingChars >= 0 || UsesTwitlonger) && SelectedAccounts.Count > 0 && !IsLoading);
            selectImage = new DelegateCommand(StartImageChooser, (param) => SelectedAccounts.Count > 0 && !IsLoading);
            saveDraft = new DelegateCommand(SaveAsDraft, (param) => !IsLoading);
            sendWithBuffer = new DelegateCommand(SendBufferUpdate, (param) => !IsLoading && SelectedAccounts.Count > 0);

            commandsSet = true;
        }

        void RaiseExecuteChanged()
        {
            if (!commandsSet)
                return;

            sendTweet.RaiseCanExecuteChanged();
            scheduleTweet.RaiseCanExecuteChanged();
            selectImage.RaiseCanExecuteChanged();
            saveDraft.RaiseCanExecuteChanged();
            sendWithBuffer.RaiseCanExecuteChanged();
        }

        void AskBufferLogin()
        {
            var result = MessageService.AskYesNoQuestion(Resources.NoBufferConfigured);

            if (result)
            {
                Ocell.Settings.OAuth.Type = Ocell.Settings.AuthType.Buffer;
                Navigate(Uris.LoginPage);
            }
        }

        void SendBufferUpdate(object param)
        {
            if (!TrialInformation.IsFullFeatured)
            {
                TrialInformation.ShowBuyDialog();
                return;
            }

            if (Config.BufferProfiles.Count == 0)
            {
                AskBufferLogin();
                return;
            }

            List<string> profiles = new List<string>();

            foreach (var account in SelectedAccounts.Cast<UserToken>())
            {
                var profile = Config.BufferProfiles.Where(x => x.ServiceUsername == account.ScreenName).FirstOrDefault();

                if (profile != null)
                    profiles.Add(profile.Id);
            }

            var service = ServiceDispatcher.GetBufferService();

            if (service != null)
            {
                IsLoading = true;
                service.PostUpdate(TweetText, profiles, ReceiveBufferResponse);
            }
        }

        void Send(object param)
        {
            if (!CheckProtectedAccounts())
                return;

            requestsLeft = 0;

            if (SelectedAccounts.Count == 0)
            {
                MessageService.ShowError(Resources.SelectAccount);
                return;
            }

            BarText = Resources.SendingTweet;
            IsLoading = true;

            if (IsDM)
            {
                SendDirectMessage();
            }
            else
            {

                if (UsesTwitlonger)
                {
                    if (!EnsureTwitlonger())
                    {
                        IsLoading = false;
                        return;
                    }

                    BarText = Resources.UploadingTwitlonger;
                    foreach (UserToken account in SelectedAccounts.Cast<UserToken>())
                    {
                        ServiceDispatcher.GetTwitlongerService(account).PostUpdate(TweetText, ReceiveTLResponse);
                        requestsLeft++;
                    }
                }
                else
                {
                    foreach (UserToken account in SelectedAccounts.Cast<UserToken>())
                        SendTweetToTwitter(TweetText, account);
                }
            }

            if (DataTransfer.Draft != null)
            {
                if (Config.Drafts.Contains(DataTransfer.Draft))
                    Config.Drafts.Remove(DataTransfer.Draft);

                DataTransfer.Draft = null;
                Config.SaveDrafts();
            }
        }

        private async void SendDirectMessage()
        {
            var service = ServiceDispatcher.GetService(DataTransfer.CurrentAccount);
            var response = await service.SendDirectMessageAsync(new SendDirectMessageOptions { UserId = (int)DataTransfer.DMDestinationId, Text = TweetText });

            IsLoading = false;
            BarText = "";

            if (response.StatusCode == HttpStatusCode.Forbidden)
                MessageService.ShowError(Resources.ErrorDuplicateTweet);
            else if (response.StatusCode != HttpStatusCode.OK)
                MessageService.ShowError(Resources.ErrorMessage);
            else
            {
                TweetText = "";
                DataTransfer.Text = "";
                GoBack();
                DataTransfer.ReplyingDM = false;
            }
        }

        public void ReceiveBufferResponse(BufferUpdateCreation updates, BufferResponse response)
        {
            IsLoading = false;
            if (response.StatusCode != HttpStatusCode.OK || updates == null || !updates.Success)
            {
                MessageService.ShowError(Resources.ErrorCreatingBuffer);
                return;
            }

            TweetText = "";
            DataTransfer.Text = "";
            MessageService.ShowMessage(Resources.BufferUpdateSent);
            GoBack();
        }

        bool EnsureTwitlonger()
        {
            return MessageService.AskOkCancelQuestion(Resources.AskTwitlonger);
        }

        object dicLock = new object();
        Dictionary<string, string> TwitlongerIds = new Dictionary<string, string>();

        void ReceiveTLResponse(TwitlongerPost post, TwitlongerResponse response)
        {
            requestsLeft--;

            if (response.StatusCode != HttpStatusCode.OK || post == null || post.Post == null || string.IsNullOrEmpty(post.Post.Content) || response.Sender == null)
            {
                IsLoading = false;
                MessageService.ShowError(Resources.ErrorCreatingTwitlonger);
                return;
            }

            BarText = Resources.SendingTweet;

            string name = response.Sender.Username;

            var account = Config.Accounts.FirstOrDefault(x => x.ScreenName == name);

            if (account == null)
            {
                IsLoading = false;
                MessageService.ShowError(Resources.ErrorCreatingTwitlonger);
                return;
            }

            try
            {
                lock (dicLock)
                    TwitlongerIds.Add(name, post.Post.Id);
            }
            catch
            {
                // TODO: Sometimes, this gives a weird OutOfRange exception. Don't know why, investigate it.
            }

            SendTweetToTwitter(post.Post.Content, account);
        }

        private async void SendTweetToTwitter(string post, UserToken account)
        {
            double? latitude = null, longitude = null;

            if (IsGeotagged)
            {
                GeoCoordinate location = IsGeotagged ? geoWatcher.Position.Location : null;
                latitude = location.Latitude;
                longitude = location.Longitude;
            }

            var sendOptions = new SendTweetOptions
            {
                Status = post,
                InReplyToStatusId = DataTransfer.ReplyId,
                Lat = latitude,
                Long = longitude
            };

            requestsLeft++;

            var response = await ServiceDispatcher.GetService(account).SendTweetAsync(sendOptions);
            var status = response.Content;

            requestsLeft--;

            if (requestsLeft <= 0)
                IsLoading = false;

            if (response == null)
                MessageService.ShowError(Resources.Error);
            else if (response.StatusCode == HttpStatusCode.Forbidden)
                MessageService.ShowError(Resources.ErrorDuplicateTweet);
            else if (!response.RequestSucceeded)
            {
                var errorMsg = response.Error != null ? response.Error.Message : "";
                MessageService.ShowError(String.Format("{0}: {1} ({2})", Resources.ErrorMessage, errorMsg, response.StatusCode));
            }
            else
            {
                TryAssociateWithTLId(status.Author.ScreenName, status.Id);
                if (requestsLeft <= 0)
                {
                    TweetText = "";
                    DataTransfer.Text = "";
                    GoBack();
                }
            }
        }

        void TryAssociateWithTLId(string name, long tweetId)
        {
            if (!UsesTwitlonger)
                return;

            string id = null;
            lock (dicLock)
                TwitlongerIds.TryGetValue(name, out id);

            if (id != null)
                ServiceDispatcher.GetTwitlongerService(name).SetId(id, tweetId, null);
        }

        void Schedule(object param)
        {
            if (!CheckProtectedAccounts())
                return;

            var scheduleTime = new DateTime(
                ScheduledDate.Year,
                ScheduledDate.Month,
                ScheduledDate.Day,
                ScheduledTime.Hour,
                ScheduledTime.Minute,
                0);

            if (TrialInformation.IsFullFeatured)
                ScheduleWithServer(scheduledTime);
            else
                ScheduleWithBackgroundAgent(scheduleTime);
        }

        private void ScheduleWithBackgroundAgent(DateTime scheduleTime)
        {
            TwitterStatusTask task = new TwitterStatusTask
            {
                InReplyTo = DataTransfer.ReplyId
            };

            task.Text = TweetText;

            if (ScheduledDate == null || ScheduledTime == null)
            {
                MessageService.ShowError(Resources.SelectDateTimeToSchedule);
                return;
            }

            task.Scheduled = scheduleTime;

            task.Accounts = new List<UserToken>();

            foreach (var user in SelectedAccounts.OfType<UserToken>())
                task.Accounts.Add(user);

            Config.TweetTasks.Add(task);
            Config.SaveTweetTasks();

            MessageService.ShowMessage(Resources.MessageScheduled, "");
            GoBack();
        }

        bool error;
        void ScheduleWithServer(DateTime scheduleTime)
        {
            requestsLeft = 0;
            error = false;
            foreach (var user in SelectedAccounts.OfType<UserToken>())
            {
                IsLoading = true;
                requestsLeft++;

                var scheduler = new Scheduler(user.Key, user.Secret);

                scheduler.ScheduleTweet(TweetText, scheduleTime, (sender, response) =>
                {
                    requestsLeft--;
                    if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NoContent)
                    {
                        error = true;
                        MessageService.ShowError(string.Format(Resources.ScheduleError, user.ScreenName));
                    }

                    if (requestsLeft <= 0)
                    {
                        IsLoading = false;
                        if (!error)
                        {
                            MessageService.ShowMessage(Resources.MessageScheduled);
                            GoBack();
                        }
                    }
                });
            }
        }

        void StartImageChooser(object param)
        {
            PhotoChooserTask chooser = new PhotoChooserTask();
            chooser.ShowCamera = true;
            chooser.Completed += new EventHandler<PhotoResult>(ChooserCompleted);
            chooser.Show();
        }

        void ChooserCompleted(object sender, PhotoResult e)
        {
            // TODO: Complete.

            MessageService.ShowError("Woops, not supported.");
        }

        void uploadCompleted(RestRequest request, RestResponse response, object userstate)
        {
            IsLoading = false;
            BarText = "";

            if (response.StatusCode != HttpStatusCode.OK)
            {
                MessageService.ShowError(Resources.ErrorUploadingImage);
                return;
            }

            XDocument doc = XDocument.Parse(response.Content);
            XElement node = doc.Descendants("url").FirstOrDefault();

            if (string.IsNullOrWhiteSpace(node.Value) || !node.Value.Contains("http://"))
            {
                MessageService.ShowError(Resources.ErrorUploadingImage);
                return;
            }

            TweetText += " " + node.Value + " ";
        }

        public void SaveAsDraft(object param)
        {
            TwitterDraft draft = CreateDraft();

            Config.Drafts.Add(draft);
            Config.Drafts = Config.Drafts;

            MessageService.ShowMessage(Resources.DraftSaved);
        }

        public TwitterDraft CreateDraft()
        {
            var draft = new TwitterDraft();
            draft.Text = TweetText;

            if (IsScheduled == true)
            {
                draft.Scheduled = new DateTime(
                ScheduledDate.Year,
                ScheduledDate.Month,
                ScheduledDate.Day,
                ScheduledTime.Hour,
                ScheduledTime.Minute,
                0);
            }
            else
                draft.Scheduled = null;

            draft.CreatedAt = DateTime.Now;
            draft.Accounts = new List<UserToken>();

            foreach (var acc in SelectedAccounts.OfType<UserToken>())
                draft.Accounts.Add(acc as UserToken);

            draft.ReplyId = DataTransfer.ReplyId;

            return draft;
        }

        IEnumerable<string> GetUrls(string text)
        {
            if (text == null)
                yield break;

            foreach (var word in text.Split(' '))
                if (Uri.IsWellFormedUriString(word, UriKind.Absolute))
                    yield return word;
        }

        bool CheckProtectedAccounts()
        {
            foreach (var user in SelectedAccounts.OfType<UserToken>())
            {
                if (user != null && ProtectedAccounts.IsProtected(user))
                {
                    var result = MessageService.AskYesNoQuestion(String.Format(Resources.AskTweetProtectedAccount, user.ScreenName), "");
                    if (!result)
                        return false;
                }
            }

            return true;
        }

        #region User suggestions
        void UpdateAutocompleter()
        {
            OnPropertyChanged("Suggestions");
            if (Completer != null)
            {
                Completer.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "IsAutocompleting")
                        IsSuggestingUsers = Completer.IsAutocompleting;
                };
            }
        }
        #endregion
    }
}
