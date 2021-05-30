﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Kerberos.NET.Logging;
using Microsoft.Win32.SafeHandles;

namespace Benchmark.Kerberos.NET
{
    public enum BenchmarkType
    {
        None,
        Stress,
        Timing,
        DH
    }

    internal class CommandLineParameters
    {
        public BenchmarkType Benchmark { get; set; }

        public bool IsClient { get; set; }

        public string ClientIdentifier { get; set; }

        public int? Port { get; set; }

        public int? ThreadCount { get; set; }

        public bool IsServer { get; set; }

        public string ServerIdentifier { get; set; }

        public int? WorkerCount { get; set; }

        public int? RequestCount { get; set; }

        public override string ToString()
        {
            var parameters = new List<string>();

            if (this.Benchmark != BenchmarkType.None)
            {
                parameters.Add($"-benchmark {this.Benchmark}");
            }

            if (!string.IsNullOrWhiteSpace(this.ClientIdentifier))
            {
                parameters.Add($"-client {this.ClientIdentifier}");
            }

            if (this.Port > 0)
            {
                parameters.Add($"-port {this.Port}");
            }

            if (this.ThreadCount > 0)
            {
                parameters.Add($"-threads {this.ThreadCount}");
            }

            if (!string.IsNullOrWhiteSpace(this.ServerIdentifier))
            {
                parameters.Add($"-server {this.ServerIdentifier}");
            }

            if (this.WorkerCount > 0)
            {
                parameters.Add($"-workers {(this.WorkerCount.Value > 64 ? 64 : this.WorkerCount)}");
            }

            if (this.RequestCount > 0)
            {
                parameters.Add($"-requests {this.RequestCount}");
            }

            return string.Join(" ", parameters);
        }

        public static CommandLineParameters Parse(string[] args)
        {
            if (args.Length % 2 > 0 || args.Length == 0)
            {
                return null;
            }

            var cmd = new CommandLineParameters();

            for (var i = 0; i < args.Length; i += 2)
            {
                var key = args[i];
                var val = args[i + 1];

                switch (key)
                {
                    case "-benchmark":
                        cmd.Benchmark = Enum.Parse<BenchmarkType>(val, true);
                        break;
                    case "-client":
                        cmd.IsClient = true;
                        cmd.ClientIdentifier = val;
                        break;
                    case "-port":
                        cmd.Port = int.Parse(val);
                        break;
                    case "-threads":
                        cmd.ThreadCount = int.Parse(val);
                        break;
                    case "-requests":
                        cmd.RequestCount = int.Parse(val);
                        break;

                    case "-workers":
                        cmd.WorkerCount = int.Parse(val);
                        break;

                    case "-server":
                        cmd.IsServer = true;
                        cmd.ServerIdentifier = val;
                        break;
                }
            }

            return cmd;
        }
    }

    class Program
    {
        private static readonly EventWaitHandle WaitForAllProcessesToStart = new EventWaitHandle(false, EventResetMode.ManualReset, "KerbBenchmarkRunnerWaitForAllProcesses");

        static async Task Main(string[] args)
        {
            var cmd = CommandLineParameters.Parse(args);

            if (cmd == null)
            {
                BenchmarkRunner.Run(typeof(Program).Assembly);
                return;
            }

            if (cmd.Benchmark == BenchmarkType.Timing)
            {
                BenchmarkRunner.Run<MessageBenchmarks>();
                return;
            }

            if (cmd.Benchmark == BenchmarkType.Stress)
            {
                BenchmarkRunner.Run<StressAsReq>();
                BenchmarkRunner.Run<StressTgsReq>();
                return;
            }

            if (cmd.Benchmark == BenchmarkType.DH)
            {
                BenchmarkRunner.Run<BCryptDiffieHellmanBenchmarks>();
                return;
            }

            Console.WriteLine($"Command Line: {cmd}");

            if (cmd.IsServer)
            {
                await StartServer();

                StartClients(cmd);

                Console.WriteLine("All clients started");
            }

            if (cmd.IsClient)
            {
                BeginClient(cmd);
            }

            var waits = ProcessWaits.Select(p => new ProcessWaitHandle(p)).ToArray();

            if (waits.Length > 0)
            {
                WaitHandle.WaitAll(waits);
            }

            Teardown();
        }

        private class ProcessWaitHandle : WaitHandle
        {
            public ProcessWaitHandle(Process process)
            {
                this.SafeWaitHandle = new SafeWaitHandle(process.Handle, false);
            }
        }

        private static readonly StressAsReq Stresser = new StressAsReq();

        private static void Teardown()
        {
            Stresser.Teardown();
        }

        private static void BeginClient(CommandLineParameters cmd)
        {
            WaitForAllProcessesToStart.WaitOne();

            if (cmd.Port != null)
            {
                Stresser.Port = cmd.Port.Value;
            }

            Stresser.DisplayProgress = true;

            Stresser.ConcurrentRequests = cmd.ThreadCount ?? 1;
            Stresser.AuthenticationAttempts = cmd.RequestCount ?? 1000;

            var sw = Stopwatch.StartNew();

            try
            {
                Stresser.RequestTgt();
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }

            var elapsed = sw.Elapsed;

            sw.Stop();

            Console.WriteLine($"Client completed {Stresser.AuthenticationAttempts} attempts * {Stresser.ConcurrentRequests} requests in {elapsed}");
        }

        private static void StartClients(CommandLineParameters cmd)
        {
            var workerCount = cmd.WorkerCount ?? 1;

            if (workerCount > 64)
            {
                workerCount = 64;
            }

            for (var i = 0; i < workerCount; i++)
            {
                var process = StartClientProcess(new CommandLineParameters
                {
                    ClientIdentifier = Guid.NewGuid().ToString(),
                    ThreadCount = cmd.ThreadCount <= 0 ? Environment.ProcessorCount * 4 : cmd.ThreadCount,
                    Port = cmd.Port,
                    RequestCount = cmd.RequestCount <= 0 ? 1000 : cmd.RequestCount
                });

                ProcessWaits.Add(process);
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));

            WaitForAllProcessesToStart.Set();
        }

        private static readonly HashSet<Process> ProcessWaits = new HashSet<Process>();

        private static Process StartClientProcess(CommandLineParameters cmd)
        {
            cmd.Port = Stresser.Port;

            var assembly = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");

            return Process.Start(new ProcessStartInfo
            {
                FileName = assembly,
                Arguments = cmd.ToString()
            });
        }

        private static async Task StartServer()
        {
            Stresser.Logger = new KerberosDelegateLogger(
                (level, cateogry, id, scopeState, logState, exception, log)
                    =>
                {
                    if (level == TraceLevel.Verbose)
                    {
                        return;
                    }

                    Console.Write($"[{level}] {log} {exception}");
                }
            );

            await Stresser.Setup();
        }
    }
}
