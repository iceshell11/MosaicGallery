using MosaicGallery.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MosaicGallery
{
    public class ResourceController
    {
        public int ImageLoadDelay;

        private Grid scrollGrid;

        public ResourceController(Grid scrollGrid)
        {
            this.scrollGrid = scrollGrid;
        }

        public void StartVisabilityControl(ConcurrentBag<ImageUIInfo> imgPositions, Func<double, bool> isVisiblePred, Func<bool> isScrolling)
        {
            Task.Run(async () => {

                long CG_CallTime = 0;

                while (true)
                {

                    double scaleY = scrollGrid.RenderSize.Width / 6 / 1.4142857;

                    var toUpdate = imgPositions.Where(x => x.visible != isVisiblePred(x.pos * scaleY)).ToArray();

                    if (toUpdate.Any())
                    {
                        foreach (var item in toUpdate)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.visible = !item.visible;

                                var img = item.img;

                                if (!item.visible && img.Source is BitmapImage bitmap && bitmap.StreamSource is Stream stream)
                                {
                                    img.Source = null;
                                    img.UpdateLayout();

                                    //stream.Close();
                                    //stream.Dispose();

                                }
                                else if (item.visible)
                                {
                                    img.Source = null;

                                    bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                    bitmap.StreamSource = File.OpenRead(img.Tag.ToString());
                                    bitmap.EndInit();

                                    img.Source = bitmap;
                                }

                                img.Visibility = item.visible ? Visibility.Visible : Visibility.Collapsed;
                            });

                            await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);
                        }
                    }

                    if (CG_CallTime < DateTime.Now.Ticks)
                    {
                        CG_CallTime = DateTime.Now.Ticks + 5000 * TimeSpan.TicksPerMillisecond;
                        GC.Collect();
                    }

                    await Task.Delay(isScrolling() ? ImageLoadDelay * 4 : ImageLoadDelay);
                }
            });

        }
    }
}
