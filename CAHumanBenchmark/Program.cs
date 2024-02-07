using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    private const int MOUSEEVENTF_LEFTDOWN = 0x02;
    private const int MOUSEEVENTF_LEFTUP = 0x04;
    
    static void Main( string [] args )
    {
        List<Point> lastDetectedCenters = new List<Point>();
        GameState gameState = GameState.Detecting;
        Console.WriteLine( "Press any key to start." );
        Console.ReadKey();
        Console.WriteLine( "Press ESC to stop." );
        do
        {
            while ( !Console.KeyAvailable )
            {
                // Define the screen area to analyze
                Rectangle area = new Rectangle(765, 269, 390, 390);
                Bitmap screenshot = CaptureScreenArea(area);
                Bitmap scaledScreenshot = new Bitmap(screenshot, new Size(screenshot.Width / 2, screenshot.Height / 2));
                Color boxColor = Color.FromArgb(37, 115, 193); // Example color, adjust as needed
                
                int proximityThreshold = 2; // Adjust based on your observation
                Size screenSize = new Size(area.Width, area.Height); // Example screen size, adjust as needed

                List<Point> significantPixels = CollectSignificantPixels(scaledScreenshot, boxColor);
                var clusters = ClusterPixels(significantPixels, proximityThreshold, screenSize);
                
                List<Point> centers = new List<Point>();

                foreach ( var cluster in clusters )
                {
                    Point center = CalculateCenter(cluster);
                    centers.Add( center );
                }
                Thread.Sleep( 100 );
                switch ( gameState )
                {
                    case GameState.Detecting:
                        if (centers.Count > 0)
                        {
                            lastDetectedCenters = new List<Point>( centers );
                            Console.WriteLine( $"Number of boxes detected: {clusters.Count}" );
                            gameState = GameState.WaitingForHide;
                        }
                        break;

                    case GameState.WaitingForHide:
                        Thread.Sleep( 1000 );
                        gameState = GameState.Clicking;
                        break;

                    case GameState.Clicking:
                        foreach ( var center in lastDetectedCenters )
                        {
                            int originalX = center.X * 2 + area.Left;
                            int originalY = center.Y * 2 + area.Top;
                            Console.WriteLine( $"Box center at: {originalX}, {originalY}" );
                            SimulateMouseClick( originalX, originalY );
                        }
                        lastDetectedCenters.Clear();
                        gameState = GameState.Detecting;
                        Thread.Sleep( 200 );
                        break;
                }
                Thread.Sleep( 10 );
            }
        } while ( Console.ReadKey(true).Key != ConsoleKey.Escape );
    }

    private static void SimulateMouseClick( int x, int y )
    {
        SetCursorPos( x, y );

        mouse_event( MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint) x, (uint) y, 0, 0 );
    }

    private static Point CalculateCenter( List<Point> points )
    {
        if (points.Count == 0 )
        {
            return Point.Empty;
        }

        int sumX = 0, sumY = 0;
        foreach (var point in points)
        {
            sumX += point.X;
            sumY += point.Y;
        }

        return new Point( sumX / points.Count, sumY / points.Count);
    }

    public static Bitmap CaptureScreenArea( Rectangle area )
    {
        Bitmap bmpScreenshot = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using ( Graphics g = Graphics.FromImage( bmpScreenshot ) )
        {
            g.CopyFromScreen( area.Left, area.Top, 0, 0, area.Size, CopyPixelOperation.SourceCopy );
        }
        return bmpScreenshot;
    }

    public static List<Point> CollectSignificantPixels(Bitmap image, Color color1)
    {
        int whiteThreshold = 240; // Example threshold for white color
        Color white = Color.FromArgb(255,255,255);
        return CollectSignificantPixels(image, color1, white, whiteThreshold);
    }

    public static List<Point> CollectSignificantPixels( Bitmap image, Color boxColor, Color targetColor, int tolerance )
    {
        List<Point> significantPoints = new List < Point >();
        for ( int x = 0; x < image.Width; x+=2 )
        {
            for ( int y = 0; y < image.Height; y+=2 )
            {
                Color pixelColor = image.GetPixel(x, y);
                if ( IsColorSimilar( pixelColor, boxColor, tolerance ) )
                {
                    //significantPoints.Add( new Point( x, y ) );
                }
                else if ( IsColorSimilar( pixelColor, targetColor, tolerance ) )
                {
                    significantPoints.Add( new Point( x, y ) );
                }
            }
        }
        return significantPoints;
    }

    private static bool IsColorSimilar( Color color1, Color color2, int tolerance )
    {
        return Math.Abs( color1.R - color2.R ) < 5 &&
               Math.Abs( color1.G - color2.G ) < 5 &&
               Math.Abs( color1.B - color2.B ) < 5;
    }

    public static List<List<Point>> ClusterPixels( List<Point> points, int proximityThreshold, Size screenSize )
    {
        int cols = screenSize.Width / proximityThreshold;
        int rows = screenSize.Height / proximityThreshold;
        List<Point>[,] grid = new List<Point>[cols, rows];
        for ( int i = 0; i < cols; i++ )
        {
            for ( int j = 0; j < rows; j++ )
            {
                grid [i, j] = new List<Point>();
            }
        }

        foreach ( var point in points )
        {
            int col = point.X / proximityThreshold;
            int row = point.Y / proximityThreshold;
            grid [col, row].Add( point );
        }

        HashSet<Point> visitedPoints = new HashSet<Point>();
        List<List<Point>> clusters = new List<List<Point>>();

        for ( int col = 0; col < cols; col++ )
        {
            for ( int row = 0; row < rows; row++ )
            {
                foreach ( var point in grid [col, row] )
                {
                    if ( !visitedPoints.Contains( point ) )
                    {
                        List<Point> cluster = new List<Point>();
                        ClusterPoints( cluster, point, grid, visitedPoints, cols, rows, proximityThreshold );
                        if ( cluster.Count > 0 )
                        {
                            clusters.Add( cluster );
                        }
                    }
                }
            }
        }
        return clusters;
    }

    static void ClusterPoints( List<Point> cluster, Point startPoint, List<Point> [,] grid, HashSet<Point> visitedPoints, int cols, int rows, int proximityThreshold )
    {
        Queue<Point> pointsQueue = new Queue<Point>();
        pointsQueue.Enqueue( startPoint );

        while ( pointsQueue.Count > 0 )
        {
            Point currentPoint = pointsQueue.Dequeue();
            if ( visitedPoints.Contains( currentPoint ) )
            {
                continue;
            }

            visitedPoints.Add( currentPoint );
            cluster.Add( currentPoint );

            int currentCol = currentPoint.X / proximityThreshold;
            int currentRow = currentPoint.Y / proximityThreshold;

            for ( int x = Math.Max( 0, currentCol - 1 ); x <= Math.Min( currentCol + 1, cols - 1 ); x++ )
            {
                for ( int y = Math.Max( 0, currentRow - 1 ); y <= Math.Min( currentRow + 1, rows - 1 ); y++ )
                {
                    foreach ( var point in grid [x, y] )
                    {
                        if ( !visitedPoints.Contains( point ) && IsNearby( point, currentPoint, proximityThreshold ) )
                        {
                            pointsQueue.Enqueue( point );
                        }
                    }
                }
            }
        }
    }

    static bool IsNearby( Point a, Point b, int threshold )
    {
        return Math.Abs( a.X - b.X ) <= threshold && Math.Abs( a.Y - b.Y ) <= threshold;
    }
}

enum GameState
{
    Detecting,
    WaitingForHide,
    Clicking,
    WaitingForNewRound
}

