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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

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

        (int size, int weight)[] Sizes => new (int size, int weight)[]
        {
            (1, SmallCount),
            (2, MediumCount),
            (3, LargeCount),
        };

        public async void LoadImages(ConcurrentBag<ImageUIInfo> imgPositions, Func<double, bool> isVisiblePred, Func<bool> continueLoadingPred, Func<bool> isScrolling)
        {
            LoadTime = DateTime.Now;

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
                    if (item.Img.Source is BitmapImage img)
                    {
                        item.Img.Source = null;
                        item.Img.UpdateLayout();
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

            Point scale = Scale;
            Random rand = new Random(Seed);

            var usorted_files = Directory.GetFiles(Path, "*", SearchOption).Where(x => Extentions.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase)));
            string[] files = null;
            switch (OrderType)
            {
                case OrderType.CreationTypeDes:
                    files = usorted_files.OrderByDescending(x => System.IO.File.GetCreationTime(x).Ticks).ToArray();
                    break;
                case OrderType.NameDes:
                    files = usorted_files.OrderByDescending(x => x).ToArray();
                    break;
                case OrderType.Random:
                    files = usorted_files.OrderByDescending(x => rand.Next()).ToArray();
                    break;
                case OrderType.CreationTypeAsc:
                    files = usorted_files.OrderBy(x => System.IO.File.GetCreationTime(x).Ticks).ToArray();
                    break;
                case OrderType.NameAsc:
                    files = usorted_files.OrderBy(x => x).ToArray();
                    break;
            }

            ImageInfo[] parallelRes = new ImageInfo[files.Length];

            Parallel.ForEach(Partitioner.Create(0, files.Length), range => {
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    string file = files[i];

                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var bitmapFrame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        var width = bitmapFrame.PixelWidth;
                        var height = bitmapFrame.PixelHeight;
                        parallelRes[i] = new ImageInfo(file, width > height ? Model.Orientation.Horizontal : Model.Orientation.Vertical);
                    }

                }
            });

            List<ImageInfo> images = new List<ImageInfo>(parallelRes);

            //foreach (var file in files)
            //{
            //    var bitmap = new BitmapImage();

            //    bitmap.BeginInit();
            //    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            //    bitmap.StreamSource = File.OpenRead(file);
            //    bitmap.EndInit();

            //    images.Add(new ImageInfo(file, bitmap.Width > bitmap.Height ? Model.Orientation.Horizontal : Model.Orientation.Vertical));
            //}

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

                mosaic.FitImages(taken, rand, Sizes);
                mosaic.FixLevels();
                mosaic.FixLevelsWithImages(images);

                foreach (BlockInfo block in mosaic.blocks)
                {
                    string metadata = block.Img.Filename.EndsWith(".png") ? PngMetadataReader.ReadMetadata(block.Img.Filename) : null;

                    Application.Current.Dispatcher.Invoke(() =>
                    {

                        var img = block.Img;
                        var margin = GetImageMargin(block.Pos, Scale, h);

                        var bitmap = new BitmapImage();

                        var imgItem = new Image()
                        {
                            Visibility = Visibility.Collapsed,
                            Source = bitmap,
                            Stretch = Stretch.UniformToFill,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Tag = img.Filename
                        };

                        if (ImageClickHandler != null)
                        {
                            imgItem.PreviewMouseLeftButtonDown += ImageClickHandler;
                        }
                        if (ContextMenu != null)
                        {
                            imgItem.ContextMenu = ContextMenu;
                        }

                        var imageContainer = new ImageContainer(imgItem, new Size(block.Size.Width * scale.X, block.Size.Height * scale.Y), margin, metadata);

                        imgPositions.Add(new ImageUIInfo(block.Img)
                        {
                            Img = imgItem,
                            Pos = (int)(block.Pos.Row + h),
                            Container = imageContainer
                        });

                        scrollGrid.Children.Add(imageContainer);
                    });
                    await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);

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
        }

        private Thickness GetImageMargin((int Row, int Col) pos, Point scale, double h)
        {
            return new Thickness(pos.Col * scale.X, (pos.Row + h) * scale.Y, 0, 0);
        }

        private Point Scale => new Point(scrollGrid.RenderSize.Width / SplitCount, scrollGrid.RenderSize.Width / SplitCount / 1.4142857);

        public async void PlaceImages(IEnumerable<ImageUIInfo> imgPositions, Random rand)
        {
            IsLoading = true;
            while (IsLoading)
            {
                await Task.Delay(100);
            }

            var scale = Scale;

            var images = new List<ImageUIInfo>(imgPositions);

            List<ImageUIInfo> taken;
            if (IsGrouping)
            {
                taken = images.Take(rand.Next(ImgPerGroup.from, ImgPerGroup.to + 1)).ToList();
                images.RemoveRange(0, taken.Count);
            }
            else
            {
                taken = new List<ImageUIInfo>(images);
                images.Clear();
            }

            double h = 0;
            while (images.Any())
            {
                var mosaic = new MosaicMatrix(SplitCount);

                mosaic.FitImages(taken.Cast<ImageInfo>().ToList(), rand, Sizes);
                mosaic.FixLevels();
                mosaic.FixLevelsWithImages(images.Cast<ImageInfo>().ToList());

                foreach (BlockInfo block in mosaic.blocks)
                {
                    var info = (ImageUIInfo)block.Img;
                    info.Container.Margin = GetImageMargin(block.Pos, scale, h);
                }
                h += mosaic.matrix.Count + GroupSpace / 100.0;
            }

            IsLoading = false;
        }
    }
}
