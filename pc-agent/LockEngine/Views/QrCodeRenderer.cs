using System.Drawing;
using System.Drawing.Drawing2D;

namespace PC.SecurityAgent.LockEngine.Views
{
    /// <summary>
    /// Utility for rendering stylized cybersecurity QR codes and positioning markers.
    /// </summary>
    public static class QrCodeRenderer
    {
        public static void RenderMockQrBadge(Graphics g, int size)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(15, 23, 42));

            using var brush = new SolidBrush(Color.FromArgb(56, 189, 248));

            DrawQrEye(g, 15, 15, 40);
            DrawQrEye(g, size - 55, 15, 40);
            DrawQrEye(g, 15, size - 55, 40);

            for (int x = 65; x < size - 65; x += 15)
            {
                for (int y = 20; y < size - 20; y += 15)
                {
                    if ((x * y + 7) % 3 == 0)
                    {
                        g.FillRectangle(brush, x, y, 10, 10);
                    }
                }
            }

            g.FillRectangle(new SolidBrush(Color.FromArgb(10, 15, 29)), (size - 36) / 2, (size - 36) / 2, 36, 36);
            using var font = new Font("Segoe UI", 13f, FontStyle.Bold);
            g.DrawString("\uD83D\uDD12", font, Brushes.White, (size - 30) / 2, (size - 34) / 2);
        }

        public static void DrawQrEye(Graphics g, int x, int y, int size)
        {
            using var outerPen = new Pen(Color.FromArgb(56, 189, 248), 3);
            using var innerBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
            g.DrawRectangle(outerPen, x, y, size, size);
            g.FillRectangle(innerBrush, x + 10, y + 10, size - 20, size - 20);
        }
    }
}
