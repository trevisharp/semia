using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Threading.Tasks;

const float diff = 0.00001f;
var pends = new PendulumSistem[40];

float angle0 = -diff / 2;
for (int i = 0; i < pends.Length; i++)
{
    angle0 += diff * pends.Length;

    pends[i] = new PendulumSistem {
        A2 = angle0
    };
}

ApplicationConfiguration.Initialize();

var form = new Form {
    WindowState = FormWindowState.Maximized,
    FormBorderStyle = FormBorderStyle.None
};

form.KeyDown += (o, e) =>
{
    if (e.KeyCode == Keys.Escape)
        Application.Exit();
};

var pb = new PictureBox {
    Dock = DockStyle.Fill
};
form.Controls.Add(pb);

var now = DateTime.UtcNow;
var timer = new Timer {
    Interval = 25
};

Bitmap bmp = null!;
form.Load += (o, e) =>
{
    bmp = new Bitmap(form.Width, form.Height);
    pb.Image = bmp;
    timer.Start();
    now = DateTime.UtcNow;
};

timer.Tick += (o, e) =>
{
    simulate();
    draw();
};

Application.Run(form);

void draw()
{
    var g = Graphics.FromImage(bmp);
    var center = bmp.Width / 2;

    g.Clear(Color.White);

    var brush = new SolidBrush(
        Color.FromArgb(int.Min(255, 600 / pends.Length), 0, 0, 255)
    );

    foreach (var pend in pends)
    {
        var start = new PointF(pb.Width / 2, 0);
        var (dy, dx) = MathF.SinCos(pend.A1 + MathF.PI / 2);
        g.FillPolygon(brush,
            new PointF(start.X + 20 * dy, start.Y - 20 * dx),
            new PointF(start.X - 20 * dy, start.Y + 20 * dx),
            new PointF(start.X + 400 * dx - 20 * dy, start.Y + 400 * dy + 20 * dx),
            new PointF(start.X + 400 * dx + 20 * dy, start.Y + 400 * dy - 20 * dx)
        );

        start = new PointF(
            center + 400 * dx, 
            400 * dy
        );
        (dy, dx) = MathF.SinCos(pend.A2 + MathF.PI / 2);
        g.FillPolygon(brush,
            new PointF(start.X + 20 * dy, start.Y - 20 * dx),
            new PointF(start.X - 20 * dy, start.Y + 20 * dx),
            new PointF(start.X + 400 * dx - 20 * dy, start.Y + 400 * dy + 20 * dx),
            new PointF(start.X + 400 * dx + 20 * dy, start.Y + 400 * dy - 20 * dx)
        );
    }

    pb.Refresh();
}

void simulate()
{
    Parallel.ForEach(pends, p => p.Simulate());
}

public class PendulumSistem
{
    DateTime now = DateTime.UtcNow;

    public float A1 { get; set; } = MathF.PI / 2;
    public float A2 { get; set; } = 0;
    public float Da1 { get; set; } = 0;
    public float Da2 { get; set; } = 0;
    public int N { get; set; } = 10;

    public void Simulate()
    {
        var newNow = DateTime.UtcNow;
        var dt = (float)(newNow - now).TotalSeconds / N;
        now = newNow;

        for (int i = 0; i < N; i++)
            Tick(dt);
    }

    void Tick(float dt)
    {
        // 1/3 l ddA2 + 1/2 l ddA1 cos(A1 - A2) - 1/2 l dA1^2 sin(A1 - A2) + 1/2 g sin(A2) = 0
        // 4/3 l ddA1 + 1/2 l ddA2 cos(A1 - A2) + 1/2 l dA2^2 sin(A1 - A2) + 3/2 g sin(A1) = 0
        // l = 0.6, g = 10
        // 0.2 ddA2 + 0.3 ddA1 cos(A1 - A2) - 0.3 dA1^2 sin(A1 - A2) + 05 sin(A2) = 0
        // 0.8 ddA1 + 0.3 ddA2 cos(A1 - A2) + 0.3 dA2^2 sin(A1 - A2) + 15 sin(A1) = 0

        var (sin, cos) = MathF.SinCos(A1 - A2);
        var sinA1 = MathF.Sin(A1);
        var sinA2 = MathF.Sin(A2);

        // ddA1 = x && ddA2 = y
        // 0.2 y + 0.3 cos(A1 - A2) x - 0.3 dA1^2 sin(A1 - A2) + 5 sin(A2) = 0
        // 0.8 x + 0.3 cos(A1 - A2) y + 0.3 dA2^2 sin(A1 - A2) + 15 sin(A1) = 0

        var a1 = 0.3f * cos;
        var b1 = 0.2f;
        var c1 = - 0.3f * Da1 * Da1 * sin + 5 * sinA2;
        var a2 = 0.8f;
        var b2 = 0.3f * cos;
        var c2 = 0.3f * Da2 * Da2 * sin + 15 * sinA1;

        // a1 x + b1 y + c1 = 0
        // a2 x + b2 y + c2 = 0
        // y = -(a1 x + c1) / b1
        // a2 x - a1 b2 / b1 x - c1 b2 / b1 + c2 = 0
        // (a2 - a1 b2 / b1) x = c1 b2 / b1 - c2

        var Xu = a2 - a1 * b2 / b1;
        var Xo = c1 * b2 / b1 - c2;
        var ddA1 = Xo / Xu;

        var ddA2 = -(a1 * ddA1 + c1) / b1;

        Da1 += ddA1 * dt;
        Da2 += ddA2 * dt;

        A1 += Da1 * dt;
        A2 += Da2 * dt;
    }
}