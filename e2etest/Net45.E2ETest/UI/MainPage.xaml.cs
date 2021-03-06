﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Microsoft.WindowsAzure.MobileServices.Test
{
    /// <summary>
    /// Test setup UI page.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Initializes a new instance of the MainPage class.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            string mobileServiceRuntimeURL = Settings.Default.MobileServiceRuntimeUrl;
            string tags = Settings.Default.MobileServiceTags;

            txtRuntimeUri.Text = mobileServiceRuntimeURL ?? "";
            txtTags.Text = tags ?? "";

            Loaded += (s, e) => btnUnitTests.Focus();
        }

        /// <summary>
        /// Start the unit test run.
        /// </summary>
        /// <param name="sender">Start button.</param>
        /// <param name="e">Event arguments.</param>
        private void ExecuteUnitTests(object sender, RoutedEventArgs e)
        {
            // Get the test settings from the UI
            App.Harness.Settings.Custom["MobileServiceRuntimeUrl"] = txtRuntimeUri.Text;
            App.Harness.Settings.TagExpression = txtTags.Text;

            Settings.Default.MobileServiceRuntimeUrl = txtRuntimeUri.Text;
            Settings.Default.MobileServiceTags = txtTags.Text;

            if (!string.IsNullOrEmpty(App.Harness.Settings.TagExpression))
            {
                App.Harness.Settings.TagExpression += " - notNetFramework";
            }
            else
            {
                App.Harness.Settings.TagExpression = "!notNetFramework";
            }

            this.NavigationService.Navigate(new TestPage());
        }
    }
}
