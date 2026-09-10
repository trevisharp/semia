using System;
using System.Drawing;
using System.Windows.Forms;

const float m = 0.1f;

const int N1 = 2;
const float T1 = 0.1f;

const int N2 = 5 * N1;
const float T2 = T1 / 10;

const int ConcFrameImpact = 55;
const int Frames = 100;
const int SimulTicksPerFrame = 20;

var x = new float[N1 + N2];
var y = new float[N1 + N2];
var dx = new float[N1 + N2];
var dy = new float[N1 + N2];

var wx = 1f;
var wdx = 0f;
const float wm = 10f;

var v1 = float.Sqrt(2 * T1 / (N1 * m));
var v2 = float.Sqrt(2 * T2 / (N2 * m));

for (int k = 0; k < N1; k++)
{
    x[k] = Random.Shared.NextSingle();
    y[k] = Random.Shared.NextSingle();

    var theta = MathF.Tau * Random.Shared.NextSingle();
    dx[k] = v1 * MathF.Cos(theta);
    dy[k] = v1 * MathF.Sin(theta);
}

for (int k = 0; k < N2; k++)
{
    x[N1 + k] = 1f + Random.Shared.NextSingle();
    y[N1 + k] = Random.Shared.NextSingle();

    var theta = MathF.Tau * Random.Shared.NextSingle();
    dx[N1 + k] = v2 * MathF.Cos(theta);
    dy[N1 + k] = v2 * MathF.Sin(theta);
}

#region Core

ApplicationConfiguration.Initialize();

var form = new Form {
    WindowState = FormWindowState.Maximized,
    FormBorderStyle = FormBorderStyle.None,
    TransparencyKey = Color.Magenta,
    ShowInTaskbar = false,
    TopMost = true
};

form.KeyDown += (o, e) =>
{
    if (e.KeyCode == Keys.Escape)
        form.Close();
};

form.Load += (o, e) => Start();

Bitmap bmp = null!;
var pb = new PictureBox {
    Dock = DockStyle.Fill
};
form.Controls.Add(pb);
form.Load += (o, e) => 
    pb.Image = bmp = new Bitmap(pb.Width, pb.Height);

var timer = new Timer {
    Interval = 20
};
form.Load += (o, e) => timer.Start();

var now = DateTime.UtcNow;
form.Load += (o, e) => now = DateTime.UtcNow;

timer.Tick += (o, e) =>
{
    var g = Graphics.FromImage(bmp);

    var newNow = DateTime.UtcNow;
    var dt = (float)(newNow - now).TotalSeconds / SimulTicksPerFrame;
    for (int i = 0; i < SimulTicksPerFrame; i++)
        Simulate(dt);
    now = newNow;
    
    g.Clear(Color.Magenta);
    Draw(g);
    pb.Refresh();
};

Application.Run(form);

#endregion

void Start()
{
    
}

void Simulate(float dt)
{
    for (int k = 0; k < N1 + N2; k++)
    {
        x[k] += dx[k] * dt;
        y[k] += dy[k] * dt;

        if (x[k] < 0f)
        {
            x[k] *= -1;
            dx[k] *= -1;
        }
        else if (x[k] > 2f)
        {
            x[k] = 4f - x[k];
            dx[k] *= -1;
        }
        
        if (y[k] < 0f)
        {
            y[k] *= -1;
            dy[k] *= -1;
        }
        else if (y[k] > 1f)
        {
            y[k] = 2f - y[k];
            dy[k] *= -1;
        }

        if (k < N1 && x[k] > wx)
        {
            x[k] = 2 * wx - x[k];
            // Conservação do momento
            wdx += 2 * dx[k] * m / wm;
            dx[k] *= -1;
        }
        else if (k >= N1 && x[k] < wx)
        {
            x[k] = 2 * wx - x[k];
            // Conservação do momento
            wdx += 2 * dx[k] * m / wm;
            dx[k] *= -1;
        }
    }

    wx += wdx * dt;
}

void Draw(Graphics g)
{
    var size = float.Min(
        8 * form.Height / 10,
        4 * form.Width / 10
    );
    var rect = new RectangleF(
        (form.Width - 2 * size) / 2,
        (form.Height - size) / 2,
        2 * size, size
    );

    g.Clear(Color.Black);
    g.FillRectangle(Brushes.White, rect);

    var array = new int[2 * Frames * Frames];
    for (int k = 0; k < x.Length; k++)
    {
        var i = (int)(Frames * x[k]);
        var j = (int)(Frames * y[k]);
        array[i + j * 2 * Frames]++;
    }

    var a = size / Frames;
    for (int j = 0; j < Frames; j++)
        for (int i = 0; i < 2 * Frames; i++)
        {
            var index = i + j * 2 * Frames;
            var concentration = array[index];
            var level = int.Max(0, 255 - ConcFrameImpact * concentration);

            var color = i < wx * Frames ?
                Color.FromArgb(255, level, level) :
                Color.FromArgb(level, level, 255);
            var brush = new SolidBrush(color);
            
            g.FillRectangle(
                brush,
                rect.X + i * a,
                rect.Y + j * a,
                a, a
            );
        }
    
    var wallPos = rect.X + size * wx;
    g.DrawLine(Pens.Black, 
        wallPos, rect.Y,
        wallPos, rect.Y + rect.Height
    );
}