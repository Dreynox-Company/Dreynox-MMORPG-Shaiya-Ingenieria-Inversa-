using System;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class ParityScreenshotCapture : MonoBehaviour
    {
        [SerializeField] private KeyCode captureKey = KeyCode.F8;
        [SerializeField, Min(1)] private int superSize = 1;

        public string LastCapturePath { get; private set; }

        private void Update()
        {
            if (Input.GetKeyDown(captureKey))
                Capture("manual");
        }

        public string Capture(string scenarioName)
        {
            string safeScenario = Sanitize(string.IsNullOrWhiteSpace(scenarioName) ? "capture" : scenarioName);
            string folder = Path.Combine(Application.persistentDataPath, "ParityCaptures");
            Directory.CreateDirectory(folder);
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            LastCapturePath = Path.Combine(folder, safeScenario + "_" + stamp + ".png");
            ScreenCapture.CaptureScreenshot(LastCapturePath, Mathf.Max(1, superSize));
            Debug.Log("Dreynox parity capture: " + LastCapturePath);
            return LastCapturePath;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }
    }
}
