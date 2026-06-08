namespace ClearMic.Core;

public sealed class Profile
{
    public string Name { get; set; } = "Default";
    public bool NoiseFilter { get; set; } = true;
    public bool AecEnabled { get; set; }
    public int InputDevice { get; set; }
    public int OutputDevice { get; set; } = -1;
    public List<string> AutoSwitchProcesses { get; set; } = [];
    public bool IsDefault { get; set; }

    public Profile Clone() => new()
    {
        Name = Name,
        NoiseFilter = NoiseFilter,
        AecEnabled = AecEnabled,
        InputDevice = InputDevice,
        OutputDevice = OutputDevice,
        AutoSwitchProcesses = [.. AutoSwitchProcesses],
        IsDefault = IsDefault,
    };
}
