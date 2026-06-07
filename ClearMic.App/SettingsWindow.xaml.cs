using System.Windows;
using ClearMic.Core;

namespace ClearMic.App;

public partial class SettingsWindow : Window
{
    private readonly PipelineService _pipeline;

    public SettingsWindow(PipelineService pipeline)
    {
        InitializeComponent();
        _pipeline = pipeline;

        var inputs = AudioCapture.EnumerateDevices();
        for (int i = 0; i < inputs.Length; i++)
            InputDeviceCombo.Items.Add(inputs[i]);
        InputDeviceCombo.SelectedIndex = _pipeline.InputDeviceIndex < inputs.Length
            ? _pipeline.InputDeviceIndex : 0;

        var outputs = AudioOutput.EnumerateDevices();
        for (int i = 0; i < outputs.Length; i++)
            OutputDeviceCombo.Items.Add(outputs[i]);
        var outIdx = _pipeline.OutputDeviceIndex >= 0 ? _pipeline.OutputDeviceIndex : 0;
        OutputDeviceCombo.SelectedIndex = outIdx < outputs.Length ? outIdx : 0;

        NoiseFilterCheck.IsChecked = true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (InputDeviceCombo.SelectedIndex >= 0)
            _pipeline.SetInputDevice(InputDeviceCombo.SelectedIndex);
        if (OutputDeviceCombo.SelectedIndex >= 0)
            _pipeline.SetOutputDevice(OutputDeviceCombo.SelectedIndex);
        _pipeline.SaveSettings();
        DialogResult = true;
        Close();
    }
}
