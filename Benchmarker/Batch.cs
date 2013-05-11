﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using PlasmaShared;

namespace Benchmarker
{
    /// <summary>
    /// Full batch of tests that can be saved,loaded, executed nad measured
    /// </summary>
    public class Batch
    {
        bool isAborted;
        SpringRun run;
        /// <summary>
        /// Benchmark mutators to use
        /// </summary>
        public List<Benchmark> Benchmarks = new List<Benchmark>();
        /// <summary>
        /// Cases to check
        /// </summary>
        public List<TestCase> TestCases = new List<TestCase>();
        public event Action<BatchRunResult> AllCompleted = (result) => { };
        public event Action<TestCase, Benchmark, string> RunCompleted = (run, benchmark, log) => { };

        public void Abort() {
            isAborted = true;
            if (run != null) run.Abort();
        }

        /// <summary>
        /// Returns folders of interest - for example if you set "Mods" it will look in all datadirs and current dir for "Mods" and "Benchmarks/Mods"
        /// </summary>
        public static List<DirectoryInfo> GetBenchmarkFolders(SpringPaths paths, string folderName) {
            var dirsToCheck = new List<string>(paths.DataDirectories);
            dirsToCheck.Add(Directory.GetCurrentDirectory());
            var ret = new List<DirectoryInfo>();
            foreach (var dir in dirsToCheck) {
                var sub = Path.Combine(dir, folderName);
                if (Directory.Exists(sub)) ret.Add(new DirectoryInfo(sub));

                var bsub = Path.Combine(dir, "Benchmarks", folderName);
                if (Directory.Exists(bsub)) ret.Add(new DirectoryInfo(bsub));
            }

            return ret;
        }

        public static Batch Load(string path, SpringPaths springPaths) {
            var batch = JsonConvert.DeserializeObject<Batch>(File.ReadAllText(path));
            batch.PostLoad(springPaths);
            return batch;
        }

        public void RunTests(SpringPaths paths) {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            isAborted = false;
            var result = new BatchRunResult();
            foreach (var tr in TestCases) {
                foreach (var b in Benchmarks) {
                    if (isAborted) return;
                    b.ModifyModInfo(tr);
                    try {
                        run = new SpringRun();
                        var log = run.Start(paths, tr, b);
                        result.AddRun(tr, b, log);
                        RunCompleted(tr, b, log);
                    } finally {
                        b.RestoreModInfo();
                    }
                }
            }
            if (isAborted) return;
            AllCompleted(result);
        }

        public void Save(string s) {
            File.WriteAllText(s, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>
        /// Validates content - downloads files
        /// </summary>
        public string Validate(PlasmaDownloader.PlasmaDownloader downloader) {
            if (!Benchmarks.Any()) return "No benchmarks selected - please add benchmarks (mutators/mods) into Mods or Benchmarks/Mods folder - in the folder.sdd format";
            if (!TestCases.Any()) return "Please add test case runs using add button here";

            foreach (var bench in Benchmarks) {
                var ret = bench.Validate(downloader);
                if (ret != null) return ret;
            }

            foreach (var run in TestCases) {
                var ret = run.Validate(downloader);
                if (ret != null) return ret;
            }
            return "OK";
        }

        void PostLoad(SpringPaths paths) {
            Benchmarks =
                Benchmarks.Select(
                    x =>
                    Benchmark.GetBenchmarks(paths).SingleOrDefault(y => y.BenchmarkPath == x.BenchmarkPath) ??
                    Benchmark.GetBenchmarks(paths).First(y => y.Name == x.Name)).ToList();

            foreach (var tr in TestCases) {
                tr.Config = Config.GetConfigs(paths).SingleOrDefault(x => x.ConfigPath == tr.Config.ConfigPath) ??
                            Config.GetConfigs(paths).First(x => x.Name == tr.Config.Name);

                tr.StartScript = StartScript.GetStartScripts(paths).SingleOrDefault(x => x.ScriptPath == tr.StartScript.ScriptPath) ??
                            StartScript.GetStartScripts(paths).First(x => x.Name == tr.StartScript.Name);
            }
        }
    }
}