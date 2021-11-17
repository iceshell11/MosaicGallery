using MosaicGallery.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Text.RegularExpressions;
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

namespace MosaicGallery
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private double scrollRemain = 0;
        private double scrollContentOffset = 0;

        private double visabilityDistance = 2000;

        private int imageLoadDelay = 10;


        Point scrollMousePoint = new Point();
        double hOff = 1;

        private ConcurrentBag<ImageUIInfo> imgPositions = new ConcurrentBag<ImageUIInfo>();


        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            seed_num.Text = new Random().Next(99999).ToString();
            path_textbox.Focus();

            new ResourceController(scrollGrid).StartVisabilityControl(imgPositions, (double pos) => Math.Abs(pos - scrollContentOffset) < visabilityDistance);
        }

        private void scrollGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                var d = e.NewSize.Width / e.PreviousSize.Width;

                foreach (var item in scrollGrid.Children.OfType<Border>())
                {
                    item.Margin = new Thickness(item.Margin.Left * d, item.Margin.Top * d, 0, 0);
                    item.Width *= d;
                    item.Height *= d;
                }
            }
        }

        private void scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollRemain = (e.ExtentHeight - e.VerticalOffset);
            scrollContentOffset = scrollViewer.ContentVerticalOffset;
        }

        private void ok_button_Click_1(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(seed_num.Text, out int seed))
            {
                seed = DateTime.Now.Millisecond;
            }

            if (!int.TryParse(group_spacing_num.Text, out int group_space))
            {
                group_space = 0;
            }

            var imPlacer = new ImagePlacer(path_textbox.Text, scrollGrid)
            {
                SearchOption = subfolders_checkbox.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly,
                Seed = seed,
                GroupSpace = group_space,
                IsGrouping = grouping_checkbox.IsChecked ?? false,
                SmallCount = (int)small_slider.Value,
                MediumCount = (int)medium_slider.Value,
                LargeCount = (int)large_slider.Value,
                ImageLoadDelay = imageLoadDelay,
            };

            switch (orderType.SelectedIndex)
            {
                case 0:
                    imPlacer.OrderType = OrderType.CreationTypeDes;
                    break;
                case 1:
                    imPlacer.OrderType = OrderType.NameDes;
                    break;
                case 2:
                    imPlacer.OrderType = OrderType.Random;
                    break;
                case 3:
                    imPlacer.OrderType = OrderType.CreationTypeAsc;
                    break;
                case 4:
                    imPlacer.OrderType = OrderType.NameAsc;
                    break;
            }

            imPlacer.ImageClickHandler = (sender1, e1) => {
                if(e1.ClickCount == 2)
                {
                    var img = sender1 as Image;
                    bigImage.Source = img.Source;
                    bigImageContainer.Visibility = Visibility.Visible;
                }
            };

            {
                imPlacer.ContextMenu = new ContextMenu();
                var reopen_item = new MenuItem() { Header = "Select folder..." };
                reopen_item.Click += (s1, e1) => {
                    OpenMenu_Click(s1, e1);
                };
                imPlacer.ContextMenu.Items.Add(reopen_item);

                var open_file = new MenuItem() { Header = "Open file" };
                open_file.Click += (s1, e1) => {
                    var image = ((s1 as MenuItem).Parent as ContextMenu).PlacementTarget as Image;
                    var path = image.Tag.ToString();
                    System.Diagnostics.Process.Start(path);
                };
                imPlacer.ContextMenu.Items.Add(open_file);

                var reveal_menu = new MenuItem() { Header = "Reveal in explorer" };
                reveal_menu.Click += (s1, e1) => {
                    var image = ((s1 as MenuItem).Parent as ContextMenu).PlacementTarget as Image;
                    var path = image.Tag.ToString();
                    string argument = "/select, \"" + path + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                };
                imPlacer.ContextMenu.Items.Add(reveal_menu);
            }

            if (imPlacer.LoadImages(imgPositions, (double pos) => Math.Abs(pos - scrollContentOffset) < visabilityDistance, ()=> scrollRemain >= 1500))
            {
                scrollViewer.ScrollToVerticalOffset(0);
                load_grid.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Folder not found");
            }
        }

       
        private void open_btn_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    path_textbox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            load_grid.Visibility = Visibility.Visible;
        }

        private void grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            scrollMousePoint = e.GetPosition(scrollViewer);
            hOff = scrollViewer.VerticalOffset;
            scrollGrid.CaptureMouse();
        }

        private void grid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                scrollViewer.ScrollToVerticalOffset(hOff + (scrollMousePoint.Y - e.GetPosition(scrollViewer).Y));
            }
        }

        private void grid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollGrid.ReleaseMouseCapture();
        }

        private void group_spacing_num_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[0-9]+");
            if (!regex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void seed_num_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[0-9]+");
            if (!regex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void scrollViewer_PreviewMouseWheel_1(object sender, MouseWheelEventArgs e)
        {
            int SpeedFactor = 1;
            if (!e.Handled)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta * SpeedFactor);
                e.Handled = true;
            }
        }

        private void close_button_Copy_Click(object sender, RoutedEventArgs e)
        {
            load_grid.Visibility = Visibility.Collapsed;
        }

        private void RevealInFolder_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            var path = image.Tag.ToString();
            string argument = "/select, \"" + path + "\"";
            System.Diagnostics.Process.Start("explorer.exe", argument);
        }

        private void bigImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ClickCount == 2)
            {
                bigImageContainer.Visibility = Visibility.Collapsed;
                bigImage.Source = null;
            }
        }

        private void bigImageContainer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
        }
    }
}
