﻿using Microsoft.Win32;
using Newtonsoft.Json;
using Silverfeelin.StarboundDrawables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DrawablesGeneratorTool.Exporters;
using DrawablesGeneratorTool.Exporters.ItemExporters;
using DrawablesGeneratorTool.Utilities;

namespace DrawablesGeneratorTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>]
    // ReSharper disable once RedundantExtendsListEntry
    public partial class MainWindow : Window
    {
        private const int PreviewMarginLeft = 153;
        private const int PreviewMarginTop = 306;

        private readonly FileSystemWatcher watcher;
        private BitmapImage previewImage;
        private string imagePath;
        private bool warned;

        private Dictionary<string, FileInfo> templates;

        public MainWindow()
        {
            InitializeComponent();

            watcher = new FileSystemWatcher { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
            watcher.Changed += Watcher_Changed;
            watcher.Deleted += Watcher_Deleted;
            watcher.Renamed += Watcher_Deleted;

            PopulateTemplates();
        }

        private void PopulateTemplates()
        {
            if (templates == null)
                templates = new Dictionary<string, FileInfo>();

            if (!IsInitialized) return;

            cbxGenerateType.Items.Clear();
            templates.Clear();

            // Resources
            cbxGenerateType.Items.Add("Common Pistol");
            cbxGenerateType.Items.Add("Common Shortsword");
            cbxGenerateType.Items.Add("Tesla Staff");

            // Templates from files
            templates = new Dictionary<string, FileInfo>();
            var currentDirectory = Directory.GetCurrentDirectory();
            if (Directory.Exists(currentDirectory + "\\Templates"))
            {
                foreach (var item in Directory.EnumerateFiles(Directory.GetCurrentDirectory() + "\\Templates"))
                {
                    var fi = new FileInfo(item);
                    templates.Add(fi.Name, fi);

                    cbxGenerateType.Items.Add(fi.Name);
                }
            }

            cbxGenerateType.SelectedIndex = 0;
        }

        #region File Watcher

        // Update selected image when modified externally.
        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => { SelectImage(null); });
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(UpdatePreviewImage);
        }

        #endregion

        #region Select Image

        /// <summary>
        /// Prompt an OpenFileDialog for the user to select an image file, sets this.imagePath if it's valid and the dimensions are within this.pixelLimit.
        /// Calls UpdatePreviewImage() to update the preview.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select an image.",
                Filter = "Image files|*.png;*.jpg;*.bmp;*.gif",
            };

            var ofdResult = ofd.ShowDialog();
            if (ofdResult != true) return;

            SelectImage(ofd.FileName);
        }

        /// <summary>
        /// Event handler for dropping a file onto the application. Attempts to select it as an image.
        /// <see cref="SelectImage(string)"/>
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing event data, including the dropped item(s).</param>
        private void SelectImage_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length == 1)
                    SelectImage(files[0]);
            }
            catch (ArgumentException)
            {
                MessageBox.Show("The image could not be loaded. Please try another image.");
            }
            catch (DrawableException exc)
            {
                MessageBox.Show(exc.Message + Environment.NewLine + "The selection has been cleared.");
            }
        }

        public void SelectImage(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    throw new DrawableException("Invalid image selected, or the file no longer exists.");

                imagePath = path;
                watcher.Path = Path.GetDirectoryName(path);
                watcher.Filter = Path.GetFileName(path);
                watcher.EnableRaisingEvents = true;
            }
            catch (DrawableException exc)
            {
                imagePath = null;
                previewImage = null;
                MessageBox.Show(exc.Message + Environment.NewLine + "The selection has been cleared.");
            }
            finally
            {
                NewImageSelected();
            }
        }

        private void UpdatePreviewImage()
        {
            try
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    tbxImage.Text = imagePath;

                    var imageUri = new Uri(imagePath);
                    var bi = new BitmapImage();

                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = imageUri;
                    bi.EndInit();
                    bi.Freeze();

                    previewImage = bi;
                    imgPreview.Source = bi;
                    imgPreview.Width = bi.PixelWidth * 2;
                    imgPreview.Height = bi.PixelHeight * 2;
                }
            }
            catch
            {
                previewImage = null;
                imagePath = null;
                imgPreview.Source = null;
            }

            var clear = false;

            if (previewImage != null) // Check dimensions if image is loaded.
            {
                var pixels = previewImage.PixelWidth * previewImage.PixelHeight;
                if (pixels > 32768)
                {
                    if (!warned)
                    {
                        var res = MessageBox.Show("The image (" + previewImage.PixelWidth + "x" + previewImage.PixelHeight + "=" + pixels + ") exceeds the limit of " + 32768 + " total pixels.\nAre you sure you would like to continue?", "Warning", MessageBoxButton.YesNo);
                        if (res == MessageBoxResult.Yes)
                            warned = true;
                        else
                            clear = true;
                    }
                    
                }
            }
            else
            {
                clear = true;
            }

            if (clear)
            {
                // Remove image.
                imgPreview.Source = null;
                previewImage = null;

                tbxImage.Text = string.Empty;
                MessageBox.Show("The selection has been cleared.");
            }
            else
            {
                // Set preview position to match the last known position.
                var t = imgPreview.Margin;
                t.Left = PreviewMarginLeft + (Convert.ToInt32(oldTbxHandX) * 2);
                t.Top = PreviewMarginTop - Convert.ToInt32(imgPreview.Height) - (Convert.ToInt32(oldTbxHandY) * 2);
                imgPreview.Margin = t;
            }
        }

        /// <summary>
        /// Updates the preview image, and sets the image to the the '0,0' hand position if this.imagePath points to a valid image.
        /// Resets the hand and fire position textboxes.
        /// </summary>
        private void NewImageSelected()
        {
            tbxHandX.Text = "0";
            tbxHandY.Text = "0";

            // Display image.
            UpdatePreviewImage();
        }

        #endregion

        #region Drag on Preview

        /// <summary>
        /// Starts capturing the mouse for the preview window, to update the position of the image in the Preview_MouseMove event.
        /// Also calls Preview_MouseMove, to update the preview even when the mouse isn't moved.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            brdPreview.CaptureMouse();
            Preview_MouseMove(sender, e);
        }

        /// <summary>
        /// Adjusts the hand position textboxes by clicking (and dragging the mouse) on the preview window. 
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            // TODO: Change generator.Image to self contained value.
            if (previewImage == null || !brdPreview.IsMouseCaptured) return;

            var pos = e.GetPosition(brdPreview);
            var margin = new Thickness(pos.X - (imgPreview.Width / 2), pos.Y - (imgPreview.Height / 2), 0, 0);

            // Prevent displaying the image 'between' game pixels.
            if (Math.Abs(margin.Top % 2) < 0.1d)
                margin.Top++;

            if (Math.Abs(margin.Left % 2) < 0.1d)
                margin.Left++;

            int originalWidth = PreviewMarginLeft,
                originalHeight = PreviewMarginTop - Convert.ToInt32(imgPreview.Height);

            // This also fires HandX_TextChanged and HandY_TextChanged.
            tbxHandX.Text = Math.Ceiling((margin.Left - originalWidth) / 2).ToString(CultureInfo.InvariantCulture);
            tbxHandY.Text = (-Math.Ceiling((margin.Top - originalHeight) / 2)).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Stops capturing the mouse for the preview window.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            brdPreview.ReleaseMouseCapture();
        }

        #endregion

        #region Themes

        /// <summary>
        /// Changes the background of the preview to a dark image.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void ThemeBlack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ChangeTheme("DarkSmall.png");

        /// <summary>
        /// Changes the background of the preview to a light image.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void ThemeWhite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ChangeTheme("LightSmall.png");

        /// <summary>
        /// Changes the background of the preview to a natural image.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void ThemeGreen_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ChangeTheme("NaturalSmall.png");

        /// <summary>
        /// Changes the background of the preview.
        /// </summary>
        /// <param name="resourcePath">Path to an image, relative to Project/Resources/. Do not start with a slash.</param>
        private void ChangeTheme(string resourcePath)
        {
            if (resourcePath.IndexOf("/", StringComparison.Ordinal) == 0)
                resourcePath = resourcePath.Substring(1);

            imgPreviewBackground.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/" + resourcePath));
        }

        #endregion

        #region Positioning

        /// <summary>
        /// Variables to store the last valid value for the position textboxes; used to restore the value if the user enters an invalid character.
        /// </summary>
        private string oldTbxHandX = "0", oldTbxHandY = "0";

        /// <summary>
        /// Use the value of the hand position textboxes to render the preview image in the right location.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void HandX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;

            var tbx = tbxHandX;
            if (tbx.Text == string.Empty)
            {
                oldTbxHandX = "0";
                tbx.CaretIndex = 1;
                return;
            }

            e.Handled = !DrawableUtilities.IsNumber(tbx.Text);

            if (e.Handled)
            {
                var index = DrawableUtilities.Clamp(tbx.CaretIndex - 1, 0, tbx.Text.Length - 1);
                tbx.Text = oldTbxHandX;
                tbx.CaretIndex = index;
            }
            else
            {
                oldTbxHandX = tbx.Text;

                var t = imgPreview.Margin;
                t.Left = PreviewMarginLeft + (Convert.ToInt32(tbx.Text) * 2);
                imgPreview.Margin = t;
            }
        }

        /// <summary>
        /// Confirms if the new text value is a valid positive or negative integer, and updates the preview image position if it is.
        /// If the value is invalid, restores it to the preview value.
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void HandY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;

            var tbx = tbxHandY;
            if (tbx.Text == string.Empty)
            {
                oldTbxHandY = "0";
                tbx.CaretIndex = 1;
                return;
            }

            e.Handled = !DrawableUtilities.IsNumber(tbx.Text);

            if (e.Handled)
            {
                var index = DrawableUtilities.Clamp(tbx.CaretIndex - 1, 0, tbx.Text.Length - 1);
                tbx.Text = oldTbxHandY;
                tbx.CaretIndex = index;
            }
            else
            {
                oldTbxHandY = tbx.Text;
                var t = imgPreview.Margin;
                t.Top = PreviewMarginTop - Convert.ToInt32(imgPreview.Height) - (Convert.ToInt32(tbx.Text) * 2);
                imgPreview.Margin = t;
            }
        }

        /// <summary>
        /// Event to increase or decrease the numbers in a textbox by using the up/down arrow keys for convenience (as there's only one line) 
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Hand_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox tbx)) return;

            if (tbx.Text == string.Empty)
            {
                tbx.Text = "0";
            }

            var text = tbx.Text;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (e.Key)
            {
                case Key.Up:
                    tbx.Text = string.Empty;
                    tbx.AppendText((Convert.ToInt32(text) + 1).ToString());
                    e.Handled = true;
                    break;
                case Key.Down:
                    tbx.Text = string.Empty;
                    tbx.AppendText((Convert.ToInt32(text) - 1).ToString());
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Resets Textbox.Text back to 0, if it's left empty when leaving the control
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Hand_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tbx)) return;

            if (tbx.Text == string.Empty)
            {
                tbx.Text = "0";
            }
        }

        #endregion

        #region Modes

        private void ChkScale_Click(object sender, RoutedEventArgs e)
        {
            if (chkScale.IsChecked == true)
                chkFade.IsChecked = false;
        }

        private void ChkFade_Click(object sender, RoutedEventArgs e)
        {
            if (chkFade.IsChecked == true)
                chkScale.IsChecked = false;
        }

        #endregion

        #region Export Options

        private void PlainText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var generator = new DrawablesGenerator(imagePath);
                generator = DrawableUtilities.SetUpGenerator(generator, tbxHandX.Text, tbxHandY.Text, tbxIgnoreColor.Text);
                generator.ReplaceWhite = true;

                var output = generator.Generate();
                var exporter = GetExporter(output);
                (new OutputWindow("Item Descriptor (Export):", exporter.GetDescriptor(chkAddWeaponGroup.IsChecked.HasValue && chkAddWeaponGroup.IsChecked.Value ? "weapon" : null, chkAddInventoryIcon.IsChecked.Value))).Show();
            }
            catch(JsonReaderException exc)
            {
                MessageBox.Show("The template does not appear to be valid JSON.\n\nException:\n" + exc.Message);
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show("Argument may not be null. Did you select a valid image?");
            }
            catch (ArgumentException exc)
            {
                MessageBox.Show("Illegal argument:\n" + exc.Message);
            }
            catch (FormatException)
            {
                MessageBox.Show("Could not convert hand offsets to numbers.");
            }
            catch (Exception exc)
            {
                MessageBox.Show("Uncaught exception:\n" + exc.Message);
            }
        }

        private void SingleTextureDirectives_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var generator = new DrawablesGenerator(imagePath);

                var fade = chkFade.IsChecked == true;
                var scale = chkScale.IsChecked == true;

                generator = DrawableUtilities.SetUpGenerator(generator, tbxHandX.Text, tbxHandY.Text, tbxIgnoreColor.Text);
                generator.ReplaceBlank = !fade;
                generator.ReplaceWhite = true;

                var j = 64;
                int.TryParse(tbxSourceImageSize.Text, out j);

                var output = scale ? generator.GenerateScale() : generator.Generate();

                
                var data = !scale ? DrawableUtilities.GenerateSingleTextureDirectives(output, j, fade) : output.Drawables[0,0].Directives;
                (new OutputWindow("Single Texture Directives:", data, false)).Show();
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid format. Did you provide a correct ignore color code? (hexadecimal RRGGBB or RRGGBBAA)");
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show("Argument may not be null. Did you select a valid image?");
            }
            catch (DrawableException exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void InventoryIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var generator = new DrawablesGenerator(imagePath);
                generator = DrawableUtilities.SetUpGenerator(generator, "0", "0", tbxIgnoreColor.Text);

                var output = generator.Generate();
                (new OutputWindow("Inventory Icon:", DrawableUtilities.GenerateInventoryIcon(output))).Show();
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid format. Did you provide a correct ignore color code? (hexadecimal RRGGBB or RRGGBBAA)");
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show("Argument may not be null. Did you select a valid image?");
            }
            catch (DrawableException exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void Command_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var generator = new DrawablesGenerator(imagePath);
                generator = DrawableUtilities.SetUpGenerator(generator, tbxHandX.Text, tbxHandY.Text, tbxIgnoreColor.Text);
                generator.ReplaceWhite = true;

                var output = generator.Generate();
                var exporter = GetExporter(output);
                (new OutputWindow("Item Command:", exporter.GetCommand(chkAddWeaponGroup.IsChecked.HasValue && chkAddWeaponGroup.IsChecked.Value ? "weapon" : null, chkAddInventoryIcon.IsChecked.Value), false)).Show();
            }
            catch (JsonReaderException exc)
            {
                MessageBox.Show("The template does not appear to be valid JSON.\n\nException:\n" + exc.Message);
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show("Argument may not be null. Did you select a valid image?");
            }
            catch (ArgumentException exc)
            {
                MessageBox.Show("Illegal argument:\n" + exc.Message);
            }
            catch (FormatException)
            {
                MessageBox.Show("Could not convert hand offsets to numbers.");
            }
            catch (Exception exc)
            {
                MessageBox.Show("Uncaught exception:\n" + exc.Message);
            }
        }

        #endregion
        
        private Exporter GetExporter(DrawablesOutput output)
        {
            var value = (string)cbxGenerateType.SelectedItem;
            switch (value)
            {
                default:
                    {
                        var file = templates[value];
                        string template;
                        try
                        {
                            template = File.ReadAllText(file.FullName);
                        }
                        catch (Exception e)
                        {
                            throw new ArgumentException("Could not load the template '" + file.Name + "'.\n\nException:\n" + e.Message);
                        }

                        return new TemplateExporter(output, template);
                    }
                case null:
                case "Common Pistol":
                    return new PistolExporter(output);
                case "Common Shortsword":
                    return new ShortswordExporter(output);
                case "Tesla Staff":
                    return new TeslaStaffExporter(output);
            }
        }
    }
}
