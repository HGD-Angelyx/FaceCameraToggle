using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FaceCameraToggle;

public sealed class ConfigWindow : Window
{
    private readonly Configuration _configuration;
    private readonly Plugin _plugin;

    public ConfigWindow(Configuration configuration, Plugin plugin) : base("Face Camera Toggle")
    {
        _configuration = configuration;
        _plugin = plugin;
        Size = new Vector2(232, 120);
        SizeCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var enabled = _configuration.Enabled;
        if (ImGui.Checkbox("Enable", ref enabled))
        {
            _configuration.Enabled = enabled;
            _plugin.UpdateState();
        }

        var eyeOnly = _configuration.EyeOnlyMode;
        if (ImGui.Checkbox("Eye-only tracking", ref eyeOnly))
        {
            _configuration.EyeOnlyMode = eyeOnly;
            _plugin.UpdateState();
        }

        ImGui.Text("Commands:");
        ImGui.Text("/afc toggle - on/off");
        ImGui.Text("/afc eye - eye-only mode");
        ImGui.Text("/afc head - head tracking");
    }
}
