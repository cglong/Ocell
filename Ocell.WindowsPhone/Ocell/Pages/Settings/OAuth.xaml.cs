﻿using System;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Web;
using System.Windows;
using Hammock;
using Hammock.Authentication.OAuth;
using Microsoft.Phone.Controls;
using System.Linq;
using Ocell.Library;
using Ocell.Library.Twitter;
using Ocell.Library.Notifications;
using System.Text;
using Ocell.Localization;

namespace Ocell.Settings
{
    public partial class OAuth : PhoneApplicationPage
    {
        protected string consumerKey = SensitiveData.ConsumerToken;
        protected string consumerSecret = SensitiveData.ConsumerSecret;
        private string _requestToken;
        private string _requestSecret;

        public OAuth()
        {
            InitializeComponent(); Loaded += (sender, e) => { if (ApplicationBar != null) ApplicationBar.MatchOverriddenTheme(); }; 
            

            this.Loaded += new RoutedEventHandler(OAuth_Loaded);
        }

        void OAuth_Loaded(object sender, RoutedEventArgs e)
        {
            string callBackUrl = "http://www.google.es";

            // Use Hammock to set up our authentication credentials
            OAuthCredentials credentials = new OAuthCredentials()
            {
                Type = OAuthType.RequestToken,
                SignatureMethod = OAuthSignatureMethod.HmacSha1,
                ParameterHandling = OAuthParameterHandling.HttpAuthorizationHeader,
                ConsumerKey = consumerKey,
                ConsumerSecret = consumerSecret,
                CallbackUrl = callBackUrl,
                Version = "1.0a"
            };

            // Use Hammock to create a rest client
            var client = new RestClient
            {
                Authority = "https://api.twitter.com",
                Credentials = credentials
            };

            // Use Hammock to create a request
            var request = new RestRequest
            {
                Path = "/oauth/request_token/"
            };

            // Get the response from the request
            client.BeginRequest(request, new RestCallback(GetRequestTokenResponse));
        }

        private void GetRequestTokenResponse(RestRequest request, RestResponse response, object userstate)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(Localization.Resources.ErrorAuthURL);
                    if (NavigationService.CanGoBack)
                        NavigationService.GoBack();
                });

            }

            string request_token = "";
            string token_secret = "";
            try
            {
                var collection = HttpUtility.ParseQueryString(response.Content);
                request_token = collection["oauth_token"];
                token_secret = collection["oauth_token_secret"];
            }
            catch (Exception)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(Localization.Resources.ErrorAuthURL);
                    if (NavigationService.CanGoBack)
                        NavigationService.GoBack();
                });
            }

            _requestToken = request_token;
            _requestSecret = token_secret;

            Dispatcher.BeginInvoke(() => { wb.Navigate(new Uri("https://api.twitter.com/oauth/authorize?oauth_token=" + request_token)); });
        }

        private string GetQueryString(string Query)
        {
            int index = Query.IndexOf("?");
            if (index > 0)
                Query = Query.Substring(index).Remove(0, 1);

            return Query;
        }

        private void wb_Navigating(object sender, NavigatingEventArgs e)
        {
            if (e.Uri.Host.Contains("google"))
            {
                // This is the Twitter callback, so cancel the call and manage the tokens.
                e.Cancel = true;
                string url = GetQueryString(e.Uri.Query);

                var collection = HttpUtility.ParseQueryString(url);

                string token; 
                string verifier;

                try
                {
                    token = collection["oauth_token"];
                    verifier = collection["oauth_verifier"];
                }
                catch (Exception)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show("");
                        if (NavigationService.CanGoBack)
                            NavigationService.GoBack();
                    });
                    return;
                }

                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(verifier))
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show(Localization.Resources.ErrorClientTokens);
                        if (NavigationService.CanGoBack)
                            NavigationService.GoBack();
                    });
                    return;
                }

                GetFullTokens(token, verifier);
            }
        }

        private void GetFullTokens(string token, string verifier)
        {
            // Use Hammock to set up our authentication credentials
            OAuthCredentials credentials = new OAuthCredentials()
            {
                Type = OAuthType.RequestToken,
                SignatureMethod = OAuthSignatureMethod.HmacSha1,
                ParameterHandling = OAuthParameterHandling.HttpAuthorizationHeader,
                ConsumerKey = consumerKey,
                ConsumerSecret = consumerSecret,
                Token = token,
                TokenSecret = _requestSecret,
                Verifier = verifier,
                CallbackUrl = "http://google.es",
                Version = "1.0a"
            };

            // Use Hammock to create a rest client
            var client = new RestClient
            {
                Authority = "https://api.twitter.com",
                Credentials = credentials
            };

            // Use Hammock to create a request
            var request = new RestRequest
            {
                Path = "/oauth/access_token/",
                DecompressionMethods = Hammock.Silverlight.Compat.DecompressionMethods.None
            };

            // Get the response from the request
            client.BeginRequest(request, new RestCallback(RequestCompleted));
        }

        public void RequestCompleted(RestRequest req, RestResponse response, object userstate)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(Localization.Resources.ErrorClientTokens);
                    if (NavigationService.CanGoBack)
                        NavigationService.GoBack();
                });
                return;
            }

            try
            {
                var collection = HttpUtility.ParseQueryString(response.Content);

                UserToken Token = new UserToken
                {
                    Key = collection["oauth_token"],
                    Secret = collection["oauth_token_secret"],
                    Preferences = new NotificationPreferences
                    {
                        MentionsPreferences = NotificationType.Tile,
                        MessagesPreferences = NotificationType.Tile
                    }
                };

                var filler = new UserTokenFiller(Token);
                filler.UserDataFilled += new UserTokenFiller.OnUserDataFilled(InsertTokenIntoAccounts);
                filler.FillUserData();

                
            }
            catch (Exception)
            {
                Dispatcher.BeginInvoke(() => MessageBox.Show(Localization.Resources.ErrorClientTokens));
            }
        }

        private void CreateColumns(UserToken user)
        {
            TwitterResource Home = new TwitterResource { Type = ResourceType.Home, User = user };
            TwitterResource Mentions = new TwitterResource { Type = ResourceType.Mentions, User = user };
            TwitterResource Messages = new TwitterResource { Type = ResourceType.Messages, User = user };
            Dispatcher.BeginInvoke(() =>
            {
                if (!Config.Columns.Contains(Home))
                    Config.Columns.Add(Home);
                if (!Config.Columns.Contains(Mentions))
                    Config.Columns.Add(Mentions);
                if (!Config.Columns.Contains(Messages))
                    Config.Columns.Add(Messages);
                Config.SaveColumns();
            });
        }

        private void InsertTokenIntoAccounts(UserToken Token)
        {
            CheckIfExistsAndInsert(Token);
            CreateColumns(Token);
            Dispatcher.BeginInvoke(() => { NavigationService.Navigate(Uris.MainPage); });
            return;
        }

        private static void CheckIfExistsAndInsert(UserToken Token)
        {
            foreach (var item in Config.Accounts)
            {
                if (item.Key == Token.Key && item.ScreenName == Token.ScreenName)
                {
                    if (item.Secret != Token.Secret)
                    {
                        Config.Accounts.Remove(item);
                        Config.Accounts.Add(Token);
                        Config.SaveAccounts();
                    }
                    return;
                }
            }
            Config.Accounts.Add(Token);
            Config.SaveAccounts();
        }

        private void wb_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            if (e.Uri.Host.Contains("api.twitter.com"))
                Dispatcher.BeginInvoke(() =>
                {
                    txtAuth.Visibility = Visibility.Collapsed;
                    pBar.Visibility = Visibility.Collapsed;
                    wb.Visibility = Visibility.Visible;
                });
        }
    }
}