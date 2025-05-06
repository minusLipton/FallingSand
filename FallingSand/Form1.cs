using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace FallingSand
{
    public partial class Form1 : Form
    {
        private int GridWidth = 300;
        private int GridHeight = 225;
        private const int PixelSize = 4;

        private enum ElementType { Empty, Wood, Fire, Smoke, Ember, Sand }

        private class FireElement
        {
            public DateTime TimeToDie;
            public int Lifetime { get; private set; }

            public FireElement(int lifetime)
            {
                Lifetime = lifetime;
                TimeToDie = DateTime.Now.AddMilliseconds(lifetime);
            }
        }

        private ElementType[,] grid;
        private FireElement[,] fireLifetimes;
        private Color[,] woodColors;

        private Bitmap canvas;
        private PictureBox display;
        private System.Windows.Forms.Timer simulationTimer;
        private Random rand = new Random();

        private bool isMouseDown = false;
        private Point mouseGridPos = Point.Empty;
        private ElementType currentElement = ElementType.Fire;

        public Form1()
        {
            InitializeComponent();
            InitializeSimulation();
        }

        private void InitializeSimulation()
        {
            grid = new ElementType[GridWidth, GridHeight];
            fireLifetimes = new FireElement[GridWidth, GridHeight];
            woodColors = new Color[GridWidth, GridHeight];
            canvas = new Bitmap(GridWidth, GridHeight);

            Width = GridWidth * PixelSize + 40;
            Height = GridHeight * PixelSize + 60;
            Text = "Image Fire Simulator";

            display = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = new Bitmap(GridWidth * PixelSize, GridHeight * PixelSize),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Black
            };
            Controls.Add(display);

            AllowDrop = true;
            DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            DragDrop += OnImageDropped;

            display.MouseDown += (s, e) =>
            {
                isMouseDown = true;
                UpdateMouseGridPosition(e.Location);
            };

            display.MouseUp += (s, e) => isMouseDown = false;

            display.MouseMove += (s, e) =>
            {
                if (isMouseDown)
                    UpdateMouseGridPosition(e.Location);
            };

            this.KeyDown += (s, e) =>
            {
                switch (e.KeyCode)
                {
                    case Keys.D1: currentElement = ElementType.Fire; break;
                    case Keys.D2: currentElement = ElementType.Sand; break;
                    case Keys.D3: currentElement = ElementType.Wood; break;
                    case Keys.D4: currentElement = ElementType.Smoke; break;
                    case Keys.D5: currentElement = ElementType.Ember; break;
                }
            };

            simulationTimer = new System.Windows.Forms.Timer { Interval = 33 };
            simulationTimer.Tick += (s, e) => UpdateSimulation();
            simulationTimer.Start();
        }

        private void UpdateMouseGridPosition(Point mousePos)
        {
            int x = mousePos.X / PixelSize;
            int y = mousePos.Y / PixelSize;
            if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
            {
                mouseGridPos = new Point(x, y);
            }
        }

        private async void OnImageDropped(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                try
                {
                    Image img = Image.FromFile(files[0]);
                    Bitmap resized = await Task.Run(() => Pixelate(img, GridWidth, GridHeight));

                    simulationTimer.Stop();

                    for (int y = 0; y < GridHeight; y++)
                    {
                        for (int x = 0; x < GridWidth; x++)
                        {
                            Color color = resized.GetPixel(x, y);
                            if (color.A > 128)
                            {
                                grid[x, y] = ElementType.Wood;
                                woodColors[x, y] = color;
                                fireLifetimes[x, y] = null;
                            }
                            else
                            {
                                grid[x, y] = ElementType.Empty;
                            }
                        }
                    }

                    simulationTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading image: " + ex.Message);
                }
            }
        }

        private Bitmap Pixelate(Image image, int width, int height)
        {
            Bitmap resized = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image, new Rectangle(0, 0, width, height));
            }
            return resized;
        }

        private void UpdateSimulation()
        {
            if (isMouseDown)
            {
                int x = mouseGridPos.X;
                int y = mouseGridPos.Y;

                if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                {
                    if (currentElement == ElementType.Fire)
                    {
                        grid[x, y] = ElementType.Fire;
                        fireLifetimes[x, y] = new FireElement(rand.Next(500, 2000));
                    }
                    else if (currentElement == ElementType.Wood)
                    {
                        grid[x, y] = ElementType.Wood;
                        woodColors[x, y] = Color.SaddleBrown;
                    }
                    else
                    {
                        grid[x, y] = currentElement;
                    }
                }
            }

            ElementType[,] nextGrid = (ElementType[,])grid.Clone();
            FireElement[,] nextFireLifetimes = (FireElement[,])fireLifetimes.Clone();

            for (int y = GridHeight - 2; y >= 1; y--)
            {
                for (int x = 1; x < GridWidth - 1; x++)
                {
                    if (grid[x, y] == ElementType.Fire)
                    {
                        if (fireLifetimes[x, y] != null && DateTime.Now >= fireLifetimes[x, y].TimeToDie)
                        {
                            nextGrid[x, y] = rand.NextDouble() < 0.3 ? ElementType.Smoke : ElementType.Ember;
                            nextFireLifetimes[x, y] = null;
                        }
                        else
                        {
                            TrySpread(x - 1, y, nextGrid, nextFireLifetimes);
                            TrySpread(x + 1, y, nextGrid, nextFireLifetimes);
                            TrySpread(x, y + 1, nextGrid, nextFireLifetimes);
                            TrySpread(x, y - 1, nextGrid, nextFireLifetimes);
                        }
                    }

                    if (grid[x, y] == ElementType.Sand && grid[x, y + 1] == ElementType.Empty)
                    {
                        nextGrid[x, y + 1] = ElementType.Sand;
                        nextGrid[x, y] = ElementType.Empty;
                    }
                }
            }

            for (int y = 1; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (grid[x, y] == ElementType.Smoke)
                    {
                        int swayAmount = rand.Next(-1, 2);
                        int newX = x + swayAmount;

                        if (newX >= 0 && newX < GridWidth && grid[newX, y - 1] == ElementType.Empty)
                        {
                            nextGrid[newX, y - 1] = ElementType.Smoke;
                            nextGrid[x, y] = ElementType.Empty;
                        }
                        else if (rand.NextDouble() < 0.02)
                        {
                            nextGrid[x, y] = ElementType.Empty;
                        }
                    }
                    else if (grid[x, y] == ElementType.Ember && rand.NextDouble() < 0.05)
                    {
                        nextGrid[x, y] = ElementType.Empty;
                    }
                }
            }

            grid = nextGrid;
            fireLifetimes = nextFireLifetimes;
            Render();
        }

        private void TrySpread(int x, int y, ElementType[,] nextGrid, FireElement[,] nextFireLifetimes)
        {
            if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
            {
                if (grid[x, y] == ElementType.Wood && rand.NextDouble() < 0.3)
                {
                    nextGrid[x, y] = ElementType.Fire;
                    nextFireLifetimes[x, y] = new FireElement(rand.Next(500, 2000));
                }
            }
        }

        private void Render()
        {
            using (Graphics g = Graphics.FromImage(canvas))
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        Color color = Color.Black;
                        switch (grid[x, y])
                        {
                            case ElementType.Wood:
                                color = woodColors[x, y]; break;
                            case ElementType.Fire:
                                color = Color.OrangeRed; break;
                            case ElementType.Smoke:
                                color = Color.FromArgb(80, 80, 80); break;
                            case ElementType.Ember:
                                color = Color.FromArgb(139, 0, 0); break;
                            case ElementType.Sand:
                                color = Color.Gold; break;
                        }
                        canvas.SetPixel(x, y, color);
                    }
                }
            }

            Bitmap scaled = new Bitmap(GridWidth * PixelSize, GridHeight * PixelSize);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(canvas, new Rectangle(0, 0, scaled.Width, scaled.Height));
            }

            display.Image?.Dispose();
            display.Image = scaled;
        }

        private void Form1_Load(object sender, EventArgs e) { }
    }
}
