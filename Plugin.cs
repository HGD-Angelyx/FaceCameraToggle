using System;
using System.Runtime.CompilerServices;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace FaceCameraToggle;

public sealed class Plugin : IDalamudPlugin
{
    public const string CommandName = "/afc";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly IChatGui _chatGui;
    private readonly IFramework _framework;
    private readonly Configuration _configuration;
    private readonly WindowSystem _windowSystem;
    private readonly ConfigWindow _configWindow;

    private bool _active;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IFramework framework)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;
        _chatGui = chatGui;
        _framework = framework;

        _configuration = _pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _active = _configuration.Enabled;

        _windowSystem = new WindowSystem("FaceCameraToggle");
        _configWindow = new ConfigWindow(_configuration, this);
        _windowSystem.AddWindow(_configWindow);

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggles Always Face Camera. Usage: /afc [on|off|toggle|status|eye|head|config]",
            ShowInHelp = true,
        });

        _framework.Update += OnUpdate;
        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
    }

    public string Name => "Face Camera Toggle";

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "":
            case "toggle":
                ToggleActive();
                break;
            case "on":
                SetActive(true);
                break;
            case "off":
                SetActive(false);
                break;
            case "status":
                PrintStatus();
                break;
            case "eye":
                ToggleEyeOnly();
                break;
            case "head":
                SetEyeOnly(false);
                break;
            case "config":
                ToggleConfig();
                break;
            default:
                _chatGui.PrintError($"Unknown argument '{arguments.Trim()}'. Usage: {CommandName} [on|off|toggle|status|eye|head|config]");
                break;
        }
    }

    private void ToggleActive()
    {
        SetActive(!_active);
    }

    private void ToggleConfig()
    {
        _configWindow.Toggle();
    }

    public void UpdateState()
    {
        _active = _configuration.Enabled;
        _pluginInterface.SavePluginConfig(_configuration);

        if (!_active)
            DisableFaceCamera();
    }

    private void SetActive(bool value)
    {
        _active = value;
        _configuration.Enabled = value;
        _pluginInterface.SavePluginConfig(_configuration);

        if (!_active)
            DisableFaceCamera();

        _chatGui.Print(_active ? "Always Face Camera: ON" : "Always Face Camera: OFF");
    }

    private void ToggleEyeOnly()
    {
        SetEyeOnly(!_configuration.EyeOnlyMode);
    }

    private void SetEyeOnly(bool value)
    {
        _configuration.EyeOnlyMode = value;
        _pluginInterface.SavePluginConfig(_configuration);
        _chatGui.Print(value ? "Mode: Eye tracking only" : "Mode: Head tracking");
    }

    private void PrintStatus()
    {
        var mode = _configuration.EyeOnlyMode ? "Eye tracking" : "Head tracking";
        _chatGui.Print(_active ? $"Always Face Camera is ON ({mode})" : "Always Face Camera is OFF");
    }

    private unsafe void OnUpdate(IFramework framework)
    {
        if (!_active)
            return;

        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null || localPlayer->InCombat || localPlayer->GetTargetId() != 0xE0000000)
        {
            DisableFaceCamera();
            return;
        }

        var cameraManager = CameraManager.Instance();
        if (cameraManager == null || cameraManager->Camera == null || cameraManager->ActiveCameraIndex != 0)
        {
            DisableFaceCamera();
            return;
        }

        var player = (Character*)localPlayer;
        var cameraPos = cameraManager->Camera->SceneCamera.Position;

        if (_configuration.EyeOnlyMode)
        {
            EnableEyeCamera(player, cameraPos);
        }
        else
        {
            var playerForwardDirection = new Vector3(MathF.Sin(player->Rotation), 0f, MathF.Cos(player->Rotation));
            var directionToCamera = Vector3.Normalize(cameraPos - player->Position);
            var dot = Vector3.Dot(playerForwardDirection, directionToCamera);

            if (dot <= 0.0f)
            {
                DisableFaceCamera();
                return;
            }

            EnableHeadCamera(player, cameraPos);
        }
    }

    private unsafe void EnableHeadCamera(Character* localPlayer, Vector3 cameraPos)
    {
        localPlayer->LookAt.FaceCameraFlag |= 1;
        localPlayer->LookAt.CameraVector = cameraPos;
    }

    private unsafe void EnableEyeCamera(Character* localPlayer, Vector3 cameraPos)
    {
        localPlayer->LookAt.FaceCameraFlag &= 0xFE;

        fixed (CharacterLookAtControlParam* eyeParam = &localPlayer->LookAt.Controller.Params[2])
        {
            var targetParam = &eyeParam->TargetParam;
            targetParam->Type = CharacterLookAtTargetParam.TargetInfoType.Unk2;
            Unsafe.AsRef<Vector3>(&targetParam->TargetId) = cameraPos;
        }
    }

    private unsafe void DisableFaceCamera()
    {
        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null)
            return;

        var player = (Character*)localPlayer;
        player->LookAt.FaceCameraFlag &= 0xFE;

        fixed (CharacterLookAtControlParam* eyeParam = &player->LookAt.Controller.Params[2])
        {
            var targetParam = &eyeParam->TargetParam;
            targetParam->Type = CharacterLookAtTargetParam.TargetInfoType.None;
            Unsafe.AsRef<Vector3>(&targetParam->TargetId) = default;
        }
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _framework.Update -= OnUpdate;
        _commandManager.RemoveHandler(CommandName);
        _windowSystem.RemoveWindow(_configWindow);
        DisableFaceCamera();
    }
}
