using Quinstance;
using Quinstance.Quin3d;
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

        static string exe_name = Assembly.GetAssembly(typeof(Program)).GetName().Name,
                      tmp_dir = exe_name + "_temp",
                      tmp_path = Path.GetTempPath() + tmp_dir + sep,
                      tld;

        static Dictionary<string, List<string>> deletables = new Dictionary<string, List<string>>() {
            { "dirs", new List<string>() }, { "files", new List<string>() }
        };

        static List<string> placeholders = new List<string>();

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

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.Write("Press any key to exit: ");
                    Console.ReadKey();
                }

                return;
            }
            catch (InvalidDataException e) {
                Console.WriteLine(e.Message);
                Console.WriteLine();

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.Write("Press any key to exit: ");
                    Console.ReadKey();
                }

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
                Console.WriteLine("Couldn't access temp directory " + tmp_path + "! Please use -t to specify another.");
                PrintUsage();
                return;
            }
            catch (IOException) {
                Console.WriteLine("Couldn't access temp directory " + tmp_path + "! Please use -t to specify another.");
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

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.Write("Press any key to exit: ");
                Console.ReadKey();
            }
        }

        static void CleanUp(string map_in)
        {
            string path = Path.GetDirectoryName(map_in),
                   temp = path + sep + Path.GetFileNameWithoutExtension(map_in) + ".temp";

            // The only file I don't process here is the log, since that would
            // delete old logs with every new compile. Leaving it where it is
            // lets QBSP simply append, which can be useful for troubleshooting.
            var extensions = new List<string>() { ".map", ".bsp", ".lin", ".prt", ".pts", ".texinfo" };

            Console.Write("Cleaning up...");

            if (File.Exists(temp + ".map"))
                File.Delete(temp + ".map");

            foreach (var extension in extensions)
            {
                string renamable = temp + extension;

                if (File.Exists(renamable))
                {
                    File.Delete(renamable.Replace(".temp" + extension, extension));
                    File.Move(renamable, renamable.Replace(".temp" + extension, extension));
                }
            }

            Console.WriteLine("done!");
        }

        static void CollapseInstances(string map_out, List<string> fgds)
        {
            string exe_dir = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Program)).Location),
                   vmfii_abs = exe_dir + sep + "lib" + sep + "vmfii" + sep + "vmfii.exe";

            Process vmfii = new Process();
            vmfii.StartInfo.Arguments = map_out + " --fgd \"" + String.Join(",", fgds.ToArray()) + '"';
            vmfii.StartInfo.CreateNoWindow = true;
            vmfii.StartInfo.FileName = vmfii_abs;
            vmfii.StartInfo.RedirectStandardError = true;
            vmfii.StartInfo.RedirectStandardInput = true;
            vmfii.StartInfo.RedirectStandardOutput = true;
            vmfii.StartInfo.UseShellExecute = false;
            vmfii.StartInfo.WorkingDirectory = exe_dir;

            Console.WriteLine("Starting VMFII:\n");
            vmfii.Start();
            Console.Write(vmfii.StandardOutput.ReadToEnd());
            Console.Write(vmfii.StandardError.ReadToEnd());
            vmfii.WaitForExit();
            Console.WriteLine("\nVMFII done!");
            deletables["files"].Add(map_out.Replace(".vmf", ".temp.vmf"));
        }

        static List<string> ConvertQuakeEdTo220(List<string> lines_in)
        {
            List<string> lines_out = new List<string>();

            // The order of planes here is specifically designed to match up
            // with the texture orientation results provided by editors and
            // compilers. First -Z and its normal-flipped counterpart, then -X
            // and its alternative, and finally -Y.
            List<Plane> cardinals = new List<Plane>() {
                new Plane("(0 0 0) (1 0 0) (0 -1 0)"), // XY, normal points toward -Z
                new Plane("(0 0 0) (1 0 0) (0 -1 0)", true),
                new Plane("(0 0 0) (0 1 0) (0 0 -1)"), // YZ, normal points toward -X
                new Plane("(0 0 0) (0 1 0) (0 0 -1)", true),
                new Plane("(0 0 0) (1 0 0) (0 0 -1)"), // ZX, normal points toward -Y
                new Plane("(0 0 0) (1 0 0) (0 0 -1)", true)};

            for (int i = 0; i < lines_in.Count; ++i) {
                string line_in = Quinstance.Util.StripComment(lines_in[i]);
                string trimmed = line_in.Trim();
                if (!trimmed.StartsWith("(")) {
                    if (i > 0 && lines_in[i - 1].Trim().EndsWith("\"worldspawn\""))
                        lines_out.Add("\"mapversion\" \"220\"");
                    lines_out.Add(line_in);
                    continue;
                }
                
                string indent = line_in.Substring(0, line_in.IndexOf('('));
                Plane p = new Plane(line_in.Substring(line_in.IndexOf('('), line_in.LastIndexOf(')') + 1));
                char[] texinfo_delims = { ' ' };
                string[] texinfo = line_in.Substring(line_in.LastIndexOf(')') + 1).Split(texinfo_delims, StringSplitOptions.RemoveEmptyEntries);

                double smallest_angle = 180.0;
                Plane closest_plane = cardinals[0];
                foreach (Plane q in cardinals) {
                    double current_angle = Quin3d.Util.UnsignedAngleBetweenVectors(p.normal, q.normal);
                    if (current_angle < smallest_angle) {
                        closest_plane = q;
                        smallest_angle = current_angle;
                    }
                }

                double angle;
                Double.TryParse(texinfo[3], out angle);

                // TODO: Add something for angle == 0.0!

                double cos = Math.Cos(angle * (System.Math.PI / 180.0)),
                       sin = Math.Sin(angle * (System.Math.PI / 180.0));

                Matrix3x3 matrix_x = new Matrix3x3(new double[] { 1.0,  0.0,  0.0,
                                                                  0.0,  cos, -sin,
                                                                  0.0,  sin,  cos }),
                                           
                          matrix_y = new Matrix3x3(new double[] { cos,  0.0,  sin,
                                                                  0.0,  1.0,  0.0,
                                                                 -sin,  0.0,  cos }),
                          
                          matrix_z = new Matrix3x3(new double[] { cos, -sin,  0.0,
                                                                  sin,  cos,  0.0,
                                                                  0.0,  0.0,  1.0 });

                Matrix3x3 matrix = new Matrix3x3();

                // Equality checks for doubles, I know, but in this case the
                // only possible values of 'closest_plane' will have normals
                // with exact coordinates. I hope.
                if (closest_plane.normal.x == 1.0 || closest_plane.normal.x == -1.0)
                    matrix = matrix_x;
                else if (closest_plane.normal.y == 1.0 || closest_plane.normal.y == -1.0)
                    matrix = matrix_y;
                else
                    matrix = matrix_z;
                Point3d rot_b = Quin3d.Util.MulMatrix3x3ByPoint3d(matrix, closest_plane.b);
                Point3d rot_c = Quin3d.Util.MulMatrix3x3ByPoint3d(matrix, closest_plane.c);

                // Each cardinal plane's vectors are assumed to start at 0 0 0,
                // which means no need to subtract anything to get the actual
                // vectors to use here, just throw in the values.
                lines_out.Add(indent + p.ToString() + ' ' + texinfo[0] + ' ' +
                              rot_b.ToString().Replace('(', '[').Replace(")", "") + texinfo[1] + " ] " +
                              rot_c.ToString().Replace('(', '[').Replace(")", "") + texinfo[2] + " ] " +
                              texinfo[3] + ' ' + texinfo[4] + ' ' + texinfo[5]);
            }
            return lines_out;
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
                            foreach (string fgd in args[++i].Split(',')) {
                                if (!File.Exists(Path.GetFullPath(fgd)))
                                    throw new System.ArgumentException("FGD " + fgd + " not found!");
                                fgds.Add(Path.GetFullPath(fgd));
                            }
                            break;
                        case "-k":
                        case "--keep":
                            keep = true;
                            break;
                        case "-t":
                        case "--tmpdir":
                            tmp_path = args[++i];
                            break;
                        case "-r":
                        case "--remove_entities":
                            placeholders = args[++i].Split(',').ToList();
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

        public static void PreprocessFgd(string fgd)
        {
            List<string> lines_out = new List<string>();

            using (StreamReader sr = File.OpenText(fgd)) {

                while (!sr.EndOfStream) {
                    string line_in = sr.ReadLine();

                    // Lines that begin a class definition, but don't contain
                    // the actual classname, will trip up VMFII. It expects the
                    // equals sign, followed by said classname, to be on one
                    // line, and the easiest way to ensure that is to combine
                    // everything through the opening bracket.
                    if (line_in.Trim().StartsWith("@"))
                        while (!line_in.Contains('['))
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

                        string leftover = "";
                        if (trimmed.Contains('[')) {
                            if (!trimmed.StartsWith("["))
                                lines_out.Add(line_in.Substring(0, line_in.IndexOf('[')));
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
                                lines_out.Add(leftover.Substring(0, leftover.IndexOf(']')));
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
            string mapversion = "QuakeEd";
            foreach (string line in lines_in) {
                if (line.Trim().StartsWith("\"mapversion\"")) {
                    mapversion = line.Trim().Split('"')[3].Trim();
                    break;
                } else if (line.Trim().EndsWith("//TX1")) {
                    mapversion = "QuArK TX1";
                    break;
                } else if (line.Trim().EndsWith("//TX2")) {
                    mapversion = "QuArK TX2";
                    break;
                }
            }
            if (mapversion != "220" && mapversion != "QuakeEd")
                throw new InvalidDataException("Unsupported .map format! Expected QuakeEd or Valve 220, got \"" + mapversion + "\" from " + map_in);

            if (mapversion == "QuakeEd")
                lines_in = ConvertQuakeEdTo220(lines_in);

            List<string> lines_out = new List<string>();

            // For VMFII to work, worldspawn needs to be given the block name
            // 'world' in our output, not just 'entity' like every other block
            // that contains key/value pairs. Since there's only one worldspawn,
            // and since a robust way to know the classname of the current block
            // will require major changes, a separate loop will do for now.
            int start = 0;
            for (int i = 0; i < lines_in.Count; ++i) {
                start = i + 1;

                string line_in = Quinstance.Util.StripComment(lines_in[i]),
                       trimmed = line_in.Trim();

                if (trimmed == "")
                    continue; 
                
                Regex regex_indent = new Regex(@"(\s*?)\S+");
                string indent = regex_indent.Match(line_in).Groups[1].ToString();

                if (trimmed == "{") {
                    lines_out.Add(indent + "world");
                    lines_out.Add(line_in);
                    break;
                } else {
                    lines_out.Add(line_in);
                }
            }

            for (int i = start; i < lines_in.Count; ++i) {
                string line_in = Quinstance.Util.StripComment(lines_in[i]),
                       trimmed = line_in.Trim();

                if (trimmed == "")
                    continue;

                Regex regex_indent = new Regex(@"(\s*?)\S+");
                string indent = regex_indent.Match(line_in).Groups[1].ToString();

                if (trimmed == "{") {
                    string line_next = lines_in[i + 1].Trim();
                    if (line_next.StartsWith("\"")) {
                        lines_out.Add(indent + "entity");
                        lines_out.Add(indent + line_in);
                    } else if (line_next.StartsWith("(")) {
                        lines_out.Add(indent + "solid");
                        lines_out.Add(indent + line_in);
                    }
                } else if (trimmed.StartsWith("(")) {
                    lines_out.Add(indent + "side");
                    lines_out.Add(indent + "{");

                    Regex regex_side = new Regex(@"\s*?" + // Leading space
                                                 @"(\(.+\))\s+?" + // Plane definition
                                                 @"(\S+?)\s*?" + // Texture name
                                                 @"(\[.+?\])\s*?" + // U axis
                                                 @"(\[.+?\])\s*?" + // V axis
                                                 @"(\S+)\s+?(\S+)\s+?(\S+)"); // Rotation, scale U, scale V

                    string plane = regex_side.Match(line_in).Groups[1].ToString();
                    plane = plane.Replace("( ", "(").Replace(" )", ")");

                    string material = regex_side.Match(line_in).Groups[2].ToString();

                    string uaxis = regex_side.Match(line_in).Groups[3].ToString();
                    uaxis = uaxis.Replace("[ ", "[").Replace(" ]", "]");

                    string vaxis = regex_side.Match(line_in).Groups[4].ToString();
                    vaxis = vaxis.Replace("[ ", "[").Replace(" ]", "]");

                    string rotation = regex_side.Match(line_in).Groups[5].ToString();

                    string uscale = regex_side.Match(line_in).Groups[6].ToString(),
                           vscale = regex_side.Match(line_in).Groups[7].ToString();
                    uaxis += " " + uscale;
                    vaxis += " " + vscale;

                    lines_out.Add(indent + indent + "\"plane\" \"" + plane + "\"");
                    lines_out.Add(indent + indent + "\"material\" \"" + material + "\"");
                    lines_out.Add(indent + indent + "\"uaxis\" \"" + uaxis + "\"");
                    lines_out.Add(indent + indent + "\"vaxis\" \"" + vaxis + "\"");
                    lines_out.Add(indent + indent + "\"rotation\" \"" + rotation + "\"");
                    lines_out.Add(indent + "}");
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
                foreach (string line in RemoveEntities(lines_out, placeholders))
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
                "    The input file to be processed. Must be a Quake .map in either classic",
                "    QuakeEd or Valve 220 formats.",
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
                "",
                "  -r, --remove_entities [optional]",
                "    A comma-separated list of entities to remove from all input files. Allows",
                "    placeholder geometry in editors which don't display instance contents.",
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

        static List<string> RemoveEntities(List<string> lines_in, List<string> classnames)
        {
            var lines_out = new List<string>();

            var i = 0;

            while (i < lines_in.Count) {
                string line = lines_in[i],
                       trimmed = line.Trim();

                if (trimmed != "entity") {
                    lines_out.Add(line);
                    ++i;
                    continue;
                } else {
                    List<string> remaining = lines_in.GetRange(i, lines_in.Count - i);

                    if (classnames.Contains(GetClassName(remaining))) {
                        i += GetBlockLength(remaining);
                    } else {
                        lines_out.Add(line);
                        ++i;
                        continue;
                    }
                }
            }

            return lines_out;
        }

        static int GetBlockLength(List<string> lines)
        {
            var length = 0;

            var open_braces = 0;

            // Find the opening of the block, assuming it begins at lines[0] but
            // making no assumptions about the placement of the first brace.
            for (var i = 0; i < lines.Count && open_braces == 0; ++i) {
                if (lines[i].Trim() == "{") {
                    length = i + 1;
                    open_braces = 1;
                }
            }

            // Then just loop through the rest of the provided lines, looking
            // for the closing brace to match the entity's opening.
            for (var i = length; i < lines.Count; ++i) {
                if (lines[i].Trim() == "{")
                    open_braces++;
                else if (lines[i].Trim() == "}")
                    open_braces--;

                if (open_braces == 0) {
                    length = i + 1;
                    break;
                }
            }

            return length;
        }

        static string GetClassName(List<string> lines_in)
        {
            var classname = "";

            foreach (var line in lines_in) {
                if (line.Trim().Contains("classname")) {
                    var regex = new Regex("\".*?\".*?\"(.*?)\"");
                    classname = regex.Match(line).Groups[1].ToString();
                    break;
                }
            }

            return classname;
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
