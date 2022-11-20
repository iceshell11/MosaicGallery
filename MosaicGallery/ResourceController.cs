using MosaicGallery.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MosaicGallery
{
    public class ResourceController
    {
        public int VisibilityDistance = 3000;
        public int ImageLoadDelay;
        public MouseButtonEventHandler ImageClickHandler;
        public ContextMenu ContextMenu;

        private Grid scrollGrid;

        private List<WeakReference> weakReferences = new List<WeakReference>();
        private readonly ConcurrentBag<ImageUIInfo> _placedImages;
        private readonly SemaphoreSlim _imagesSemaphore;

        public ResourceController(Grid scrollGrid, ConcurrentBag<ImageUIInfo> placedImages, SemaphoreSlim imagesSemaphore)
        {
            this.scrollGrid = scrollGrid;
            this._placedImages = placedImages;
            this._imagesSemaphore = imagesSemaphore;
        }

        public void StartVisibilityControl(Func<double> scrollContentOffsetSupplier, Func<bool> isScrolling)
        {
            weakReferences.AddRange(_placedImages.Select(x=>new WeakReference(x.Img)));

            Task.Run(async () => {

                long CG_CallTime = 0;

                while (true)
                {

                    double scaleY = scrollGrid.RenderSize.Width / 6 / 1.4142857;

                    var toUpdate = _placedImages.Where(x => x.Visible != Math.Abs(x.Pos * scaleY - scrollContentOffsetSupplier()) < VisibilityDistance).ToArray();

                    if (toUpdate.Any())
                    {
                        await _imagesSemaphore.WaitAsync();

                        foreach (var item in toUpdate)
                        {
                            var img = item.Img;
                            string path = "";
                            object imageSource = null;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                path = img.Tag.ToString();
                                imageSource = img.Source;
                            });

                            if (item.Visible && imageSource is BitmapImage bitmap && bitmap.StreamSource is Stream stream)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    item.Container.ResetImage(stream, ContextMenu, ImageClickHandler);
                                });
                            }
                            else if (!item.Visible)
                            {
                                Stream fileStream = File.OpenRead(path);
                                bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.StreamSource = fileStream;
                                bitmap.DecodePixelWidth = 800;
                                bitmap.EndInit();

                                bitmap.Freeze();


                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    img.Source = bitmap;
                                    img.Visibility = Visibility.Visible;
                                });
                            }
                            await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);
                        }

                        _imagesSemaphore.Release();
                    }

                    if (CG_CallTime < DateTime.Now.Ticks)
                    {
                        CG_CallTime = DateTime.Now.Ticks + 5000 * TimeSpan.TicksPerMillisecond;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                        });
                    }

                    await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);
                }
            });
        }
    }
}
