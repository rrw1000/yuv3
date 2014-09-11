/* DisplayYUVControl.cs */
/* (C) Kynesim Ltd 2014 */

using System;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Drawing;
using System.Threading.Tasks;

namespace yuv3
{
    public class LayerInfo
    {
        public Bitmap mBitmap;
        // The alpha for this image.
        public int     mAlpha;

        public LayerInfo()
        {
            mBitmap = null;
            mAlpha = 0;
        }
    }

    public class DisplayYUVControl : ScrollableControl
    {
        public AppState mAppState;
        // Layers, in order.
        public LayerInfo[] mLayers;
        // If non-Null, we are displaying a math function and this is it.
        public Bitmap mMath;
        public Size renderSize;

        public DisplayYUVControl(AppState in_as)
        {
            int i;
            mAppState = in_as;
            mLayers = new LayerInfo[Constants.kNumberOfChannels];
            for (i =0 ;i < Constants.kNumberOfChannels; ++i)
            {
                mLayers[i] = new LayerInfo();
            }
            mMath = null;
            Paint += new PaintEventHandler(Render);
            Size = new Size(320, 240);
        }

        public void UpdateLayer(int layer, YUVFile aFile)
        {
            /* Update mImage[layer] */
            int nr = 0, alpha = 0;
            LayerInfo my_info = mLayers[layer];
            if (aFile == null)
            {
                my_info.mBitmap = null;
            }
            else
            {
                // Let's assume that we are going to succeed in activating this layer.
                int i;
                for (i =0 ; i < mLayers.Length; ++i)
                {
                    if (mLayers[i].mBitmap != null || i == layer)
                    {
                        ++nr;
                    }
                }
                alpha = 255/nr;
                RenderToBitmap(mLayers[layer], aFile, alpha);
            }

            // Now, what is the new alpha?
            nr = 0;
            for (int i =0 ;i < mLayers.Length; ++i)
            {
                if (mLayers[i].mBitmap != null)
                {
                    ++nr;
                }
            }
            if (nr > 0)
            {
                alpha = 255/nr;
                for (int i = 0; i < mLayers.Length; ++i)
                {
                    if (mLayers[i].mBitmap != null && mLayers[i].mAlpha != alpha)
                    {
                        SetAlpha(mLayers[i], alpha);
                        mLayers[i].mAlpha = alpha;
                    }
                }
            }
            this.ImagesChanged();
        }

        public void RenderToBitmap(LayerInfo result, YUVFile aFile, int alpha)
        {
            bool ok;
            if (result.mBitmap == null ||
                result.mBitmap.Width != aFile.mWidth ||
                result.mBitmap.Height != aFile.mHeight)
            {
                result.mBitmap = new Bitmap(aFile.mWidth, aFile.mHeight, PixelFormat.Format32bppArgb);
            }
            System.Drawing.Imaging.BitmapData someData =
                result.mBitmap.LockBits(new Rectangle(0, 0, result.mBitmap.Width, result.mBitmap.Height),
                                        System.Drawing.Imaging.ImageLockMode.ReadWrite,
                                        result.mBitmap.PixelFormat);
            ok = aFile.ToRGB32(someData, alpha);
            result.mBitmap.UnlockBits(someData);
            if (!ok)
            {
                result.mBitmap = null;
            }
        }

        public unsafe void SetAlpha(LayerInfo result, int alpha)
        {
            if (result.mBitmap == null) return;
            System.Drawing.Imaging.BitmapData someData =
                result.mBitmap.LockBits(new Rectangle(0, 0, result.mBitmap.Width, result.mBitmap.Height),
                                        System.Drawing.Imaging.ImageLockMode.ReadWrite,
                                        result.mBitmap.PixelFormat);
            byte *a_buffer = (byte *)someData.Scan0.ToPointer();
            int stride = someData.Stride;
            int w = result.mBitmap.Width;
            Parallel.For(0, result.mBitmap.Height, (j) =>
            {
                byte *ptr = a_buffer + (j *stride) + 3;
                for (int i = 0; i < w; ++i, ptr += 4)
                {
                    *ptr = (byte)alpha;
                }
            });
            result.mBitmap.UnlockBits(someData);
        }


        public void ImagesChanged()
        {
            /* Work out the new size for this control */
            // We never claim to be any smaller than this.
            int max_w = 320, max_h = 240;
            int z = mAppState.Zoom;
            for (int i =0 ;i < mLayers.Length; ++i)
            {
                if (mLayers[i].mBitmap != null)
                {
                    int w = mLayers[i].mBitmap.Width * z;
                    int h = mLayers[i].mBitmap.Height * z;
                    if (w > max_w)
                    {
                        max_w =w;
                    }
                    if (h > max_h)
                    {
                        max_h = h;
                    }
                }
            }
            this.renderSize = new Size(max_w, max_h);
            this.Size = this.renderSize;
            this.Refresh();
        }

        public override Size GetPreferredSize(Size proposed)
        {
            return this.renderSize;
        }

        void Render(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            bool any = false;
            int zoom = mAppState.Zoom;
            Rectangle everywhere = new Rectangle(0, 0, zoom * this.renderSize.Width, 
                                                 zoom * this.renderSize.Height);

            // Speed up scaling -we do a lot of it.
            // g.InterpolationMode = InterpolationMode.NearestNeighbour;
            
            for (int i = 0; i < Constants.kNumberOfChannels; ++i)
            {
                if (mLayers[i].mBitmap != null)
                {
                    Bitmap a_map = mLayers[i].mBitmap;
                    Rectangle to_draw = new Rectangle(0, 0, 
                                                      a_map.Width * zoom, a_map.Height * zoom);
                    Console.WriteLine(String.Format("Paint {0}", i));
                    g.DrawImage(mLayers[i].mBitmap, to_draw);
                    any = true;
                }
            }

            if (!any)
            {
                Brush a_brush = new System.Drawing.SolidBrush(Color.FromArgb(50, Color.Gray));
                Pen blk_pen = new Pen(new System.Drawing.SolidBrush(Color.FromArgb(50, Color.Black)));
                g.FillRectangle(a_brush, everywhere);
                g.DrawLine( blk_pen, everywhere.Left, everywhere.Bottom,
                            everywhere.Right, everywhere.Top);
                g.DrawLine( blk_pen, everywhere.Right, everywhere.Bottom,
                            everywhere.Left, everywhere.Top );
            }
        }

    }
}