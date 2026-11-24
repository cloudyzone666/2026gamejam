using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CardCards.Validation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardCards.Editor
{
    /// <summary>Batch entry points. RunPlayMode owns process exit; do not pass -quit.</summary>
    [InitializeOnLoad]
    public static class BattleVerification
    {
        private const string RunningKey = "CardCards.BattleVerification.Running";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string DefaultReportPath = "Logs/BookUI-PlayModeVerification.json";
        private static string ReportPath => Argument("-battleVerificationReport", DefaultReportPath);
        private static BattleVerificationReport report;
        private static Stack<IEnumerator> stack;
        private static int previousFrame = -1;
        private static double deadline;

        static BattleVerification()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            if (SessionState.GetBool(RunningKey, false))
                EditorApplication.delayCall += ResumeAfterReload;
        }

        [MenuItem("Card Cards/Verification/Run Play Mode Verification")]
        public static void RunPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start verification from Edit Mode.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void ResumeAfterReload()
        {
            if (SessionState.GetBool(RunningKey, false) && EditorApplication.isPlaying) BeginSuite();
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) BeginSuite();
            else if (change == PlayModeStateChange.ExitingPlayMode && report != null)
                Finish(new InvalidOperationException("Play Mode exited before verification finished."));
        }

        private static void BeginSuite()
        {
            if (stack != null) return;
            report = new BattleVerificationReport { source = "Unity Editor real Play Mode" };
            stack = new Stack<IEnumerator>();
            stack.Push(BattleVerificationSuite.Run(report, Path.GetFullPath(ReportPath)));
            deadline = EditorApplication.timeSinceStartup + 240d;
            previousFrame = -1;
            Application.logMessageReceived += ReceiveLog;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(new TimeoutException("Play Mode verification exceeded 240 seconds."));
                return;
            }
            // This must advance real game frames: Editor updates alone do not run coroutines.
            if (!EditorApplication.isPlaying || previousFrame == Time.frameCount) return;
            previousFrame = Time.frameCount;
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator iterator = stack.Peek();
                    if (!iterator.MoveNext()) { stack.Pop(); continue; }
                    if (iterator.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void ReceiveLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.runtimeErrors.Add(message + "\n" + trace);
        }

        private static void Finish(Exception failure)
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Update;
            Application.logMessageReceived -= ReceiveLog;
            if (report == null) report = new BattleVerificationReport { source = "Unity Editor real Play Mode" };
            if (failure != null) report.failure = failure.ToString();
            report.success = failure == null && report.runtimeErrors.Count == 0;
            BattleVerificationSuite.WriteReport(report, ReportPath);
            if (stack != null)
                while (stack.Count > 0)
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception exception) { Debug.LogException(exception); }
            stack = null;
            if (Application.isBatchMode) EditorApplication.Exit(report.success ? 0 : 1);
            else
            {
                EditorApplication.ExitPlaymode();
                Debug.Log("Battle verification " + (report.success ? "passed" : "failed") + ": " + Path.GetFullPath(ReportPath));
            }
        }

        [MenuItem("Card Cards/Verification/Build Windows Development Player")]
        public static void BuildWindows()
        {
            string output = Argument("-battleBuildOutput", "Builds/BookUI/Windows/CardCards.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            var result = new WindowsBuildReport
            {
                success = build.summary.result == BuildResult.Succeeded,
                result = build.summary.result.ToString(),
                output = Path.GetFullPath(output),
                errors = (int)build.summary.totalErrors,
                warnings = (int)build.summary.totalWarnings,
                sizeBytes = build.summary.totalSize.ToString(),
                durationSeconds = build.summary.totalTime.TotalSeconds,
                completedUtc = DateTime.UtcNow.ToString("O")
            };
            foreach (BuildStep step in build.steps)
                foreach (BuildStepMessage message in step.messages)
                    if (message.type == LogType.Warning || message.type == LogType.Error || message.type == LogType.Exception)
                        result.messages.Add(step.name + ": " + message.content);
            string buildReportPath = Argument("-battleBuildReport", "Logs/BookUI-WindowsBuild.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(buildReportPath)));
            File.WriteAllText(buildReportPath, JsonUtility.ToJson(result, true));
            if (!result.success) throw new InvalidOperationException("Windows build failed: " + result.result);
        }

        private static string Argument(string name, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
                if (arguments[index] == name) return arguments[index + 1];
            return fallback;
        }

        [Serializable]
        private sealed class WindowsBuildReport
        {
            public bool success;
            public string result;
            public string output;
            public int errors;
            public int warnings;
            public List<string> messages = new List<string>();
            public string sizeBytes;
            public double durationSeconds;
            public string completedUtc;
        }
    }
}
