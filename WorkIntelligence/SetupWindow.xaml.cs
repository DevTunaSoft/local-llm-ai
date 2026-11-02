using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using WorkIntelligence.Services;

namespace WorkIntelligence;

public partial class SetupWindow : Window
{
    private CancellationTokenSource _cancellation=new();
    private bool _saving;
    public string? RestartDataRoot {get;private set;}
    public SetupWindow()
    {
        InitializeComponent();
        WorkspacePreferences preferences;
        try{preferences=WorkspacePreferencesStore.Load();}
        catch(Exception ex) when(ex is InvalidDataException or System.Text.Json.JsonException or IOException)
        {preferences=new();SetupStatus.Text="Existing workspace preferences need repair. Choose a different data folder or review settings before finishing.";}
        SetupDataFolder.Text=LocalPaths.Root;
        SetupModelsFolder.Text=preferences.ModelDownloadFolder ?? LocalPaths.Models;
        SetupBackupFolder.Text=preferences.BackupRepository ?? "";
    }
    private static string? Folder(string title)
    {
        var picker=new OpenFolderDialog{Title=title};
        return picker.ShowDialog()==true?picker.FolderName:null;
    }
    private void ChooseData_Click(object sender,RoutedEventArgs e)
    {
        var selected=Folder("Choose dedicated local application data folder");
        if(selected is not null)SetupDataFolder.Text=selected;
    }
    private void ChooseModels_Click(object sender,RoutedEventArgs e){var folder=Folder("Choose external model download folder");if(folder is not null)SetupModelsFolder.Text=folder;}
    private void ChooseKnowledge_Click(object sender,RoutedEventArgs e){var folder=Folder("Choose organization-approved source folder");if(folder is not null)SetupKnowledgeFolder.Text=folder;}
    private void ChooseBackup_Click(object sender,RoutedEventArgs e){var folder=Folder("Choose approved plaintext backup folder");if(folder is not null)SetupBackupFolder.Text=folder;}
    private void ChooseModel_Click(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Title="Choose trusted local GGUF",Filter="GGUF model|*.gguf"};
        if(picker.ShowDialog()==true)SetupModelFile.Text=picker.FileName;
    }
    private void Back_Click(object sender,RoutedEventArgs e)=>SetupSteps.SelectedIndex=Math.Max(0,SetupSteps.SelectedIndex-1);
    private void Next_Click(object sender,RoutedEventArgs e)=>SetupSteps.SelectedIndex=Math.Min(4,SetupSteps.SelectedIndex+1);
    private void Cancel_Click(object sender,RoutedEventArgs e)
    {
        if(_saving){_cancellation.Cancel();SetupStatus.Text="Cancelling setup; waiting for native/index operations to finish...";}
        else DialogResult=false;
    }
    private async void Finish_Click(object sender,RoutedEventArgs e)
    {
        if(_saving)return;
        if(SetupAuthorization.IsChecked!=true){SetupSteps.SelectedIndex=4;SetupStatus.Text="Confirm organizational authorization before finishing setup.";return;}
        if(_cancellation.IsCancellationRequested){_cancellation.Dispose();_cancellation=new();}
        _saving=true;SetupFinish.IsEnabled=false;SetupSteps.IsEnabled=false;SetupBack.IsEnabled=false;SetupNext.IsEnabled=false;
        try
        {
            var root=FirstRunSetup.ValidateDataLocation(SetupDataFolder.Text.Trim());
            var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
            if(!root.Equals(Path.TrimEndingDirectorySeparator(LocalPaths.Root),comparison))
            {
                StartupSelection.Save(root);RestartDataRoot=root;DialogResult=true;return;
            }
            SetupStatus.Text="Validating model and importing approved sources. This may take time...";
            var choices=new SetupChoices(SetupModelsFolder.Text.Trim(),SetupModelFile.Text.Trim(),
                SetupKnowledgeFolder.Text.Trim(),SetupBackupFolder.Text.Trim(),true);
            var result=await FirstRunSetup.ApplyAsync(choices,_cancellation.Token);
            SetupStatus.Text=result.Summary;DialogResult=true;
        }
        catch(OperationCanceledException){SetupStatus.Text="Setup cancelled. Any successfully imported source copies were preserved.";}
        catch(Exception ex){AuditLog.Error("FirstRunSetup",ex);SetupStatus.Text=ex.Message;}
        finally{_saving=false;SetupFinish.IsEnabled=true;SetupSteps.IsEnabled=true;SetupBack.IsEnabled=true;SetupNext.IsEnabled=true;}
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if(_saving && DialogResult is null){e.Cancel=true;_cancellation.Cancel();}
        base.OnClosing(e);
    }
}
