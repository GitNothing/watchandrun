using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConsoleApp1
{
    class Program
    {
        public static int delayedTime = 100; //(milliseconds) prevents double event call bug
        private static List<string> local;
        private static List<string> exact;
        private static string command;
        private static HashSet<string> hold = new HashSet<string>();
        static void Main(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            Except(!File.Exists(currentDirectory + @"\war.config.json"), "You need to provide a war.config.json file");
            using (StreamReader file = File.OpenText(currentDirectory + @"\war.config.json"))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                local = o2["local"].ToObject<List<string>>();
                exact = o2["exact"].ToObject<List<string>>();
                command = o2["command"].ToObject<string>();
            }

            Except(!local.Any() && !exact.Any(), "No files to detect");
            Except(String.IsNullOrEmpty(command), "No command was provided");

            var remove = new List<string>();
            local = local.Select(e => currentDirectory + "\\" + e).ToList();
            foreach (var file in local.Concat(exact))
            {
                if (!File.Exists(file))
                {
                    remove.Add(file);
                }
            }

            Console.WriteLine("Command: " + command);
            foreach (var file in remove)
            {
                local.Remove(file);
                exact.Remove(file);
                Console.WriteLine($"WARNING: File not exist: " + file);
            }

            foreach (var file in local.Concat(exact))
            {
                var watch = new FileSystemWatcher();
                watch.Path = Path.GetDirectoryName(file);
                watch.NotifyFilter = NotifyFilters.LastWrite;
                watch.Filter = Path.GetFileName(file);
                watch.Changed += new FileSystemEventHandler(OnChanged);
                watch.EnableRaisingEvents = true;
            }
            Console.ReadKey();
        }

        private static void Except(bool condition, string message)
        {
            if (!condition) return;
            try
            {
                throw new Exception(message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            }
        }
        private static void OnChanged(object s, FileSystemEventArgs e)
        {
            if (hold.Contains(e.FullPath)) return;
            Console.WriteLine($"FILE CHANGE DETECTED {DateTime.Now.ToString("HH:mm:ss")}: {Path.GetFileName(e.FullPath)}");
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            var d = cmd.StandardOutput.ReadToEnd();
            var ss = d.Split(Environment.NewLine.ToCharArray());
            for (var i = 8; i < ss.Length-2; i++)
            {
                if(!String.IsNullOrEmpty(ss[i]))
                Console.WriteLine(ss[i]);
            }

            hold.Add(e.FullPath);
            var p = e.FullPath;
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(delayedTime);
                hold.Remove(p);
            });

        }
    }
}
