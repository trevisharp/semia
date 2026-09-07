using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

const int SimulTicksPerFrame = 1;

List<PhysicalObject> objs = [];

#region Basic Setup

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
    objs.AddRange(CreateObject(900, 400, 50));

    objs.Add(new Ground
    {
        X = 0,
        Y = form.Height - 20,
        Width = form.Width,
        Height = 80,
        Refs = objs
    });
}

void Simulate(float dt)
{
    foreach (var obj in objs)
        obj.Interact(dt);
    
    foreach (var obj in objs)
        obj.Move(dt);
}

void Draw(Graphics g)
{
    foreach (var obj in objs)
        obj.Draw(g);
}

List<PhysicalObject> CreateObject(float x, float y, float size)
{
    var m1 = new Mass {
        X = x - size / 2,
        Y = y - size / 2
    };
    var m2 = new Mass {
        X = x + size / 2,
        Y = y - size / 2
    };
    var m3 = new Mass {
        X = x - size / 2,
        Y = y + size / 2
    };
    var m4 = new Mass {
        X = x + size / 2,
        Y = y + size / 2
    };

    var s1 = new Spring {
        MassA = m1,
        MassB = m2,
        K = 100,
        Size = size
    };

    var s2 = new Spring {
        MassA = m2,
        MassB = m3,
        K = 100,
        Size = size
    };

    var s3 = new Spring {
        MassA = m3,
        MassB = m4,
        K = 100,
        Size = size
    };

    var s4 = new Spring {
        MassA = m4,
        MassB = m1,
        K = 100,
        Size = size
    };

    return [ m1, m2, m3, m4, s1, s2, s3, s4 ];
}

public abstract class PhysicalObject
{
    public abstract void Draw(Graphics g);
    public abstract void Interact(float dt);
    public abstract void Move(float dt);
}

public class Mass : PhysicalObject
{
    public float X { get; set; }
    public float Y { get; set; }

    public float Sx { get; set; }
    public float Sy { get; set; }

    public float Fx { get; set; }
    public float Fy { get; set; }

    public float Weight { get; set; } = 1;

    public override void Draw(Graphics g)
    {
        g.FillEllipse(
            Brushes.WhiteSmoke,
            X - 4, Y - 4, 8, 8
        );
    }

    public override void Interact(float dt)
        => Fy += Weight * 98f;

    public override void Move(float dt)
    {
        Sx += Fx * dt / Weight;
        Sy += Fy * dt / Weight;

        X += Sx * dt;
        Y += Sy * dt;

        Fx = Fy = 0;
    }
}

public class Spring : PhysicalObject
{
    public required Mass MassA { get; init; }
    public required Mass MassB { get; init; }
    public required float Size { get; init; }
    public required float K { get; init; }

    public override void Draw(Graphics g)
    {
        g.DrawLine(
            Pens.Red,
            MassA.X, MassA.Y,
            MassB.X, MassB.Y
        );
    }

    public override void Interact(float dt)
    {
        var dx = MassA.X - MassB.X;
        var dy = MassA.Y - MassB.Y;
        var size = float.Sqrt(dx * dx + dy * dy);
        var nx = dx / size;
        var ny = dy / size;

        var delta = Size - size;
        var force = K * delta;
        var fx = force * nx;
        var fy = force * ny;

        MassA.Fx += fx;
        MassA.Fy += fy;

        MassB.Fx -= fx;
        MassB.Fy -= fy;
    }

    public override void Move(float dt) { }
}

public class Ground : PhysicalObject
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public required List<PhysicalObject> Refs { get; init; }

    public override void Draw(Graphics g)
    {
        g.FillRectangle(Brushes.Gray, X, Y, Width, Height);
        g.DrawRectangle(Pens.Black, X, Y, Width, Height);
    }

    public override void Interact(float dt)
    {
        foreach (var obj in Refs)
        {
            if (obj is not Mass mass)
                continue;
            
            if (mass.Y < Y)
                continue;
            
            mass.Y = -(Y - mass.Y);
            mass.Sy *= -1;
        }
    }

    public override void Move(float dt) { }
}