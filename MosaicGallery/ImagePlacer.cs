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
using System.Windows.Shapes;
using System.Windows.Documents;

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
        public Task ImagePrepareTask = Task.CompletedTask;

        private string _filter = "";
        private double hOffset = 0;

        private readonly List<ImageInfo> _unplacedImages = new List<ImageInfo>();
        private readonly List<ImageInfo> _ignoredImages = new List<ImageInfo>();
        private readonly ConcurrentBag<ImageUIInfo> _placedImages;
        private readonly SemaphoreSlim _imagesSemaphore;
        public ImagePlacer(Grid scrollGrid, ConcurrentBag<ImageUIInfo> placedImages, SemaphoreSlim imagesSemaphore)
        {
            this.scrollGrid = scrollGrid;
            this._placedImages = placedImages;
            this._imagesSemaphore = imagesSemaphore;
        }

        public string Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                ResetDisplayedImages();
            }
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
                var files = Directory.GetFiles(Path, "*", SearchOption).Where(x =>
                    Extentions.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase))).ToArray();

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
                        parallelRes[i].Metadata = file.EndsWith(".png") ? PngMetadataReader.ReadMetadata(file) : null;
                    }
                });

                _unplacedImages.AddRange(SortImages(parallelRes.ToList()));

                return Task.CompletedTask;
            });

            return ImagePrepareTask;
        }

        private List<ImageInfo> SortImages(List<ImageInfo> imgArray)
        {
            Random rand = new Random(Seed);
            switch (OrderType)
            {
                case OrderType.CreationTypeDes:
                    imgArray = imgArray.OrderByDescending(x => File.GetCreationTime(x.Filename).Ticks).ToList();
                    break;
                case OrderType.NameDes:
                    imgArray = imgArray.OrderByDescending(x => x.Filename).ToList();
                    break;
                case OrderType.Random:
                    imgArray = imgArray.OrderByDescending(x => rand.Next()).ToList();
                    break;
                case OrderType.CreationTypeAsc:
                    imgArray = imgArray.OrderBy(x => File.GetCreationTime(x.Filename).Ticks).ToList();
                    break;
                case OrderType.NameAsc:
                    imgArray = imgArray.OrderBy(x => x.Filename).ToList();
                    break;
            }

            return imgArray;
        }

        private async void ResetDisplayedImages()
        {
            await _imagesSemaphore.WaitAsync();
            hOffset = 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var imageUiInfo in _placedImages)
                {
                    imageUiInfo.Container.ResetImage(ContextMenu, ImageClickHandler);
                    imageUiInfo.Container.Visibility = Visibility.Collapsed;
                }
            });


            var sorted = SortImages(_unplacedImages.Concat(_placedImages).Concat(_ignoredImages).ToList());
            var filterLookup = sorted.ToLookup(x => x.IsMatchFilter(Filter));
            _unplacedImages.Clear();
            _ignoredImages.Clear();
            _placedImages.Clear();
            _unplacedImages.AddRange(filterLookup[true]);
            _ignoredImages.AddRange(filterLookup[false]);
            GC.Collect();
            Application.Current.Dispatcher.Invoke(() =>
            {
                scrollGrid.UpdateLayout();
            });
            _imagesSemaphore.Release();
        }

        public async void LoadImages(Func<bool> continueLoadingPred, Func<bool> isScrolling, CancellationToken cancellationToken)
        {
            Point scale = Scale;
            Random rand = new Random(Seed);

            while (true)
            {
                while (!_unplacedImages.Any() || continueLoadingPred())
                {
                    await Task.Delay(100);
                }

                await _imagesSemaphore.WaitAsync();

                List<ImageInfo> taken;
                if (IsGrouping)
                {
                    int toTakeCount = rand.Next(ImgPerGroup.from, ImgPerGroup.to + 1);
                    taken = _unplacedImages.Take(toTakeCount).ToList();
                    _unplacedImages.RemoveRange(0, taken.Count);
                }
                else
                {
                    taken = new List<ImageInfo>(_unplacedImages);
                    _unplacedImages.Clear();
                }

                if (!taken.Any())
                {
                    continue;
                }

                var mosaic = new MosaicMatrix(SplitCount);

                mosaic.FitImages(taken, rand, Sizes);
                mosaic.FixLevels();
                mosaic.FixLevelsWithImages(_unplacedImages);

                foreach (BlockInfo block in mosaic.blocks)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ImageContainer container = null;

                        if (block.Img is ImageUIInfo uiInfo)
                        {
                            container = uiInfo.Container;
                            _placedImages.Add(uiInfo);
                        }
                        else
                        {
                            var imgItem = new Image()
                            {
                                Visibility = Visibility.Collapsed,
                                Stretch = Stretch.UniformToFill,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Tag = block.Img.Filename
                            };

                            if (ImageClickHandler != null)
                            {
                                imgItem.PreviewMouseLeftButtonDown += ImageClickHandler;
                            }
                            if (ContextMenu != null)
                            {
                                imgItem.ContextMenu = ContextMenu;
                            }

                            container = new ImageContainer(imgItem, block.Img.Metadata);

                            _placedImages.Add(new ImageUIInfo(block.Img)
                            {
                                Pos = (int)(block.Pos.Row + hOffset),
                                Container = container
                            });

                            scrollGrid.Children.Add(container);
                        }

                        container.Margin = GetImageMargin(block.Pos, scale, hOffset);
                        container.Width = block.Size.Width * scale.X;
                        container.Height = block.Size.Height * scale.Y;
                        container.Visibility = Visibility.Visible;
                    });

                    // await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                hOffset += mosaic.matrix.Count + GroupSpace / 100.0;

                _imagesSemaphore.Release();

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
    }
}
