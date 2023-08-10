using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Bev.Instruments.Counters.HpSerial;
using Bev.UI;

namespace HpLogger
{
    class Program
    {
        static string outputFilename;
        static StreamWriter streamWriter;
        static string outputFormat; // the format for the y-value

        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Options options = new Options();
            ConsoleUI.Verbatim = !options.BeQuiet;
            ConsoleUI.Welcome();

            #region The CLA stuff
            if (!CommandLine.Parser.Default.ParseArgumentsStrict(args, options))
                Console.WriteLine("*** ParseArgumentsStrict returned false");

            string[] filenames = options.ListOfFileNames.ToArray();
            if (filenames.Length == 0)
                outputFilename = ConsoleUI.Title;
            if (filenames.Length == 1)
                outputFilename = Path.ChangeExtension(filenames[0], null);
            if (filenames.Length > 1)
                ConsoleUI.ErrorExit("More than one file name given!", 2);
            outputFilename = Path.ChangeExtension(outputFilename, "dat");
            #endregion

            ConsoleUI.StartOperation("Initializing stuff");
            SerialHpCounter hpCounter = new SerialHpCounter(options.ComPortNumber);
            if (!hpCounter.IsConnected)
                ConsoleUI.ErrorExit("Counter not ready (wrong port?)!", 1);

            // register the event handlers
            hpCounter.UpdatedEventHandler += UpdateView;
            hpCounter.ReadyEventHandler += LoopReady;

            // consume the missing command line options
            if (options.ForceTotalize) hpCounter.ForceTotalizeMode();
            if (options.GateTime > 0) hpCounter.ForceGateTime(options.GateTime);

            // determine the output quantity for formatting
            string columnDescription = "<not set>";
            outputFormat = "  {1}";
            switch (hpCounter.Mode)
            {
                case MeasureMode.Unknown:
                    columnDescription = "unknown quantity";
                    if ((hpCounter.Unit == UnitSymbol.s) || (hpCounter.Unit == UnitSymbol.us))
                        columnDescription = "period/risetime/width or some other time in s";
                    break;
                case MeasureMode.Frequency:
                    outputFormat = "{1,16:F3}";
                    columnDescription = "frequency in Hz";
                    break;
                case MeasureMode.Totalize:
                    outputFormat = "{1,12:F0}";
                    columnDescription = "counts (totalize mode)";
                    break;
                case MeasureMode.Ratio:
                    columnDescription = "frequency ratio";
                    break;
                case MeasureMode.DutyCycle:
                    columnDescription = "duty cycle";
                    break;
                case MeasureMode.Phase:
                    columnDescription = "phase in degree";
                    break;
                case MeasureMode.Voltage:
                    columnDescription = "peak voltage in V";
                    break;
                default:
                    break;
            }

            // output file stuff
            streamWriter = new StreamWriter(outputFilename);
            streamWriter.WriteLine($"Output of {ConsoleUI.Title} ver. {ConsoleUI.FullVersion}");
            if (options.UserComment != "")
                streamWriter.WriteLine($"User comment: {options.UserComment}");
            streamWriter.WriteLine($"Logging started at {hpCounter.InitTime.ToString("dd.MM.yyyy hh:mm")}");
            streamWriter.WriteLine($"Manufacturer: {hpCounter.InstrumentManufacturer}");
            streamWriter.WriteLine($"Type: {hpCounter.InstrumentType}");
            streamWriter.WriteLine($"Serial number: {hpCounter.InstrumentSerialNumber}");
            streamWriter.WriteLine("(Instrument identification might be wrong!)");
            streamWriter.WriteLine($"Gate time: {hpCounter.GateTime} s");
            streamWriter.WriteLine($"Mode: {hpCounter.Mode}");
            streamWriter.WriteLine($"Unit: {hpCounter.Unit}");
            streamWriter.WriteLine($"Connected to {hpCounter.DevicePort}");
            streamWriter.WriteLine("Column 1: time since start in s");
            streamWriter.WriteLine($"Column 2: {columnDescription}");
            streamWriter.WriteLine("@@@@");
            streamWriter.Close();
            ConsoleUI.Done();

            // start the actual measurement
            hpCounter.StartMeasurementLoopThread(options.NumberOfSamples);

            // continue until user presses 'q' or 'Q'
            ConsoleUI.WriteLine("Press 'q' to exit application. (May take some time)");
            do { NOP(); } while (Console.ReadKey(true).Key != ConsoleKey.Q);
            hpCounter.RequestStopMeasurementLoop();
        }

        #region Data output handler
        static void UpdateView(object sender, EventArgs e)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            SerialHpCounter ob = sender as SerialHpCounter;
            // format the output string
            double timeSinceStart = (ob.SampleTime - ob.InitTime).TotalSeconds;
            string dataLine = string.Format("{0,10:F1} " + outputFormat, timeSinceStart, ob.LastValue);
            streamWriter = File.AppendText(outputFilename);
            streamWriter.WriteLine(dataLine);
            streamWriter.Close();
            ConsoleUI.WriteLine(dataLine);
        }
        #endregion

        #region Exit handler
        static void LoopReady(object sender, EventArgs e)
        {
            SerialHpCounter ob = sender as SerialHpCounter;
            ConsoleUI.WriteLine("Application stopped, no errors.");
            ob.Disconnect();
            Environment.Exit(0);
        }
        #endregion

        static void NOP() => Thread.Sleep(1);
    }
}
