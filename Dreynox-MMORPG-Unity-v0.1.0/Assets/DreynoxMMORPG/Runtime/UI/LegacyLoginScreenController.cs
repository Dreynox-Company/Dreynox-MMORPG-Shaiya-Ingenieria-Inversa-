using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public sealed class LegacyLoginScreenController : MonoBehaviour
    {
        [SerializeField] private InputField accountInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Toggle saveIdToggle;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Text statusText;

        public event Action<string, string, bool> LoginRequested;
        public event Action ExitRequested;

        private void Awake()
        {
            if (loginButton != null)
                loginButton.onClick.AddListener(SubmitLogin);
            if (exitButton != null)
                exitButton.onClick.AddListener(RequestExit);

            if (passwordInput != null)
                passwordInput.contentType = InputField.ContentType.Password;
        }

        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(SubmitLogin);
            if (exitButton != null)
                exitButton.onClick.RemoveListener(RequestExit);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter))
                SubmitLogin();

            if (Input.GetKeyDown(KeyCode.Escape))
                RequestExit();
        }

        public void Bind(
            InputField account,
            InputField password,
            Toggle saveId,
            Button login,
            Button exit,
            Text status)
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(SubmitLogin);
            if (exitButton != null)
                exitButton.onClick.RemoveListener(RequestExit);

            accountInput = account;
            passwordInput = password;
            saveIdToggle = saveId;
            loginButton = login;
            exitButton = exit;
            statusText = status;

            if (passwordInput != null)
                passwordInput.contentType = InputField.ContentType.Password;

            if (loginButton != null)
                loginButton.onClick.AddListener(SubmitLogin);
            if (exitButton != null)
                exitButton.onClick.AddListener(RequestExit);
        }

        public void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message ?? string.Empty;
        }

        public void SubmitLogin()
        {
            string account =
                accountInput != null ? accountInput.text.Trim() : string.Empty;
            string password =
                passwordInput != null ? passwordInput.text : string.Empty;

            if (account.Length == 0 || password.Length == 0)
            {
                SetStatus("Enter account and password.");
                return;
            }

            LoginRequested?.Invoke(
                account,
                password,
                saveIdToggle != null && saveIdToggle.isOn);
        }

        public void RequestExit()
        {
            ExitRequested?.Invoke();
        }
    }
}
