using System;
using System.Globalization;

namespace Bev.Instruments.Counters.HpSerial
{
    public class DataPod
    {

        public DataPod(string line) : this()
        {
            ResponseLine = line.Trim();
            ParseTextLine(ResponseLine);
        }

        public DataPod()
        {
            TimeStamp = DateTime.UtcNow;
        }

        public string ResponseLine { get; } = string.Empty;
        public double Value { get; private set; } = double.NaN;
        public DateTime TimeStamp { get; }
        public MeasureMode Mode { get; private set; } = MeasureMode.Unknown;
        public UnitSymbol Unit { get; private set; } = UnitSymbol.Unknown;

        public void ConvertTotalizeToFrequency(double gateTime) => Value = FrequencyFromTotalize(gateTime);

        private void ParseTextLine(string line)
        {
            Value = double.NaN;
            Unit = UnitSymbol.Unknown;
            Mode = MeasureMode.Unknown;

            string[] separator = { " ", "\t" };
            string[] tokens = line.Replace("\r", "").Split(separator, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0) return;
            if (tokens.Length >= 3) return;

            // catch voltage measurements, which are not parsed yet.
            if (line.Contains("V")) // TODO why not in ParseUnitAndMode() ?
            {
                Unit = UnitSymbol.V;
                Mode = MeasureMode.Voltage;
                return;
            }

            Value = StringToNumber(RemoveGroupingDelimiters(tokens[0]));
            Mode = MeasureMode.Unknown; // for the time beeing

            if (tokens.Length == 2)
            {
                ParseUnitAndMode(tokens[1]);
                double k = DecimalMultiple(Unit);
                Value *= k;
            }

        }

        private double FrequencyFromTotalize(double gateTime)
        {
            if (Mode == MeasureMode.Unknown || Mode == MeasureMode.Totalize)
            {
                Mode = MeasureMode.Totalize;
                Unit = UnitSymbol.Hz;
                return Value / gateTime;
            }
            else
            {
                return Value;
            }
        }

        private void ParseUnitAndMode(string str)
        {
            Unit = UnitSymbol.Unknown;
            switch (str)
            {
                case "Hz":
                    Mode = MeasureMode.Frequency;
                    Unit = UnitSymbol.Hz;
                    return;
                case "MHz":
                    Mode = MeasureMode.Frequency;
                    Unit = UnitSymbol.MHz;
                    return;
                case "M":
                    Mode = MeasureMode.Totalize;
                    Unit = UnitSymbol.M;
                    return;
                case "DEG":
                    Mode = MeasureMode.Phase;
                    Unit = UnitSymbol.Deg;
                    return;
                case "s":
                    Mode = MeasureMode.Time;
                    Unit = UnitSymbol.s;
                    return;
                case "us":
                    Mode = MeasureMode.Time;
                    Unit = UnitSymbol.us;
                    return;
            }
        }

        private double DecimalMultiple(UnitSymbol symbol)
        {
            switch (symbol)
            {
                case UnitSymbol.MHz:
                    return 1.0e6;
                case UnitSymbol.Hz:
                    return 1.0;
                case UnitSymbol.M:
                    return 1.0e6;
                case UnitSymbol.us:
                    return 1.0e-6;
                default:
                    return 1.0;
            }
        }

        private string RemoveGroupingDelimiters(string str) => str.Replace(",", ""); // 1000er Trennzeichen entfernen.

        private double StringToNumber(string str)
        {
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double x))
                return x;
            return double.NaN;
        }

    }
}
