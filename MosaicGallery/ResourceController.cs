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
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MosaicGallery
{
    public class ResourceController
    {
        public int ImageLoadDelay;

        private Grid scrollGrid;

        private List<WeakReference> weakReferences = new List<WeakReference>();

        public ResourceController(Grid scrollGrid)
        {
            this.scrollGrid = scrollGrid;
        }

        public void StartVisibilityControl(ConcurrentBag<ImageUIInfo> imgPositions, Func<double, bool> isVisiblePred, Func<bool> isScrolling)
        {
            weakReferences.AddRange(imgPositions.Select(x=>new WeakReference(x.Img)));

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
                                stream.Close();
                                stream.Dispose();
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    img.Source = null;
                                    img.Visibility = Visibility.Collapsed;
                                    img.CacheMode = new BitmapCache();
                                    img.UpdateLayout();
                                    item.Container.Image = CloneImage(item.Img);
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

        private Image CloneImage(Image itemImg)
        {
            return new Image() { 
                Tag = itemImg.Tag,
                Margin = itemImg.Margin,
                Width = itemImg.Width, 
                Height = itemImg.Height,
                Visibility = itemImg.Visibility
            };
        }
    }
}
