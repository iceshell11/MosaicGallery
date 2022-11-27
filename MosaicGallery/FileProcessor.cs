using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MosaicGallery
{
    public class FileProcessor
    {
        public void DeleteImages(params ImageContainer[] containers)
        {
            if (MessageBox.Show("Are you sure?", $"Deleting {containers.Length} files", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var container in containers)
                {
                    var image = container.Image;
                    var path = image.Tag.ToString();
                    BitmapImage source = (image.Source as BitmapImage)!;
                    var sourceStreamSource = source.StreamSource;
                    if (sourceStreamSource.CanRead)
                    {
                        sourceStreamSource.Close();
                    }
                    image.Source = null;
                    File.Delete(path);
                    container.Visibility = Visibility.Hidden;
                }
            }
        }
    }
}
