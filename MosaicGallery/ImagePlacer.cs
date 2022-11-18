using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        private Point Scale => new Point(scrollGrid.RenderSize.Width / SplitCount, scrollGrid.RenderSize.Width / SplitCount / 1.4142857);

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


        private Grid scrollGrid;

        public MouseButtonEventHandler ImageClickHandler;
        public ContextMenu ContextMenu;
        public OrderType OrderType = OrderType.CreationTypeDes;

        public List<ImageInfo> images { get; set; }
        public List<ImageInfo> uiImages { get; set; }
        public Task ImagePrepareTask = Task.CompletedTask;

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

        public Task PrepareImages()
        {
            ImagePrepareTask.Wait();

            ImagePrepareTask = Task.Run(() =>
            {
                Random rand = new Random(Seed);

                var usorted_files = Directory.GetFiles(Path, "*", SearchOption).Where(x =>
                    Extentions.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase)));
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

                Parallel.ForEach(Partitioner.Create(0, files.Length), range =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        string file = files[i];

                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var bitmapFrame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation,
                                BitmapCacheOption.None);
                            var width = bitmapFrame.PixelWidth;
                            var height = bitmapFrame.PixelHeight;
                            parallelRes[i] = new ImageInfo(file,
                                width > height ? Model.Orientation.Horizontal : Model.Orientation.Vertical);
                        }

                    }
                });

                images = new List<ImageInfo>(parallelRes);
                return Task.CompletedTask;
            });

            return ImagePrepareTask;
        }


        public async void LoadImages(ConcurrentBag<ImageUIInfo> imgPositions, Func<bool> continueLoadingPred, Func<bool> isScrolling, CancellationToken cancellationToken)
        {
            Point scale = Scale;
            Random rand = new Random(Seed);
            //foreach (var file in files)
            //{
            //    var bitmap = new BitmapImage();

            //    bitmap.BeginInit();
            //    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            //    bitmap.StreamSource = File.OpenRead(file);
            //    bitmap.EndInit();

            //    images.Add(new ImageInfo(file, bitmap.Width > bitmap.Height ? Model.Orientation.Horizontal : Model.Orientation.Vertical));
            //}

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
            });

            while (imgPositions.Any())
            {
                imgPositions.TryTake(out _);
            }


            GC.Collect();

            double h = 0;
            while (true)
            {
                if (!images.Any())
                {
                    await Task.Delay(100);
                    return;
                }
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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ImageContainer container = null;

                        if (block.Img is ImageUIInfo uiInfo)
                        {
                            container = uiInfo.Container;
                        }
                        else
                        {

                            string metadata = block.Img.Filename.EndsWith(".png") ? PngMetadataReader.ReadMetadata(block.Img.Filename) : null;

                            var img = block.Img;
                            // var margin = GetImageMargin(block.Pos, Scale, h);


                            var imgItem = new Image()
                            {
                                Visibility = Visibility.Collapsed,
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

                            container = new ImageContainer(imgItem, metadata);

                            imgPositions.Add(new ImageUIInfo(block.Img)
                            {
                                Pos = (int)(block.Pos.Row + h),
                                Container = container
                            });

                            scrollGrid.Children.Add(container);
                        }

                        container.Margin = GetImageMargin(block.Pos, scale, h);
                        container.Width = block.Size.Width * scale.X;
                        container.Height = block.Size.Height * scale.Y;
                    });

                    await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                if (images.Any())
                {
                    while (continueLoadingPred())
                    {
                        await Task.Delay(100);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }

                h += mosaic.matrix.Count + GroupSpace / 100.0;
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private Thickness GetImageMargin((int Row, int Col) pos, Point scale, double h)
        {
            return new Thickness(pos.Col * scale.X, (pos.Row + h) * scale.Y, 0, 0);
        }


        //public async void PlaceImages()
        //{
        //    Random rand = new Random(Seed);
        //    var scale = Scale;

        //    double h = 0;
        //    while (images.Any())
        //    {
        //        List<ImageUIInfo> taken;
        //        if (IsGrouping)
        //        {
        //            taken = images.Take(rand.Next(ImgPerGroup.from, ImgPerGroup.to + 1)).ToList();
        //            images.RemoveRange(0, taken.Count);
        //        }
        //        else
        //        {
        //            taken = new List<ImageUIInfo>(images);
        //            images.Clear();
        //        }

        //        var mosaic = new MosaicMatrix(SplitCount);

        //        mosaic.FitImages(taken.Cast<ImageInfo>().ToList(), rand, Sizes);
        //        mosaic.FixLevels();
        //        mosaic.FixLevelsWithImages(images.Cast<ImageInfo>().ToList());

        //        foreach (BlockInfo block in mosaic.blocks)
        //        {
        //            var info = (ImageUIInfo)block.Img;
        //            info.Container.Margin = GetImageMargin(block.Pos, scale, h);
        //            info.Container.Width = block.Size.Width * scale.X;
        //            info.Container.Height = block.Size.Height * scale.Y;
        //        }
        //        h += mosaic.matrix.Count + GroupSpace / 100.0;
        //    }
        //}
    }
}
