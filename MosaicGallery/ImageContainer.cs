using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;

namespace MosaicGallery
{
    public class ImageContainer : Border
    {
        private readonly Grid _grid = new Grid();
        private readonly Image _image;
        public ImageContainer(Image imgItem, Size size, Thickness margin, string metadata = null)
        {
            this._image = imgItem;
            Child = _grid;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = margin;
            Width = size.Width;
            Height = size.Height;
            BorderThickness = new Thickness(2, 2, 2, 2);
            Tag = "Image";

            _grid.Children.Add(imgItem);
            if (metadata != null)
            {
                var image = new Image()
                {
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Source = new BitmapImage(new Uri("pack://application:,,,/Resources/comment.png")),
                    Stretch = Stretch.UniformToFill,
                    Visibility = Visibility.Visible,
                    ToolTip = new ToolTip()
                    {
                        Content = metadata
                    }
                };
                image.PreviewMouseLeftButtonDown += (sender, args) =>
                {
                    if (args.ClickCount == 2)
                    {
                        Clipboard.SetText(metadata);
                    }
                };
                _grid.Children.Add(image);
            }
        }

        public Image Image => _image;
    }
}
