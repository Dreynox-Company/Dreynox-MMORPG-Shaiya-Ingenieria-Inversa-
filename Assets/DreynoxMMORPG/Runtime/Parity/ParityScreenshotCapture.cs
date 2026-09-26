using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class ParityScreenshotCapture : MonoBehaviour
    {
        private const int NativeReferenceWidth = 1024;
        private const int NativeReferenceHeight = 768;

        [SerializeField] private KeyCode captureKey = KeyCode.F8;
        [SerializeField, Min(1)] private int superSize = 1;

        public string LastCapturePath { get; private set; }

        private void Start()
        {
            CommandLineCaptureRequest request =
                ParseCommandLine(
                    Environment.GetCommandLineArgs());

            if (request == null)
                return;

            StartCoroutine(
                CaptureFromCommandLine(
                    request));
        }

        private void Update()
        {
            if (Input.GetKeyDown(captureKey))
                Capture("manual");
        }

        public string Capture(string scenarioName)
        {
            string safeScenario =
                Sanitize(
                    string.IsNullOrWhiteSpace(
                        scenarioName)
                        ? "capture"
                        : scenarioName);

            string folder =
                Path.Combine(
                    Application.persistentDataPath,
                    "ParityCaptures");

            Directory.CreateDirectory(folder);

            string stamp =
                DateTime.UtcNow.ToString(
                    "yyyyMMddTHHmmssfffZ",
                    CultureInfo.InvariantCulture);

            LastCapturePath =
                Path.Combine(
                    folder,
                    safeScenario +
                    "_" +
                    stamp +
                    ".png");

            ScreenCapture.CaptureScreenshot(
                LastCapturePath,
                Mathf.Max(1, superSize));

            Debug.Log(
                "Dreynox parity capture: " +
                LastCapturePath);

            return LastCapturePath;
        }

        private IEnumerator CaptureFromCommandLine(
            CommandLineCaptureRequest request)
        {
            Screen.SetResolution(
                request.Width,
                request.Height,
                false);

            int settleFrames =
                Mathf.Max(
                    2,
                    request.SettleFrames);

            for (int i = 0;
                 i < settleFrames;
                 i++)
            {
                yield return null;
            }

            if (request.DelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    request.DelaySeconds);
            }

            yield return new WaitForEndOfFrame();

            string output =
                request.OutputPath;

            if (string.IsNullOrWhiteSpace(
                    output))
            {
                string directory =
                    Path.Combine(
                        Application.persistentDataPath,
                        "ParityCaptures");

                Directory.CreateDirectory(
                    directory);

                output =
                    Path.Combine(
                        directory,
                        Sanitize(
                            request.ScenarioId) +
                        ".png");
            }
            else
            {
                output =
                    Path.GetFullPath(
                        output);

                string directory =
                    Path.GetDirectoryName(
                        output);

                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }
            }

            Texture2D texture =
                new Texture2D(
                    Screen.width,
                    Screen.height,
                    TextureFormat.RGB24,
                    false);

            try
            {
                texture.ReadPixels(
                    new Rect(
                        0f,
                        0f,
                        Screen.width,
                        Screen.height),
                    0,
                    0,
                    false);

                texture.Apply(
                    false,
                    false);

                File.WriteAllBytes(
                    output,
                    texture.EncodeToPNG());

                LastCapturePath =
                    output;

                Debug.Log(
                    "Dreynox parity auto-capture '" +
                    request.ScenarioId +
                    "': " +
                    output +
                    " (" +
                    Screen.width +
                    "x" +
                    Screen.height +
                    ")");
            }
            finally
            {
                Destroy(texture);
            }

            if (request.ExitAfterCapture)
            {
                yield return null;
                Application.Quit(0);
            }
        }

        private static CommandLineCaptureRequest ParseCommandLine(
            string[] args)
        {
            if (args == null ||
                args.Length == 0)
                return null;

            string scenario =
                ValueAfter(
                    args,
                    "--parity-capture");

            if (string.IsNullOrWhiteSpace(
                    scenario))
                return null;

            var request =
                new CommandLineCaptureRequest
                {
                    ScenarioId =
                        scenario.Trim(),
                    OutputPath =
                        ValueAfter(
                            args,
                            "--parity-output"),
                    Width =
                        ParseInt(
                            ValueAfter(
                                args,
                                "--parity-width"),
                            NativeReferenceWidth),
                    Height =
                        ParseInt(
                            ValueAfter(
                                args,
                                "--parity-height"),
                            NativeReferenceHeight),
                    SettleFrames =
                        ParseInt(
                            ValueAfter(
                                args,
                                "--parity-settle-frames"),
                            5),
                    DelaySeconds =
                        ParseFloat(
                            ValueAfter(
                                args,
                                "--parity-delay"),
                            0.25f),
                    ExitAfterCapture =
                        HasFlag(
                            args,
                            "--parity-exit")
                };

            request.Width =
                Mathf.Max(
                    64,
                    request.Width);

            request.Height =
                Mathf.Max(
                    64,
                    request.Height);

            request.SettleFrames =
                Mathf.Max(
                    0,
                    request.SettleFrames);

            request.DelaySeconds =
                Mathf.Max(
                    0f,
                    request.DelaySeconds);

            return request;
        }

        private static string ValueAfter(
            string[] args,
            string key)
        {
            for (int i = 0;
                 i < args.Length - 1;
                 i++)
            {
                if (string.Equals(
                        args[i],
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static bool HasFlag(
            string[] args,
            string key)
        {
            for (int i = 0;
                 i < args.Length;
                 i++)
            {
                if (string.Equals(
                        args[i],
                        key,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static int ParseInt(
            string value,
            int fallback)
        {
            int parsed;

            return int.TryParse(
                       value,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out parsed)
                ? parsed
                : fallback;
        }

        private static float ParseFloat(
            string value,
            float fallback)
        {
            float parsed;

            return float.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out parsed)
                ? parsed
                : fallback;
        }

        private static string Sanitize(
            string value)
        {
            foreach (char invalid in
                     Path.GetInvalidFileNameChars())
            {
                value =
                    value.Replace(
                        invalid,
                        '_');
            }

            return value.Replace(
                ' ',
                '_');
        }

        private sealed class CommandLineCaptureRequest
        {
            public string ScenarioId =
                string.Empty;

            public string OutputPath =
                string.Empty;

            public int Width =
                NativeReferenceWidth;

            public int Height =
                NativeReferenceHeight;

            public int SettleFrames = 5;
            public float DelaySeconds = 0.25f;
            public bool ExitAfterCapture;
        }
    }
}
