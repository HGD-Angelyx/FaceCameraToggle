using System;
using System.Runtime.CompilerServices;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Common.Math;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.CharacterLookAtTargetParam;

namespace FaceCameraToggle;

public sealed class Plugin : IDalamudPlugin
{
    public const string CommandName = "/afc";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly IChatGui _chatGui;
    private readonly IFramework _framework;
    private readonly Configuration _configuration;

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

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggles Always Face Camera. Usage: /afc [on|off|toggle|status|eye|head]",
            ShowInHelp = true,
        });

        _framework.Update += OnUpdate;
        _pluginInterface.UiBuilder.OpenConfigUi += ToggleActive;
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
            default:
                _chatGui.PrintError($"Unknown argument '{arguments.Trim()}'. Usage: {CommandName} [on|off|toggle|status|eye|head]");
                break;
        }
    }

    private void ToggleActive()
    {
        SetActive(!_active);
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

        var cameraPos = cameraManager->Camera->SceneCamera.Position;

        if (_configuration.EyeOnlyMode)
        {
            localPlayer->LookAt.FaceCameraFlag |= 1;
            localPlayer->LookAt.CameraVector = cameraPos;

            ref var headParam = ref localPlayer->LookAt.Controller.Params[1];
            headParam.TargetParam.Type = TargetInfoType.None;

            ref var eyeParam = ref localPlayer->LookAt.Controller.Params[2];
            eyeParam.TargetParam.Type = TargetInfoType.Unk2;
            var eyeTargetAddr = (byte*)Unsafe.AsPointer(ref eyeParam.TargetParam);
            *(Vector3*)(eyeTargetAddr + 0x10) = cameraPos;
        }
        else
        {
            var playerForwardDirection = new Vector3(MathF.Sin(localPlayer->Rotation), 0f, MathF.Cos(localPlayer->Rotation));
            var directionToCamera = Vector3.Normalize(cameraPos - localPlayer->Position);
            var dot = Vector3.Dot(playerForwardDirection, directionToCamera);

            if (dot <= 0.0f)
            {
                DisableFaceCamera();
                return;
            }

            localPlayer->LookAt.FaceCameraFlag |= 1;
            localPlayer->LookAt.CameraVector = cameraPos;
        }
    }

    private unsafe void DisableFaceCamera()
    {
        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null)
            return;

        localPlayer->LookAt.FaceCameraFlag &= 0xFE;
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleActive;
        _framework.Update -= OnUpdate;
        _commandManager.RemoveHandler(CommandName);
        DisableFaceCamera();
    }
}
