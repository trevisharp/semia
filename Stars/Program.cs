using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

const int SimulTicksPerFrame = 40;

float total = 0;
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
    objs.Add(new Ground {
        X = 0,
        Y = form.Height - 20,
        Width = form.Width,
        Height = 80,
        Refs = objs
    });
}

void Simulate(float dt)
{
    total += dt;
    if (total > 0.1f)
    {
        total = 0;
        AddStar(Cursor.Position);
        AddStar(Cursor.Position);
        AddStar(Cursor.Position);
    }

    objs.RemoveAll(x => x.Dead);
    
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

void AddStar(PointF point)
{
    var brush = new LinearGradientBrush(
        new PointF(form.Width / 2, 0),
        new PointF(form.Width / 2, form.Height),
        Color.Purple,
        Color.Orange
    );

    objs.AddRange(
        [ .. 
            Creator.StableStar(
                point.X, point.Y, 
                5 + 10 * Random.Shared.NextSingle(), 
                1_000_000
            )
            .AddSprite((objs, g) =>
            {
                var points = objs
                    .Where(obj => obj is Mass mass && !mass.Internal)
                    .Select(obj => (Mass)obj)
                    .Select(m => new PointF(m.X, m.Y));

                g.FillPolygon(brush, [ ..points ]);
            })
            .AddBehaviour((objs, dt, total) =>
            {
                if (total < 5f)
                    return;
                
                foreach (var obj in objs)
                    obj.Dead = true;
            })
        ]
    );
}

public abstract class PhysicalObject
{
    public bool Visible { get; set; } = true;
    public bool Physical { get; set; } = true;
    public bool Dead { get; set; } = false;

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
    public bool Internal { get; set; } = false;

    public override void Draw(Graphics g)
    {
        if (!Visible)
            return;
        
        g.FillEllipse(
            Brushes.WhiteSmoke,
            X - 4, Y - 4, 8, 8
        );
    }

    public override void Interact(float dt)
        => Fy += Weight * 500f;

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
        if (!Visible)
            return;
        
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
        var force = K * delta / 50;
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
    readonly Dictionary<Mass, int> contactMap = [];

    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public required List<PhysicalObject> Refs { get; init; }

    public override void Draw(Graphics g)
    {
        if (!Visible)
            return;
        
        g.FillRectangle(Brushes.Gray, X, Y, Width, Height);
        g.DrawRectangle(Pens.Black, X, Y, Width, Height);
    }

    public override void Interact(float dt)
    {
        foreach (var obj in Refs)
        {
            if (obj is not Mass mass)
                continue;
            
            if (!mass.Physical)
                continue;
            
            if (mass.Y < Y)
                continue;
            
            mass.Y = Y;
            mass.Sy *= -0.8f;

            if (!contactMap.TryAdd(mass, 1))
                contactMap[mass]++;
        }
    }

    public override void Move(float dt) { }
}

public class Sprite : PhysicalObject
{
    public Action<Graphics>? OnDraw;

    public override void Draw(Graphics g)
    {
        if (!Visible)
            return;
        
        OnDraw?.Invoke(g);
    }

    public override void Interact(float dt) { }

    public override void Move(float dt) { }
}

public class Ticker : PhysicalObject
{
    float total = 0;
    public event Action<float, float>? OnTick;

    public override void Draw(Graphics g) { }

    public override void Interact(float dt)
    {
        total += dt;
        OnTick?.Invoke(dt, total);
    }

    public override void Move(float dt) { }
}

public static class PhysicalObjectExtension
{
    public static float Distance(this Mass mass, Mass other)
    {
        var dx = mass.X - other.X;
        var dy = mass.Y - other.Y;
        return float.Sqrt(dx * dx + dy * dy);
    }

    public static (float x, float y) UnitVec(this Mass mass, Mass other)
    {
        var dx = other.X - mass.X;
        var dy = other.Y - mass.Y;
        var mod = float.Sqrt(dx * dx + dy * dy);
        return (dx / mod, dy / mod);
    }

    public static Spring Connect(this Mass mass, Mass other, float K)
        => new() {
            K = K,
            MassA = mass,
            MassB = other,
            Size = mass.Distance(other)
        };

    public static IEnumerable<PhysicalObject> ConvexConnect(
        this IEnumerable<Mass> masses, float springK
    )
    {
        foreach (var mass in masses)
        {
            yield return mass;
            
            foreach (var other in masses)
            {
                if (mass == other)
                    continue;
                
                yield return mass.Connect(other, springK);
            }
        }
    }

    public static IEnumerable<PhysicalObject> AddSprite(
        this IEnumerable<PhysicalObject> objs,
        Action<IEnumerable<PhysicalObject>, Graphics> onDraw)
    {
        var copy = objs.ToArray();
        foreach (var obj in copy)
        {
            obj.Visible = false;
            yield return obj;
        }

        var sprite = new Sprite();
        sprite.OnDraw += g => onDraw(copy, g);
        yield return sprite;
    }

    public static IEnumerable<PhysicalObject> AddBehaviour(
        this IEnumerable<PhysicalObject> objs,
        Action<IEnumerable<PhysicalObject>, float, float> behaviour
    )
    {
        var copy = objs.ToArray();
        foreach (var obj in copy)
            yield return obj;

        var ticker = new Ticker();
        ticker.OnTick += (dt, total) => behaviour(copy, dt, total);
        yield return ticker;
    }
}

public static class Creator
{
    public static IEnumerable<Mass> Polygon(
        float xcenter, float ycenter,
        float radius, int sides
    )
    {
        List<Mass> masses = [];
        var theta = Random.Shared.NextSingle();
        var dtheta = MathF.Tau / sides;
        
        for (int i = 0; i < sides; i++)
        {
            masses.Add(new Mass
            {
                Weight = 1,
                X = xcenter + MathF.Cos(theta) * radius,
                Y = ycenter + MathF.Sin(theta) * radius
            });
            theta += dtheta;
        }

        return masses;
    }

    public static IEnumerable<PhysicalObject> StableStar(
        float xcenter, float ycenter,
        float radius, int springK
    )
    {
        var center = 
            Polygon(xcenter, ycenter, radius, 10)
            .ToArray();

        center[0].Sx = (Random.Shared.NextSingle() - 0.5f) * 5_000;

        var sy = - Random.Shared.NextSingle() * 500;
        foreach (var mass in center)
            mass.Sy = sy;
        
        var springs = center
            .ConvexConnect(springK)
            .Where(b => b is Spring)
            .ToArray();
        
        foreach (var spring in springs)
            yield return spring;

        for (int i = 0; i < 5; i++)
        {
            var m1 = center[2 * i];
            var m2 = center[2 * i + 1];
            var index = 2 * i + 2 >= center.Length
                ? 0 : 2 * i + 2; 
            var m3 = center[index];

            m2.Internal = true;

            var (ux, uy) = m1.UnitVec(m3);
            var m4 = new Mass {
                X = m2.X + uy * radius,
                Y = m2.Y - ux * radius,
                Weight = 1
            };

            List<Mass> subStar = [ m1, m2, m3, m4 ];
            var stableSubStar = subStar.ConvexConnect(springK);
            foreach (var spring in stableSubStar.Where(b => b is Spring))
                yield return spring;
            

            yield return m1;
            yield return m2;
            yield return m4;
        }
    }
}