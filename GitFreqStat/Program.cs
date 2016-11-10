using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using ShellProgressBar;

namespace GitFreqStat
{
    internal class Program
    {
        private static readonly Dictionary<string, int> filesFrequency = new Dictionary<string, int>();

        private static void Main(string[] args)
        {
            var path = Directory.GetCurrentDirectory();
            if (args.Length > 0)
            {
                path = args[0];
            }
            else
            {
                Console.WriteLine($"Enter repository path to scan please [{path}]");
                var newPath = Console.ReadLine();
                if (!string.IsNullOrEmpty(newPath))
                    path = newPath;
            }

            var daysToWatch = 30;
            if (args.Length > 1)
            {
                daysToWatch = Convert.ToInt32(args[1]);
            }
            else
            {
                Console.WriteLine($"How many days back should we analyze? [{daysToWatch}]");
                var dayInput = Console.ReadLine();
                if (!string.IsNullOrEmpty(dayInput))
                    daysToWatch = Convert.ToInt32(dayInput);
            }

            var changesToReport = 5;
            if (args.Length > 2)
            {
                changesToReport = Convert.ToInt32(args[2]);
            }
            else
            {
                Console.WriteLine($"What threshold of edits to start reporting on? [{changesToReport}]");
                var changesInput = Console.ReadLine();
                if (!string.IsNullOrEmpty(changesInput))
                    changesToReport = Convert.ToInt32(changesInput);
            }

            var stopAt = DateTime.Now.Subtract(new TimeSpan(daysToWatch, 0, 0, 0, 0));


            using (var repo = new Repository(path))
            {
                var commitsToTake = repo.Commits.Where(c => c.Committer.When.DateTime >= stopAt);
                var count = commitsToTake.Count();
                var current = count;

                using (var pbar = new ProgressBar(count, "Analyzing git repository", ConsoleColor.Green))
                {
                    foreach (var log in commitsToTake)
                    {
                        current -= 1;
                        pbar.Tick($"Commits left: {current}");

                        //break if commit is older than we need
                        if (log.Committer.When.DateTime < stopAt)
                            break;

                        if (log.Parents.Any())
                        {
                            var oldTree = log.Parents.First().Tree;
                            var changes = repo.Diff.Compare<TreeChanges>(oldTree, log.Tree);
                            foreach (var change in changes)
                                UpdateFileFrequency(change.Path);
                        }
                        else
                        {
                            foreach (var entry in log.Tree)
                                CountUpdatedFilesFrequency(entry);
                        }
                    }
                }
            }


            var frequencyReport = filesFrequency.Select(f => new { Frequency = f.Value, File = f.Key })
                                                .Where(p => p.Frequency >= changesToReport)
                                                .OrderByDescending(f => f.Frequency);

            if (!frequencyReport.Any())
            {
                Console.WriteLine("No data to show... Change filter criteria. Press any key to exit.");
                Console.Read();
                Environment.Exit(0);
            }

            Console.WriteLine();
            Console.Write("Edits\tFile");
            Console.WriteLine();

            int max = frequencyReport.First().Frequency;
            int min = frequencyReport.Last().Frequency;

            foreach (var f in frequencyReport)
            {
                var oldColor = Console.ForegroundColor;
                if (f.Frequency > min + ((2 * (max - min)) / 3))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else if (f.Frequency > min + (((max - min)) / 3))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }

                Console.Write(f.Frequency);
                Console.ForegroundColor = oldColor;
                Console.Write($"\t {f.File}");
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to finish...");
            Console.ReadKey();
        }

        private static void CountUpdatedFilesFrequency(TreeEntry file)
        {
            if (file.Mode == Mode.Directory)
                foreach (var child in file.Target as Tree)
                    CountUpdatedFilesFrequency(child);
            else
                UpdateFileFrequency(file.Path);
        }

        private static void UpdateFileFrequency(string file)
        {
            var frequency = 0;
            filesFrequency.TryGetValue(file, out frequency);
            frequency++;
            filesFrequency[file] = frequency;
        }
    }
}