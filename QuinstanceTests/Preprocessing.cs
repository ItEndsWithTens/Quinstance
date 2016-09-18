using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace QuinstanceTests.Preprocessing
{
    public class QuinstanceExecutable
    {
        Assembly assembly = Assembly.GetAssembly(typeof(Quinstance.Program));

        public string tmp_dir,
                      tmp_path;

        public string working_dir,
                      input_dir,
                      expected_dir;

        public QuinstanceExecutable()
        {
            tmp_dir = assembly.GetName().Name + "_temp";

            tmp_path = Path.GetTempPath() + tmp_dir + Path.DirectorySeparatorChar;

            working_dir = Path.GetDirectoryName(assembly.Location) + Path.DirectorySeparatorChar + "../../../test/";

            input_dir = working_dir + "input/";

            expected_dir = working_dir + "expected/";
        }

        public void Run(string[] args)
        {
            Process quinstance = new Process();

            quinstance.StartInfo.Arguments = String.Join(" ", args);
            quinstance.StartInfo.CreateNoWindow = true;
            quinstance.StartInfo.FileName = assembly.Location;
            quinstance.StartInfo.RedirectStandardError = true;
            quinstance.StartInfo.RedirectStandardInput = true;
            quinstance.StartInfo.RedirectStandardOutput = true;
            quinstance.StartInfo.UseShellExecute = false;
            quinstance.StartInfo.WorkingDirectory = Path.GetDirectoryName(assembly.Location);

            quinstance.ErrorDataReceived += (sender, e) => { Console.WriteLine(e.Data); };
            quinstance.OutputDataReceived += (sender, e) => { Console.WriteLine(e.Data); };

            quinstance.Start();
            quinstance.BeginOutputReadLine();
            quinstance.BeginErrorReadLine();
            quinstance.WaitForExit();
        }
    }

    [TestFixture]
    public class Fgd
    {
        [TestCase]
        public void LineBreakWithoutColon()
        {
            var quinstance = new QuinstanceExecutable();

            var filename = "LineBreakWithoutColon.fgd";

            var expected = File.ReadAllLines(quinstance.expected_dir + filename).ToList();

            Quinstance.Program.PreprocessFgd(quinstance.input_dir + filename);

            var actual = File.ReadAllLines(quinstance.tmp_path + filename).ToList();

            for (var i = 0; i < actual.Count; ++i)
                Assert.That(actual[i], Is.EqualTo(expected[i]));
        }

        [TestCase]
        public void LineBreakWithColon()
        {
            var quinstance = new QuinstanceExecutable();

            var filename = "LineBreakWithColon.fgd";

            var expected = File.ReadAllLines(quinstance.expected_dir + filename).ToList();

            Quinstance.Program.PreprocessFgd(quinstance.input_dir + filename);

            var actual = File.ReadAllLines(quinstance.tmp_path + filename).ToList();

            for (var i = 0; i < actual.Count; ++i)
                Assert.That(actual[i], Is.EqualTo(expected[i]));
        }

        [TestCase]
        public void LineBreakAfterColon()
        {
            var quinstance = new QuinstanceExecutable();

            var filename = "LineBreakAfterColon.fgd";

            var expected = File.ReadAllLines(quinstance.expected_dir + filename).ToList();

            Quinstance.Program.PreprocessFgd(quinstance.input_dir + filename);

            var actual = File.ReadAllLines(quinstance.tmp_path + filename).ToList();

            for (var i = 0; i < actual.Count; ++i)
                Assert.That(actual[i], Is.EqualTo(expected[i]));
        }
    }
}
