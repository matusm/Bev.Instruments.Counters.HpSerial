using System.Globalization;
using System.Threading;
using System;
using Bev.Instruments.Counters.HpSerial;

namespace HpTest
{
    class Program
    {
        static void Main(string[] args)
        {

            SerialHpCounter instrument = new SerialHpCounter("COM1");

            // register the event handlers
            instrument.UpdatedEventHandler += UpdateView;
            instrument.ReadyEventHandler += LoopReady;
            instrument.TimeOutEventHandler += Foo;

            // start the actual measurement
            instrument.StartMeasurementLoopThread();

            // continue until user presses 'q' or 'Q'
            Console.WriteLine("Press 'q' to exit thread. (May take some time)");
            do { } while (Console.ReadKey(true).Key != ConsoleKey.Q);
            
            instrument.RequestStopMeasurementLoop();
            instrument.Disconnect();
        }

        static void UpdateView(object sender, EventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            SerialHpCounter ob = sender as SerialHpCounter;
            double timeSinceStart = (ob.SampleTime - ob.InitTime).TotalSeconds;
            string line = $"{timeSinceStart}  {ob.LastValue}";
            Console.WriteLine(line);
        }

        static void LoopReady(object sender, EventArgs e)
        {
            var ob = sender as SerialHpCounter;
            Console.WriteLine("LoopReady Event");
            Environment.Exit(0);
        }

        static void Foo(object sender, EventArgs e)
        {
            Console.WriteLine("Timeout");
        }
    }
}

