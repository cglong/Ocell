﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Ocell.Library;

namespace Ocell
{
    public partial class Filters : PhoneApplicationPage
    {
        public Filters()
        {
            InitializeComponent();
			ThemeFunctions.ChangeBackgroundIfLightTheme(LayoutRoot);

            this.Loaded += new RoutedEventHandler(Filters_Loaded);
        }

        void Filters_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataTransfer.cFilter == null)
                DataTransfer.cFilter = new ColumnFilter();
            FilterList.DataContext = null;
            FilterList.ItemsSource = null;
            FilterList.DataContext = DataTransfer.cFilter.Predicates;
            FilterList.ItemsSource = DataTransfer.cFilter.Predicates;
            FilterList.UpdateLayout();
        }

        private void add_Click(object sender, System.EventArgs e)
        {
            DataTransfer.Filter = new ITweetableFilter();
            DataTransfer.Filter.Type = FilterType.User;
            DataTransfer.Filter.Filter = "";
            DataTransfer.Filter.Inclusion = IncludeOrExclude.Include;
            NavigationService.Navigate(new Uri("/Pages/ManageFilter.xaml", UriKind.Relative));
        }

        private void ApplicationBarIconButton_Click(object sender, System.EventArgs e)
        {
            // Save.
            ColumnFilter ToRemove = Config.Filters.FirstOrDefault(item => item.Resource == DataTransfer.cFilter.Resource);
            if (ToRemove != null)
                Config.Filters.Remove(ToRemove);

            Config.Filters.Add(DataTransfer.cFilter);
            Config.SaveFilters();
            NavigationService.GoBack();
        }

        private void Grid_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Grid grid = sender as Grid;
            if (grid != null && grid.Tag is ITweetableFilter)
            {
                DataTransfer.Filter = grid.Tag as ITweetableFilter;
                NavigationService.Navigate(new Uri("/Pages/ManageFilter.xaml", UriKind.Relative));
            }
        }

        private void Grid_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Grid grid = sender as Grid;
            Dispatcher.BeginInvoke(() =>
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this filter?", "", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    if (grid != null && grid.Tag is ITweetableFilter)
                    {
                        DataTransfer.cFilter.RemoveFilter(grid.Tag as ITweetableFilter);
                        FilterList.DataContext = null;
                        FilterList.ItemsSource = null;
                        FilterList.DataContext = DataTransfer.cFilter.Predicates;
                        FilterList.ItemsSource = DataTransfer.cFilter.Predicates;
                    }
                }
            });
        }

        private void FilterList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (FilterList.SelectedIndex != -1)
                FilterList.SelectedIndex = -1;
        }
    }
}
