using Quinstance;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;

namespace Quinstance
{
    public class Program
    {
        static bool cleanup = false,
                    keep = false;

        static char sep = Path.DirectorySeparatorChar;

        static string exe_name = Assembly.GetEntryAssembly().GetName().Name,
                      tmp_dir = exe_name + "_temp",
                      tmp_path = Path.GetTempPath() + tmp_dir + sep,
                      tld;

        static Dictionary<string, List<string>> deletables = new Dictionary<string, List<string>>() {
            { "dirs", new List<string>() }, { "files", new List<string>() }
        };

        static void Main(string[] args)
        {
            List<string>
                paths = new List<string>(),
                fgds = new List<string>();

            try {
                ParseArgs(ref args, ref paths, ref fgds);
            }
            catch (ArgumentException e) {
                Console.WriteLine(e.Message);
                Console.WriteLine();
                PrintUsage();
#if DEBUG
                Console.Write("Press any key to exit: ");
                Console.ReadKey();
#endif
                return;
            }
            catch (InvalidDataException e) {
                Console.WriteLine(e.Message);
                Console.WriteLine();
#if DEBUG
                Console.Write("Press any key to exit: ");
                Console.ReadKey();
#endif
                return;
            }

            string map_in = Path.GetFullPath(paths.First());
            tld = Path.GetDirectoryName(map_in);

            if (cleanup) {
                CleanUp(map_in);
                return;
            }

            // Checking for directory write access ahead of time like this is
            // hardly foolproof, since that access could change by the time we
            // try to write the real files, but it's better than nothing at all.
            try {
                Directory.CreateDirectory(tmp_path + sep);
                File.Open(tmp_path + sep + exe_name + "_access_check", FileMode.CreateNew).Close();
                File.Delete(tmp_path + sep + exe_name + "_access_check");
            }
            catch (UnauthorizedAccessException) {
                Console.WriteLine("Couldn't access temp directory! Please use -t to specify another.");
                PrintUsage();
                return;
            }
            catch (IOException) {
                Console.WriteLine("Couldn't access temp directory! Please use -t to specify another.");
                PrintUsage();
                return;
            }
            deletables["dirs"].Add(tmp_path + sep);

            string map_preprocessed = tmp_path + sep + Path.GetFileNameWithoutExtension(map_in) + ".vmf";

            Console.Write("Preprocessing FGDs...");
            foreach (string fgd in fgds)
                PreprocessFgd(fgd);
            Console.WriteLine("done!");

            Console.Write("Preprocessing map and instances...");
            try {
                PreprocessMap(map_in);
            }
            catch (InvalidDataException e) {
                Console.Write("error: ");
                Console.WriteLine(e.Message);
                return;
            }
            Console.WriteLine("done!");

            CollapseInstances(map_preprocessed, fgds.Select(x => tmp_path + sep + Path.GetFileName(x)).ToList());

            Console.Write("Postprocessing map...");
            PostprocessMap(map_preprocessed.Replace(".vmf", ".temp.vmf"));
            Console.WriteLine("done!");

            string map_postprocessed = map_preprocessed.Replace(".vmf", ".temp.map");

            // To avoid unnecessary work above when writing map_postprocessed,
            // we only spit out users' requested output during the final copy.
            string map_final;
            if (paths.Count == 2)
                map_final = paths[1];
            else
                map_final = tld + sep + Path.GetFileName(map_postprocessed);

            // Setting the working directory to the location of the input map
            // means that whether paths[1] is relative or absolute, users will
            // find the output file where they expect to.
            Directory.SetCurrentDirectory(tld + sep);
            if (File.Exists(map_final))
                File.Delete(map_final);
            File.Copy(map_postprocessed, map_final);
            deletables["files"].Add(map_postprocessed);

            if (!keep) {
                Console.Write("Deleting temp files...");
                foreach (string file in deletables["files"])
                    File.Delete(file);

                // We can't delete a directory until its subdirectories are gone,
                // but we know everything in this list is an absolute path, which
                // means sorting by length and reversing the list is enough.
                deletables["dirs"].Sort();
                deletables["dirs"].Reverse();

                bool del_success = true;
                foreach (string dir in deletables["dirs"]) {
                    try {
                        Directory.Delete(dir);
                    }
                    catch (IOException e) {
                        del_success = false;
                        Console.Write("\nError deleting directory " + tmp_path + ": ");
                        Console.WriteLine(e.Message);
                    }
                }
                if (del_success)
                    Console.WriteLine("done!");
            } else {
                Console.WriteLine("Keeping temp files in " + tmp_path + '!');
            }
#if DEBUG
            Console.Write("Press any key to exit:");
            Console.ReadKey();
#endif
        }

        static void CleanUp(string map_in)
        {
            string path = Path.GetDirectoryName(map_in),
                   temp = path + sep + Path.GetFileNameWithoutExtension(map_in) + ".temp",
                   temp_map = temp + ".map",
                   temp_bsp = temp + ".bsp",
                   temp_lin = temp + ".lin",
                   temp_prt = temp + ".prt",
                   temp_pts = temp + ".pts";

            Console.Write("Cleaning up...");

            if (File.Exists(temp_map))
                File.Delete(temp_map);

            // The only file I don't process here is the log, since that would
            // delete old logs with every new compile. Leaving it where it is
            // lets QBSP simply append, which can be useful for troubleshooting.

            if (File.Exists(temp_bsp)) {
                File.Delete(temp_bsp.Replace(".temp.bsp", ".bsp"));
                File.Move(temp_bsp, temp_bsp.Replace(".temp.bsp", ".bsp"));
            }

            if (File.Exists(temp_lin)) {
                File.Delete(temp_lin.Replace(".temp.lin", ".lin"));
                File.Move(temp_lin, temp_lin.Replace(".temp.lin", ".lin"));
            }

            if (File.Exists(temp_prt)) {
                File.Delete(temp_prt.Replace(".temp.prt", ".prt"));
                File.Move(temp_prt, temp_prt.Replace(".temp.prt", ".prt"));
            }

            if (File.Exists(temp_pts)) {
                File.Delete(temp_pts.Replace(".temp.pts", ".pts"));
                File.Move(temp_pts, temp_pts.Replace(".temp.pts", ".pts"));
            }

            Console.WriteLine("done!");
        }

        static void CollapseInstances(string map_out, List<string> fgds)
        {
            Process vmfii = new Process();
            vmfii.StartInfo.Arguments = map_out + " --fgd \"" + String.Join(",", fgds.ToArray()) + '"';
            vmfii.StartInfo.CreateNoWindow = true;
            vmfii.StartInfo.FileName = "lib/vmfii/vmfii.exe";
            vmfii.StartInfo.RedirectStandardError = true;
            vmfii.StartInfo.RedirectStandardInput = true;
            vmfii.StartInfo.RedirectStandardOutput = true;
            vmfii.StartInfo.UseShellExecute = false;
            vmfii.StartInfo.WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            Console.WriteLine("Starting VMFII:\n");
            vmfii.Start();
            Console.Write(vmfii.StandardOutput.ReadToEnd());
            Console.Write(vmfii.StandardError.ReadToEnd());
            vmfii.WaitForExit();
            Console.WriteLine("\nVMFII done!");
            deletables["files"].Add(map_out.Replace(".vmf", ".temp.vmf"));
        }

        static void ParseArgs(ref string[] args, ref List<string> paths, ref List<string> fgds)
        {
            // For users' sake this program is set up to accept the same
            // parameters as VMFII, please pardon the arg parsing mimicry.
            //
            // Implementing this as a while loop makes me feel better about
            // fiddling with the loop index when reading the fgd list.
            int i = 0;
            while (i < args.Length) {
                string arg = args[i];
                if (!arg.StartsWith("-")) {
                    paths.Add(arg);
                } else {
                    switch (arg.ToLower()) {
                        case "-c":
                        case "--cleanup":
                            cleanup = true;
                            break;
                        case "-d":
                        case "--fgd":
                            foreach (string fgd in args[++i].Split(','))
                                fgds.Add(Path.GetFullPath(fgd));
                            break;
                        case "-k":
                        case "--keep":
                            keep = true;
                            break;
                        case "-t":
                        case "--tmpdir":
                            tmp_path = args[++i];
                            if (!Directory.Exists(tmp_path))
                                throw new System.ArgumentException("Specified temp directory doesn't exist!");
                            break;
                        default:
                            throw new System.ArgumentException("Unrecognized parameter!");
                    }
                }
                ++i;
            }

            if (paths.Count == 0)
                throw new System.ArgumentException("No input file specified!");

            if (paths.Count > 2)
                throw new System.ArgumentException("Too many paths! Expected only input and output.");

            if (!File.Exists(paths[0]))
                throw new System.ArgumentException("Input file not found!");
        }

        static void PreprocessFgd(string fgd)
        {
            List<string> lines_out = new List<string>();

            using (StreamReader sr = File.OpenText(fgd)) {

                while (!sr.EndOfStream) {
                    string line_in = sr.ReadLine();

                    // Lines with a class name but no equals sign will trip up
                    // VMFII, so we combine lines until we have one.
                    if (line_in.Trim().StartsWith("@"))
                        while (!line_in.Contains('='))
                            line_in += sr.ReadLine();

                    // At the moment VMFII can't handle lowercase class names, so
                    // onward to CamelCase!
                    string class_pattern = "(.*?)@(\\S+?)[cC]lass(.*)";
                    Match match = Regex.Match(line_in, class_pattern);
                    if (match.Success) {
                        string leader = match.Groups[1].ToString(),
                               name = match.Groups[2].ToString(),
                               cap = name.First().ToString().ToUpper();
                        line_in = leader + '@' + cap + name.Substring(1) + "Class" + match.Groups[3].ToString();
                    }

                    // We're only trying to do some formatting cleanup for these
                    // FGDs, so we want to keep indentation and comments. That means
                    // doing our bracket searches on a stripped, trimmed version of
                    // the current line, but adding the original unstripped,
                    // untrimmed line to the output list.
                    string trimmed = Util.StripComment(line_in).Trim();

                    if ((trimmed.Contains('[') && trimmed != "[") || (trimmed.Contains(']') && trimmed != "]")) {
                        int bracket_pos = 0;

                        // It's possible the line could contain both brackets, but
                        // in that case the open bracket check should cover us.
                        if (trimmed.Contains('['))
                            bracket_pos = trimmed.IndexOf('[');
                        else
                            bracket_pos = trimmed.IndexOf(']');

                        // A bracket surrounded by at least one double quote mark on
                        // each side is not a delimiter, but part of a description
                        // string, and should remain on its current line.
                        bool quote_before = trimmed.Substring(0, bracket_pos).Contains('\"'),
                             quote_after = trimmed.Substring(bracket_pos + 1).Contains('\"');
                        if (quote_before && quote_after) {
                            lines_out.Add(line_in);
                            continue;
                        }

                        String leftover = "";
                        if (trimmed.Contains('[')) {
                            if (!trimmed.StartsWith("["))
                                lines_out.Add(line_in.Substring(0, line_in.IndexOf('[') - 1));
                            lines_out.Add("[");
                            if (!trimmed.EndsWith("[")) {
                                int start = line_in.IndexOf('[') + 1;
                                leftover = line_in.Substring(start, line_in.Length - start);
                            }
                        } else {
                            leftover = line_in;
                        }

                        if (leftover == "") {
                            continue;
                        } else if (leftover == "]" || !leftover.Contains(']')) {
                            lines_out.Add(leftover);
                            continue;
                        } else {
                            if (!leftover.StartsWith("]"))
                                lines_out.Add(leftover.Substring(0, leftover.IndexOf(']') - 1));
                            lines_out.Add("]");
                            if (!trimmed.EndsWith("]")) {
                                int start = line_in.IndexOf(']') + 1;
                                lines_out.Add(leftover.Substring(start, leftover.Length - start));
                            }
                        }
                    } else {
                        lines_out.Add(line_in);
                    }
                }
            }

            string fgd_out = tmp_path + sep + Path.GetFileName(fgd);
            using (StreamWriter sw = File.CreateText(fgd_out))
                foreach (string line in lines_out)
                    sw.WriteLine(line);
            if (!deletables["files"].Contains(fgd_out))
                deletables["files"].Add(fgd_out);
        }

        static void PreprocessMap(string map_in)
        {
            // Later on, when processing a given line, we'll need to peek ahead
            // at the next line without consuming it, so instead of going line
            // by line with a StreamReader we just prepare a list of strings.
            List<string> lines_in = File.ReadAllLines(map_in).ToList();

            // We need to check each map individually, since any of them could
            // be in an unsupported format. Better not to silently or even
            // quietly let some maps go unprocessed, just quit.
            string mapversion = "";
            foreach (string line in lines_in) {
                if (line.Trim().StartsWith("\"mapversion\"")) {
                    mapversion = line.Trim().Split('"')[3].Trim();
                    break;
                }
            }
            if (mapversion != "220")
                throw new InvalidDataException("Unsupported .map version! Expected Valve 220, got \"" + mapversion + "\" from " + map_in);

            List<string> lines_out = new List<string>();
            for (int i = 0; i < lines_in.Count; ++i) {
                string line_in = lines_in[i],
                       trimmed = line_in.Trim();

                if (trimmed == "")
                    continue;

                if (trimmed == "{") {
                    string line_next = lines_in[i + 1].Trim();
                    if (line_next.EndsWith("\"worldspawn\"") && line_next.StartsWith("\"classname\"")) {
                        lines_out.Add("world");
                        lines_out.Add(line_in);
                    } else if (line_next.StartsWith("\"")) {
                        lines_out.Add("entity");
                        lines_out.Add(line_in);
                    } else if (line_next.StartsWith("(")) {
                        lines_out.Add("solid");
                        lines_out.Add(line_in);
                    }
                } else if (trimmed.StartsWith("(")) {
                    lines_out.Add("side");
                    lines_out.Add("{");

                    Regex qSideRegex = new Regex("(\\s*?)(\\(.+\\))\\s(\\S+?)\\s(\\[.+?\\])\\s(\\[.+?\\])\\s(\\S+?)\\s(\\S+?)\\s(\\S+)");

                    string indent = qSideRegex.Match(line_in).Groups[1].ToString();

                    string plane = qSideRegex.Match(line_in).Groups[2].ToString();
                    plane = plane.Replace("( ", "(").Replace(" )", ")");

                    string material = qSideRegex.Match(line_in).Groups[3].ToString();

                    string uaxis = qSideRegex.Match(line_in).Groups[4].ToString();
                    uaxis = uaxis.Replace("[ ", "[").Replace(" ]", "]");

                    string vaxis = qSideRegex.Match(line_in).Groups[5].ToString();
                    vaxis = vaxis.Replace("[ ", "[").Replace(" ]", "]");

                    string rotation = qSideRegex.Match(line_in).Groups[6].ToString();

                    string uscale = qSideRegex.Match(line_in).Groups[7].ToString(),
                           vscale = qSideRegex.Match(line_in).Groups[8].ToString();
                    uaxis += " " + uscale;
                    vaxis += " " + vscale;

                    lines_out.Add(indent + "\"plane\" \"" + plane + "\"");
                    lines_out.Add(indent + "\"material\" \"" + material + "\"");
                    lines_out.Add(indent + "\"uaxis\" \"" + uaxis + "\"");
                    lines_out.Add(indent + "\"vaxis\" \"" + vaxis + "\"");
                    lines_out.Add(indent + "\"rotation\" \"" + rotation + "\"");
                    lines_out.Add("}");
                } else {
                    if (trimmed.StartsWith("\"file\"") && trimmed.Contains(".map")) {
                        line_in = line_in.Replace(".map", ".vmf");

                        string path_pattern = "\"file\".*?\"(.+?\\.map)\"",
                               path_rel = Regex.Match(trimmed, path_pattern).Groups[1].ToString(),
                               path_abs = Path.GetFullPath(Path.GetDirectoryName(map_in) + sep + path_rel);

                        // Map files are only added to deletables once they've
                        // been processed, which serves as a handy cache.
                        if (!deletables["files"].Contains(path_abs))
                            PreprocessMap(path_abs);
                    }
                    lines_out.Add(line_in);
                }
            }

            // Preprocessed FGDs just get dumped at the top level of the temp
            // directory, but for preprocessing .maps it's important to have the
            // temp directory tree layout match that of the input files. We
            // therefore create intermediate paths accordingly.
            string path_intermediate = tmp_path + Path.GetDirectoryName(map_in).Replace(tld, "") + sep,
                   vmf_out = path_intermediate + Path.GetFileName(map_in).Replace(".map", ".vmf");

            Directory.CreateDirectory(Path.GetDirectoryName(path_intermediate));
            if (!deletables["dirs"].Contains(path_intermediate))
                deletables["dirs"].Add(path_intermediate);

            using (StreamWriter sw = File.CreateText(vmf_out))
                foreach (string line in lines_out)
                    sw.WriteLine(line);
            if (!deletables["files"].Contains(vmf_out))
                deletables["files"].Add(vmf_out);
        }

        static void PrintUsage()
        {
            List<string> help = new List<string>() {

                "Usage:",
                "",
                "quinstance.exe input [output] -d FGD [-c] [-k] [-t TMPDIR]",
                "",
                "Linux/OS X users will need to add 'mono ' to the head of their command line.",
                "",
                "Parameters:",
                "",
                "  input",
                "    The input file to be processed. Must be a Quake .map in Valve 220 format.",
                "",
                "  output [optional]",
                "    The file to output after processing. Defaults to input.temp.map.",
                "",
                "  -d, --fgd",
                "    Specify one or more FGD files, as a comma separated string, to be",
                "    preprocessed and passed along to VMFII.",
                "",
                "  -c, --cleanup [optional]",
                "    Deletes 'output' and renames the associated BSP, PRT, LIN and PTS files.",
                "",
                "  -k, --keep [optional]",
                "    Keep the generated temporary files instead of deleting them.",
                "",
                "  -t, --tmpdir [optional]",
                "    Specify the directory in which to store temporary files. Defaults to the",
                "    user's temp directory.",
                "" };

            foreach (string line in help)
                Console.WriteLine(line);
        }

        static void PostprocessMap(string map_in)
        {
            List<string> lines_in = new List<string>();
            using (StreamReader sr = File.OpenText(map_in))
                while (!sr.EndOfStream)
                    lines_in.Add(sr.ReadLine());

            List<string> lines_out = new List<string>();
            for (int i = 0; i < lines_in.Count; ++i) {
                string line_in = lines_in[i];
                string trimmed = line_in.Trim();

                // Ignore VMF type names.
                if (trimmed == "world" || trimmed == "solid" || trimmed == "side" || trimmed == "entity")
                    continue;

                // Ignore braces for sides, which aren't blocks in Quake maps.
                if ((trimmed == "{" && lines_in[i - 1].Trim() == "side") ||
                    (trimmed == "}" && lines_in[i - 1].Trim().StartsWith("\"rotation\"")))
                    continue;

                if (trimmed.StartsWith("\"material\"") || trimmed.StartsWith("\"uaxis\"") || trimmed.StartsWith("\"vaxis\"") || trimmed.StartsWith("\"rotation\""))
                    continue;

                if (trimmed.StartsWith("\"plane\"")) {
                    Regex regexKeyVal = new Regex("(\\s*?)\".+?\".*?\"(.+?)\"");
                    Regex regexAxis = new Regex("\\s*?\".+?\".*?\"(.+?\\])\\s(.+?)\"");

                    string indent = regexKeyVal.Match(line_in).Groups[1].ToString();

                    string plane = regexKeyVal.Match(line_in).Groups[2].ToString();
                    plane = plane.Replace("(", "( ").Replace(")", " )");

                    string material = regexKeyVal.Match(lines_in[i + 1].Trim()).Groups[2].ToString();

                    string uaxis = regexAxis.Match(lines_in[i + 2].Trim()).Groups[1].ToString();
                    uaxis = uaxis.Replace("[", "[ ").Replace("]", " ]");

                    string vaxis = regexAxis.Match(lines_in[i + 3].Trim()).Groups[1].ToString();
                    vaxis = vaxis.Replace("[", "[ ").Replace("]", " ]");

                    string uscale = regexAxis.Match(lines_in[i + 2].Trim()).Groups[2].ToString(),
                           vscale = regexAxis.Match(lines_in[i + 3].Trim()).Groups[2].ToString();

                    string rotation = regexKeyVal.Match(lines_in[i + 4].Trim()).Groups[2].ToString();

                    lines_out.Add(indent + plane + " " + material + " " + uaxis + " " + vaxis + " " + rotation + " " + uscale + " " + vscale);
                } else {
                    lines_out.Add(line_in);
                }
            }

            using (StreamWriter sw = File.CreateText(map_in.Replace(".temp.vmf", ".temp.map")))
                foreach (string line in lines_out)
                    sw.WriteLine(line);
        }
    }

    public class Util
    {
        public static string StripComment(string line)
        {
            if (line.Contains("//"))
                return line.Substring(0, line.IndexOf("//"));
            else
                return line;
        }
    }
}
