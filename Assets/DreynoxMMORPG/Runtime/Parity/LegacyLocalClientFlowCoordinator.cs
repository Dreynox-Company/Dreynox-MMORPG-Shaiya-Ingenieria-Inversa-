using System;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Parity
{
    /// <summary>
    /// Explicit local-only scene-flow adapter for end-to-end client parity QA.
    /// It never claims to be the recovered ps0032 network protocol.
    /// Activate with --parity-local-flow.
    /// </summary>
    public sealed class LegacyLocalClientFlowCoordinator : MonoBehaviour
    {
        private const string LoginScene = "LegacyLoginParity";
        private const string SelectScene = "LegacyCharacterSelectParity";
        private const string MakeScene = "LegacyCharacterMakeParity";
        private const string WorldScene = "CanonicalMap000World";

        private static LegacyLocalClientFlowCoordinator _instance;

        private readonly ClientFlowCore _flow =
            new ClientFlowCore();

        private long _nextCharacterId = 2000;

        private LegacyLoginScreenController _login;
        private LegacyCharacterSelectScreenController _select;
        private LegacyCharacterMakeScreenController _make;

        public static bool IsActive =>
            _instance != null;

        public ClientFlowCore Flow =>
            _flow;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (!HasFlag(
                    Environment.GetCommandLineArgs(),
                    "--parity-local-flow"))
                return;

            EnsureInstalled();
        }

        public static LegacyLocalClientFlowCoordinator EnsureInstalled()
        {
            if (_instance != null)
                return _instance;

            GameObject go =
                new GameObject(
                    "DreynoxLocalClientFlow");

            DontDestroyOnLoad(go);

            _instance =
                go.AddComponent<
                    LegacyLocalClientFlowCoordinator>();

            return _instance;
        }

        private void Awake()
        {
            if (_instance != null &&
                _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded +=
                OnSceneLoaded;
        }

        private void Start()
        {
            BindCurrentScene(
                SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            UnbindControllers();
            _instance = null;
        }

        private void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            BindCurrentScene(scene);
        }

        private void BindCurrentScene(
            Scene scene)
        {
            UnbindControllers();

            string sceneName =
                scene.name;

            BootstrapStateForScene(
                sceneName);

            if (string.Equals(
                    sceneName,
                    LoginScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                _login =
                    FindFirstObjectByType<
                        LegacyLoginScreenController>();

                if (_login != null)
                {
                    _login.LoginRequested +=
                        OnLoginRequested;

                    _login.ExitRequested +=
                        OnExitRequested;

                    _login.SetStatus(
                        "Local parity flow ready.");
                }

                return;
            }

            if (string.Equals(
                    sceneName,
                    SelectScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                _select =
                    FindFirstObjectByType<
                        LegacyCharacterSelectScreenController>();

                if (_select != null)
                {
                    _select.EmptySlotRequested +=
                        OnEmptySlotRequested;

                    _select.EnterRequested +=
                        OnEnterRequested;

                    _select.DeleteRequested +=
                        OnDeleteRequested;

                    _select.SetCharacters(
                        _flow.Characters);

                    if (_flow.Characters.Count > 0)
                    {
                        _select.SelectSlot(
                            _flow.Characters[0].Slot);
                    }
                }

                return;
            }

            if (string.Equals(
                    sceneName,
                    MakeScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                _make =
                    FindFirstObjectByType<
                        LegacyCharacterMakeScreenController>();

                if (_make != null)
                {
                    _make.CreateRequested +=
                        OnCreateRequested;

                    _make.CancelRequested +=
                        OnCreateCancelled;

                    int slot =
                        _flow.CharacterCreateSlot ??
                        0;

                    _make.Configure(
                        slot,
                        0,
                        0,
                        0,
                        0,
                        0,
                        CharacterDifficultyMode.Basic);
                }

                return;
            }

            if (string.Equals(
                    sceneName,
                    WorldScene,
                    StringComparison.OrdinalIgnoreCase) &&
                _flow.State ==
                    ClientFlowState.EnteringWorld)
            {
                _flow.WorldAccepted();
            }
        }

        private void BootstrapStateForScene(
            string sceneName)
        {
            if (_flow.State !=
                ClientFlowState.Boot)
                return;

            _flow.ReadyForLogin();

            if (string.Equals(
                    sceneName,
                    LoginScene,
                    StringComparison.OrdinalIgnoreCase))
                return;

            BootstrapAuthenticatedSession();

            if (string.Equals(
                    sceneName,
                    MakeScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                _flow.BeginCharacterCreate(
                    FirstEmptySlot());
            }
            else if (string.Equals(
                         sceneName,
                         WorldScene,
                         StringComparison.OrdinalIgnoreCase))
            {
                EnsureDefaultCharacter();

                long characterId =
                    _flow.Characters[0]
                        .CharacterId;

                _flow.EnterCharacter(
                    characterId);
            }
        }

        private void BootstrapAuthenticatedSession()
        {
            if (_flow.State ==
                ClientFlowState.Login)
            {
                _flow.BeginConnect();
                _flow.LoginAccepted();
                _flow.SelectServer(1);
                EnsureDefaultCharacter();
            }
        }

        private void EnsureDefaultCharacter()
        {
            if (_flow.State !=
                ClientFlowState.CharacterSelect ||
                _flow.Characters.Count > 0)
                return;

            _flow.SetCharacterList(
                new[]
                {
                    new CharacterSummaryCore(
                        1001,
                        "DreynoxLocal",
                        1,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        CharacterDifficultyMode.Basic)
                });
        }

        private void OnLoginRequested(
            string account,
            string password,
            bool saveId)
        {
            if (_flow.State ==
                ClientFlowState.Boot)
                _flow.ReadyForLogin();

            if (!_flow.BeginConnect())
            {
                _login?.SetStatus(
                    "Local flow is not ready to connect.");
                return;
            }

            if (!_flow.LoginAccepted() ||
                !_flow.SelectServer(1))
            {
                _login?.SetStatus(
                    "Local flow could not enter character select.");
                return;
            }

            EnsureDefaultCharacter();

            LoadScene(
                SelectScene);
        }

        private void OnExitRequested()
        {
            Application.Quit(0);
        }

        private void OnEmptySlotRequested(
            int slot)
        {
            if (!_flow.BeginCharacterCreate(
                    slot))
            {
                _select?.SetStatus(
                    "Could not open character creation slot.");
                return;
            }

            LoadScene(
                MakeScene);
        }

        private void OnEnterRequested(
            long characterId)
        {
            if (!_flow.EnterCharacter(
                    characterId))
            {
                _select?.SetStatus(
                    "Could not enter selected character.");
                return;
            }

            LoadScene(
                WorldScene);
        }

        private void OnDeleteRequested(
            long characterId)
        {
            if (!_flow.CharacterDeleted(
                    characterId))
            {
                _select?.SetStatus(
                    "Could not delete selected character.");
                return;
            }

            _select?.SetCharacters(
                _flow.Characters);
        }

        private void OnCreateRequested(
            CharacterCreationRequestCore request)
        {
            if (!_flow.CharacterCreateSlot.HasValue ||
                request.Slot !=
                    _flow.CharacterCreateSlot.Value)
            {
                _make?.SetStatus(
                    "Character slot does not match local flow.");
                return;
            }

            CharacterSummaryCore created =
                new CharacterSummaryCore(
                    _nextCharacterId++,
                    request.Name,
                    1,
                    request.Slot,
                    request.Family,
                    request.Job,
                    request.Sex,
                    request.Face,
                    request.Hair,
                    0,
                    request.Mode);

            if (!_flow.CharacterCreated(
                    created))
            {
                _make?.SetStatus(
                    "Local character creation failed.");
                return;
            }

            LoadScene(
                SelectScene);
        }

        private void OnCreateCancelled()
        {
            if (_flow.CancelCharacterCreate())
            {
                LoadScene(
                    SelectScene);
            }
        }

        private int FirstEmptySlot()
        {
            for (int slot = 0;
                 slot < 5;
                 slot++)
            {
                bool used = false;

                for (int i = 0;
                     i < _flow.Characters.Count;
                     i++)
                {
                    if (_flow.Characters[i].Slot ==
                        slot)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                    return slot;
            }

            return 0;
        }

        private void UnbindControllers()
        {
            if (_login != null)
            {
                _login.LoginRequested -=
                    OnLoginRequested;

                _login.ExitRequested -=
                    OnExitRequested;

                _login = null;
            }

            if (_select != null)
            {
                _select.EmptySlotRequested -=
                    OnEmptySlotRequested;

                _select.EnterRequested -=
                    OnEnterRequested;

                _select.DeleteRequested -=
                    OnDeleteRequested;

                _select = null;
            }

            if (_make != null)
            {
                _make.CreateRequested -=
                    OnCreateRequested;

                _make.CancelRequested -=
                    OnCreateCancelled;

                _make = null;
            }
        }

        private static void LoadScene(
            string sceneName)
        {
            SceneManager.LoadScene(
                sceneName,
                LoadSceneMode.Single);
        }

        private static bool HasFlag(
            string[] args,
            string flag)
        {
            if (args == null)
                return false;

            for (int i = 0;
                 i < args.Length;
                 i++)
            {
                if (string.Equals(
                        args[i],
                        flag,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
