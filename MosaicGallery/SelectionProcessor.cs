using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace MosaicGallery
{
    public class SelectionProcessor
    {
        private List<ImageContainer> containers = new List<ImageContainer>();
        public ImageContainer[] SelectedImages => containers.ToArray();

        public void Select(ImageContainer obj)
        {
            obj.BorderBrush = new SolidColorBrush(Colors.Fuchsia);
            containers.Add(obj);
        }

        public void Deselect(ImageContainer obj)
        {
            obj.BorderBrush = null;
            containers.Remove(obj);
        }

        public void ResetSelection()
        {
            foreach (var container in containers)
            {
                container.BorderBrush = null;
            }

            containers.Clear();
        }

        public void ToggleSelection(ImageContainer obj)
        {
            if (containers.Contains(obj))
            {
                Deselect(obj);
            }
            else
            {
                Select(obj);
            }
        }

        public bool Contains(Border obj)
        {
            return containers.Contains(obj);
        }
    }
}
