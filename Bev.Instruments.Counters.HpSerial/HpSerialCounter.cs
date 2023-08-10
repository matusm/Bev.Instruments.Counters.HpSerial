using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using System.Threading;

namespace Bev.Instruments.Counters.HpSerial
{
    public class SerialHpCounter
    {
        private DataPod currentDataPod = new DataPod();


        public SerialHpCounter(string portName)
        {
            DevicePort = portName.Trim();
            IsConnected = false;
            stillInLoop = false;
            stopRequest = false;
            loopSamples = 0;
            InitTime = DateTime.UtcNow;
            Connect();
            IdentifyInstrument();
        }

        public SerialHpCounter(int portNumber) : this($"COM{portNumber}") { }

        public string DevicePort { get; }
        public string InstrumentManufacturer { get; private set; }
        public string InstrumentType { get; private set; }
        public string InstrumentSerialNumber { get; private set; }
        public string InstrumentFirmwareVersion { get; private set; }
        public string InstrumentID => $"{InstrumentManufacturer} {InstrumentType} SN:{InstrumentSerialNumber} {InstrumentFirmwareVersion} @ {DevicePort}";

        public UnitSymbol Unit => currentDataPod.Unit;
        public MeasureMode Mode { get => mode; private set { mode = value; InterpretMode(); } }

        public bool IsConnected { get; private set; }
        public double? LastValue => currentDataPod.Value;
        public DateTime SampleTime => currentDataPod.TimeStamp;
        public DateTime InitTime { get; }
        public MeasurementMode MeasurementMode { get => measurementMode; set { measurementMode = value; } }
        public GateTime GateTime { get => gateTime; set { gateTime = value; } }
        public double DGateTime { get => gateTimeValue; set { gateTimeValue = value; } }


        public void SetupMeasurementMode(MeasurementMode measurementMode, GateTime gateTime)
        {
            this.measurementMode = measurementMode;
            this.gateTime = gateTime;
            GateTimeToDouble();
        }

        public void EstimateGateTime(int sampleSize)
        {
            double? t = GetTimeBetweenSamples(sampleSize);
            gateTime = InterpretGateTime(t);
            GateTimeToDouble();
        }

        public void EstimateGateTime() => EstimateGateTime(3);

        public void Disconnect()
        {
            if (!IsConnected) return;
            serialPort.Close();
            IsConnected = false;
        }

        public void Connect()
        {
            // Disconnect() necessary only for this class
            Disconnect();
            try
            {
                serialPort = new SerialPort(DevicePort, 9600, Parity.None, 8, StopBits.One);
                serialPort.ReadTimeout = 20000;
                serialPort.Open();
                serialPort.Handshake = Handshake.None;
                serialPort.DiscardInBuffer();
                IsConnected = true;
            }
            catch
            {
                serialPort = null;
                IsConnected = false;
            }
        }

        public void ForceTotalizeMode()
        {
            if (mode == MeasureMode.Unknown)
                Mode = MeasureMode.Totalize;
        }

        public void ForceGateTime(double t)
        {
            gateTime = InterpretGateTime(t);
            GateTimeToDouble();
        }

        public double GetCounterValue()
        {
            UpdateCurrentDataPod();
            if(double.IsNaN(currentDataPod.Value)) 
                TimeOutEventHandler(this, new EventArgs());
            UpdatedEventHandler(this, new EventArgs());
            return currentDataPod.Value;
        }

        public void StartMeasurementLoop()
        {
            if (!IsConnected)
                return;
            serialPort.DiscardInBuffer();
            _StartMeasurementLoop();
        }

        public void StartMeasurementLoopThread(int n)
        {
            if (stillInLoop) return;
            loopSamples = n;
            stopRequest = false;
            thread = new Thread(new ThreadStart(StartMeasurementLoop));
            thread.Start();
        }

        public void StartMeasurementLoopThread() => StartMeasurementLoopThread(Int32.MaxValue);

        public void RequestStopMeasurementLoop() => stopRequest = true;

        private void _StartMeasurementLoop()
        {
            if (stillInLoop) return;
            stillInLoop = true;
            int i = 0;
            while (!stopRequest && i < loopSamples)
            {
                double? x = GetCounterValue();
                if (x != null)
                {
                    i++;
                }
            }
            stillInLoop = false;
            ReadyEventHandler(this, new EventArgs());
        }

        #region event declarations

        public delegate void CounterEventHandler(object obj, EventArgs e);

        public event CounterEventHandler UpdatedEventHandler;

        public event CounterEventHandler ReadyEventHandler;

        public event CounterEventHandler TimeOutEventHandler;

        #endregion

        private GateTime InterpretGateTime(double? time)
        {
            if (time == null)
                return GateTime.GateUnknown;
            if (time < 1.0)
                return gateTime = GateTime.Gate01s;
            double gt1 = Math.Truncate((double)time);
            if (gt1 == 1) return GateTime.Gate1s;
            if (gt1 == 10) return GateTime.Gate10s;
            return GateTime.GateOther;
        }

        private double? GetTimeBetweenSamples(int sampleSize)
        {
            if (sampleSize < 2) sampleSize = 2;
            List<double> times = new List<double>();
            UpdateCurrentDataPod();  // first value to be discarded
            for (int i = 0; i < sampleSize; i++)
            {
                UpdateCurrentDataPod();
                times.Add(outputInterval);
            }
            if (times.Count == 0) return null;
            return times.Average();
        }

        private void ClearCounterValue() => currentDataPod = new DataPod();

        private void UpdateCurrentDataPod()
        {
            if (!IsConnected)
            {
                ClearCounterValue();
                return;
            }
            try
            {
                // receive text line from RS232 port (can stall!)
                DateTime oldtimeStamp = currentDataPod.TimeStamp;
                string line = serialPort.ReadLine();
                currentDataPod = new DataPod(line);
                // find time since last call
                DateTime newTimeStamp = currentDataPod.TimeStamp;
                outputInterval = (newTimeStamp - oldtimeStamp).TotalSeconds;
            }
            catch
            {
                ClearCounterValue();
                return;
            }
        }

 
        private void InterpretMode()
        {
            measurementMode = MeasurementMode.Unknown;
            if (mode == MeasureMode.Frequency) measurementMode = MeasurementMode.Frequency;
            if (mode == MeasureMode.Totalize) measurementMode = MeasurementMode.Totalize;
        }

        private void GateTimeToDouble()
        {
            switch (gateTime)
            {
                case GateTime.Gate01s:
                    gateTimeValue = 0.1;
                    break;
                case GateTime.Gate1s:
                    gateTimeValue = 1;
                    break;
                case GateTime.Gate10s:
                    gateTimeValue = 10;
                    break;
                default:
                    gateTimeValue = 0;
                    break;
            }
        }

        private void IdentifyInstrument()
        {
            switch (DevicePort.ToUpper())
            {
                case "COM6":
                    InstrumentManufacturer = "HEWLETT PACKARD";
                    InstrumentType = "53131 A";
                    InstrumentSerialNumber = "3736A23165";
                    break;
                case "COM3":
                    InstrumentManufacturer = "HEWLETT PACKARD";
                    InstrumentType = "53181 A";
                    InstrumentSerialNumber = "3548A02330";
                    break;
                case "COM1":
                    InstrumentManufacturer = "HEWLETT PACKARD";
                    InstrumentType = "53131 A";
                    InstrumentSerialNumber = "3736A21306";
                    break;
                default:
                    InstrumentManufacturer = "HEWLETT PACKARD / AGILENT";
                    InstrumentType = "<unknown>";
                    InstrumentSerialNumber = "<unknown>";
                    break;
            }
        }


        private SerialPort serialPort;
        private MeasurementMode measurementMode;
        private MeasureMode mode;
        private double outputInterval;
        private double gateTimeValue;
        private GateTime gateTime;
        private bool stillInLoop;
        private bool stopRequest;
        private int loopSamples;
        private Thread thread;
    }

}
