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

        public void StartVisibilityControl(ConcurrentBag<ImageUIInfo> imgPositions, Func<double, bool> isVisiblePred, Func<bool> isScrolling)
        {
            Task.Run(async () => {

                long CG_CallTime = 0;

                while (true)
                {

                    double scaleY = scrollGrid.RenderSize.Width / 6 / 1.4142857;

                    var toUpdate = imgPositions.Where(x => x.Visible != isVisiblePred(x.Pos * scaleY)).ToArray();

                    if (toUpdate.Any())
                    {
                        foreach (var item in toUpdate)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.Img.Visibility = item.Visible ? Visibility.Collapsed : Visibility.Visible;

                                var img = item.Img;

                                if (!item.Visible && img.Source is BitmapImage bitmap && bitmap.StreamSource is Stream stream)
                                {
                                    img.Source = null;
                                    img.UpdateLayout();

                                    //stream.Close();
                                    //stream.Dispose();

                                }
                                else if (item.Visible)
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
