using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SCSKiller.Core;

namespace SCSKiller.App.Pages;

/// <summary>A game added by hand: the exe the recorder goes next to, and the game folder the user confirms (or changes),
/// which SCSKiller checks whole for anti-cheat before it records.</summary>
static class GameFolderDialog
{
    /// <summary>The folder confirmed; null = cancelled.</summary>
    public static async Task<string?> ShowAsync(XamlRoot root, Game game, string title, string primary)
    {
        var folder = game.InstallDir;
        var folderText = new TextBlock { Text = folder, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var problem = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false };
        var change = new Button { Content = "更改…", VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        row.Children.Add(folderText);
        Grid.SetColumn(change, 1);
        row.Children.Add(change);
        TextBlock Label(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], Margin = new Thickness(0, 8, 0, 0) };
        var dialog = new ContentDialog
        {
            XamlRoot = root, Title = title, PrimaryButtonText = primary, CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 4, MaxWidth = 520,
                Children =
                {
                    new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "录制前，SCSKiller 会检查整个游戏目录中的反作弊组件。"
                        + "请选择此游戏自己的目录，不要选择包含多个游戏的上级目录。" },
                    Label("游戏"),
                    new TextBlock { Text = game.Name, TextWrapping = TextWrapping.Wrap },
                    Label("执行文件（录制器会放在它旁边）"),
                    new TextBlock { Text = game.ExePath, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
                    Label("游戏目录"),
                    row,
                    problem,
                },
            },
        };

        async Task Check()
        {
            folderText.Text = folder;
            var why = await Task.Run(() => App.Core.ManualFolderProblem(game.ExePath, folder));   // lists the folder's subfolders
            problem.Message = why ?? "";
            problem.IsOpen = why != null;
            dialog.IsPrimaryButtonEnabled = why == null;
        }

        change.Click += async (_, _) =>
        {
            try
            {
                var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(App.Main.AppWindow.Id) { CommitButtonText = "使用此目录" };
                if ((await picker.PickSingleFolderAsync())?.Path is { } picked) folder = picked;
            }
            catch (Exception e)
            {
                problem.Message = "无法打开目录选择器：" + e.Message;
                problem.IsOpen = true;
                return;
            }
            await Check();
        };
        await Check();
        return await App.ShowAsync(dialog) == ContentDialogResult.Primary ? folder : null;
    }
}
