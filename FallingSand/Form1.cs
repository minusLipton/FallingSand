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

        private enum ElementType { Empty, Wood, Fire, Smoke, Ember, Sand, Water }

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

        private ProgressBar progressBar;
        private Button stopButton;
        private Label processStatusLabel;
        private bool isGeneratingImage = false;

        public Form1()
        {
            InitializeComponent();
            InitializeSimulation();
        }

        private void InitializeSimulation()
        {
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            grid = new ElementType[GridWidth, GridHeight];
            fireLifetimes = new FireElement[GridWidth, GridHeight];
            woodColors = new Color[GridWidth, GridHeight];
            canvas = new Bitmap(GridWidth, GridHeight);

            Width = GridWidth * PixelSize + 40;
            Height = GridHeight * PixelSize + 100;
            Text = "Image Fire Simulator";

            display = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = new Bitmap(GridWidth * PixelSize, GridHeight * PixelSize),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Black
            };
            Controls.Add(display);

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(10)
            };
            Controls.Add(bottomPanel);

            progressBar = new ProgressBar
            {
                Width = Width - 40,
                Style = ProgressBarStyle.Continuous
            };
            bottomPanel.Controls.Add(progressBar);

            stopButton = new Button
            {
                Text = "Stop",
                Top = progressBar.Bottom + 10,
                Width = 100
            };
            stopButton.Click += StopButton_Click;
            bottomPanel.Controls.Add(stopButton);

            processStatusLabel = new Label
            {
                Text = "Status: Waiting for image...",
                Top = progressBar.Bottom + 10,
                Left = stopButton.Right + 10,
                Width = 250
            };
            bottomPanel.Controls.Add(processStatusLabel);

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

            simulationTimer = new System.Windows.Forms.Timer { Interval = 33 };
            simulationTimer.Tick += (s, e) => UpdateSimulation();
            simulationTimer.Start();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.D1: currentElement = ElementType.Fire; break;
                case Keys.D2: currentElement = ElementType.Sand; break;
                case Keys.D3: currentElement = ElementType.Wood; break;
                case Keys.D4: currentElement = ElementType.Smoke; break;
                case Keys.D5: currentElement = ElementType.Ember; break;
                case Keys.D6: currentElement = ElementType.Water; break;
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            isGeneratingImage = false;
            stopButton.Enabled = false;
            processStatusLabel.Text = "Status: Generation stopped by user";
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

                    //simulationTimer.Stop();

                    for (int y = 0; y < GridHeight; y++)
                        for (int x = 0; x < GridWidth; x++)
                        {
                            grid[x, y] = ElementType.Empty;
                            fireLifetimes[x, y] = null;
                            woodColors[x, y] = Color.Empty;
                        }

                    isGeneratingImage = true;
                    progressBar.Value = 0;
                    stopButton.Enabled = true;
                    processStatusLabel.Text = "Status: Generation in progress...";

                    await GenerateImageGradually(resized);

                    if (isGeneratingImage)
                    {
                        progressBar.Value = 100;
                        processStatusLabel.Text = "Status: Generation completed.";
                        stopButton.Enabled = false;
                    }
                    else
                    {
                        processStatusLabel.Text = "Status: Generation stopped by user";
                    }

                    isGeneratingImage = false;
                    simulationTimer.Start();
                }
                catch (Exception ex)
                {
                    processStatusLabel.Text = "Status: Error loading image.";
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

        private async Task GenerateImageGradually(Bitmap resized)
        {
            int totalRows = GridHeight;
            for (int y = 0; y < totalRows; y++)
            {
                if (!isGeneratingImage) break;

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

                progressBar.Value = (int)((double)(y + 1) / totalRows * 100);
                await Task.Delay(20);
                Render();
            }
        }


        private void UpdateSimulation()
        {
            if (isMouseDown)
            {
                int x = mouseGridPos.X;
                int y = mouseGridPos.Y;

                if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                {
                    // Determine brush size based on the current element
                    int brushSizeX = 0, brushSizeY = 0;
                    if (currentElement == ElementType.Water || currentElement == ElementType.Sand)
                    {
                        brushSizeX = 3; // 3x3 for Water and Sand
                        brushSizeY = 3;
                    }
                    else if (currentElement == ElementType.Wood)
                    {
                        brushSizeX = 5; // 5x5 for Wood
                        brushSizeY = 5;
                    }

                    // Loop through the grid cells within the brush size
                    for (int i = -brushSizeX / 2; i <= brushSizeX / 2; i++)
                    {
                        for (int j = -brushSizeY / 2; j <= brushSizeY / 2; j++)
                        {
                            int newX = x + i;
                            int newY = y + j;

                            if (newX >= 0 && newX < GridWidth && newY >= 0 && newY < GridHeight)
                            {
                                if (currentElement == ElementType.Fire)
                                {
                                    grid[newX, newY] = ElementType.Fire;
                                    fireLifetimes[newX, newY] = new FireElement(rand.Next(500, 2000));
                                }
                                else if (currentElement == ElementType.Wood)
                                {
                                    grid[newX, newY] = ElementType.Wood;
                                    woodColors[newX, newY] = Color.SaddleBrown;
                                }
                                else
                                {
                                    grid[newX, newY] = currentElement;
                                }
                            }
                        }
                    }
                }
            }

            // Continue the rest of the simulation logic as before
            ElementType[,] nextGrid = (ElementType[,])grid.Clone();
            FireElement[,] nextFireLifetimes = (FireElement[,])fireLifetimes.Clone();
            bool[,] moved = new bool[GridWidth, GridHeight];

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

                    if ((grid[x, y] == ElementType.Sand || grid[x, y] == ElementType.Water) && !moved[x, y])
                    {
                        bool isWater = grid[x, y] == ElementType.Water;

                        // Movement logic for water and sand as before...
                        if (grid[x, y + 1] == ElementType.Empty)
                        {
                            nextGrid[x, y + 1] = grid[x, y];
                            nextGrid[x, y] = ElementType.Empty;
                            moved[x, y + 1] = true;
                        }
                        else if (grid[x - 1, y + 1] == ElementType.Empty)
                        {
                            nextGrid[x - 1, y + 1] = grid[x, y];
                            nextGrid[x, y] = ElementType.Empty;
                            moved[x - 1, y + 1] = true;
                        }
                        else if (grid[x + 1, y + 1] == ElementType.Empty)
                        {
                            nextGrid[x + 1, y + 1] = grid[x, y];
                            nextGrid[x, y] = ElementType.Empty;
                            moved[x + 1, y + 1] = true;
                        }
                        else if (isWater)
                        {
                            // 1. Try to move straight down
                            if (grid[x, y + 1] == ElementType.Empty)
                            {
                                nextGrid[x, y + 1] = ElementType.Water;
                                nextGrid[x, y] = ElementType.Empty;
                                moved[x, y + 1] = true;
                            }
                            else
                            {
                                // 2. Try diagonal left
                                if (x > 0 && grid[x - 1, y + 1] == ElementType.Empty)
                                {
                                    nextGrid[x - 1, y + 1] = ElementType.Water;
                                    nextGrid[x, y] = ElementType.Empty;
                                    moved[x - 1, y + 1] = true;
                                }
                                // 3. Try diagonal right
                                else if (x < GridWidth - 1 && grid[x + 1, y + 1] == ElementType.Empty)
                                {
                                    nextGrid[x + 1, y + 1] = ElementType.Water;
                                    nextGrid[x, y] = ElementType.Empty;
                                    moved[x + 1, y + 1] = true;
                                }
                                else
                                {
                                    // 4. Scan left
                                    for (int offset = 1; x - offset > 0; offset++)
                                    {
                                        if (grid[x - offset, y] != ElementType.Empty)
                                            break; // Wall on left

                                        if (grid[x - offset, y + 1] == ElementType.Empty)
                                        {
                                            nextGrid[x - offset, y + 1] = ElementType.Water;
                                            nextGrid[x, y] = ElementType.Empty;
                                            moved[x - offset, y + 1] = true;
                                            break;
                                        }
                                    }

                                    // 5. Scan right
                                    for (int offset = 1; x + offset < GridWidth; offset++)
                                    {
                                        if (grid[x + offset, y] != ElementType.Empty)
                                            break; // Wall on right

                                        if (grid[x + offset, y + 1] == ElementType.Empty)
                                        {
                                            nextGrid[x + offset, y + 1] = ElementType.Water;
                                            nextGrid[x, y] = ElementType.Empty;
                                            moved[x + offset, y + 1] = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (int y = 1; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (grid[x, y] == ElementType.Smoke)
                    {
                        int sway = rand.Next(-1, 2);
                        int newX = x + sway;
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



        private void FillContainer(int startX, int startY, ref ElementType[,] nextGrid)
        {
            // Create a stack to simulate DFS (Depth-First Search) to find and fill the connected empty spaces
            Stack<Point> stack = new Stack<Point>();
            stack.Push(new Point(startX, startY));

            while (stack.Count > 0)
            {
                Point current = stack.Pop();
                int cx = current.X;
                int cy = current.Y;

                // If the position is out of bounds or not empty, continue to the next iteration
                if (cx < 0 || cx >= GridWidth || cy < 0 || cy >= GridHeight || grid[cx, cy] != ElementType.Empty)
                    continue;

                // Mark the current position as water (this simulates filling the container)
                nextGrid[cx, cy] = ElementType.Water;

                // Add neighboring cells to the stack (left, right, down, up)
                stack.Push(new Point(cx + 1, cy));
                stack.Push(new Point(cx - 1, cy));
                stack.Push(new Point(cx, cy + 1));
                stack.Push(new Point(cx, cy - 1));
            }
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
                            case ElementType.Wood: color = woodColors[x, y]; break;
                            case ElementType.Fire: color = Color.OrangeRed; break;
                            case ElementType.Smoke: color = Color.FromArgb(80, 80, 80); break;
                            case ElementType.Ember: color = Color.FromArgb(139, 0, 0); break;
                            case ElementType.Sand: color = Color.Gold; break;
                            case ElementType.Water: color = Color.DeepSkyBlue; break;
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
    }
}
