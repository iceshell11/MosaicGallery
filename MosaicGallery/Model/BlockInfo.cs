using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MosaicGallery.Model
{
    public class BlockInfo
    {
        public (int Width, int Height) Size;
        public (int Row, int Col) Pos;
        public ImageInfo Img;

        public override string ToString()
        {
            return $"{Img} ({Pos.Row}, {Pos.Col}, {Size.Width}, {Size.Height})";
        }
    }

}
