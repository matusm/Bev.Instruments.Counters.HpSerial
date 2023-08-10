using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace HpLogger
{
    public class Options
    {
        [Option('c', "comport", DefaultValue = 1, HelpText = "RS232 port number the counter is connected to")]
        public int ComPortNumber { get; set; }

        [Option('n', DefaultValue = int.MaxValue, HelpText = "Number of data points to record")]
        public int NumberOfSamples { get; set; }

        [Option('q', "quiet", HelpText = "Quiet mode. No screen output (except for errors).")]
        public bool BeQuiet { get; set; }

        [Option('g', "gatetime", DefaultValue = 0, HelpText = "Gate time value in s.")]
        public double GateTime { get; set; }

        [Option('t', "totalize", HelpText = "Force totalize mode for unknown mode.")]
        public bool ForceTotalize { get; set; }

        [Option("comment", DefaultValue = "", HelpText = "Comment for outpot file.")]
        public string UserComment { get; set; }

        [ValueList(typeof(List<string>), MaximumElements = 1)]
        public IList<string> ListOfFileNames { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            string AppName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            string AppVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            HelpText help = new HelpText
            {
                Heading = new HeadingInfo(AppName, "version " + AppVer),
                Copyright = new CopyrightInfo("Michael Matus", 2016),
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            string sPre = "Program to record data send by an HP/Agilent 53131A or 53181A frequency counter connected via RS232. " +
                          "The counter settings must be entered manually on the instrument. ";
            help.AddPreOptionsLine(sPre);
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("Usage: " + AppName + " filename1 [options]");
            help.AddPostOptionsLine("");
            help.AddOptions(this);
            return help;
        }
    }
}
