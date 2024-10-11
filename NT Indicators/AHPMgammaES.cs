#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Linq; // Required for sorting
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class FuturesGammaExposure : Indicator
    {
        // User-configurable properties
        private Brush positiveGammaLineColor = Brushes.Green;
        private Brush negativeGammaLineColor = Brushes.Red;
        private Brush gammaFlipLineColor = Brushes.Blue;
        private Brush textColor = Brushes.White;
        private int lineWidth = 2;
        private int textWidth = 10; // New user-configurable text width
        private Dictionary<double, int> lineWidths = new Dictionary<double, int>(); // Store line width for each strike price

        private List<Tuple<double, double, string, double>> gammaData = new List<Tuple<double, double, string, double>>(); // Holds Strike Price, TotalGamma, GammaType, and GammaFlip
        private string path = @"C:\Users\Chris\OneDrive\Documents\NinjaTrader 8\bin\Custom\ESgammaLVL.csv"; // Path to your CSV file
        private double gammaFlipLevel = 0;
        private FileSystemWatcher fileWatcher; // FileSystemWatcher to monitor CSV changes

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Reads ES strike prices and gamma values from a CSV file and plots horizontal lines on the chart.";
                Name = "CB Charts - AH/PM Futures v0.1";
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
                fileWatcher.EnableRaisingEvents = true; // Start watching for file changes
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
            try
            {
                // Small delay to ensure file writing is complete
                System.Threading.Thread.Sleep(500);

                // Reload the CSV when the file is changed
                LoadGammaDataFromCsv();
                
                // Force the chart to refresh after data reload
                TriggerCustomEvent(o =>
                {
                    ForceRefresh();
                }, null);
                
                Print("CSV file updated, reloading data.");
            }
            catch (Exception ex)
            {
                Print($"Error reloading CSV: {ex.Message}");
            }
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
                        if (fields.Length >= 4) // Ensure there are at least four columns
                        {
                            double strikePrice, totalGamma, gammaFlip;
                            string gammaType = fields[2];

                            if (double.TryParse(fields[0], out strikePrice) && double.TryParse(fields[1], out totalGamma) && double.TryParse(fields[3], out gammaFlip))
                            {
                                gammaData.Add(new Tuple<double, double, string, double>(strikePrice, totalGamma, gammaType, gammaFlip));
                            }
                        }
                    }
                }

                // Get the Gamma Flip value from the first row (it is the same across all rows)
                gammaFlipLevel = gammaData.FirstOrDefault()?.Item4 ?? 0;

                Print("Gamma levels loaded successfully.");
            }
            catch (Exception e)
            {
                Print("Error reading CSV file: " + e.Message);
            }
        }

        // Draw horizontal lines and text labels on the chart based on the Strike Prices and Gamma Flip
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

            if (gammaData == null || gammaData.Count == 0) 
            {
                Print("No data to draw.");
                return;  // Skip drawing if no data is loaded
            }

            // Process gamma data and draw lines and text
            foreach (var data in gammaData)
            {
                double strikePrice = data.Item1;
                double totalGamma = data.Item2;
                string gammaType = data.Item3;

                Print($"Drawing for StrikePrice: {strikePrice}, TotalGamma: {totalGamma}");

                // Determine if it's Call or Put and assign colors accordingly
                Brush lineColor = gammaType.Contains("Call") ? positiveGammaLineColor : negativeGammaLineColor;

                // Set line width and draw the horizontal line at Strike Price
                int specificLineWidth = lineWidths.ContainsKey(strikePrice) ? lineWidths[strikePrice] : lineWidth;
                string lineName = "PriceLevel_" + strikePrice.ToString();
                HorizontalLine line = Draw.HorizontalLine(this, lineName, strikePrice, lineColor);
                line.Stroke.Width = specificLineWidth;

                // Add line name to the list for future removal
                drawnObjects.Add(lineName);

                // Add text label next to the line
                string labelText = $"{gammaType} ({totalGamma:F6})";
                string textName = "Text_" + strikePrice.ToString();

                Draw.Text(this, textName, labelText, 0, strikePrice, textColor);

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
		private FuturesGammaExposure[] cacheFuturesGammaExposure;
		public FuturesGammaExposure FuturesGammaExposure(Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth)
		{
			return FuturesGammaExposure(Input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth);
		}

		public FuturesGammaExposure FuturesGammaExposure(ISeries<double> input, Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth)
		{
			if (cacheFuturesGammaExposure != null)
				for (int idx = 0; idx < cacheFuturesGammaExposure.Length; idx++)
					if (cacheFuturesGammaExposure[idx] != null && cacheFuturesGammaExposure[idx].PositiveGammaLineColor == positiveGammaLineColor && cacheFuturesGammaExposure[idx].NegativeGammaLineColor == negativeGammaLineColor && cacheFuturesGammaExposure[idx].GammaFlipLineColor == gammaFlipLineColor && cacheFuturesGammaExposure[idx].TextColor == textColor && cacheFuturesGammaExposure[idx].LineWidth == lineWidth && cacheFuturesGammaExposure[idx].TextWidth == textWidth && cacheFuturesGammaExposure[idx].EqualsInput(input))
						return cacheFuturesGammaExposure[idx];
			return CacheIndicator<FuturesGammaExposure>(new FuturesGammaExposure(){ PositiveGammaLineColor = positiveGammaLineColor, NegativeGammaLineColor = negativeGammaLineColor, GammaFlipLineColor = gammaFlipLineColor, TextColor = textColor, LineWidth = lineWidth, TextWidth = textWidth }, input, ref cacheFuturesGammaExposure);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.FuturesGammaExposure FuturesGammaExposure(Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth)
		{
			return indicator.FuturesGammaExposure(Input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth);
		}

		public Indicators.FuturesGammaExposure FuturesGammaExposure(ISeries<double> input , Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth)
		{
			return indicator.FuturesGammaExposure(input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.FuturesGammaExposure FuturesGammaExposure(Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth)
		{
			return indicator.FuturesGammaExposure(Input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth);
		}

		public Indicators.FuturesGammaExposure FuturesGammaExposure(ISeries<double> input , Brush positiveGammaLineColor, Brush negativeGammaLineColor, Brush gammaFlipLineColor, Brush textColor, int lineWidth, int textWidth)
		{
			return indicator.FuturesGammaExposure(input, positiveGammaLineColor, negativeGammaLineColor, gammaFlipLineColor, textColor, lineWidth, textWidth);
		}
	}
}

#endregion
