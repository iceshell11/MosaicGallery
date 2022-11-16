using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace MosaicGallery
{
    public static class Extentions
    {
        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            return parent as T ?? FindParent<T>(parent);
        }

        public static T GetRandom<T>(this LinkedList<T> list, Random rand)
        {
            int r = rand.Next(list.Count);
            var f = list.First;
            for (int i = 0; i < r; i++)
            {
                f = f.Next;
            }
            list.Remove(f);

            return f.Value;
        }

        public static T GetRandom<T>(this LinkedList<T> list, Random rand, Func<T, bool> expr)
        {
            int count = list.Count(expr);

            int r = rand.Next(count);
            var f = list.First;
            for (int i = 0; i < r;)
            {
                if (expr(f.Value))
                {
                    i++;
                }

                f = f.Next;
            }
            list.Remove(f);

            return f.Value;
        }
    }
}
