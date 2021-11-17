using MosaicGallery.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MosaicGallery
{
    public class MosaicMatrix
    {
        public List<BlockInfo[]> matrix = new List<BlockInfo[]>();
        public List<BlockInfo> blocks = new List<BlockInfo>();

        private int[] level;

        public int SplitCount { get; private set; }

        public MosaicMatrix(int split = 6)
        {
            SplitCount = split;
            level = new int[split];
            matrix.Add(new BlockInfo[SplitCount]);
        }

        private (int, int) FitImage(int level, int width, int direction)
        {
            int k = level;
            while (true)
            {
                int counter = 0;

                for (int i = 0; i < SplitCount; i++)
                {
                    var t = direction > 0 ? i : SplitCount - i - 1;
                    if (matrix[k][t] == null)
                    {
                        counter++;
                        if (counter == width)
                        {
                            return (k, direction > 0 ? t - width + 1 : t);
                        }
                    }
                    else
                    {
                        counter = 0;
                        if (direction > 0 ? t + width > SplitCount : t - width < 0)
                        {
                            break;
                        }
                    }
                }
                k++;
            }

        }

        private void SetImage(ImageInfo img, (int, int) p, int width)
        {
            int height = img.Orientation == Orientation.Horizontal ? width : width * 2;

            while (matrix.Count <= p.Item1 + height)
            {
                matrix.Add(new BlockInfo[SplitCount]);
            }

            var blockInfo = new BlockInfo()
            {
                Img = img,
                Pos = p,
                Size = (width, height),
            };
            blocks.Add(blockInfo);

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    matrix[p.Item1 + j][p.Item2 + i] = blockInfo;
                }

                level[p.Item2 + i] = p.Item1 + height;
            }
        }

        public void FitImages(List<ImageInfo> images, Random rand, (int size, int weight)[] sizes)
        {
            images.Reverse();
            var wRand = new Weighted_Randomizer.DynamicWeightedRandomizer<int>(rand.Next());

            while (images.Any())
            {
                (int level, int maxWidth) = GetMinLevelAndSize();

                wRand.Clear();
                foreach (var s in sizes.Where(x=>x.size <= maxWidth))
                {
                    wRand.Add(s.size, s.weight);
                }

                int size = wRand.NextWithReplacement();
                //int size = rand.Next(1, Math.Min(maxWidth, 3));
                ImageInfo img = images[images.Count - 1];
                images.RemoveAt(images.Count - 1);

                var p = FitImage(level, size, rand.Next(2) == 0 ? 1 : -1);
                SetImage(img, p, size);
            }

            bool keepRow = false;
            for (int i = 0; i < SplitCount; i++)
            {
                if(matrix[matrix.Count - 1][i] != null)
                {
                    keepRow = true;
                    break;
                }
            }

            if (!keepRow)
            {
                matrix.RemoveAt(matrix.Count - 1);
            }

        }

        private (int, int) GetMinLevelAndSize()
        {
            var min = level.Min();

            int width = 0;

            for (int i = 0; i < level.Length; i++)
            {
                if(level[i] == min)
                {
                    width += 1;
                }
                else if(width > 0)
                {
                    break;
                }
            }

            return (min, width);
        }

        private List<BlockInfo> GetMissingBlocks()
        {
            List<BlockInfo> res = new List<BlockInfo>();

            int[] height = new int[SplitCount];

            for (int i = 0; i < level.Length; i++)
            {
                if (matrix[matrix.Count - 1][i] == null)
                {
                    for (int k = 1; k < matrix.Count; k++)
                    {
                        if (matrix[matrix.Count - 1 - k][i] != null)
                        {
                            height[i] = k;
                            break;
                        }
                    }
                }
            }

            void CreateBlocks(int _x, int _width, int _height)
            {
                int row = matrix.Count - _height;

                int sliced = 0;

                for (int i = 0; i < _height - sliced; i++)
                {
                    if (_width * 2 == (_height - sliced - i) || _width == (_height - sliced - i))
                    {
                        var block = new BlockInfo
                        {
                            Pos = (row + sliced, _x),
                            Size = (_width, _height - sliced - i),
                        };
                        res.Add(block);

                        sliced += _height - sliced - i;

                        i = -1;
                    }
                }
            }

            int lastHeight = 0;
            int startX = 0;

            for (int i = 0; i < SplitCount; i++)
            {
                if (height[i] != lastHeight || i - startX >= lastHeight)
                {
                    if (lastHeight > 0)
                    {
                        CreateBlocks(startX, i - startX, lastHeight);
                    }

                    startX = i;
                    lastHeight = height[i];
                }
            }

            if(lastHeight > 0)
            {
                CreateBlocks(startX, SplitCount - startX, lastHeight);
            }


            return res;
        }

        public void AddBlock(BlockInfo block)
        {
            blocks.Add(block);
            FixBlock(block);
        }

        public void FixBlock(BlockInfo block)
        {
            for (int i = 0; i < block.Size.Height; i++)
            {
                for (int j = 0; j < block.Size.Width; j++)
                {
                    matrix[block.Pos.Row + i][block.Pos.Col + j] = block;
                }
            }
        }

        public void RemoveBlock(BlockInfo block)
        {
            for (int i = 0; i < block.Size.Height; i++)
            {
                for (int j = 0; j < block.Size.Width; j++)
                {
                    matrix[block.Pos.Row + i][block.Pos.Col + j] = null;
                }
            }

            blocks.Remove(block);
        }

        private List<(Func<MosaicMatrix, List<ImageInfo>> Func, int Hor, int Vert)> ReplaceActions = new List<(Func<MosaicMatrix, List<ImageInfo>>, int, int)>()
        {
            (new Func<MosaicMatrix, List<ImageInfo>>((mosaic) =>
            {
                foreach (var d in new int[]{-1, 1})
                {
                    var v2 = mosaic.blocks.FirstOrDefault(b =>
                    {
                        return b.Size == (1, 2)
                            && b.Pos.Col + d >= 0
                            &&  b.Pos.Col + d < mosaic.SplitCount
                            && b.Pos.Row + 1 < mosaic.matrix.Count
                            && mosaic.matrix[b.Pos.Row][b.Pos.Col + d]?.Size == (1, 1)
                            && mosaic.matrix[b.Pos.Row + 1][b.Pos.Col + d]?.Size == (1, 1);
                    });

                    if(v2 != null)
                    {
                        var h1_1 = mosaic.matrix[v2.Pos.Row][v2.Pos.Col + d];
                        var h1_2 = mosaic.matrix[v2.Pos.Row + 1][v2.Pos.Col + d];

                        if(d != -1)
                        {
                            h1_1.Pos = v2.Pos;
                        }

                        h1_1.Size = (2, 2);

                        mosaic.RemoveBlock(h1_2);
                        mosaic.RemoveBlock(v2);

                        mosaic.FixBlock(h1_1);

                        return new List<ImageInfo> { h1_2.Img, v2.Img};
                    }
                }



                return null;
            }), 1, 1),

            (new Func<MosaicMatrix, List<ImageInfo>>((mosaic) =>
            {
                var v2 = mosaic.blocks.FirstOrDefault(b =>
                {
                    return b.Size == (1, 2)
                        &&  b.Pos.Col + 1 < mosaic.SplitCount
                        && b.Pos.Row + 1 < mosaic.matrix.Count
                        && mosaic.matrix[b.Pos.Row][b.Pos.Col + 1]?.Size == (1, 2)
                        && mosaic.matrix[b.Pos.Row + 2][b.Pos.Col]?.Size == (1, 2)
                        && mosaic.matrix[b.Pos.Row + 2][b.Pos.Col + 1]?.Size == (1, 2);
                });

                if(v2 != null)
                {
                    var res = new List<ImageInfo>()
                    {
                        mosaic.matrix[v2.Pos.Row][v2.Pos.Col + 1].Img,
                        mosaic.matrix[v2.Pos.Row + 2][v2.Pos.Col].Img,
                        mosaic.matrix[v2.Pos.Row + 2][v2.Pos.Col + 1].Img,
                    };

                    v2.Size = (2, 4);

                    mosaic.RemoveBlock(mosaic.matrix[v2.Pos.Row][v2.Pos.Col + 1]);
                    mosaic.RemoveBlock(mosaic.matrix[v2.Pos.Row + 2][v2.Pos.Col]);
                    mosaic.RemoveBlock(mosaic.matrix[v2.Pos.Row + 2][v2.Pos.Col + 1]);

                    mosaic.FixBlock(v2);

                    return res;
                }

                return null;
            }), 0, 3),

            (new Func<MosaicMatrix, List<ImageInfo>>((mosaic) =>
            {
                var h1 = mosaic.blocks.FirstOrDefault(b =>
                {
                    return b.Size == (1, 1)
                        &&  b.Pos.Col + 1 < mosaic.SplitCount
                        && b.Pos.Row + 1 < mosaic.matrix.Count
                        && mosaic.matrix[b.Pos.Row][b.Pos.Col + 1]?.Size == (1, 1)
                        && mosaic.matrix[b.Pos.Row + 1][b.Pos.Col]?.Size == (1, 1)
                        && mosaic.matrix[b.Pos.Row + 1][b.Pos.Col + 1]?.Size == (1, 1);
                });

                if(h1 != null)
                {
                    var res = new List<ImageInfo>()
                    {
                        mosaic.matrix[h1.Pos.Row][h1.Pos.Col + 1].Img,
                        mosaic.matrix[h1.Pos.Row + 1][h1.Pos.Col].Img,
                        mosaic.matrix[h1.Pos.Row + 1][h1.Pos.Col + 1].Img,
                    };

                    h1.Size = (2, 2);

                    mosaic.RemoveBlock(mosaic.matrix[h1.Pos.Row][h1.Pos.Col + 1]);
                    mosaic.RemoveBlock(mosaic.matrix[h1.Pos.Row + 1][h1.Pos.Col]);
                    mosaic.RemoveBlock(mosaic.matrix[h1.Pos.Row + 1][h1.Pos.Col + 1]);

                    mosaic.FixBlock(h1);


                    return res;
                }

                return null;
            }), 3, 0),

            (new Func<MosaicMatrix, List<ImageInfo>>((mosaic) =>
            {
                var v2 = mosaic.blocks.FirstOrDefault(b =>
                {
                    return b.Size == (1, 2)
                        &&  b.Pos.Col + 1 < mosaic.SplitCount
                        && mosaic.matrix[b.Pos.Row][b.Pos.Col + 1]?.Size == (1, 2);
                });

                var h1 = mosaic.blocks.FirstOrDefault(b =>
                {
                    return b.Size == (1, 1)
                        && b.Pos.Row + 1 < mosaic.matrix.Count
                        && mosaic.matrix[b.Pos.Row + 1][b.Pos.Col]?.Size == (1, 1);
                });

                if(v2 != null && h1 != null)
                {
                    var block1 = mosaic.matrix[v2.Pos.Row][v2.Pos.Col + 1];
                    var block2 =  mosaic.matrix[h1.Pos.Row + 1][h1.Pos.Col];

                    var res = new List<ImageInfo>()
                    {
                        block1.Img,
                        block2.Img,
                    };

                    mosaic.RemoveBlock(block1);
                    mosaic.RemoveBlock(block2);


                    h1.Size = (2,2);

                    var v2_p = v2.Pos;
                    var h1_p = h1.Pos;
                    h1.Pos = v2_p;
                    v2.Pos = h1_p;


                    mosaic.FixBlock(h1);
                    mosaic.FixBlock(v2);

                    return res;
                }

                return null;
            }), 1, 1),
        };

        public void FixLevels()
        {
            var missing = GetMissingBlocks();
            ILookup<bool, BlockInfo> splitted = missing.ToLookup(x => x.Pos.Row >= x.Pos.Col);
            var vert = new Queue<BlockInfo>(splitted[false]);
            var hor = new Queue<BlockInfo>(splitted[true]);

            bool stop = false;

            while((vert.Count > 0 || hor.Count > 0) && !stop)
            {
                stop = true;

                var algs = ReplaceActions.Where(x => x.Vert <= vert.Count && x.Hor <= hor.Count).OrderByDescending(x => x.Vert + x.Hor).ToList();

                foreach (var a in algs)
                {
                    var imgs = a.Func(this);

                    if (imgs != null)
                    {
                        foreach (ImageInfo img in imgs)
                        {
                            var block = img.Orientation == Orientation.Horizontal ? hor.Dequeue() : vert.Dequeue();
                            block.Img = img;

                            for (int i = 0; i < block.Size.Width; i++)
                            {
                                for (int j = 0; j < block.Size.Height; j++)
                                {
                                    matrix[block.Pos.Row + j][block.Pos.Col + i] = block;
                                }
                            }

                            blocks.Add(block);
                        }

                        stop = false;

                        break;
                    }
                }

                if (stop)
                {
                    int hor_size = hor.Count;
                    var r = SplitMissing(hor, vert);

                    hor = new Queue<BlockInfo>(r.hor);
                    vert = new Queue<BlockInfo>(r.vert);

                    if (hor.Count != hor_size)
                    {
                        stop = false;
                    }
                }
            }

        }

        public void FixLevelsWithImages(List<ImageInfo> images)
        {
            var missing = GetMissingBlocks();
            foreach (BlockInfo item in missing)
            {
                var orient = item.Size.Height > item.Size.Width ? Orientation.Vertical : Orientation.Horizontal;
                var img = images.FirstOrDefault(x => x.Orientation == orient);
                images.Remove(img);

                if(img != null)
                {
                    item.Img = img;
                    AddBlock(item);
                }
            }
        }

        private (List<BlockInfo> hor, List<BlockInfo> vert) SplitMissing(IEnumerable<BlockInfo> hor, IEnumerable<BlockInfo> vert)
        {
            List<BlockInfo> r_hor = new List<BlockInfo>();
            List<BlockInfo> r_vert = new List<BlockInfo>();

            int hor_count = hor.Count();
            int vert_count = vert.Count();

            foreach (var item in hor)
            {
                if(item.Size.Width > 1 && hor_count <= vert_count + 1)
                {
                    r_vert.Add(new BlockInfo()
                    {
                        Pos = item.Pos,
                        Size = (item.Size.Width/2, item.Size.Height)
                    });

                    r_vert.Add(new BlockInfo()
                    {
                        Pos = (item.Pos.Row, item.Pos.Col + item.Size.Width / 2),
                        Size = (item.Size.Width / 2, item.Size.Height)
                    });
                    hor_count -= 1;
                    vert_count += 2;
                }
                else
                {
                    r_hor.Add(item);
                }
            }

            foreach (var item in vert)
            {
                if (item.Size.Height > 1 && vert_count <= hor_count + 1)
                {
                    r_hor.Add(new BlockInfo()
                    {
                        Pos = item.Pos,
                        Size = (item.Size.Width, item.Size.Height / 2)
                    });

                    r_hor.Add(new BlockInfo()
                    {
                        Pos = (item.Pos.Row + item.Size.Height / 2, item.Pos.Col),
                        Size = (item.Size.Width, item.Size.Height / 2)
                    });
                    vert_count -= 1;
                    hor_count += 2;
                }
                else
                {
                    r_vert.Add(item);
                }
            }

            return (r_hor, r_vert);
        }

        public BlockInfo[][] GetMatrix()
        {
            return matrix.ToArray();
        }

    }
}
