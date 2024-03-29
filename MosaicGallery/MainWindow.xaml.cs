﻿using MosaicGallery.Model;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MosaicGallery
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private double scrollRemain = 0;
        private double scrollContentOffset = 0;
        private long lastScrollTime = 0;
        private double hOff = 1;
        private Point scrollMousePoint = new Point();
        private string _searchText = "";


        private int imageLoadDelay = 0;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly ConcurrentBag<ImageUIInfo> _placedImages = new ConcurrentBag<ImageUIInfo>();
        private readonly SelectionProcessor _selectionProcessor = new SelectionProcessor();
        private readonly FileProcessor _fileProcessor = new FileProcessor();
        private readonly ImagePlacer _imPlacer;
        private readonly ResourceController _resourceController;

        private readonly MouseButtonEventHandler _imageClickHandler;
        private readonly ContextMenu _contextMenu;

        private readonly SemaphoreSlim _imagesSemaphore = new SemaphoreSlim(1, 1);

        public MainWindow()
        {
            InitializeComponent();
            _imPlacer = new ImagePlacer(scrollGrid, _placedImages, _imagesSemaphore);

            _imageClickHandler = (sender1, e1) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    var img = sender1 as Image;
                    _selectionProcessor.ToggleSelection(img.FindParent<ImageContainer>());
                }
                else
                {
                    _selectionProcessor.ResetSelection();

                    if (e1.ClickCount == 2)
                    {
                        var img = sender1 as Image;
                        SetBigImage(img.FindParent<ImageContainer>());
                    }
                }
            };
            _contextMenu = CreateContextMenu();

            seed_num.Text = new Random().Next(99999).ToString();
            path_textbox.Focus();

            _resourceController = new ResourceController(scrollGrid, _placedImages, _imagesSemaphore)
            {
                ContextMenu = _contextMenu,
                ImageClickHandler = _imageClickHandler,
                ImageLoadDelay = imageLoadDelay
            };


            int pathIndex = Array.IndexOf(Environment.GetCommandLineArgs(), "--path");
            if (pathIndex == -1 && Environment.GetCommandLineArgs().Length > 1)
            {
                var path = Environment.GetCommandLineArgs()[1];
                if (Directory.Exists(path))
                {
                    pathIndex = 0;
                }
            }

            if (pathIndex != -1)
            {
                var arg1 = Environment.GetCommandLineArgs()[pathIndex + 1];
                if (Directory.Exists(arg1))
                {
                    path_textbox.Text = arg1;
                }
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--no-dialog") != -1)
            {
                Task.Delay(10).ContinueWith(x => Application.Current.Dispatcher.Invoke(Start));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _resourceController.StartVisibilityControl(() => scrollContentOffset, () => DateTime.Now.Ticks - lastScrollTime < 1000 * TimeSpan.TicksPerMillisecond);

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(100);
                    if (_searchText != _imPlacer.Filter)
                    {
                        _imPlacer.Filter = _searchText;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            scrollViewer.ScrollToVerticalOffset(0);
                        });
                        await Task.Delay(1000);
                    }
                }
            });
        }

        private void Start()
        {
            var path = path_textbox.Text;

            if (!Directory.Exists(path))
            {
                MessageBox.Show("Folder not found");
                return;
            }

            if (!int.TryParse(seed_num.Text, out int seed))
            {
                seed = DateTime.Now.Millisecond;
            }

            if (!int.TryParse(group_spacing_num.Text, out int group_space))
            {
                group_space = 0;
            }

            _imPlacer.Path = path;
            _imPlacer.SearchOption = subfolders_checkbox.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            _imPlacer.Seed = seed;
            _imPlacer.GroupSpace = group_space;
            _imPlacer.IsGrouping = grouping_checkbox.IsChecked ?? false;
            _imPlacer.SmallCount = (int)small_slider.Value;
            _imPlacer.MediumCount = (int)medium_slider.Value;
            _imPlacer.LargeCount = (int)large_slider.Value;
            _imPlacer.ImageLoadDelay = imageLoadDelay;
            _imPlacer.ImageClickHandler = _imageClickHandler;
            _imPlacer.ContextMenu = _contextMenu;

            switch (orderType.SelectedIndex)
            {
                case 0:
                    _imPlacer.OrderType = OrderType.CreationTypeDes;
                    break;
                case 1:
                    _imPlacer.OrderType = OrderType.NameDes;
                    break;
                case 2:
                    _imPlacer.OrderType = OrderType.Random;
                    break;
                case 3:
                    _imPlacer.OrderType = OrderType.CreationTypeAsc;
                    break;
                case 4:
                    _imPlacer.OrderType = OrderType.NameAsc;
                    break;
            }

            _imPlacer.PrepareImages().ContinueWith(_ =>
            {
                _imPlacer.LoadImages(() => scrollRemain >= 1500, () => DateTime.Now.Ticks - lastScrollTime < 1000 * TimeSpan.TicksPerMillisecond, _cancellationTokenSource.Token);
            });
            scrollViewer.ScrollToVerticalOffset(0);
            load_grid.Visibility = Visibility.Collapsed;
        }

        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            var reopen_item = new MenuItem() { Header = "Select folder..." };
            reopen_item.Click += OpenMenu_Click;
            contextMenu.Items.Add(reopen_item);

            var open_file = new MenuItem() { Header = "Open file" };
            open_file.Click += (s1, e1) =>
            {
                var image = ((s1 as MenuItem).Parent as ContextMenu).PlacementTarget as Image;
                var imgPath = image.Tag.ToString();

                ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", $"/c {imgPath}")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(procStartInfo);
            };
            contextMenu.Items.Add(open_file);

            var reveal_menu = new MenuItem() { Header = "Reveal in explorer" };
            reveal_menu.Click += (s1, e1) =>
            {
                var image = ((s1 as MenuItem)!.Parent as ContextMenu)!.PlacementTarget as Image;
                var imgPath = image!.Tag.ToString();
                string argument = "/select, \"" + imgPath + "\"";
                Process.Start("explorer.exe", argument);
            };
            contextMenu.Items.Add(reveal_menu);


            var delete_menu = new MenuItem() { Header = "Delete" };
            delete_menu.Click += (s1, e1) =>
            {
                var image = (Image)((ContextMenu)((MenuItem)s1).Parent)!.PlacementTarget;
                var imageContainer = image.FindParent<ImageContainer>();

                if (_selectionProcessor.Contains(imageContainer))
                {
                    _fileProcessor.DeleteImages(_selectionProcessor.SelectedImages);
                }
                else
                {
                    _fileProcessor.DeleteImages(imageContainer);
                }
                _selectionProcessor.ResetSelection();
            };
            contextMenu.Items.Add(delete_menu);
            return contextMenu;
        }

        private void scrollGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged && e.PreviousSize.Width != 0)
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
            lastScrollTime = DateTime.Now.Ticks;
        }

        private void ok_button_Click_1(object sender, RoutedEventArgs e)
        {
            Start();
        }

        private void open_btn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                path_textbox.Text = dialog.SelectedPath;
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
            Process.Start("explorer.exe", argument);
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
        
        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (bigImageContainer.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Left)
                {
                    var imageContainer = bigImageContainer.Tag as ImageContainer;
                    if (imageContainer?.Image != null)
                    {
                        var children = ((Grid)imageContainer.Parent).Children;
                        int bIndex = children.IndexOf(imageContainer);
                        if (bIndex > 0)
                        {
                            SetBigImage((ImageContainer)children[bIndex - 1]);
                        }
                    }
                }
                else if(e.Key == Key.Right)
                {
                    var imageContainer = bigImageContainer.Tag as ImageContainer;
                    if (imageContainer?.Image != null)
                    {
                        var children = ((Grid)imageContainer.Parent).Children;
                        int bIndex = children.IndexOf(imageContainer);
                        if (bIndex + 1 < children.Count)
                        {
                            SetBigImage((ImageContainer)children[bIndex + 1]);
                        }
                    }
                }

            }
            else
            {
                if(e.Key == Key.F && Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    searchGrid.Visibility = Visibility.Visible;
                    searchTextbox.Focus();
                }
            }
        }

        private void SetBigImage(ImageContainer imgContainer)
        {
            if (imgContainer.Image.Source != null)
            {
                bigImage.Source = imgContainer.Image.Source;
                bigImageContainer.Visibility = Visibility.Visible;
                bigImageContainer.Tag = imgContainer;
            }
        }

        private void CloseSearchButtonClick(object sender, RoutedEventArgs e)
        {
            searchGrid.Visibility = Visibility.Collapsed;
        }

        private void searchTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = searchTextbox.Text;
        }

        private void SearchTextbox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                searchGrid.Visibility = Visibility.Collapsed;
            }
        }
    }
}
