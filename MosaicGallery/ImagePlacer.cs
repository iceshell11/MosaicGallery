using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MosaicGallery.Model;
using System.Windows.Input;

namespace MosaicGallery
{
    public class ImagePlacer
    {
        public string Path;
        public string[] Extentions = new string[] { ".jpeg", ".jpg", ".png", ".bmp" };

        public SearchOption SearchOption;
        public int Seed;
        public int GroupSpace;
        public bool IsGrouping;

        public int SmallCount;
        public int MediumCount;
        public int LargeCount;

        public (int from, int to) ImgPerGroup = (6, 12);
        public int SplitCount = 6;
        public int ImageLoadDelay;


        private static DateTime LoadTime;
        private static bool IsLoading = false;
        private Grid scrollGrid;

        public MouseButtonEventHandler ImageClickHandler;
        public ContextMenu ContextMenu;
        public OrderType OrderType = OrderType.CreationTypeDes;

        public ImagePlacer(string path, Grid scrollGrid)
        {
            Path = path;
            this.scrollGrid = scrollGrid;
        }

        public bool LoadImages(ConcurrentBag<ImageUIInfo> imgPositions, Func<double, bool> isVisiblePred, Func<bool> continueLoadingPred)
        {
            if (!Directory.Exists(Path))
            {
                return false;
            }

            var sizes = new (int size, int weight)[]
{
                (1, SmallCount),
                (2, MediumCount),
                (3, LargeCount),
            };


            LoadTime = DateTime.Now;

            Task.Run(async () =>
            {
                List<ImageInfo> images = new List<ImageInfo>();

                while (IsLoading)
                {
                    await Task.Delay(100);
                }

                var taskStartTime = LoadTime;
                IsLoading = true;


                //Clearing
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var item in imgPositions)
                    {
                        if (item.img.Source is BitmapImage img)
                        {
                            item.img.Source = null;
                            item.img.UpdateLayout();
                        }
                    }
                    scrollGrid.Children.Clear();
                    scrollGrid.UpdateLayout();
                    GC.Collect();
                });

                while (imgPositions.Any())
                {
                    imgPositions.TryTake(out _);
                }

                Random rand = new Random(Seed);

                var usorted_files = Directory.GetFiles(Path, "*", SearchOption).Where(x => Extentions.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase)));
                Queue<string> files = null;
                switch (OrderType)
                {
                    case OrderType.CreationTypeDes:
                        files = new Queue<string>(usorted_files.OrderByDescending(x => System.IO.File.GetCreationTime(x).Ticks));
                        break;
                    case OrderType.NameDes:
                        files = new Queue<string>(usorted_files.OrderByDescending(x => x));
                        break;
                    case OrderType.Random:
                        files = new Queue<string>(usorted_files.OrderByDescending(x => rand.Next()));
                        break;
                    case OrderType.CreationTypeAsc:
                        files = new Queue<string>(usorted_files.OrderBy(x => System.IO.File.GetCreationTime(x).Ticks));
                        break;
                    case OrderType.NameAsc:
                        files = new Queue<string>(usorted_files.OrderBy(x => x));
                        break;
                }

                foreach (var file in files)
                {
                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.StreamSource = File.OpenRead(file);
                    bitmap.EndInit();

                    images.Add(new ImageInfo(file, bitmap.Width > bitmap.Height ? Model.Orientation.Horizontal : Model.Orientation.Vertical));
                }
                GC.Collect();

                double h = 0;
                while (images.Any() && taskStartTime == LoadTime)
                {
                    List<ImageInfo> taken;
                    if (IsGrouping)
                    {
                        taken = images.Take(rand.Next(ImgPerGroup.from, ImgPerGroup.to + 1)).ToList();
                        images.RemoveRange(0, taken.Count);
                    }
                    else
                    {
                        taken = new List<ImageInfo>(images);
                        images.Clear();
                    }


                    var mosaic = new MosaicMatrix(SplitCount);

                    mosaic.FitImages(taken, rand, sizes);
                    mosaic.FixLevels();
                    mosaic.FixLevelsWithImages(images);

                    foreach (BlockInfo block in mosaic.blocks)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var img = block.Img;

                            double scaleX = scrollGrid.RenderSize.Width / SplitCount;
                            double scaleY = scrollGrid.RenderSize.Width / SplitCount / 1.4142857;
                            var margin = new Thickness(block.Pos.Col * scaleX, (block.Pos.Row + h) * scaleY, 0, 0);
                            bool visible = isVisiblePred((int)margin.Top);

                            var bitmap = new BitmapImage();

                            if (visible)
                            {
                                bitmap.BeginInit();
                                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                bitmap.StreamSource = File.OpenRead(img.Filename);
                                bitmap.EndInit();
                            }

                            var imgItem = new Image()
                            {
                                Visibility = visible ? Visibility.Visible : Visibility.Collapsed,
                                Source = bitmap,
                                Stretch = Stretch.UniformToFill,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Tag = img.Filename
                            };

                            if(ImageClickHandler != null)
                            {
                                imgItem.PreviewMouseLeftButtonDown += ImageClickHandler;
                            }
                            if (ContextMenu != null)
                            {
                                imgItem.ContextMenu = ContextMenu;
                            }

                            imgPositions.Add(new ImageUIInfo()
                            {
                                img = imgItem,
                                pos = (int)(block.Pos.Row + h),
                                visible = visible,
                            });

                            scrollGrid.Children.Add(new Border()
                            {
                                Child = imgItem,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = margin,
                                Width = block.Size.Width * scaleX,
                                Height = block.Size.Height * scaleY,
                                BorderThickness = new Thickness(2, 2, 2, 2),
                                Tag = "Image",
                            });
                        });
                        await Task.Delay(ImageLoadDelay);

                        if (taskStartTime != LoadTime)
                        {
                            break;
                        }
                    }
                    if (images.Any())
                    {
                        while (continueLoadingPred() && taskStartTime == LoadTime)
                        {
                            await Task.Delay(100);
                        }
                    }

                    h += mosaic.matrix.Count + GroupSpace / 100.0;
                }

                IsLoading = false;
            });


            return true;
        }
    }
}
