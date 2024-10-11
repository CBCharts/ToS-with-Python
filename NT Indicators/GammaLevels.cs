#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Text.RegularExpressions;
using System.Linq; // Required for sorting
using System.Windows.Media;
using System.Windows;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class NorthernGammaExposure : Indicator
    {
        // User-configurable properties
        private Brush positiveGammaLineColor = Brushes.Green;
        private Brush negativeGammaLineColor = Brushes.Red;
        private Brush gammaFlipLineColor = Brushes.Blue;
        private Brush textColor = Brushes.White;
        private int lineWidth = 2;
        private int textWidth = 10; // New user-configurable text width
        private TextAlignment textPlacement = TextAlignment.Center; // Updated to TextAlignment for placement
        private Dictionary<double, int> lineWidths = new Dictionary<double, int>(); // Store line width for each strike price

        private List<Tuple<double, double, string, double>> gammaData = new List<Tuple<double, double, string, double>>(); // Holds Theo ES Price, TotalGamma, GammaType, and GammaFlip
        private string path = @"C:\Users\Chris\OneDrive\Documents\NinjaTrader 8\bin\Custom\Top_Lowest_5_Gamma_Exposures.csv"; // Path to your csv file
        private double gammaFlipLevel = 0;
        private FileSystemWatcher fileWatcher; // FileSystemWatcher to monitor CSV changes

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Reads Theo ES prices and gamma values from a CSV file and plots horizontal lines on the chart.";
                Name = "CB Charts - GammaExpo v0.1";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                AddPlot(Brushes.Green, "PositiveGammaLine"); // Default line color for positive gamma
                AddPlot(Brushes.Red, "NegativeGammaLine");   // Default line color for negative gamma
            }
            else if (State == State.Configure)
            {
                // Load initial data
                LoadGammaDataFromCsv();

                // Set up file watcher to monitor CSV changes
                fileWatcher = new FileSystemWatcher();
                fileWatcher.Path = Path.GetDirectoryName(path);
                fileWatcher.Filter = Path.GetFileName(path);
                fileWatcher.Changed += OnCsvChanged;
                fileWatcher.EnableRaisingEvents = true; // Start watching
            }
            else if (State == State.Terminated)
            {
                // Stop and dispose the file watcher when the indicator is removed
                if (fileWatcher != null)
                {
                    fileWatcher.EnableRaisingEvents = false;
                    fileWatcher.Dispose();
                }
            }
        }

        // Method to handle file changes when the CSV file is updated
        private void OnCsvChanged(object sender, FileSystemEventArgs e)
{
    // Reload the CSV when the file is changed
    LoadGammaDataFromCsv();
    
    // Force the chart to refresh after data reload
    ForceRefresh();
    
    Print("CSV file updated, reloading data.");
}

        // Method to read strike prices, gamma values, levels, and Gamma Flip from the CSV file
        private void LoadGammaDataFromCsv()
        {
            try
            {
                // Clear old data before loading new data
                gammaData.Clear();

                using (StreamReader reader = new StreamReader(path))
                {
                    string line;
                    bool isHeader = true;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (isHeader)
                        {
                            isHeader = false;
                            continue; // Skip header line
                        }

                        var fields = line.Split(',');
                        if (fields.Length >= 5) // Ensure there are at least five columns
                        {
                            double theoESPrice, totalGamma, gammaFlip;
                            string gammaType = fields[2];

                            if (double.TryParse(fields[3], out theoESPrice) && double.TryParse(fields[1], out totalGamma) && double.TryParse(fields[4], out gammaFlip))
                            {
                                gammaData.Add(new Tuple<double, double, string, double>(theoESPrice, totalGamma, gammaType, gammaFlip));
                            }
                        }
                    }
                }

                // Get the Gamma Flip value from any row (it's the same for all rows)
                gammaFlipLevel = gammaData.FirstOrDefault()?.Item4 ?? 0;

                Print("Gamma levels loaded successfully.");
            }
            catch (Exception e)
            {
                Print("Error reading CSV file: " + e.Message);
            }
        }

        // Draw horizontal lines and text labels on the chart based on the Theo ES Prices and Gamma Flip
        // List to keep track of the drawn objects (lines and text)
private List<string> drawnObjects = new List<string>();

protected override void OnBarUpdate()
{
    // Remove previous lines and text
    foreach (var objName in drawnObjects)
    {
        RemoveDrawObject(objName);
    }
    
    // Clear the drawnObjects list
    drawnObjects.Clear();

    if (gammaData == null || gammaData.Count == 0) return;  // Skip drawing if no data is loaded

    // Process gamma data and draw lines and text
    foreach (var data in gammaData)
    {
        double theoESPrice = data.Item1;
        double totalGamma = data.Item2;
        string gammaType = data.Item3;

        // Determine if it's Call or Put and assign colors accordingly
        Brush lineColor = gammaType.Contains("Call") ? positiveGammaLineColor : negativeGammaLineColor;

        // Set line width and draw the horizontal line at Theo ES Price
        int specificLineWidth = lineWidths.ContainsKey(theoESPrice) ? lineWidths[theoESPrice] : lineWidth;
        string lineName = "PriceLevel_" + theoESPrice.ToString();
        HorizontalLine line = Draw.HorizontalLine(this, lineName, theoESPrice, lineColor);
        line.Stroke.Width = specificLineWidth;

        // Add line name to the list for future removal
        drawnObjects.Add(lineName);

        // Add text label next to the line
        string labelText = $"{gammaType} ({totalGamma:F6})";
        string textName = "Text_" + theoESPrice.ToString();
        Draw.Text(this, textName, false, labelText, 0, theoESPrice, 0, textColor, new SimpleFont("Arial", textWidth), textPlacement, Brushes.Transparent, textColor, 0);

        // Add text name to the list for future removal
        drawnObjects.Add(textName);
    }

    // Draw the Gamma Flip line
    string gammaFlipName = "GammaFlipLevel";
    HorizontalLine gammaFlipLine = Draw.HorizontalLine(this, gammaFlipName, gammaFlipLevel, gammaFlipLineColor);
    gammaFlipLine.Stroke.Width = lineWidth;

    // Add Gamma Flip line to the list for future removal
    drawnObjects.Add(gammaFlipName);
}


        #region Properties
        [NinjaScriptProperty]
        public Brush PositiveGammaLineColor
        {
            get { return positiveGammaLineColor; }
            set { positiveGammaLineColor = value; }
        }

        [NinjaScriptProperty]
        public Brush NegativeGammaLineColor
        {
            get { return negativeGammaLineColor; }
            set { negativeGammaLineColor = value; }
        }

        [NinjaScriptProperty]
        public Brush GammaFlipLineColor
        {
            get { return gammaFlipLineColor; }
            set { gammaFlipLineColor = value; }
        }

        [NinjaScriptProperty]
        public Brush TextColor
        {
            get { return textColor; }
            set { textColor = value; }
        }

        [NinjaScriptProperty]
        public int LineWidth
        {
            get 
            { 
                if (lineWidth < 1 || lineWidth > 10) 
                    lineWidth = 2; // Default if invalid value
                return lineWidth; 
            }
            set { lineWidth = value; }
        }

        [NinjaScriptProperty]
        public int TextWidth
        {
            get 
            { 
                if (textWidth < 1 || textWidth > 20) 
                    textWidth = 10; // Default if invalid value
                return textWidth; 
            }
            set { textWidth = value; }
        }

        [NinjaScriptProperty]
        public TextAlignment TextPlacement
        {
            get { return textPlacement; }
            set { textPlacement = value; }
        }

        public Dictionary<double, int> LineWidths
        {
            get { return lineWidths; }
            set { lineWidths = value; }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private NorthernGammaExposure[] cacheNorthernGammaExposure;
		public NorthernGammaExposure NorthernGammaExposure(Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth, TextAlignment textPlacement)
		{
			return NorthernGammaExposure(Input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth, textPlacement);
		}

		public NorthernGammaExposure NorthernGammaExposure(ISeries<double> input, Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth, TextAlignment textPlacement)
		{
			if (cacheNorthernGammaExposure != null)
				for (int idx = 0; idx < cacheNorthernGammaExposure.Length; idx++)
					if (cacheNorthernGammaExposure[idx] != null && cacheNorthernGammaExposure[idx].PositiveGammaLineColor == positiveGammaLineColor && cacheNorthernGammaExposure[idx].NegativeGammaLineColor == negativeGammaLineColor && cacheNorthernGammaExposure[idx].GammaFlipLineColor == gammaFlipLineColor && cacheNorthernGammaExposure[idx].TextColor == textColor && cacheNorthernGammaExposure[idx].LineWidth == lineWidth && cacheNorthernGammaExposure[idx].TextWidth == textWidth && cacheNorthernGammaExposure[idx].TextPlacement == textPlacement && cacheNorthernGammaExposure[idx].EqualsInput(input))
						return cacheNorthernGammaExposure[idx];
			return CacheIndicator<NorthernGammaExposure>(new NorthernGammaExposure(){ PositiveGammaLineColor = positiveGammaLineColor, NegativeGammaLineColor = negativeGammaLineColor, GammaFlipLineColor = gammaFlipLineColor, TextColor = textColor, LineWidth = lineWidth, TextWidth = textWidth, TextPlacement = textPlacement }, input, ref cacheNorthernGammaExposure);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.NorthernGammaExposure NorthernGammaExposure(Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth, TextAlignment textPlacement)
		{
			return indicator.NorthernGammaExposure(Input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth, textPlacement);
		}

		public Indicators.NorthernGammaExposure NorthernGammaExposure(ISeries<double> input , Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth, TextAlignment textPlacement)
		{
			return indicator.NorthernGammaExposure(input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth, textPlacement);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.NorthernGammaExposure NorthernGammaExposure(Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth, TextAlignment textPlacement)
		{
			return indicator.NorthernGammaExposure(Input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth, textPlacement);
		}

		public Indicators.NorthernGammaExposure NorthernGammaExposure(ISeries<double> input , Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth, TextAlignment textPlacement)
		{
			return indicator.NorthernGammaExposure(input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth, textPlacement);
		}
	}
}

#endregion
