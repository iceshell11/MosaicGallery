using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MosaicGallery.Model
{
    public class ImageUIInfo : ImageInfo
    {
        public int Pos;
        public ImageContainer Container;

        public ImageUIInfo(ImageInfo imageInfo) : base(imageInfo.Filename, imageInfo.Orientation)
        {
            Metadata = imageInfo.Metadata;
        }

        public Image Img => Container.Image;
        public bool Visible => Img.Visibility == Visibility.Visible;
    }
}
