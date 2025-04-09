using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Controls;

public sealed partial class UpdateNotification : UserControl
{
    public event EventHandler<ModuleUpdateActionEventArgs>? UpdateActionSelected;

    public string UpdateMessage
    {
        get => (string)GetValue(UpdateMessageProperty);
        set => SetValue(UpdateMessageProperty, value);
    }

    public static readonly DependencyProperty UpdateMessageProperty =
        DependencyProperty.Register(nameof(UpdateMessage), typeof(string), typeof(UpdateNotification), new PropertyMetadata(string.Empty));

    public IEnumerable<InstallableTrackingModule> AvailableUpdates { get; private set; } = Array.Empty<InstallableTrackingModule>();

    public UpdateNotification()
    {
        InitializeComponent();
    }

    public void SetUpdatesInfo(IEnumerable<InstallableTrackingModule> updates)
    {
        AvailableUpdates = updates;
        UpdateMessage = $"{updates.Count()} module updates are available. Would you like to update now?";
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateActionSelected?.Invoke(this, new ModuleUpdateActionEventArgs(AvailableUpdates, true));
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateActionSelected?.Invoke(this, new ModuleUpdateActionEventArgs(AvailableUpdates, false));
    }
}

public class ModuleUpdateActionEventArgs : EventArgs
{
    public IEnumerable<InstallableTrackingModule> Updates
    {
        get;
    }
    public bool InstallUpdates
    {
        get;
    }

    public ModuleUpdateActionEventArgs(IEnumerable<InstallableTrackingModule> updates, bool installUpdates)
    {
        Updates = updates;
        InstallUpdates = installUpdates;
    }
}
