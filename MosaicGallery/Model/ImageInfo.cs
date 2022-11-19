using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MosaicGallery.Model
{
    public class ImageInfo
    {
        public string Filename { get; set; }

        public Orientation Orientation { get; set; }
        public string? Metadata { get; set; }

        public ImageInfo(string filename, Orientation orientation)
        {
            Filename = filename;
            Orientation = orientation;
        }

        public override string ToString()
        {
            return Filename;
        }

        public bool IsMatchFilter(string filter)
        {
            return string.IsNullOrWhiteSpace(filter) 
                   || Filename.Contains(filter, StringComparison.InvariantCultureIgnoreCase) 
                   || Metadata?.Contains(filter, StringComparison.InvariantCultureIgnoreCase) == true;
        }
    }
}
