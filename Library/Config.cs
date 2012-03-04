﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.IsolatedStorage;

namespace Ocell.Library
{
    public static class Config
    {
        private static readonly string AccountsKey = "ACCOUNTS";
        private static readonly string ColumnsKey = "COLUMNS";
        private static readonly string FollowMsg = "FOLLOWMSG";
        private static readonly string TweetTasksKey = "TWEETTASKS";

        private static List<UserToken> _accounts;
        private static ObservableCollection<TwitterResource> _columns;
        private static bool? _FollowMessageShown;
        private static List<ITweetableTask> _TweetTasks;

        public static List<ITweetableTask> TweetTasks
        {
            get
            {
                return GenericGetFromConfig<List<ITweetableTask>>(TweetTasksKey, ref _TweetTasks);
            }
            set
            {
                GenericSaveToConfig<List<ITweetableTask>>(TweetTasksKey, ref _TweetTasks, value);
            }
        }

        public static List<UserToken> Accounts
        {
            get
            {
                return GenericGetFromConfig<List<UserToken>>(AccountsKey, ref _accounts);
            }
            set
            {
                GenericSaveToConfig<List<UserToken>>(AccountsKey, ref _accounts, value);
            }
        }

        public static ObservableCollection<TwitterResource> Columns
        {
            get
            {
                return GenericGetFromConfig<ObservableCollection<TwitterResource>>(ColumnsKey, ref _columns);
            }
            set
            {

                GenericSaveToConfig<ObservableCollection<TwitterResource>>(ColumnsKey, ref _columns, value);
            }
        }

        private static T GenericGetFromConfig<T>(string Key, ref T element) where T : new()
        {
            if (element != null)
                return element;

            IsolatedStorageSettings config = IsolatedStorageSettings.ApplicationSettings;

            try
            {
                if (!config.TryGetValue<T>(Key, out element))
                {
                    element = new T();
                    config.Add(Key, element);
                    config.Save();
                }
            }
            catch (InvalidCastException)
            {
                config.Remove(Key);
            }
            catch (Exception)
            {
            }

            if (element == null)
                element = new T();

            return element;
        }

        public static bool? FollowMessageShown
        {
            get
            {
                return GenericGetFromConfig<bool?>(FollowMsg, ref _FollowMessageShown);
            }
            set
            {
                GenericSaveToConfig<bool?>(FollowMsg, ref _FollowMessageShown, value);
            }

        }

        private static void GenericSaveToConfig<T>(string Key, ref T element, T value) where T : new()
        {
            if (value == null)
                return;

            IsolatedStorageSettings conf = IsolatedStorageSettings.ApplicationSettings;

            try
            {
                element = value;
                if (conf.Contains(Key))
                    conf[Key] = value;
                else
                    conf.Add(Key, value);
                conf.Save();
            }
            catch (Exception)
            {
            }
        }

        public static void SaveAccounts()
        {
            Accounts = _accounts;
        }

        public static void SaveColumns()
        {
            Columns = _columns;
        }

        public static void SaveTasks()
        {
            TweetTasks = _TweetTasks;
        }
    }
}
