﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;

namespace MosaicGallery
{
    public class ImageContainer : Border
    {
        private readonly Grid _grid = new Grid();
        private Image _image;
        public ImageContainer(Image imgItem, string? metadata)
        {
            this._image = imgItem;
            Child = _grid;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
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
                    Source = new BitmapImage(new Uri("/Resources/comment.png", UriKind.Relative)),
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

        public Image Image
        {
            get => _image;
            set
            {
                _grid.Children.Remove(_image);
                _grid.Children.Insert(0, value);
                _image = value;
            }
        }

        public void ResetImage(ContextMenu contextMenu, MouseButtonEventHandler imageClickHandler)
        {
            Stream? stream = (Image.Source as BitmapImage)?.StreamSource;
            ResetImage(stream, contextMenu, imageClickHandler);
        }

        public void ResetImage(Stream? stream, ContextMenu contextMenu, MouseButtonEventHandler imageClickHandler)
        {
            if (stream != null)
            {
                stream.Close();
                stream.Dispose();
                Image.Source = null;
                Image.Visibility = Visibility.Collapsed;
                Image.CacheMode = new BitmapCache();
                Image.UpdateLayout();
                Image = CloneImage(Image, contextMenu, imageClickHandler);
                Image.Visibility = Visibility.Collapsed;
            }
        }

        private Image CloneImage(Image itemImg, ContextMenu contextMenu, MouseButtonEventHandler imageClickHandler)
        {
            var image = new Image()
            {
                Tag = itemImg.Tag,
                Margin = itemImg.Margin,
                Width = itemImg.Width,
                Height = itemImg.Height,
                Visibility = itemImg.Visibility,
            };

            image.ContextMenu = contextMenu;
            image.PreviewMouseLeftButtonDown += imageClickHandler;
            return image;
        }
    }
}
