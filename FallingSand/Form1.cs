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
        private bool isImageGenerationStopped = false;


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

        // New UI Controls
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
            grid = new ElementType[GridWidth, GridHeight];
            fireLifetimes = new FireElement[GridWidth, GridHeight];
            woodColors = new Color[GridWidth, GridHeight];
            canvas = new Bitmap(GridWidth, GridHeight);

            Width = GridWidth * PixelSize + 40;
            Height = GridHeight * PixelSize + 100; // Added more space for the progress bar and button
            Text = "Image Fire Simulator";

            display = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = new Bitmap(GridWidth * PixelSize, GridHeight * PixelSize),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Black
            };
            Controls.Add(display);

            // Create a panel to hold the progress bar, stop button, and process label
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(10)
            };
            Controls.Add(bottomPanel);

            // Create the progress bar
            progressBar = new ProgressBar
            {
                Width = Width - 40,
                Style = ProgressBarStyle.Continuous
            };
            bottomPanel.Controls.Add(progressBar);

            // Create the stop button
            stopButton = new Button
            {
                Text = "Stop",
                Top = progressBar.Bottom + 10,
                Width = 100
            };
            stopButton.Click += StopButton_Click;
            bottomPanel.Controls.Add(stopButton);

            // Create the process status label
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

        private void StopButton_Click(object sender, EventArgs e)
        {
            isGeneratingImage = false;  // Stop image generation
            isImageGenerationStopped = true; // Set the flag that the process was stopped by the user
            stopButton.Enabled = false;  // Disable the stop button
            processStatusLabel.Text = "Status: Generation stopped by user.";  // Update the label to reflect the user stop
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
                    // Reset the flag to allow new generation to proceed
                    isImageGenerationStopped = false;

                    // Load the image asynchronously
                    Image img = Image.FromFile(files[0]);
                    Bitmap resized = await Task.Run(() => Pixelate(img, GridWidth, GridHeight));

                    // Stop the simulation while we are processing the image
                    simulationTimer.Stop();

                    // Reset the grid
                    for (int y = 0; y < GridHeight; y++)
                    {
                        for (int x = 0; x < GridWidth; x++)
                        {
                            grid[x, y] = ElementType.Empty;
                            fireLifetimes[x, y] = null;
                            woodColors[x, y] = Color.Empty;
                        }
                    }

                    // Start the image generation
                    isGeneratingImage = true;
                    progressBar.Value = 0;
                    stopButton.Enabled = true;
                    processStatusLabel.Text = "Status: Generation in progress...";

                    // Start a new task to generate the image row by row
                    await GenerateImageGradually(resized);

                    // Restart the simulation timer after image is generated
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
                if (!isGeneratingImage) break; // Stop if user clicked the stop button

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

                // Update the progress bar
                progressBar.Value = (int)((double)(y + 1) / totalRows * 100);

                // Delay to simulate gradual image generation (adjust this to control the speed)
                await Task.Delay(20); // 20ms delay between each row

                Render(); // Re-render the grid with the updated row
            }

            // Finish the progress bar at 100% when done
            progressBar.Value = 100;

            // Check if the process was stopped by the user
            if (isImageGenerationStopped)
            {
                processStatusLabel.Text = "Status: Generation stopped by user."; // Set the status message
            }
            else
            {
                // Update the label when the generation is complete
                processStatusLabel.Text = "Status: Generation completed.";
            }

            // Disable the stop button once the generation is complete or stopped
            stopButton.Enabled = false;
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

                    if (grid[x, y] == ElementType.Sand && !moved[x, y])
                    {
                        if (grid[x, y + 1] == ElementType.Empty)
                        {
                            nextGrid[x, y + 1] = ElementType.Sand;
                            nextGrid[x, y] = ElementType.Empty;
                            moved[x, y + 1] = true;
                        }
                        else if (grid[x - 1, y + 1] == ElementType.Empty)
                        {
                            nextGrid[x - 1, y + 1] = ElementType.Sand;
                            nextGrid[x, y] = ElementType.Empty;
                            moved[x - 1, y + 1] = true;
                        }
                        else if (grid[x + 1, y + 1] == ElementType.Empty)
                        {
                            nextGrid[x + 1, y + 1] = ElementType.Sand;
                            nextGrid[x, y] = ElementType.Empty;
                            moved[x + 1, y + 1] = true;
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
