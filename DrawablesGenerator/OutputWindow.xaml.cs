﻿using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Windows;

namespace DrawablesGeneratorTool
{
    /// <summary>
    /// Interaction logic for OutputWindow.xaml
    /// </summary>
    // ReSharper disable once RedundantExtendsListEntry
    public partial class OutputWindow : Window
    {
        private readonly string contentString;
        private readonly JToken contentObject;

        private bool formatted = true;

        public OutputWindow(string title, JToken content, bool showToggleContent = true)
        {
            InitializeComponent();

            btnJoin.Visibility = showToggleContent ? Visibility.Visible : Visibility.Hidden;

            contentObject = content;
            tbxCode.Text = content.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        public OutputWindow(string title, string content, bool showToggleContent = true)
        {
            InitializeComponent();

            btnJoin.Visibility = showToggleContent ? Visibility.Visible : Visibility.Hidden;

            contentString = content;
            tbxCode.Text = content;   
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(tbxCode.Text);
        }

        private void ToggleFormat_Click(object sender, RoutedEventArgs e)
        {
            formatted = !formatted;
            if (contentObject != null)
                tbxCode.Text = contentObject.ToString(formatted ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
            else
            {
                // TODO: Remove tabs and spaces, but only outside of string.. probably parse to JObject then format using ToString
                tbxCode.Text = formatted ? contentString : contentString.Replace(Environment.NewLine, "");
            }
                
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save file to...",
                Filter = "JSON file|*.json|All files|*.*"
            };

            var result = sfd.ShowDialog();

            if (result == true)
            {
                File.WriteAllText(sfd.FileName, tbxCode.Text);
            }
        }
    }
}
