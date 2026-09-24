using System;
using System.Collections;
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
    /// Use --parity-local-flow-smoke to run an automated end-to-end
    /// Login -> Select -> Make -> Select -> World QA pass.
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

        private bool _smokeRunning;
        private bool _smokeFailed;

        public static bool IsActive =>
            _instance != null;

        public ClientFlowCore Flow =>
            _flow;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            string[] args =
                Environment.GetCommandLineArgs();

            if (!HasFlag(
                    args,
                    "--parity-local-flow") &&
                !HasFlag(
                    args,
                    "--parity-local-flow-smoke"))
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

            if (HasFlag(
                    Environment.GetCommandLineArgs(),
                    "--parity-local-flow-smoke"))
            {
                StartCoroutine(
                    RunLocalFlowSmoke());
            }
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

        private IEnumerator RunLocalFlowSmoke()
        {
            if (_smokeRunning)
                yield break;

            _smokeRunning = true;
            _smokeFailed = false;

            Debug.Log(
                "DREYNOX_LOCAL_FLOW_SMOKE_BEGIN");

            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    LoginScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                LoadScene(
                    LoginScene);
            }

            yield return WaitForSmokeCondition(
                () =>
                    string.Equals(
                        SceneManager.GetActiveScene().name,
                        LoginScene,
                        StringComparison.OrdinalIgnoreCase) &&
                    _login != null,
                "Login scene/controller did not become ready.");

            if (_smokeFailed)
                yield break;

            OnLoginRequested(
                "local_parity",
                "local_parity",
                false);

            yield return WaitForSmokeCondition(
                () =>
                    _flow.State ==
                        ClientFlowState.CharacterSelect &&
                    _select != null,
                "Login -> CharacterSelect transition failed.");

            if (_smokeFailed)
                yield break;

            _select.SelectSlot(1);
            _select.RequestCreate();

            yield return WaitForSmokeCondition(
                () =>
                    _flow.State ==
                        ClientFlowState.CharacterCreate &&
                    _make != null,
                "CharacterSelect -> CharacterMake transition failed.");

            if (_smokeFailed)
                yield break;

            int slot =
                _flow.CharacterCreateSlot ??
                1;

            OnCreateRequested(
                new CharacterCreationRequestCore(
                    "SmokeHero",
                    slot,
                    (int)LegacyCharacterFamily.Human,
                    (int)LegacyCharacterJob.Fighter,
                    0,
                    1,
                    1,
                    CharacterDifficultyMode.Basic));

            yield return WaitForSmokeCondition(
                () =>
                    _flow.State ==
                        ClientFlowState.CharacterSelect &&
                    _select != null &&
                    FindCharacterBySlot(
                        slot) != null,
                "Character creation did not return to CharacterSelect.");

            if (_smokeFailed)
                yield break;

            CharacterSummaryCore created =
                FindCharacterBySlot(
                    slot);

            _select.SelectSlot(
                created.Slot);

            _select.RequestPrimaryAction();

            yield return WaitForSmokeCondition(
                () =>
                    string.Equals(
                        SceneManager.GetActiveScene().name,
                        WorldScene,
                        StringComparison.OrdinalIgnoreCase) &&
                    _flow.State ==
                        ClientFlowState.InWorld,
                "CharacterSelect -> World transition failed.");

            if (_smokeFailed)
                yield break;

            Debug.Log(
                "DREYNOX_LOCAL_FLOW_SMOKE_OK " +
                "character=" +
                created.CharacterId +
                " slot=" +
                created.Slot +
                " state=" +
                _flow.State);

            yield return null;

            Application.Quit(0);
        }

        private IEnumerator WaitForSmokeCondition(
            Func<bool> condition,
            string failureMessage,
            float timeoutSeconds = 30f)
        {
            float started =
                Time.realtimeSinceStartup;

            while (!condition())
            {
                if (Time.realtimeSinceStartup -
                    started >=
                    timeoutSeconds)
                {
                    FailSmoke(
                        failureMessage);

                    yield break;
                }

                yield return null;
            }
        }

        private void FailSmoke(
            string message)
        {
            if (_smokeFailed)
                return;

            _smokeFailed = true;

            Debug.LogError(
                "DREYNOX_LOCAL_FLOW_SMOKE_FAIL: " +
                message +
                " · scene=" +
                SceneManager.GetActiveScene().name +
                " · state=" +
                _flow.State);

            Application.Quit(2);
        }

        private CharacterSummaryCore FindCharacterBySlot(
            int slot)
        {
            for (int i = 0;
                 i < _flow.Characters.Count;
                 i++)
            {
                if (_flow.Characters[i].Slot ==
                    slot)
                {
                    return _flow.Characters[i];
                }
            }

            return null;
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
